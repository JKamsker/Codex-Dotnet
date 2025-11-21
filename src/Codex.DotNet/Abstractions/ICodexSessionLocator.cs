using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for locating and discovering Codex session files.
/// </summary>
/// <remarks>
/// This interface provides methods for finding session log files, waiting for new sessions
/// to be created, and querying sessions based on filter criteria. Implementations should
/// handle file system monitoring and provide efficient session discovery.
/// </remarks>
public interface ICodexSessionLocator
{
    /// <summary>
    /// Waits for a new session file to be created after the specified start time.
    /// </summary>
    /// <param name="sessionsRoot">The root directory containing session data.</param>
    /// <param name="startTime">
    /// The reference timestamp. Only session files created after this time are considered.
    /// </param>
    /// <param name="timeout">
    /// The maximum time to wait for a new session file to appear.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the wait operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous wait operation. The task result contains
    /// the absolute path to the newly created session log file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionsRoot"/> is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the sessions root directory does not exist.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when no new session file is found within the specified <paramref name="timeout"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method polls the sessions root directory for new session subdirectories and log files.
    /// It's typically used after starting a new Codex process to discover the session log file path.
    /// The implementation should use efficient polling with appropriate intervals to minimize overhead.
    /// </remarks>
    Task<string> WaitForNewSessionFileAsync(
        string sessionsRoot,
        DateTimeOffset startTime,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the log file path for a specific session.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session to locate.</param>
    /// <param name="sessionsRoot">The root directory containing session data.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the search operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous search operation. The task result contains
    /// the absolute path to the session's log file.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sessionId"/> is invalid or empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionsRoot"/> is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the sessions root directory does not exist.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the session log file cannot be found.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method searches for a session's log file using the session ID.
    /// The typical search pattern is: {sessionsRoot}/{sessionId}/session.jsonl
    /// If the session directory exists but the log file is missing, an exception is thrown.
    /// </remarks>
    Task<string> FindSessionLogAsync(
        SessionId sessionId,
        string sessionsRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the log file of a specific session ID to become available.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="sessionsRoot">Root directory containing session logs.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the session log file.</returns>
    Task<string> WaitForSessionLogByIdAsync(
        SessionId sessionId,
        string sessionsRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates that a provided log file path points to a readable JSONL file.
    /// </summary>
    /// <param name="logFilePath">Absolute path to the JSONL log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated absolute log file path.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logFilePath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="logFilePath"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be read due to permissions.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be opened for reading.</exception>
    Task<string> ValidateLogFileAsync(
        string logFilePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates all sessions in the sessions root directory that match the specified filter criteria.
    /// </summary>
    /// <param name="sessionsRoot">The root directory containing session data.</param>
    /// <param name="filter">
    /// Optional filter criteria to apply when enumerating sessions.
    /// When null, all sessions are returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the enumeration operation.
    /// </param>
    /// <returns>
    /// An async enumerable that yields <see cref="CodexSessionInfo"/> instances for each matching session.
    /// Sessions are yielded as they are discovered, allowing for streaming consumption.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionsRoot"/> is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the sessions root directory does not exist.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method scans the sessions root directory for session subdirectories and their log files.
    /// Each valid session is parsed to extract metadata and filtered according to the provided criteria.
    /// Sessions are yielded in an undefined order. The caller should not assume any specific ordering.
    /// Invalid or corrupted session directories are skipped without throwing exceptions.
    /// </remarks>
    IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        string sessionsRoot,
        SessionFilter? filter,
        CancellationToken cancellationToken);
}
