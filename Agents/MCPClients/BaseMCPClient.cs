using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Agents
{
    /// <summary>
    /// Provides a base implementation for an MCP (Multi-Client Protocol) client, enabling interaction with an MCP
    /// server.
    /// </summary>
    /// <remarks>This abstract class serves as a foundation for creating MCP clients that connect to an MCP
    /// server via HTTP or Command Line Interface (CLI). It provides properties for accessing the initialized MCP client
    /// and kernel, as well as methods for discovering and executing tools on the server. Derived classes can extend
    /// this functionality by implementing additional features or custom behavior.</remarks>
    public abstract class BaseMCPClient
    {
        public readonly string ClientName;
        public readonly string ServerName;
        private IMcpClient? _mcpClient;
        private Kernel? _kernel;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseMCPClient"/> class with the specified client name, server
        /// name, and optional server HTTP address.
        /// </summary>
        /// <remarks>This constructor initializes the client by creating a kernel and connecting to the
        /// specified server. If <paramref name="serverHttpAddress"/> is not empty, the client connects to the server
        /// via HTTP. Otherwise, it connects to the server via the command-line interface.</remarks>
        /// <param name="clientName">The name of the client. This parameter cannot be <see langword="null"/>.</param>
        /// <param name="serverName">The name of the server. Defaults to "TimeseriesMCPServer" if not specified. This parameter cannot be <see
        /// langword="null"/>.</param>
        /// <param name="serverHttpAddress">The HTTP address of the server. If provided, the client will connect to the server using the HTTP address.
        /// If not provided, the client will connect to the server using the command-line interface.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="clientName"/> or <paramref name="serverName"/> is <see langword="null"/>.</exception>
        protected BaseMCPClient(string clientName, string serverName = "TimeseriesMCPServer", string serverHttpAddress = "", Kernel? kernel = null)
        {
            ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
            ServerName = serverName ?? throw new ArgumentNullException(nameof(serverName));
            _kernel = kernel;

            if (!string.IsNullOrEmpty(serverHttpAddress))
            {
                _mcpClient = CreateMcpClientForHttpServerAsync(serverHttpAddress, kernel: _kernel).GetAwaiter().GetResult();
            }
            else
            {
                _mcpClient = CreateMcpClientForCLServerAsync(kernel: _kernel).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Gets the MCP client instance used for communication with the MCP service.
        /// </summary>
        public IMcpClient McpClient
        {
            get
            {
                if (_mcpClient is null)
                {
                    throw new InvalidOperationException("MCP Client has not been initialized.");
                }
                return _mcpClient;
            }
        }

        /// <summary>
        /// Asynchronously retrieves a list of tools available from the MCP server.
        /// </summary>
        /// <remarks>This method requires the MCP client to be initialized before calling. If the client
        /// is not initialized, an <see cref="InvalidOperationException"/> is thrown.</remarks>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of  <see
        /// cref="McpClientTool"/> objects representing the tools available from the MCP server.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the MCP client has not been initialized.</exception>

        public async Task<IList<McpClientTool>> DiscoverToolsAsync()
        {
            if (_mcpClient is null)
            {
                throw new InvalidOperationException("MCP Client has not been initialized.");
            }
            // Retrieve and return the list of tools provided by the MCP server
            return await _mcpClient.ListToolsAsync();
        }


        /// <summary>
        /// Executes the specified tool with the provided parameters.
        /// </summary>
        /// <remarks>Ensure that the MCP client is properly initialized before calling this method.</remarks>
        /// <param name="toolName">The name of the tool to execute. Cannot be null or empty.</param>
        /// <param name="parameters">A dictionary containing the parameters to pass to the tool. Keys represent parameter names,  and values
        /// represent their corresponding values. Values can be null if the tool supports optional parameters.</param>
        /// <returns>A <see cref="CallToolResponse"/> object containing the result of the tool execution.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the MCP client has not been initialized.</exception>
        public async Task<CallToolResponse> ExecuteTool(string toolName, Dictionary<string, object?> parameters)
        {
            if (_mcpClient is null)
            {
                throw new InvalidOperationException("MCP Client has not been initialized.");
            }
            // Execute the specified tool with the provided parameters
            return await _mcpClient.CallToolAsync(toolName, parameters);
        }
        
        /// <summary>
        /// Creates an MCP client configured for communication with an HTTP server.
        /// </summary>
        /// <remarks>If <paramref name="samplingRequestHandler"/> is provided, the MCP client will be
        /// configured with  sampling capabilities, allowing it to handle sampling requests using the provided delegate.
        /// Otherwise, the client will be created without sampling capabilities.</remarks>
        /// <param name="serverAddress">The address of the HTTP server to connect to. Must be a valid URI.</param>
        /// <param name="kernel">An optional <see cref="Kernel"/> instance used for handling sampling requests. If not provided,  sampling
        /// requests will not be processed.</param>
        /// <param name="samplingRequestHandler">An optional delegate that processes sampling requests. The delegate takes a <see cref="Kernel"/>,  request
        /// parameters, a progress reporter, and a cancellation token, and returns a task that produces  a <see
        /// cref="CreateMessageResult"/>. If not provided, sampling capabilities will not be enabled.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is an <see cref="IMcpClient"/>  instance
        /// configured for communication with the specified HTTP server.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="samplingRequestHandler"/> is invoked and the request parameter is null.</exception>
        protected Task<IMcpClient> CreateMcpClientForHttpServerAsync(
            string serverAddress,
            Kernel? kernel = null,
            Func<Kernel, CreateMessageRequestParams?, IProgress<ProgressNotificationValue>, CancellationToken, Task<CreateMessageResult>>? samplingRequestHandler = null)
        {
            KernelFunction? skSamplingHandler = null;

            // Create and return the MCP client
            return McpClientFactory.CreateAsync(
            clientTransport: new SseClientTransport(new SseClientTransportOptions
            {
                Endpoint = new Uri(serverAddress),
                Name = ServerName,
                TransportMode = HttpTransportMode.StreamableHttp,
            }),
                clientOptions: samplingRequestHandler != null ? new McpClientOptions()
                {
                    Capabilities = new ClientCapabilities
                    {
                        Sampling = new SamplingCapability
                        {
                            SamplingHandler = InvokeHandlerAsync
                        },
                    },
                } : null
             );

            async ValueTask<CreateMessageResult> InvokeHandlerAsync(CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
            {
                if (request is null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                skSamplingHandler ??= KernelFunctionFactory.CreateFromMethod(
                    (CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken ct) =>
                    {
                        return samplingRequestHandler(kernel!, request, progress, ct);
                    },
                    "MCPSamplingHandler"
                );

                // The argument names must match the parameter names of the delegate the SK Function is created from
                KernelArguments kernelArguments = new()
                {
                    ["request"] = request,
                    ["progress"] = progress
                };

                FunctionResult functionResult = await skSamplingHandler.InvokeAsync(kernel!, kernelArguments, cancellationToken);

                return functionResult.GetValue<CreateMessageResult>()!;
            }
        }

        /// <summary>
        /// Creates an MCP client and connects it to the MCPServer server available through Command Line Interface (CLI).
        /// </summary>
        /// <param name="kernel">Optional kernel instance to use for the MCP client.</param>
        /// <param name="samplingRequestHandler">Optional handler for MCP sampling requests.</param>
        /// <returns>An instance of <see cref="IMcpClient"/>.</returns>
        protected Task<IMcpClient> CreateMcpClientForCLServerAsync(
            Kernel? kernel = null,
            Func<Kernel, CreateMessageRequestParams?, IProgress<ProgressNotificationValue>, CancellationToken, Task<CreateMessageResult>>? samplingRequestHandler = null)
        {
            KernelFunction? skSamplingHandler = null;

            // Create and return the MCP client
            return McpClientFactory.CreateAsync(
                clientTransport: new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = ServerName,
                    Command = GetMCPServerPath(), // Path to the MCPServer executable
                }),
                clientOptions: samplingRequestHandler != null ? new McpClientOptions()
                {
                    Capabilities = new ClientCapabilities
                    {
                        Sampling = new SamplingCapability
                        {
                            SamplingHandler = InvokeHandlerAsync
                        },
                    },
                } : null
             );

            async ValueTask<CreateMessageResult> InvokeHandlerAsync(CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
            {
                if (request is null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                skSamplingHandler ??= KernelFunctionFactory.CreateFromMethod(
                    (CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken ct) =>
                    {
                        return samplingRequestHandler(kernel!, request, progress, ct);
                    },
                    "MCPSamplingHandler"
                );

                // The argument names must match the parameter names of the delegate the SK Function is created from
                KernelArguments kernelArguments = new()
                {
                    ["request"] = request,
                    ["progress"] = progress
                };

                FunctionResult functionResult = await skSamplingHandler.InvokeAsync(kernel!, kernelArguments, cancellationToken);

                return functionResult.GetValue<CreateMessageResult>()!;
            }
        }
        /// <summary>
        /// Returns the path to the MCPServer server executable.
        /// </summary>
        /// <returns>The path to the MCPServer server executable.</returns>
        private string GetMCPServerPath()
        {
            // Determine the configuration (Debug or Release)  
            string configuration;

#if DEBUG
            configuration = "Debug";
#else
        configuration = "Release";
#endif
            Console.WriteLine($"Using configuration: {configuration}, building the path for the server to run!");
            return Path.Combine("..", "..", "..", "..", ServerName, "bin", configuration, "net8.0", $"{ServerName}.exe");
        }
    }
}
