namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a session metadata event containing session configuration and context.
/// </summary>
/// <remarks>
/// This event is typically emitted at the start of a session and contains
/// information about the session identifier and working directory.
/// </remarks>
public record SessionMetaEvent : CodexEvent
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public required SessionId SessionId { get; init; }

    /// <summary>
    /// Gets the current working directory for the session.
    /// </summary>
    /// <remarks>
    /// May be null if the working directory information is not available.
    /// </remarks>
    public string? Cwd { get; init; }
}
