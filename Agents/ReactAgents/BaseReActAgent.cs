using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using ModelContextProtocol.Client;
using Agents.ProjectResources;

namespace Agents
{
    /// <summary>
    /// Represents the base class for a ReAct agent, which processes tasks using iterative reasoning and tool execution.
    /// </summary>
    /// <remarks>This abstract class provides the foundational structure for implementing a ReAct agent. It
    /// defines key properties  and methods required for task processing, including integration with tools and reasoning
    /// loops. Derived classes  must implement the abstract members to specify the agent's behavior, tools, and kernel
    /// configuration.  The <see cref="ProcessAsync"/> method executes the ReAct reasoning loop, iteratively generating
    /// thoughts, actions,  and observations to complete a given task. It supports tool execution and handles responses
    /// from a language model  service. The loop terminates when a final answer is produced or the maximum number of
    /// iterations is reached.</remarks>
    public abstract class BaseReActAgent : IAgent
    {
        public abstract string Name { get; }

        protected abstract List<BaseMCPClient> McpClients { get; }

        protected abstract Kernel agentKernel { get; set; }

        protected abstract List<McpClientTool> availableTools { get; set; }

        protected abstract Dictionary<string, BaseMCPClient> toolToMcpClientMap { get; set; }

        protected int MaxReActIterations { get; set; } = 5;

        /// <summary>
        /// Executes a task using a ReAct loop, leveraging a large language model (LLM) to iteratively refine the task's
        /// output.
        /// </summary>
        /// <remarks>This method uses a ReAct loop to iteratively process the task, where each iteration
        /// involves rendering a prompt, invoking the LLM,  and optionally executing tools based on the LLM's response.
        /// The loop continues until a final answer is received, the maximum number  of iterations is reached, or an
        /// error occurs. <para> If no LLM service is configured, the method will return an error message immediately.
        /// </para> <para> The method is designed to handle complex tasks that may require multiple iterations and tool
        /// invocations. It is suitable for scenarios  where dynamic reasoning and decision-making are required.
        /// </para></remarks>
        /// <param name="taskFromPlanner">The initial task description provided by the planner.</param>
        /// <param name="plannerParameters">A dictionary of parameters associated with the task, used to guide the execution process.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests, allowing the operation to be terminated prematurely.</param>
        /// <returns>A string containing the final output of the task if successfully completed, or an error message if the task
        /// could not be executed.</returns>
        public async Task<string> ProcessAsync(string taskFromPlanner, Dictionary<string, string> plannerParameters, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"\n[{Name} - ReAct] Starting task: \"{taskFromPlanner}\" with initial params: {JsonSerializer.Serialize(plannerParameters)}");

            var chatCompletionService = agentKernel.GetRequiredService<IChatCompletionService>();

            if (chatCompletionService is null)
            {
                Console.WriteLine($"[{Name} - ReAct] WARNING: No LLM service configured. Cannot execute ReAct loop. Returning placeholder.");
                return $"[{Name} - ReAct ERROR] LLM service (IChatCompletion) not configured in the kernel.";
            }

            // Enable automatic function calling
            OpenAIPromptExecutionSettings executionSettings = new()
            {
                Temperature = 0,
                FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                ResponseFormat = "json_object"
            };

            List<string> reactScratchpad = new List<string>();

