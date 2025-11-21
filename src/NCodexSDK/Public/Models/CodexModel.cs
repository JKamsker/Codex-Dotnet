namespace NCodexSDK.Public.Models;

/// <summary>
/// Represents a Codex model identifier with parsing and validation.
/// </summary>
/// <remarks>
/// This value object allows users to specify any model string without requiring library updates
/// when new models are released by Codex.
/// </remarks>
public readonly record struct CodexModel
{
    /// <summary>
    /// Gets the model identifier value.
    /// </summary>
    public string Value { get; }

    private CodexModel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Model identifier cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the default Codex model ("gpt-5.1-codex-max").
    /// </summary>
    public static CodexModel Default => new("gpt-5.1-codex-max");

    /// <summary>
    /// Gets the GPT-5.1 Codex Max model.
    /// </summary>
    public static CodexModel Gpt51CodexMax => new("gpt-5.1-codex-max");

    /// <summary>
    /// Gets the GPT-5.1 Codex model.
    /// </summary>
    public static CodexModel Gpt51Codex => new("gpt-5.1-codex");

    /// <summary>
    /// Gets the GPT-5.1 Codex Mini model.
    /// </summary>
    public static CodexModel Gpt51CodexMini => new("gpt-5.1-codex-mini");

    /// <summary>
    /// Gets the GPT-5.1 general model.
    /// </summary>
    public static CodexModel Gpt51 => new("gpt-5.1");

    /// <summary>
    /// Creates a CodexModel from a string value.
    /// </summary>
    /// <param name="value">The model identifier string.</param>
    /// <returns>A CodexModel instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is empty or whitespace.</exception>
    public static CodexModel Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a string into a CodexModel.
    /// </summary>
    /// <param name="value">The model identifier string.</param>
    /// <param name="model">The parsed CodexModel if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out CodexModel model)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            model = default;
            return false;
        }

        model = new CodexModel(value);
        return true;
    }

    /// <summary>
    /// Implicitly converts a string to a CodexModel.
    /// </summary>
    public static implicit operator CodexModel(string value) => Parse(value);

    /// <summary>
    /// Implicitly converts a CodexModel to a string.
    /// </summary>
    public static implicit operator string(CodexModel model) => model.Value;

    /// <summary>
    /// Returns the string representation of the model identifier.
    /// </summary>
    public override string ToString() => Value;
}
