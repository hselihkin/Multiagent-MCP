# Model Context Protocol (MCP) Exploration
The repo contains an implementation of MCP Server (based on Semantic Kernel) and a client to go with the server. 
The server comprises of a set of mock tools, prompts, and resources designed to explore the MCP. The MCP is a protocol that allows for efficient communication between models and clients, enabling better context management and interaction.

## Components
### Timeseries MCP Server
A sample server is created. Currently, it is a mock server that simulates the behavior of an MCP server.
### Timeseries MCP Client
A sample client is created. The client is connected to the mock MCP server. Later, the client will be extended to connect to a real MCP server.
### Agents
Work in Progress.

## Running locally
Steps to run the MCP server and client:

0. **Prerequisites**: Ensure to collect *secrets.json*.
1. **Start the MCP Server**: Build the *TimeseriesMCPServer*. As of now, the client will connect to this server through CLI.
2. **Run the Client**: Set the *TimeseriesMCPClient* as the *Start-up Project*. The client will run two samples (can be found in "Program.cs"); Tool Sample and Prompt Sample.

	1. **Tool Sample**: The client will call the server to get a list of MCP tools available to the TimeseriesMCPServer. The extracted tools will be integrated into the Semantic Kernel instance. The Semantic Kernel will then take any query related to the Tools, execute the required tool(s), and synthesize the results..
	2. **Prompt Sample**: The client will call the server to get a list of MCP prompts available to the TimeseriesMCPServer. The extracted prompts will be integrated into the Semantic Kernel instance. The Semantic Kernel will then take any query related to the Prompts, and execute the prompts.
