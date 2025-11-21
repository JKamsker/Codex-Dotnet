using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for resolving Codex-related file system paths.
/// </summary>
/// <remarks>
/// This interface provides methods for locating the Codex CLI executable,
/// session storage directories, and session log files. Implementations should
/// handle platform-specific path resolution and provide sensible defaults.
/// </remarks>
public interface ICodexPathProvider
{
    /// <summary>
    /// Gets the path to the Codex CLI executable.
    /// </summary>
    /// <param name="overridePath">
    /// An optional explicit path to the Codex executable. When provided, this path is validated and returned.
    /// When null, the provider searches for the executable in standard locations or the system PATH.
    /// </param>
    /// <returns>
    /// The absolute path to the Codex CLI executable.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the Codex executable cannot be found at the override path or in standard locations.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the executable is found but is not valid or executable.
    /// </exception>
    /// <remarks>
    /// When <paramref name="overridePath"/> is null, the provider typically searches:
    /// <list type="bullet">
    /// <item><description>The system PATH environment variable</description></item>
    /// <item><description>Platform-specific default installation directories</description></item>
    /// <item><description>Common user-level installation locations</description></item>
    /// </list>
    /// </remarks>
    string GetCodexExecutablePath(string? overridePath);

    /// <summary>
    /// Gets the root directory where Codex session data is stored.
    /// </summary>
    /// <param name="overrideDirectory">
    /// An optional explicit path to the sessions root directory. When provided, this path is validated and returned.
    /// When null, the provider returns the default Codex sessions directory for the platform.
    /// </param>
    /// <returns>
    /// The absolute path to the sessions root directory.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the override directory is specified but does not exist.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the default sessions directory cannot be determined.
    /// </exception>
    /// <remarks>
    /// When <paramref name="overrideDirectory"/> is null, the provider returns the platform-specific default:
    /// <list type="bullet">
    /// <item><description>Unix-like systems: ~/.codex/sessions</description></item>
    /// <item><description>Windows: %USERPROFILE%\.codex\sessions</description></item>
    /// </list>
    /// The directory is created if it does not exist when using the default path.
    /// </remarks>
    string GetSessionsRootDirectory(string? overrideDirectory);

    /// <summary>
    /// Resolves the file path to a session's log file.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session.</param>
    /// <param name="sessionsRoot">
    /// The root directory containing session data. When null, uses the default sessions directory.
    /// </param>
    /// <returns>
    /// The absolute path to the session's log file.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sessionId"/> is invalid or empty.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the session log file cannot be found in the expected location.
    /// </exception>
    /// <remarks>
    /// This method constructs the expected path to a session log file based on Codex CLI conventions.
    /// The typical log file format is: {sessionsRoot}/{sessionId}/session.jsonl
    /// If the file does not exist at the expected location, this method throws an exception.
    /// </remarks>
    string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot);
}
