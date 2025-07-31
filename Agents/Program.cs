namespace Agents;

internal class Program
{
    public static async Task Main(string[] args)
    {

        //string question = "What are the bearing temperature tags for GE07, and do you have any stream values for that from June 17th 6:10 to June 18th 6:10 in 2025?";
        //string question = "What tools do you have on Capability Server?";
        string question = "What are the Stream Types for instance id 'digital-twin-sds-jk'? and What are the value for tag GE07 from June 17th 6:10 to June 18th 6:10 in 2025??";
        //string question = "What are the value for tag GE07 from June 17th 6:10 to June 18th 6:10 in 2025??";

        var orchestrator = new Orchestrator();
        string result = await orchestrator.OrchestrateAsync(question).ConfigureAwait(false);
    }   
}