using System.Text.Json;

namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a response_item event emitted by Codex, carrying a typed payload
/// such as reasoning, message, function_call, or ghost_snapshot.
/// </summary>
public sealed record ResponseItemEvent : CodexEvent
{
    /// <summary>
    /// Gets the payload type (e.g., "reasoning", "message", "function_call").
    /// </summary>
    public required string PayloadType { get; init; }

    /// <summary>
    /// Gets the normalized payload data, with common fields extracted when known.
    /// </summary>
    public required ResponseItemPayload Payload { get; init; }
}
