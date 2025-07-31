using Microsoft.SemanticKernel;

namespace Agents
{
    public class CapabilityMCPClient : BaseMCPClient
    {
        public CapabilityMCPClient(string clientName, string server, string serverHttpAddress, Kernel kernel) : base(clientName, server, serverHttpAddress, kernel: kernel)
        {
        }
    }
}
