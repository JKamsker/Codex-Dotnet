namespace Codex.DotNet.Public.Models;

/// <summary>
/// Represents Codex reasoning effort level with extensibility for custom values.
/// </summary>
/// <remarks>
/// This value object pattern allows extensibility without library updates
/// when Codex introduces new reasoning effort levels.
/// </remarks>
public readonly record struct CodexReasoningEffort
{
    /// <summary>
    /// Gets the effort level identifier.
    /// </summary>
    public string Value { get; }

    private CodexReasoningEffort(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Reasoning effort cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the low reasoning effort level - minimal reasoning, faster responses.
    /// </summary>
    public static CodexReasoningEffort Low => new("low");

    /// <summary>
    /// Gets the medium reasoning effort level - balanced reasoning (default).
    /// </summary>
    public static CodexReasoningEffort Medium => new("medium");

    /// <summary>
    /// Gets the high reasoning effort level - maximum reasoning, thorough analysis.
    /// </summary>
    public static CodexReasoningEffort High => new("high");

    /// <summary>
    /// Creates a CodexReasoningEffort from a string value.
    /// </summary>
    /// <param name="value">The effort level identifier.</param>
    /// <returns>A CodexReasoningEffort instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is empty or whitespace.</exception>
    public static CodexReasoningEffort Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a string into a CodexReasoningEffort.
    /// </summary>
    /// <param name="value">The effort level identifier.</param>
    /// <param name="effort">The parsed CodexReasoningEffort if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out CodexReasoningEffort effort)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            effort = default;
            return false;
        }

        effort = new CodexReasoningEffort(value);
        return true;
    }

    /// <summary>
    /// Implicitly converts a string to a CodexReasoningEffort.
    /// </summary>
    public static implicit operator CodexReasoningEffort(string value) => Parse(value);

    /// <summary>
    /// Implicitly converts a CodexReasoningEffort to a string.
    /// </summary>
    public static implicit operator string(CodexReasoningEffort effort) => effort.Value;

    /// <summary>
    /// Returns the string representation of the reasoning effort level.
    /// </summary>
    public override string ToString() => Value;
}
