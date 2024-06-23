namespace Carubbi.ResilientFlurl.Configuration;

public class HttpClientOptions
{
    public string BaseAddress { get; set; } = null!;

    public bool UseStandardResiliencePipeline { get; set; }


    public ResiliencePipelinePolicyOptions[] CustomResiliencePipeline { get; set; } = [];
}
