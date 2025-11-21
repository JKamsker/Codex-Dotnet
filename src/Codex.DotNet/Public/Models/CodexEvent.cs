using System.Text.Json;

namespace Codex.DotNet.Public.Models;

/// <summary>
/// Abstract base class for all Codex events.
/// </summary>
/// <remarks>
/// All events emitted by the Codex CLI inherit from this base class.
/// The RawPayload property preserves the original JSON for extensibility.
/// </remarks>
public abstract record CodexEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the event type identifier.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the raw JSON payload of the event.
    /// </summary>
    /// <remarks>
    /// This property preserves the original event data for custom processing
    /// and future-proofing against schema changes.
    /// </remarks>
    public required JsonElement RawPayload { get; init; }
}
