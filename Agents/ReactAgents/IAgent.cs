namespace Agents
{
    public interface IAgent
    {
        string Name { get; }
        Task<string> ProcessAsync(string taskFromPlanner, Dictionary<string, string> plannerParameters, CancellationToken cancellationToken = default);
    }
}
