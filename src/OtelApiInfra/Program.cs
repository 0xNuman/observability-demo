using Amazon.CDK;

namespace OtelApiInfra;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        _ = new OtelApiStack(app, "ObservabilityStack", new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
            },
            Description = "Observability Demo: .NET 10 API + ADOT + AMP + AMG on Windows EC2",
            Tags = new Dictionary<string, string>
            {
                ["Project"] = "ObservabilityDemo",
                ["Owner"] = "Numan Mohammed",
                ["Environment"] = "Demo"
            }
        });

        app.Synth();
    }
}