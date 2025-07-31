// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient.Samples;

/// <summary>
/// Demonstrates how to use the Model Context Protocol (MCP) prompt with the Semantic Kernel.
/// </summary>
internal sealed class TimeseriesPromptSample : BaseSample
{
    /// <summary>
    /// Demonstrates how to use the MCP prompt with the Semantic Kernel.
    /// The code in this method:
    /// 1. Creates an MCP client.
    /// 2. Retrieves the list of prompts provided by the MCP server.
    /// 3. Gets the stream using the `GetTimeseriesForStream` prompt.
    /// 4. Adds the MCP server prompts to the chat history and ask a question.
    /// 5. After receiving and processing the stream data, the AI model returns an answer.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(TimeseriesPromptSample)} sample.");

        // Create an MCP Client connect, which is already connected to the Timeseries MCP server
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // Retrieve and display the list of prompts provided by the MCP server
        IList<McpClientPrompt> prompts = await mcpClient.ListPromptsAsync();
        DisplayPrompts(prompts);

        // Retrieve and display the list of tools provided by the MCP server
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // Create a kernel
        Kernel kernel = CreateKernelWithChatCompletionService();

        // Get stream information prompt from the Timeseries MCP server
        GetPromptResult resultPrompt = await mcpClient.GetPromptAsync("GetTimeseriesForStream", new Dictionary<string, object?>() { ["streamName"] = "GE07.Bearing.001", ["startDateTime"] = "10AM", ["endDateTime"] = "04PM"});

        // Add the prompts to the chat history
        ChatHistory chatHistory = [];
        chatHistory.AddRange(resultPrompt.ToChatMessageContents());
        chatHistory.AddUserMessage("How high is the maximum temperature from the average?");

        // Execute the prompt using the MCP tools and prompts
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(chatHistory, kernel: kernel);

        Console.WriteLine(result);
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the list of available MCP prompts.
    /// </summary>
    /// <param name="prompts">The list of the prompts to display.</param>
    private static void DisplayPrompts(IList<McpClientPrompt> prompts)
    {
        Console.WriteLine("Available MCP prompts:");
        foreach (var prompt in prompts)
        {
            Console.WriteLine($"- Name: {prompt.Name}, Description: {prompt.Description}");
        }
        Console.WriteLine();
    }
}
