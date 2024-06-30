using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Threading.RateLimiting;

namespace Carubbi.ResilientFlurl.Configuration;

public class ResiliencePipelinePolicyOptions
{
    public ResiliencePipelinePolicies Policy { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = [];

    internal ConcurrencyLimiterOptions GetConcurrenyLimiterOptions()
    {
        var maxConcurrentCalls = Parameters.TryGetValue("MaxConcurrentCalls", out var maxConcurrentCallsStringValue) ? int.Parse(maxConcurrentCallsStringValue) : DefaultMaxConcurrentCalls;
        var queueLimit = Parameters.TryGetValue("QueueLimit", out var queueLimitStringValue) ? int.Parse(queueLimitStringValue) : DefaultQueueLimit;
        var queueProcessingOrder = Parameters.TryGetValue("QueueProcessingOrder", out var queueProcessingOrderStringValue) ? (QueueProcessingOrder)Enum.Parse(typeof(QueueProcessingOrder), queueProcessingOrderStringValue) : QueueProcessingOrder.OldestFirst;
        return new ConcurrencyLimiterOptions
        {
            QueueProcessingOrder = queueProcessingOrder,
            PermitLimit = maxConcurrentCalls,
            QueueLimit = queueLimit
        };
    }

    internal TimeoutStrategyOptions GetTimeoutOptions()
    {
        var timeoutInSeconds = Parameters.TryGetValue("TimeoutInSeconds", out var timeoutInSecodsStringValue) ? int.Parse(timeoutInSecodsStringValue) : DefaultTimeoutInSeconds;
        return new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(timeoutInSeconds),
            Name = "custom-timeout",
        };
    }

    internal CircuitBreakerStrategyOptions<HttpResponseMessage> GetCircuitBreakerOptions(IServiceProvider sp, string name)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<CircuitBreakerStrategyOptions<HttpResponseMessage>>();

        var durationInSeconds = Parameters.TryGetValue("DurationInSeconds", out var durationInSecondsStringValue) ? int.Parse(durationInSecondsStringValue) : DefaultDurationInSeconds;
        var samplingDurationInSeconds = Parameters.TryGetValue("SamplingDurationInSeconds", out var samplingDurationInSecondsStringValue) ? int.Parse(samplingDurationInSecondsStringValue) : DefaultSamplingDurationInSeconds;
        var failureRatio = Parameters.TryGetValue("FailureRatio", out var failureRatioStringValue) ? double.Parse(failureRatioStringValue) : DefaultFailureRatioStringValue;
        var minimumThroughput = Parameters.TryGetValue("MinimumThroughput", out var minimumThroughputStringValue) ? int.Parse(minimumThroughputStringValue) : DefaultMinimumThroughput;

        return new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-circuitBreaker",
            FailureRatio = failureRatio / 100.0,
            SamplingDuration = TimeSpan.FromSeconds(samplingDurationInSeconds),
            BreakDuration = TimeSpan.FromSeconds(durationInSeconds),
            MinimumThroughput = minimumThroughput,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<TimeoutRejectedException>()
            .Handle<HttpRequestException>()
            .HandleResult(response => response.StatusCode == HttpStatusCode.InternalServerError),
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

    internal RetryStrategyOptions<HttpResponseMessage> GetRetryOptions()
    {
        var maxAttempts = Parameters.TryGetValue("MaxAttempts", out var maxAttempsStringValue) ? int.Parse(maxAttempsStringValue) : DefaultMaxAttempts;
        var delayInSeconds = Parameters.TryGetValue("DelayInSeconds", out var delayInSecondsStringValue) ? int.Parse(delayInSecondsStringValue) : DefaultDelayInSeconds;
        var backoffType = Parameters.TryGetValue("BackoffType", out var backoffTypeStringValue) ? (DelayBackoffType)Enum.Parse(typeof(DelayBackoffType), backoffTypeStringValue) : DelayBackoffType.Exponential;
        var maxDelayInSeconds = Parameters.TryGetValue("MaxDelayInSeconds", out var maxDelayInSecondsStringValue) ? int.Parse(maxDelayInSecondsStringValue) : DefaultMaxDelayInSeconds;


        return new RetryStrategyOptions<HttpResponseMessage>
        {
            Name = "custom-retry",
            MaxRetryAttempts = maxAttempts,
            Delay = TimeSpan.FromSeconds(delayInSeconds),
            BackoffType = backoffType,
            MaxDelay = TimeSpan.FromSeconds(maxDelayInSeconds),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<TimeoutRejectedException>()
            .Handle<HttpRequestException>()
            .HandleResult(response => response.StatusCode == HttpStatusCode.InternalServerError)
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
