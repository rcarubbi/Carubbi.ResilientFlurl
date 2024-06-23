namespace Carubbi.ResilientFlurl.Configuration;

public enum ResiliencePipelinePolicies
{
    Timeout,
    Retry,
    CircuitBreaker,
    ConcurrencyLimiter
}
