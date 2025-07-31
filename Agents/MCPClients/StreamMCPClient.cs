using Microsoft.SemanticKernel;

namespace Agents
{
    public class StreamMCPClient : BaseMCPClient
    {
        public StreamMCPClient(string clientName, string server, Kernel kernel) : base(clientName, server, kernel: kernel)
        {
        }
    }
}
