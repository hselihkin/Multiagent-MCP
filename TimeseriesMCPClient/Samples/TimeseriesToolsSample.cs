// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

namespace MCPClient.Samples;

/// <summary>
/// This sample demonstrates how to use the Model Context Protocol (MCP) tools with the Semantic Kernel.
/// </summary>
internal sealed class TimeseriesToolsSample : BaseSample
{
    /// <summary>
    /// Demonstrates how to use the MCP tools with the Semantic Kernel.
    /// The code in this method:
    /// 1. Creates an MCP client.
    /// 2. Retrieves the list of tools provided by the MCP server.
    /// 3. Creates a kernel and registers the MCP tools as Kernel functions.
    /// 4. Sends the prompt to AI model together with the MCP tools represented as Kernel functions.
    /// 5. The AI model calls DateTimeUtils-GetCurrentDateTimeInUtc function to get the current date time in UTC required as an argument for the next function.
    /// 6. The AI model calls TimeseriesUtils-GetCurrentTimeseriesValuesByStreamName function with the current date time and the `GE07` arguments extracted from the prompt to get the stream information.
    /// 7. Having received the stream information from the function call, the AI model returns the answer to the prompt.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(TimeseriesToolsSample)} sample.");

        // Create an MCP client
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // Retrieve and display the list provided by the MCP server
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // Create a kernel and register the MCP tools
        Kernel kernel = CreateKernelWithChatCompletionService();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
        kernel.FunctionInvocationFilters.Add(new FunctionInvocationFilter());

        // Enable automatic function calling
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        //string prompt = "What are the value for tag GE07 from June 17th 6:10 to June 18th 6:10 in 2025?";
        string prompt = "Question: What are the Stream Types for instance id 'digital-twin-sds-jk'?";
        Console.WriteLine(prompt);

        // Execute a prompt using the MCP tools. The AI model will automatically call the appropriate MCP tools to answer the prompt.
        //FunctionResult result = await kernel.InvokePromptAsync(prompt, new(executionSettings));
        var result = await chatCompletionService.GetChatMessageContentAsync(prompt, executionSettings: executionSettings, kernel: kernel);

        Console.WriteLine($"Answer: {result}");
        Console.WriteLine();
    }

    public sealed class FunctionInvocationFilter : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            var arguments = context.Arguments.ToDictionary(arg => arg.Key, arg => arg.Value?.ToString() ?? "null");            
            var argumentStr = string.Join(", ", arguments.Select(kvp => $"{kvp.Key} => {kvp.Value}"));

            Console.WriteLine($"[Interim Event]: Function {context.Function.Name} is about to be invoked with arguments {argumentStr}.");
            await next(context);
            Console.WriteLine($"[Interim Event]: Function {context.Function.Name} was invoked.");
        }
    }
}
