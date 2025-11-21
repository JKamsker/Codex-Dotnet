namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a turn context event containing execution context information.
/// </summary>
/// <remarks>
/// This event provides information about the current turn's execution context,
/// including approval policies and sandbox settings.
/// </remarks>
public record TurnContextEvent : CodexEvent
{
    /// <summary>
    /// Gets the approval policy for the current turn.
    /// </summary>
    /// <remarks>
    /// May be null if approval policy information is not available.
    /// Common values include "auto", "manual", or custom policy identifiers.
    /// </remarks>
    public string? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets the sandbox policy type for the current turn.
    /// </summary>
    /// <remarks>
    /// May be null if sandbox policy information is not available.
    /// Common values include "none", "strict", or other sandbox configuration identifiers.
    /// </remarks>
    public string? SandboxPolicyType { get; init; }
}
