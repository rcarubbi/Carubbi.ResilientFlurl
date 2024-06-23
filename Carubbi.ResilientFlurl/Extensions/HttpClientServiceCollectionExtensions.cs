using Carubbi.ResilientFlurl.Configuration;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Threading.RateLimiting;

namespace Carubbi.ResilientFlurl.Extensions;

public static class HttpClientServiceCollectionExtensions
{
    public static IServiceCollection AddResilientHttpClient<T>(this IServiceCollection services, IConfiguration configuration)
    {
        var name = typeof(T).Name;
        services.Configure<HttpClientOptions>(name, configuration.GetSection($"{name}:HttpClient"));
        var httpClientOptions = configuration.GetSection($"{name}:HttpClient").Get<HttpClientOptions>() ?? throw new ArgumentException("HttpClient configuration missing");
        var httpClientBuilder = services.AddHttpClient(name, (httpClient) => { httpClient.BaseAddress = new Uri(httpClientOptions.BaseAddress); });
        if (httpClientOptions.UseStandardResiliencePipeline)
        {
            httpClientBuilder.AddStandardResilienceHandler().SelectPipelineByAuthority();
        }
        else
        {
            httpClientBuilder.AddResilienceHandler($"custom-{httpClientOptions.BaseAddress}", (builder, context) =>
            {
                foreach (var pipelineResiliencePolicyOptions in httpClientOptions.CustomResiliencePipeline)
                {
                    switch (pipelineResiliencePolicyOptions.Policy)
                    {
                        case ResiliencePipelinePolicies.Retry:
                            builder.AddRetry(GetRetryOptions(pipelineResiliencePolicyOptions.Parameters));
                            break;
                        case ResiliencePipelinePolicies.ConcurrencyLimiter:
                            builder.AddConcurrencyLimiter(GetConcurrenyLimiterOptions(pipelineResiliencePolicyOptions.Parameters));
                            break;
                        case ResiliencePipelinePolicies.Timeout:
                            builder.AddTimeout(GetTimeoutOptions(pipelineResiliencePolicyOptions.Parameters));
                            break;
                        case ResiliencePipelinePolicies.CircuitBreaker:
                            builder.AddCircuitBreaker(GetCircuitBreakerOptions(context.ServiceProvider, name, pipelineResiliencePolicyOptions.Parameters));
                            break;
                        default:
                            throw new ArgumentException("Invalid policy type");
                    }
                }
            });
        }

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(name);
        var flurlClient = new FlurlClient(httpClient, httpClientOptions.BaseAddress);
        flurlClient.OnError(a => a.ExceptionHandled = true);
        services.AddKeyedSingleton<IFlurlClient>(name, flurlClient);

        return services;
    }

    private static ConcurrencyLimiterOptions GetConcurrenyLimiterOptions(Dictionary<string, string> policyParameters)
    {
        var maxConcurrentCalls = policyParameters.TryGetValue("MaxConcurrentCalls", out var maxConcurrentCallsStringValue) ? int.Parse(maxConcurrentCallsStringValue) : DefaultMaxConcurrentCalls;
        var queueLimit = policyParameters.TryGetValue("QueueLimit", out var queueLimitStringValue) ? int.Parse(queueLimitStringValue) : DefaultQueueLimit;
        var queueProcessingOrder = policyParameters.TryGetValue("QueueProcessingOrder", out var queueProcessingOrderStringValue) ? (QueueProcessingOrder)Enum.Parse(typeof(QueueProcessingOrder), queueProcessingOrderStringValue) : QueueProcessingOrder.OldestFirst;
        return new ConcurrencyLimiterOptions
        {
            QueueProcessingOrder = queueProcessingOrder,
            PermitLimit = maxConcurrentCalls,
            QueueLimit = queueLimit
        };
    }

