namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents an agent reasoning event containing internal thought processes.
/// </summary>
/// <remarks>
/// This event is emitted when the Codex agent's reasoning mode is enabled,
/// providing insight into the agent's decision-making process.
/// </remarks>
public record AgentReasoningEvent : CodexEvent
{
    /// <summary>
    /// Gets the text content of the agent's reasoning.
    /// </summary>
    public required string Text { get; init; }
}
