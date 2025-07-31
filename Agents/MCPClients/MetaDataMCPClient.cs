using Microsoft.SemanticKernel;

namespace Agents
{
    public class MetaDataMCPClient : BaseMCPClient
    {
        public MetaDataMCPClient(string clientName, string server, Kernel kernel) : base(clientName, serverName: server, kernel: kernel)
        {
        }
    }
}
