using Carubbi.ResilientFlurl.Http;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Threading.RateLimiting;

namespace Carubbi.ResilientFlurl.Extensions;

public static class HttpClientServiceCollectionExtensions
{
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<List<HttpClientPolicy>>(configuration.GetSection("HttpClientPolicies"));

        var serviceProvider = services.BuildServiceProvider();
        var httpClientPoliciesOptions = serviceProvider.GetRequiredService<IOptions<List<HttpClientPolicy>>>().Value;

        foreach (var policyOptions in httpClientPoliciesOptions)
        {
            var httpClientBuilder = services.AddHttpClient(policyOptions.Name, (httpClient) => { httpClient.BaseAddress = new Uri(policyOptions.BaseAddress); });

            if (policyOptions.UseDefaultPipeline)
            {
                httpClientBuilder.AddStandardResilienceHandler().SelectPipelineByAuthority();
            }
            else
            {
                httpClientBuilder.AddResilienceHandler($"custom-{policyOptions.BaseAddress}", (builder, context) =>
                {
                    var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
                    builder
                        .ConfigureTelemetry(loggerFactory)
                        .AddTimeout(GetTimeoutOptions(context.ServiceProvider, policyOptions))
                        .AddRetry(GetRetryOptions(context.ServiceProvider, policyOptions))
                        .AddCircuitBreaker(GetCircuitBreakerOptions(context.ServiceProvider, policyOptions))
                        .AddConcurrencyLimiter(GetConcurrenyLimiterOptions(policyOptions));
                });
            }
        }

        serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        foreach (var policyOptions in httpClientPoliciesOptions)
        {
            var httpClient = httpClientFactory.CreateClient(policyOptions.Name);
            var flurlClient = new FlurlClient(httpClient, policyOptions.BaseAddress);
            flurlClient.OnError(a => a.ExceptionHandled = true);
            services.AddKeyedSingleton<IFlurlClient>(policyOptions.Name, flurlClient);
        }
 
        return services;
    }

    private static ConcurrencyLimiterOptions GetConcurrenyLimiterOptions(HttpClientPolicy policyOptions)
    {
        return new ConcurrencyLimiterOptions
        {
            PermitLimit = policyOptions.MaxConcurrentCalls,
            QueueLimit = int.MaxValue
        };
    }

    private static TimeoutStrategyOptions GetTimeoutOptions(IServiceProvider sp, HttpClientPolicy policyOptions)
    {
        return new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(policyOptions.TimeoutInSeconds),
            Name = "custom-timeout",
        };
    }

    private static CircuitBreakerStrategyOptions<HttpResponseMessage> GetCircuitBreakerOptions(IServiceProvider sp, HttpClientPolicy policyOptions)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CircuitBreakerStrategyOptions<HttpResponseMessage>>();

        return new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-circuitBreaker",
            FailureRatio = policyOptions.CircuitBreakerFailureRatio / 100.00,
            SamplingDuration = TimeSpan.FromSeconds(policyOptions.CircuitBreakerSamplingDurationInSeconds),
            BreakDuration = TimeSpan.FromSeconds(policyOptions.CircuitBreakerDurationInSeconds),
            MinimumThroughput = policyOptions.CircuitBreakerMinimumThroughput,
            OnClosed = (args) =>
            {
                logger.LogWarning(args.Outcome.Exception, "Circuit breaker closed on {policyName}", policyOptions.Name);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = (args) =>
            {
                logger.LogInformation("Circuit breaker half-opened on {policyName}", policyOptions.Name);
                return ValueTask.CompletedTask;
            },
        };
    }

    private static RetryStrategyOptions<HttpResponseMessage> GetRetryOptions(IServiceProvider sp, HttpClientPolicy policyOptions)
    {
        return new RetryStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-retry",
            MaxRetryAttempts = policyOptions.RetryCount,
            Delay = TimeSpan.FromSeconds(policyOptions.RetryDelayInSeconds),
            BackoffType = DelayBackoffType.Exponential,
            MaxDelay = TimeSpan.FromSeconds(30),
        };
    }



}
