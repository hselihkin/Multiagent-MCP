using System.Text.Json;
using Agents.ProjectResources;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace Agents
{
    /// <summary>
    /// Represents an agent responsible for coordinating planning tasks using a kernel, a pool of agents, and a memory
    /// system.
    /// </summary>
    /// <remarks>The <see cref="PlannerAgent"/> class facilitates the creation and execution of plans based on
    /// user queries. It leverages a kernel for task execution, a pool of agents for specialized operations, and a
    /// memory system for storing and retrieving relevant data. The planning process involves iterative refinement of
    /// tasks until a final result is produced or the maximum number of planning iterations is reached.</remarks>
    public class PlannerAgent
    {
        public string Name => "PlannerAgent";

        private Dictionary<string, IAgent> _agentPoolByName = new Dictionary<string, IAgent>();

        private readonly Kernel _kernel;
        private readonly PlannerMemory _memory;
        private readonly int _maxPlanningIterations = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlannerAgent"/> class, which coordinates planning tasks  using
        /// a kernel, a pool of agents, and a memory system.
        /// </summary>
        /// <param name="kernel">The kernel responsible for executing tasks and managing workflows. Cannot be null.</param>
        /// <param name="agentPool">A list of agents available for task execution. If the list is null or empty, no agents will be added to the
        /// pool.</param>
        /// <param name="memory">The memory system used to store and retrieve planning-related data. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="kernel"/> or <paramref name="memory"/> is null.</exception>
        public PlannerAgent(Kernel kernel, List<IAgent> agentPool, PlannerMemory memory)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            if (agentPool != null && agentPool.Count > 0)
            {
                agentPool.ForEach(agent => _agentPoolByName.TryAdd(agent.Name.ToLower(), agent));
            }

            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        /// <summary>
        /// Creates a plan based on the provided user query and executes it using a series of iterative steps.
        /// </summary>
        /// <remarks>This method leverages a planning prompt and a large language model (LLM) to generate
        /// and execute a sequence of tasks. It iteratively refines the plan based on the results of previous steps and
        /// relevant memory items. The process continues until a final result is produced or the maximum number of
        /// planning iterations is reached.</remarks>
        /// <param name="userQuery">The query provided by the user, which serves as the basis for generating the plan.</param>
        /// <returns>A string containing the final result of the plan execution. If the plan cannot be fully resolved,  the
        /// method returns a summary of the execution history.</returns>
        public async Task<string> CreatePlanAndExecuteAsync(string userQuery)
        {
            Console.WriteLine($"\n[{Name}] Received query: \"{userQuery}\". Starting LLM-driven planning with ReAct sub-agents...");

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            if (chatCompletionService == null)
            {
                Console.WriteLine($"[{Name}] WARNING: No LLM service configured in kernel. Planner will use STUBBED responses.");
            }

            // Load the planning prompt template from resources
            var plannerPromptYaml = EmbeddedResource.ReadAsString("Prompts.PlannerPrompt.yaml");

            // Create the prompt function from the YAML resource
            var templateFactory = new HandlebarsPromptTemplateFactory();
            var promptFunction = _kernel.CreateFunctionFromPromptYaml(plannerPromptYaml, templateFactory);

            List<string> currentSessionExecutionHistory = new List<string>();

            for (int i = 0; i < _maxPlanningIterations; i++)
            {
                Console.WriteLine($"\n[{Name}] Planning Iteration: {i + 1}");
                List<MemoryItem> relevantMemoryItems = _memory.RetrieveRelevantMemories(userQuery + " " + string.Join(" ", currentSessionExecutionHistory));
                string formattedMemories = _memory.FormatMemoriesForPrompt(relevantMemoryItems);
                var planningParameters = new KernelArguments()
                {
                    { "userInput",  userQuery },
                    { "relevantMemories", formattedMemories },
                    { "history", string.Join("\n", currentSessionExecutionHistory) }
                };

                Console.WriteLine($"[{Name}] Invoking planning LLM. Relevant Memories: \n{formattedMemories}\nExecution History: \n{planningParameters["history"]}");

                if (chatCompletionService == null)
                {
                    return string.Empty;
                }

                Console.WriteLine($"[{Name}] Calling the LLM for planning step...");
                var llmResult = await _kernel.InvokeAsync(promptFunction, planningParameters);

                PlanStep? planStep;
                try
                {
                    planStep = JsonSerializer.Deserialize<PlanStep>(llmResult.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                catch (JsonException ex)
                {
                    Console.WriteLine($"[{Name}] ERROR: Failed to deserialize plan step JSON. Error: {ex.Message}. JSON: {llmResult.ToString()}");
                    currentSessionExecutionHistory.Add($"Error: Failed to parse LLM plan step. LLM Output: {llmResult.ToString()}. Error: {ex.Message}");
                    if (i > 0 && currentSessionExecutionHistory.Last().Contains("Failed to parse LLM plan step")) break;
                    continue;
                }

                currentSessionExecutionHistory.Add($"Planner Step {i + 1} Thought: {planStep!.Reasoning}");
                _memory.AddMemory(planStep.Reasoning, "PlannerReasoning", Name, new Dictionary<string, string> { { "query", userQuery.Substring(0, Math.Min(userQuery.Length, 30)) } });
                Console.WriteLine($"[{Name}] Parsed Plan Step - Agent: {planStep.AgentToCall}, Task: {planStep.TaskForAgent}, Final: {planStep.IsFinalStep}");

                if (planStep.IsFinalStep)
                {
                    string finalResult = planStep.FinalAnswer ?? "Plan complete by LLM, but no final answer string provided.";
                    currentSessionExecutionHistory.Add($"Final Answer from Planner: {finalResult}");
                    _memory.AddMemory(finalResult, "FinalAnswer", Name, new Dictionary<string, string> { { "query", userQuery.Substring(0, Math.Min(userQuery.Length, 30)) } }, toLongTerm: true);
                    Console.WriteLine($"[{Name}] Plan complete. Final Answer: {finalResult}");
                    return finalResult;
                }

                // Get the agent to call based on the plan step
                _agentPoolByName.TryGetValue(planStep.AgentToCall.ToLower(), out IAgent? selectedAgent);

                if (selectedAgent == null)
                {
                    string errorMsg = $"Error: Planner LLM chose an unknown agent: '{planStep.AgentToCall}'. Available agents are MetaDataAgent, StreamDataAgent, DocumentContentAgent, PredictionModelCreationAgent.";
                    Console.WriteLine($"[{Name}] {errorMsg}");
                    currentSessionExecutionHistory.Add(errorMsg);
                    _memory.AddMemory(errorMsg, "PlannerError", Name);
                    continue;
                }

                var agentParameters = new Dictionary<string, string>(planStep.Parameters ?? new Dictionary<string, string>());

                Console.WriteLine($"[{Name}] Executing Agent: {selectedAgent.Name} for Task: \"{planStep.TaskForAgent}\" with params: {JsonSerializer.Serialize(agentParameters)}");
                string agentResult;
                try
                {
                    agentResult = await selectedAgent.ProcessAsync(planStep.TaskForAgent, agentParameters);
                }
                catch (Exception ex)
                {
                    agentResult = $"Error executing agent {selectedAgent.Name} for task '{planStep.TaskForAgent}': {ex.Message} {ex.StackTrace}";
                    Console.WriteLine($"[{Name}] Error during agent execution: {agentResult}");
                }

                currentSessionExecutionHistory.Add($"Result from {selectedAgent.Name} (Task: \"{planStep.TaskForAgent}\"): {agentResult}");
                var agentResultTags = new Dictionary<string, string>(planStep.Parameters ?? new Dictionary<string, string>());
                agentResultTags["agent"] = selectedAgent.Name;

                _memory.AddMemory(agentResult, "AgentResult", selectedAgent.Name, agentResultTags, toLongTerm: false);
                Console.WriteLine($"[{Name}] Result from {selectedAgent.Name}: {agentResult}");
            }

            string endSessionResult = $"[{Name}] Max planning iterations reached for query '{userQuery}'. Unable to fully resolve. Final History:\n{string.Join("\n", currentSessionExecutionHistory)}";
            _memory.AddMemory(endSessionResult, "PlannerFailure", Name, new Dictionary<string, string> { { "query", userQuery.Substring(0, Math.Min(userQuery.Length, 30)) } });
            Console.WriteLine(endSessionResult);
            return endSessionResult;
        }
    }

    /// <summary>
    /// Represents a single step in a plan, detailing the agent to be called, the task to be performed,  and any
    /// associated parameters or reasoning.
    /// </summary>
    /// <remarks>A plan step encapsulates the information required for an agent to perform a specific task
    /// within  a larger plan. It includes details such as the agent to call, the task description, and any  parameters
    /// necessary for execution. Additionally, it can indicate whether the step is the final  step in the plan and, if
    /// so, provide the final answer.</remarks>

    public class PlanStep
    {
        public string AgentToCall { get; set; } = string.Empty;
        public string TaskForAgent { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public string Reasoning { get; set; } = string.Empty;
        public bool IsFinalStep { get; set; } = false;
        public string? FinalAnswer { get; set; } = null;

    }
}
