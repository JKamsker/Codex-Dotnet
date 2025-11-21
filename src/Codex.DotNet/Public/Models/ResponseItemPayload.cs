using System.Text.Json;

namespace Codex.DotNet.Public.Models;

/// <summary>
/// Normalized fields extracted from a response_item payload.
/// Additional optional fields enable easy extension as Codex adds payload types.
/// </summary>
public sealed record ResponseItemPayload
{
    /// <summary>Payload type string, e.g., reasoning, message, function_call.</summary>
    public required string PayloadType { get; init; }

    /// <summary>Raw payload JSON for forward compatibility.</summary>
    public required JsonElement Raw { get; init; }

    /// <summary>Reasoning summary texts (if payload_type == reasoning).</summary>
    public IReadOnlyList<string>? SummaryTexts { get; init; }

    /// <summary>Assistant/user role for message payloads.</summary>
    public string? MessageRole { get; init; }

    /// <summary>Flattened text segments for message payloads.</summary>
    public IReadOnlyList<string>? MessageTextParts { get; init; }

    /// <summary>Function call information when payload_type == function_call.</summary>
    public FunctionCallInfo? FunctionCall { get; init; }

    /// <summary>Ghost snapshot commit id when payload_type == ghost_snapshot.</summary>
    public string? GhostCommitId { get; init; }
}

/// <summary>Represents a function_call payload body.</summary>
public sealed record FunctionCallInfo(string? Name, string? ArgumentsJson, string? CallId);
