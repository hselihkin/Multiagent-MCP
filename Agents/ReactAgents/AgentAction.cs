namespace Agents
{
    /// <summary>
    /// Represents an action taken by an agent, including the tool to use, its input, and the agent's thought process.
    /// </summary>
    /// <remarks>This class encapsulates the details of an agent's decision-making process, including the tool
    /// selected for execution, the input provided to the tool, and the agent's reasoning behind the action. It also
    /// indicates whether the action represents the final answer and optionally includes the agent's output.</remarks>
    public class AgentAction
    {
        public string ToolToUse { get; set; } = string.Empty;
        public Dictionary<string, string> ToolInput { get; set; } = new Dictionary<string, string>();
        public string Thought { get; set; } = string.Empty;
        public bool IsFinalAnswer { get; set; } = false;
        public string? AgentOutput { get; set; }
    }
}
