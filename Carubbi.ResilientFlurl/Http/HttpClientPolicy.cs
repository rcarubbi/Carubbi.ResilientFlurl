namespace Carubbi.ResilientFlurl.Http;
public class HttpClientPolicy
{
    public string Name { get; set; } = null!;
    public string BaseAddress { get; set; } = null!;
    public int RetryCount { get; set; }
    public int RetryDelayInSeconds { get; set; }
    public int CircuitBreakerDurationInSeconds { get; set; }
    public int TimeoutInSeconds { get; set; }
    public int MaxConcurrentCalls { get;  set; }
    public int CircuitBreakerSamplingDurationInSeconds { get; set; }
    public double CircuitBreakerFailureRatio { get; set; }
    public bool UseDefaultPipeline { get; set; }
    public int CircuitBreakerMinimumThroughput { get; set; }
}
