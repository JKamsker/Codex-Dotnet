namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a message event from the Codex agent.
/// </summary>
/// <remarks>
/// This event is emitted when the Codex agent sends a response message.
/// </remarks>
public record AgentMessageEvent : CodexEvent
{
    /// <summary>
    /// Gets the text content of the agent's message.
    /// </summary>
    public required string Text { get; init; }
}