            for (int i = 0; i < MaxReActIterations; i++)
            {
                Console.WriteLine($"\n[{Name} - ReAct] Iteration {i + 1}/{MaxReActIterations}");

                // Simulate some delay for each iteration
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

                // Render the ReAct prompt template
                var templateFactory = new HandlebarsPromptTemplateFactory();
                var promptTemplateConfig = new PromptTemplateConfig()
                {
                    Template = EmbeddedResource.ReadAsString("Prompts.ReactPrompt.yaml"),
                    TemplateFormat = "handlebars",
                };

                var reactKernelArguments = new KernelArguments()
                {
                    { "taskFromPlanner", taskFromPlanner },
                    { "plannerParametersJson", JsonSerializer.Serialize(plannerParameters) },
                    { "reactScratchpad", reactScratchpad } // Start with an empty scratchpad
                };

                var promptTemplate = templateFactory.Create(promptTemplateConfig);
                var renderedPrompt = await promptTemplate.RenderAsync(agentKernel, reactKernelArguments).ConfigureAwait(false);
                Console.WriteLine($"[{Name} - ReAct] prompt rendered successfully.");

                // Execute the prompt using the chat completion service
                var chatMessageContent = await agentKernel.InvokePromptAsync(
                    renderedPrompt,
                    new(executionSettings),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Process the response
                if (chatMessageContent is null)
                {
                    Console.WriteLine($"[{Name} - ReAct] No response received from LLM. Ending ReAct loop.");
                    break;
                }
                Console.WriteLine($"[{Name} - ReAct] Response: {chatMessageContent.ToString()}");
                AgentAction? agentAction = JsonSerializer.Deserialize<AgentAction>(chatMessageContent.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Parse the response JSON
                try
                {
                    if (agentAction == null)
                    {
                        Console.WriteLine($"[{Name} - ReAct] Invalid action JSON format. Ending ReAct loop.");
                        break;
                    }
                    reactScratchpad.Add($"Attempt {i + 1} Thought: {agentAction.Thought}");

                    string agentOutput = agentAction.AgentOutput ?? string.Empty;
                    // Update the scratchpad with the current thought and action
                    reactKernelArguments["reactScratchpad"] += $"\nThought: {agentAction.Thought}\nAction: {agentAction.ToolToUse}\nObservation: {agentOutput}";

                    if (agentAction.IsFinalAnswer)
                    {
                        Console.WriteLine($"[{Name} - ReAct] Final answer received: {agentOutput}");
                        return agentOutput;
                    }
                    else if (agentAction.ToolToUse != null)
                    {
                        // Execute the tool if specified
                        Console.WriteLine($"[{Name} - ReAct] Executing tool: {agentAction.ToolToUse} with input: {JsonSerializer.Serialize(agentAction.ToolInput)}");
                        reactScratchpad.Add($"Attempt {i + 1} Action: Use tool '{agentAction.ToolToUse}' with input: {JsonSerializer.Serialize(agentAction.ToolInput)}");

                        // Replace the current line with a regex-based splitting approach
                        string toolKey = Regex.Split(agentAction.ToolToUse.ToString(), @"[\.-]")[^1];
                        string sanitizedToolName = Regex.Replace(toolKey.ToLower(), @"[\s\n\r\-\._,;:?!'""\\\/]", "").Trim();
                        if (toolToMcpClientMap.TryGetValue(sanitizedToolName, out BaseMCPClient? mcpClient))
                        {
                            var toolResult = await mcpClient.ExecuteTool(toolKey, agentAction.ToolInput.ToDictionary(x => x.Key.ToString(), x => (object?)x.Value)).ConfigureAwait(false);

                            if (toolResult.IsError)
                            {
                                Console.WriteLine($"[{Name} - ReAct] Tool {agentAction.ToolToUse}({toolKey}) returns with an error: {toolResult.Content.FirstOrDefault()!.Text}.");
                                reactScratchpad.Add($"Attempt {i + 1} Error. Observation: {toolResult.Content.FirstOrDefault()!.Text}");
                                break;
                            }
                            reactScratchpad.Add($"Attempt {i + 1} Observation: {toolResult.Content.FirstOrDefault()!.Text}");

                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{Name} - ReAct] No tool specified, continuing to next iteration.");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[{Name} - ReAct] Error parsing action JSON: {ex.Message}. Ending ReAct loop.");
                    break;
                }
            }

            return $"[{Name} - ReAct] No final answer received after {MaxReActIterations} iterations. Ending ReAct loop.";
        }
    }

    /// <summary>
    /// Provides a filter that intercepts function invocations, allowing custom logic to be executed  before and after
    /// the invocation of a function.
    /// </summary>
    /// <remarks>This filter can be used to log function invocation details, modify invocation arguments, or 
    /// perform other pre- and post-invocation tasks. The filter is applied by implementing the  <see
    /// cref="IFunctionInvocationFilter"/> interface.</remarks>
    public sealed class FunctionInvocationFilter : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            var arguments = context.Arguments.ToDictionary(arg => arg.Key, arg => arg.Value?.ToString() ?? "null");
            var argumentStr = string.Join(", ", arguments.Select(kvp => $"{kvp.Key} => {kvp.Value}"));

            Console.WriteLine($"[Interim Event]: Function {context.Function.Name} is about to be invoked with arguments {argumentStr}.");
            await next(context);
            Console.WriteLine($"[Interim Event]: Function {context.Function.Name} was invoked with result {context.Result}.");
        }
    }
}
