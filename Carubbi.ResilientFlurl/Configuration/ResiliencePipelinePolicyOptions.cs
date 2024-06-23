namespace Carubbi.ResilientFlurl.Configuration;

public class ResiliencePipelinePolicyOptions
{
    public ResiliencePipelinePolicies Policy { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = [];
}
