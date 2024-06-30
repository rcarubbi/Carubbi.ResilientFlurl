using Carubbi.ResilientFlurl.Configuration;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Carubbi.ResilientFlurl.Extensions;

public static class HttpClientServiceCollectionExtensions
{
    public static IServiceCollection AddResilientHttpClient<T>(this IServiceCollection services, IConfiguration configuration, Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        var name = typeof(T).GetGenericTypeString();
        services.Configure<HttpClientOptions>(name, configuration.GetSection($"{name}:HttpClient"));
        var httpClientOptions = configuration.GetSection($"{name}:HttpClient").Get<HttpClientOptions>() ?? throw new ArgumentException("HttpClient configuration missing");
        var httpClientBuilder = services.AddHttpClient(name, (httpClient) => { 
            httpClient.BaseAddress = new Uri(httpClientOptions.BaseAddress);
        });

        if (configureHttpClient != null)
        {
            configureHttpClient(httpClientBuilder);
        }

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
                            builder.AddRetry(pipelineResiliencePolicyOptions.GetRetryOptions());
                            break;
                        case ResiliencePipelinePolicies.ConcurrencyLimiter:
                            builder.AddConcurrencyLimiter(pipelineResiliencePolicyOptions.GetConcurrenyLimiterOptions());
                            break;
                        case ResiliencePipelinePolicies.Timeout:
                            builder.AddTimeout(pipelineResiliencePolicyOptions.GetTimeoutOptions());
                            break;
                        case ResiliencePipelinePolicies.CircuitBreaker:
                            builder.AddCircuitBreaker(pipelineResiliencePolicyOptions.GetCircuitBreakerOptions(context.ServiceProvider, name));
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
}