    private static TimeoutStrategyOptions GetTimeoutOptions(Dictionary<string, string> policyParameters)
    {
        var timeoutInSeconds = policyParameters.TryGetValue("TimeoutInSeconds", out var timeoutInSecodsStringValue) ? int.Parse(timeoutInSecodsStringValue) : DefaultTimeoutInSeconds;
        return new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(timeoutInSeconds),
            Name = "custom-timeout",
        };
    }

    private static CircuitBreakerStrategyOptions<HttpResponseMessage> GetCircuitBreakerOptions(IServiceProvider sp, string name, Dictionary<string, string> policyParameters)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CircuitBreakerStrategyOptions<HttpResponseMessage>>();

        var durationInSeconds = policyParameters.TryGetValue("DurationInSeconds", out var durationInSecondsStringValue) ? int.Parse(durationInSecondsStringValue) : DefaultDurationInSeconds;
        var samplingDurationInSeconds = policyParameters.TryGetValue("SamplingDurationInSeconds", out var samplingDurationInSecondsStringValue) ? int.Parse(samplingDurationInSecondsStringValue) : DefaultSamplingDurationInSeconds;
        var failureRatio = policyParameters.TryGetValue("FailureRatio", out var failureRatioStringValue) ? double.Parse(failureRatioStringValue) : DefaultFailureRatioStringValue;
        var minimumThroughput = policyParameters.TryGetValue("MinimumThroughput", out var minimumThroughputStringValue) ? int.Parse(minimumThroughputStringValue) : DefaultMinimumThroughput;

        return new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-circuitBreaker",
            FailureRatio = failureRatio / 100.0,
            SamplingDuration = TimeSpan.FromSeconds(samplingDurationInSeconds),
            BreakDuration = TimeSpan.FromSeconds(durationInSeconds),
            MinimumThroughput = minimumThroughput,
            OnClosed = (args) =>
            {
                logger.LogWarning(args.Outcome.Exception, "Circuit breaker closed on {policyName}", name);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = (args) =>
            {
                logger.LogInformation("Circuit breaker half-opened on {policyName}", name);
                return ValueTask.CompletedTask;
            },
        };
    }

    private static RetryStrategyOptions<HttpResponseMessage> GetRetryOptions(Dictionary<string, string> policyParameters)
    {
        var maxAttempts = policyParameters.TryGetValue("MaxAttempts", out var maxAttempsStringValue) ? int.Parse(maxAttempsStringValue) : DefaultMaxAttempts;
        var delayInSeconds = policyParameters.TryGetValue("DelayInSeconds", out var delayInSecondsStringValue) ? int.Parse(delayInSecondsStringValue) : DefaultDelayInSeconds;
        var backoffType = policyParameters.TryGetValue("BackoffType", out var backoffTypeStringValue) ? (DelayBackoffType)Enum.Parse(typeof(DelayBackoffType), backoffTypeStringValue) : DelayBackoffType.Exponential;
        var maxDelayInSeconds = policyParameters.TryGetValue("MaxDelayInSeconds", out var maxDelayInSecondsStringValue) ? int.Parse(maxDelayInSecondsStringValue) : DefaultMaxDelayInSeconds;


        return new RetryStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-retry",
            MaxRetryAttempts = maxAttempts,
            Delay = TimeSpan.FromSeconds(delayInSeconds),
            BackoffType = backoffType,
            MaxDelay = TimeSpan.FromSeconds(maxDelayInSeconds),
        };
    }

    private const int DefaultTimeoutInSeconds = 10;
    private const int DefaultMaxConcurrentCalls = 100;
    private const int DefaultQueueLimit = int.MaxValue;
    private const int DefaultMaxAttempts = 3;
    private const int DefaultDelayInSeconds = 5;
    private const int DefaultMaxDelayInSeconds = 60;
    private const int DefaultDurationInSeconds = 60;
    private const int DefaultSamplingDurationInSeconds = 120;
    private const double DefaultFailureRatioStringValue = 100.0;
    private const int DefaultMinimumThroughput = 10;
}