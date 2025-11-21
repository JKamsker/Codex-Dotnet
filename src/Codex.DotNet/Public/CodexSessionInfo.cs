using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Public;

/// <summary>
/// Represents metadata information about a Codex session.
/// </summary>
/// <remarks>
/// This record encapsulates the essential information about a Codex session including
/// its unique identifier, log file location, creation timestamp, and configuration details.
/// It is typically returned when querying or listing sessions.
/// </remarks>
/// <param name="Id">
/// The unique identifier for the session.
/// </param>
/// <param name="LogPath">
/// The file system path to the session's log file.
/// This path can be used to read event streams or access raw session data.
/// </param>
/// <param name="CreatedAt">
/// The timestamp when the session was created.
/// </param>
/// <param name="WorkingDirectory">
/// Optional working directory path where the session was started.
/// This indicates the context in which the Codex CLI was invoked.
/// </param>
/// <param name="Model">
/// Optional model identifier that was used for this session.
/// Indicates which Codex model processed the session requests.
/// </param>
/// <param name="HumanLabel">
/// Optional human-readable label or description for the session.
/// Can be used to provide context or categorize sessions for easier identification.
/// </param>
public sealed record CodexSessionInfo(
    SessionId Id,
    string LogPath,
    DateTimeOffset CreatedAt,
    string? WorkingDirectory = null,
    CodexModel? Model = null,
    string? HumanLabel = null)
{
    /// <summary>
    /// Gets the unique identifier for the session.
    /// </summary>
    public SessionId Id { get; init; } = Id;

    private readonly string _logPath = LogPath;

    /// <summary>
    /// Gets the file system path to the session's log file.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the value is null.
    /// </exception>
    public string LogPath
    {
        get => _logPath;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Log path cannot be empty or whitespace.", nameof(LogPath));

            _logPath = value;
        }
    }

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = CreatedAt;

    /// <summary>
    /// Gets the optional working directory path where the session was started.
    /// </summary>
    public string? WorkingDirectory { get; init; } = WorkingDirectory;

    /// <summary>
    /// Gets the optional model identifier that was used for this session.
    /// </summary>
    public CodexModel? Model { get; init; } = Model;

    /// <summary>
    /// Gets the optional human-readable label or description for the session.
    /// </summary>
    public string? HumanLabel { get; init; } = HumanLabel;

    /// <summary>
    /// Returns a string representation of the session info.
    /// </summary>
    /// <returns>
    /// A formatted string containing the session ID, creation time, and model (if specified).
    /// </returns>
    public override string ToString()
    {
        var modelInfo = Model.HasValue ? $", Model: {Model.Value}" : string.Empty;
        var labelInfo = !string.IsNullOrWhiteSpace(HumanLabel) ? $", Label: {HumanLabel}" : string.Empty;
        return $"Session {Id} (Created: {CreatedAt:yyyy-MM-dd HH:mm:ss}{modelInfo}{labelInfo})";
    }
}
