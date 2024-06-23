# Carubbi.ResilientFlurl

#### Overview
Carubbi.ResilientFlurl is a library designed to enhance resilience in HTTP client communications within your .NET applications. It simplifies the setup of resilient HTTP clients by providing a straightforward way to configure standard and custom resilience pipelines using policies such as Retry, Circuit Breaker, Concurrency Limiter, and Timeout. Additionally, it integrates with FlurlClient to provide enhanced capabilities for HTTP requests.

#### Getting Started

1. **Installation**
   - Install the `Carubbi.ResilientFlurl` NuGet package in your project:
     ```
     Install-Package Carubbi.ResilientFlurl
     ```

2. **Setup**
   - In your project's startup or configuration class (e.g., `Startup.cs` in a typical ASP.NET Core application), add resilient HTTP clients using the `AddResilientHttpClient` method provided by `Carubbi.ResilientFlurl`. For each client class you want to configure, call:
     ```csharp
     builder.Services.AddResilientHttpClient<ClientA>(configuration);
     builder.Services.AddResilientHttpClient<ClientB>(configuration);
     ```
     Replace `ClientA` and `ClientB` with the actual client classes you have in your project.

3. **Configuration**
   - Configure the pipeline policies for each client in the `appsettings.json` file under respective client names (`ClientA`, `ClientB`, etc.):
     ```json
     {
       "ClientA": {
         "HttpClient": {
           "BaseAddress": "https://localhost:7277/",
           "UseStandardResiliencePipeline": true
         }
       },
       "ClientB": {
         "HttpClient": {
           "BaseAddress": "https://localhost:7276/",
           "CustomResiliencePipeline": [
             {
               "Policy": "ConcurrencyLimiter",
               "Parameters": {
                 "MaxConcurrentCalls": 10
               }
             },
             {
               "Policy": "Timeout",
               "Parameters": {
                 "TimeoutInSeconds": 15
               }
             },
             {
               "Policy": "Retry",
               "Parameters": {
                 "MaxAttempts": 10,
                 "DelayInSeconds": 2
               }
             },
             {
               "Policy": "CircuitBreaker",
               "Parameters": {
                 "DurationInSeconds": 30,
                 "SamplingDurationInSeconds": 60,
                 "FailureRatio": 50,
                 "MinimumThroughput": 1000
               }
             }
           ]
         }
       }
     }
     ```

4. **Usage**
   - After setup, inject the configured `HttpClient` instances into your services or controllers using the `IHttpClientFactory`. The `Carubbi.ResilientFlurl` library will handle the resilience policies based on the configurations provided.
   - Additionally, each configured `HttpClient` includes an associated `FlurlClient`, allowing you to leverage Flurl's fluent interface for making HTTP requests.

#### Configuration Details

- **BaseAddress**: Specifies the base URL for the HTTP client.
- **UseStandardResiliencePipeline**: If set to `true`, uses the standard resilience pipeline which includes default policies.
- **CustomResiliencePipeline**: Allows defining a custom set of resilience policies for fine-grained control over retries, timeouts, concurrency limits, and circuit breaking.
  - Policies available:
    - **Retry**: Retries failed requests according to specified parameters (`MaxAttempts`, `DelayInSeconds`).
    - **Timeout**: Sets a maximum time duration for requests (`TimeoutInSeconds`).
    - **ConcurrencyLimiter**: Limits concurrent requests (`MaxConcurrentCalls`).
    - **CircuitBreaker**: Prevents requests from being sent to a service that is likely to fail (`DurationInSeconds`, `SamplingDurationInSeconds`, `FailureRatio`, `MinimumThroughput`).

#### Additional Notes
- Ensure that the `appsettings.json` file is correctly structured and that policies are configured appropriately for each client.
- You can mix and match standard and custom resilience pipelines across different clients in your application based on their specific needs.

#### License
This library is licensed under the MIT License. See the LICENSE file for more details.

For more information and updates, visit the [Carubbi.ResilientFlurl GitHub repository](https://github.com/rcarubbi/Carubbi.ResilientFlurl).

For usage example check the [demo repository](https://github.com/rcarubbi/ConsoleAppWithResilientFlurlDemo)
