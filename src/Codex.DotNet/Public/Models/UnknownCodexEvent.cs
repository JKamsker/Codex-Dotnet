namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents an unknown or unrecognized Codex event.
/// </summary>
/// <remarks>
/// This event type is used for events that don't match any known event schema.
/// The RawPayload property from the base class can be used to access the original data.
/// This design ensures forward compatibility when new event types are introduced.
/// </remarks>
public record UnknownCodexEvent : CodexEvent
{
    // No additional properties - uses base class RawPayload for extensibility
}
