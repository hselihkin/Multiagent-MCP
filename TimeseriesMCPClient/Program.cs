using System.Threading.Tasks;
using MCPClient.Samples;

namespace MCPClient;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        // Run the TimeseriesToolsSample
        await TimeseriesToolsSample.RunAsync();

        // Run the TimeseriesPromptSample
        //await TimeseriesPromptSample.RunAsync();
    }   
}