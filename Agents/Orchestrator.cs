using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace Agents
{
    /// <summary>
    /// Provides functionality to orchestrate tasks using a planner agent and a pool of agents.
    /// </summary>
    /// <remarks>The <see cref="Orchestrator"/> class is designed to coordinate tasks by leveraging a planner
    /// agent and a kernel configured with the Azure OpenAI chat completion service. It initializes the necessary
    /// components, including memory, agents, and MCP clients, to enable task planning and execution.  This class is
    /// primarily used to process input and generate responses by creating and executing plans through the planner
    /// agent.</remarks>
    public class Orchestrator
    {
        private readonly PlannerAgent _plannerAgent;
        private readonly PlannerMemory _plannerMemory;
        private readonly Kernel _kernel;

        /// <summary>
        /// Initializes a new instance of the <see cref="Orchestrator"/> class.
        /// </summary>
        /// <remarks>The <see cref="Orchestrator"/> class is responsible for setting up the necessary
        /// components  for orchestrating tasks, including kernel initialization, agent creation, and memory management.
        /// It configures the environment using user secrets and environment variables, and establishes  connections to
        /// various MCP services required for task execution.</remarks>
        public Orchestrator()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables()
                .Build();

            // Build the kernel with the Azure OpenAI chat completion service registered.
            _kernel = CreateKernelWithChatCompletionService(config);

            // A simple planner agent that utilizes the TimeseriesAgent to fetch timeseries data based on a planner task.
            _plannerMemory = new PlannerMemory();

            // Create MCP clients for MetaData and Stream services (locally hosted)
            var streamMcpClient = new StreamMCPClient(
                clientName: "TimeseriesMCPClient",
                server: "TimeseriesMCPServer",
                kernel: _kernel);
            var metaDataMcpClient = new MetaDataMCPClient(
                clientName: "MetaDataMCPClient",
                server: "MetaDataMCPServer",
                kernel: _kernel);

            // Create MCP client for Capability service (remotely hosted)
            var capabilityMcpClient = new CapabilityMCPClient
                (clientName: "CapabilityMCPClient",
                server: "CapabilityMCPServer",
                serverHttpAddress: config["RemoteMcpServer:CapabilityServer"] ?? "",
                kernel: _kernel);

            // Initialize the MetaAgent with the MetaData and Stream MCP clients
            var metaClients = new List<BaseMCPClient> { streamMcpClient, metaDataMcpClient };
            var metaAgent = new MetaAgent(metaClients);

            // Initialize the CapabilityAgent with the Capability MCP client
            var capabilityClients = new List<BaseMCPClient> { capabilityMcpClient };
            var capabilityAgent = new CapabilityAgent(capabilityClients);

            // Prepare the agent pool with the MetaAgent and CapabilityAgent
            List<IAgent> agentPool = new List<IAgent> { metaAgent, capabilityAgent };

            // Initialize the PlannerAgent with the kernel and pool of agents
            _plannerAgent = new PlannerAgent(_kernel, agentPool, _plannerMemory);

        }

        public async Task<string> OrchestrateAsync(string input)
        {
            // Use the planner agent to process the input and return a response.
            var response = await _plannerAgent.CreatePlanAndExecuteAsync(input);
            return response;
        }

        /// <summary>
        /// Creates an instance of <see cref="Kernel"/> with the OpenAI chat completion service registered.
        /// </summary>
        /// <returns>An instance of <see cref="Kernel"/>.</returns>
        protected static Kernel CreateKernelWithChatCompletionService(IConfiguration config)
        {
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
