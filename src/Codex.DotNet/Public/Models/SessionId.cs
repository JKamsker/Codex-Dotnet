namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents a unique Codex session identifier with validation.
/// </summary>
/// <remarks>
/// The session ID is treated as an opaque string. Current Codex CLI emits UUID-like values,
/// but the library remains flexible to accept future ID formats.
/// </remarks>
public readonly record struct SessionId
{
    /// <summary>
    /// Gets the session identifier value.
    /// </summary>
    public string Value { get; }

    private SessionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Session ID cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Creates a SessionId from a string value.
    /// </summary>
    /// <param name="value">The session identifier string.</param>
    /// <returns>A SessionId instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is empty or whitespace.</exception>
    public static SessionId Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a string into a SessionId.
    /// </summary>
    /// <param name="value">The session identifier string.</param>
    /// <param name="sessionId">The parsed SessionId if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out SessionId sessionId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            sessionId = default;
            return false;
        }

        sessionId = new SessionId(value);
        return true;
    }

    /// <summary>
    /// Implicitly converts a string to a SessionId.
    /// </summary>
    public static implicit operator SessionId(string value) => Parse(value);

    /// <summary>
    /// Implicitly converts a SessionId to a string.
    /// </summary>
    public static implicit operator string(SessionId sessionId) => sessionId.Value;

    /// <summary>
    /// Returns the string representation of the session ID.
    /// </summary>
    public override string ToString() => Value;
}
