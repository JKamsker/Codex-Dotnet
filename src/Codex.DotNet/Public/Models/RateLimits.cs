namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents rate limit usage scopes returned by Codex.
/// </summary>
public sealed record RateLimits(
    RateLimitScope? Primary,
    RateLimitScope? Secondary,
    RateLimitCredits? Credits);

/// <summary>
/// Represents a single rate limit scope (e.g., primary 5-hour, secondary weekly).
/// </summary>
/// <param name="UsedPercent">Percentage of the window that has been consumed.</param>
/// <param name="WindowMinutes">Length of the rate limit window in minutes, if provided.</param>
/// <param name="ResetsAt">UTC time when the window resets, if provided.</param>
public sealed record RateLimitScope(
    double? UsedPercent,
    int? WindowMinutes,
    DateTimeOffset? ResetsAt);

/// <summary>
/// Represents credit availability reported with rate limits.
/// </summary>
/// <param name="HasCredits">Whether the account has credits.</param>
/// <param name="Unlimited">Whether credits are unlimited.</param>
/// <param name="Balance">Remaining balance text/currency as reported.</param>
public sealed record RateLimitCredits(
    bool? HasCredits,
    bool? Unlimited,
    string? Balance);
