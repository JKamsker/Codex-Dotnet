namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a message event from the user.
/// </summary>
/// <remarks>
/// This event is emitted when the user sends a message to the Codex agent.
/// </remarks>
public record UserMessageEvent : CodexEvent
{
    /// <summary>
    /// Gets the text content of the user's message.
    /// </summary>
    public required string Text { get; init; }
}
