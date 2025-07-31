using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Agents
{
    /// <summary>
    /// Represents an agent responsible for managing and interacting with capabilities provided by multiple MCP clients.
    /// </summary>
    /// <remarks>The <see cref="CapabilityAgent"/> class initializes and manages tools discovered from MCP
    /// clients, mapping them to their respective clients for later use. It also sets up a kernel with plugins and
    /// functions derived from the discovered tools, enabling seamless interaction with these capabilities.</remarks>
    public class CapabilityAgent : BaseReActAgent
    {
        public override string Name => "CapabilityAgent";

        protected override List<BaseMCPClient> McpClients { get; }

        protected override Kernel agentKernel { get; set; }

        protected override List<McpClientTool> availableTools { get; set; }

        protected override Dictionary<string, BaseMCPClient> toolToMcpClientMap { get; set; }

        public CapabilityAgent(List<BaseMCPClient> mcpClients)
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
        /// service. Each agent should have their own instance of a <see cref="Kernel"/> to allow that agent to have its own instance of Kernel with potentially
        /// different Foundation Models, plugins, and functions registered.
        /// </summary>
        /// <remarks>This method initializes a <see cref="Kernel"/> using configuration values for the
        /// Azure OpenAI service. It retrieves settings such as the API key, endpoint, and model deployment name from
        /// environment variables or user secrets. If the required configuration values are missing or invalid, an
        /// exception is thrown.</remarks>
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
