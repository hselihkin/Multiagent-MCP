using MetaDataMCPServer;
using MetaDataMCPServer.ProjectResources;
using MetaDataMCPServer.Prompts;
using MetaDataMCPServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Create an empty application builder
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        // Load and validate configuration
        (string embeddingModelId, string chatModelId, string apiKey, string endPoint, string apiVersion) = GetConfiguration();

        // Register the kernel
        IKernelBuilder kernelBuilder = builder.Services.AddKernel();

        // Register SK plugins/tools that will be exposed via the MCP server
        kernelBuilder.Plugins.AddFromType<MetaDataUtils>();

        // Register MCP server
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools();

        Console.WriteLine("Starting MCP server...");

        await builder.Build().RunAsync();

    }

    ///<summary>
    /// Get configuration.
    ///</summary>
    static (string EmbeddingModelId, string ChatModelId, string ApiKey, string endPoint, string apiVersion) GetConfiguration()
    {
        // Load and validate configuration
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        if (config["AzureOpenAI:ApiKey"] is not { } apiKey)
        {
            const string Message = "Please provide a valid AzureOpenAI:ApiKey to run this sample. See the associated README.md for more details.";
            Console.Error.WriteLine(Message);
            throw new InvalidOperationException(Message);
        }

        string embeddingModelId = config["AzureOpenAI:EmbeddingModelId"] ?? "text-embedding-3-small";

        string chatModelId = config["AzureOpenAI:ModelDeploymentName"] ?? "gpt-4o-mini";
        string endpoint = config["AzureOpenAI:Endpoint"] ?? "https://your-openai-endpoint.openai.azure.com/";
        string apiVersion = config["AzureOpenAI:ApiVersion"] ?? "2023-05-15";

        return (embeddingModelId, chatModelId, apiKey, endpoint, apiVersion);

    }


}