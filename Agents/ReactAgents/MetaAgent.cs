using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Agents
{
    /// <summary>
    /// Represents an advanced agent designed to interact with multiple MCP clients and their associated tools.
    /// </summary>
    /// <remarks>The <see cref="MetaAgent"/> class is a specialized implementation of <see
    /// cref="BaseReActAgent"/> that integrates  with multiple MCP clients, discovers their tools, and maps them for
    /// efficient usage. It initializes a kernel  with plugins and functions derived from the discovered tools, enabling
    /// dynamic function invocation. <para> This agent is primarily used in scenarios where multiple MCP clients need to
    /// be managed and their tools  utilized in a unified manner. </para></remarks>
    public class MetaAgent : BaseReActAgent
    {
        public override string Name => "MetaAgent";

        protected override List<BaseMCPClient> McpClients { get; }

        protected override Kernel agentKernel { get; set; }

        protected override List<McpClientTool> availableTools { get; set; }

        protected override Dictionary<string, BaseMCPClient> toolToMcpClientMap { get; set; }

        public MetaAgent(List<BaseMCPClient> mcpClients)
        {
            McpClients = mcpClients;

            availableTools = new List<McpClientTool>();
            toolToMcpClientMap = new Dictionary<string, BaseMCPClient>();

            foreach (var mcpClient in McpClients)
            {
                Console.WriteLine($"Initialized {Name} with MCP Client: {mcpClient.ClientName} connected to server: {mcpClient.ServerName}");

                try
                {
                    var tools = mcpClient.DiscoverToolsAsync().GetAwaiter().GetResult();

                    // Map tools to their respective MCP client for later use
                    foreach (var tool in tools)
                    {
                        // Remove whitespace, newlines, and special characters from the tool name to ensure it's a valid function name
                        string sanitizedToolName = Regex.Replace(tool.Name.ToLower(), @"[\s\n\r\-\._,;:?!'""\\\/]", "").Trim();
                        toolToMcpClientMap[sanitizedToolName] = mcpClient;
                    }

                    availableTools.AddRange(tools);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{Name} - ReAct] Error discovering tools from MCP client {mcpClient.ClientName}: {ex.Message}");
                }
            }

            // Set up the kernel with plugins and functions
            agentKernel = CreateKernelWithChatCompletionService();

            // Clear existing plugins and add MCP tools as functions
            agentKernel.Plugins.Clear();
            agentKernel.Plugins.AddFromFunctions("Tools", availableTools.Select(aiFunction => aiFunction.AsKernelFunction()));

            agentKernel.FunctionInvocationFilters.Add(new FunctionInvocationFilter());

        }

        /// <summary>
        /// Creates and configures a new instance of a <see cref="Kernel"/> with an Azure OpenAI chat completion
        /// service.
        /// </summary>
        /// <remarks>This method initializes a <see cref="Kernel"/> using configuration values for the
        /// Azure OpenAI service. It retrieves the necessary settings, such as the API key, endpoint, and model
        /// deployment name, from environment variables or user secrets. If the required configuration values are
        /// missing or invalid,  an <see cref="InvalidOperationException"/> is thrown.</remarks>
        /// <returns>A fully configured <see cref="Kernel"/> instance with the Azure OpenAI chat completion service.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the required Azure OpenAI API key is not provided or is invalid.</exception>
        protected static Kernel CreateKernelWithChatCompletionService()
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

            string modelId = config["AzureOpenAI:ModelDeploymentName"] ?? "gpt-4o-mini";

            // Create kernel
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddAzureOpenAIChatCompletion(
                deploymentName: config["AzureOpenAI:ModelDeploymentName"] ?? "gpt-4.1-mini",
                apiKey: config["AzureOpenAI:ApiKey"] ?? "dfs",
                endpoint: config["AzureOpenAI:Endpoint"] ?? "https://your-openai-endpoint.openai.azure.com/",
                apiVersion: config["AzureOpenAI:ApiVersion"] ?? "2023-05-15");

            return kernelBuilder.Build();
        }

    }
}
