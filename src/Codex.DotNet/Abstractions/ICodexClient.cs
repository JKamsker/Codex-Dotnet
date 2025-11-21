using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines the main client interface for interacting with Codex CLI sessions.
/// </summary>
/// <remarks>
/// This interface provides the primary API for managing Codex sessions, including
/// starting new sessions, resuming existing ones, attaching to log files, and
/// querying session metadata. It serves as the entry point for all Codex operations.
/// </remarks>
public interface ICodexClient : IDisposable
{
    /// <summary>
    /// Starts a new Codex session with the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the new session.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the start operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous start operation. The task result contains
    /// an <see cref="ICodexSessionHandle"/> that provides access to the started session.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are invalid, the Codex executable cannot be found,
    /// or the session fails to start.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method launches a new Codex CLI process with the specified configuration,
    /// waits for the session to be initialized, and returns a handle for interacting with it.
    /// The session runs asynchronously in a background process, and events can be consumed
    /// through the returned handle's event stream.
    /// The caller is responsible for disposing the returned handle when done with the session.
    /// </remarks>
    Task<ICodexSessionHandle> StartSessionAsync(
        CodexSessionOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resumes a Codex session by launching the Codex CLI in <c>resume</c> mode,
    /// enabling follow-up turns on an existing session.
    /// </summary>
    /// <param name="sessionId">The session identifier to resume.</param>
    /// <param name="options">
    /// Session options providing working directory, prompt (follow-up message),
    /// model selection, and additional CLI flags.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A live <see cref="ICodexSessionHandle"/> connected to the resumed Codex process.
    /// </returns>
    /// <remarks>
    /// This differs from <see cref="ResumeSessionAsync(SessionId, CancellationToken)"/>,
    /// which only attaches to an existing log file. This overload launches the Codex
    /// process in resume mode so the session can continue generating responses.
    /// </remarks>
    Task<ICodexSessionHandle> ResumeSessionAsync(
        SessionId sessionId,
        CodexSessionOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resumes an existing Codex session by its session ID.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session to resume.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the resume operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous resume operation. The task result contains
    /// an <see cref="ICodexSessionHandle"/> that provides access to the resumed session.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sessionId"/> is invalid or empty.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the session log file cannot be found.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session cannot be resumed (e.g., corrupted log file).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method locates the session's log file and creates a read-only handle for
    /// accessing the session's historical events. The returned handle is not a live session
    /// and cannot be used to send new prompts or interact with a running process.
    /// Use <see cref="AttachToLogAsync"/> if you have the direct path to the log file.
    /// The caller is responsible for disposing the returned handle when done.
    /// </remarks>
    Task<ICodexSessionHandle> ResumeSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attaches to an existing session log file for reading events.
    /// </summary>
    /// <param name="logFilePath">The absolute path to the session log file.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the attach operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous attach operation. The task result contains
    /// an <see cref="ICodexSessionHandle"/> that provides access to the log file's events.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logFilePath"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified log file does not exist.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the log file is invalid or cannot be read.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method creates a read-only handle for accessing events from an existing log file.
    /// It's useful for analyzing session logs, debugging, or post-processing session data.
    /// The log file can be from a completed session or a currently running session.
    /// If the log file is actively being written (live session), the handle will tail the file
    /// and yield new events as they are appended.
    /// The caller is responsible for disposing the returned handle when done.
    /// </remarks>
    Task<ICodexSessionHandle> AttachToLogAsync(
        string logFilePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates sessions that match the specified filter criteria.
    /// </summary>
    /// <param name="filter">
    /// Optional filter criteria to apply when listing sessions.
    /// When null, all sessions are returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the enumeration operation.
    /// </param>
    /// <returns>
    /// An async enumerable that yields <see cref="CodexSessionInfo"/> instances for each matching session.
    /// Sessions are yielded as they are discovered.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the sessions root directory does not exist.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method scans the sessions directory for session log files and returns metadata
    /// for sessions that match the filter criteria. Sessions are yielded in an undefined order.
    /// Invalid or corrupted session directories are skipped without throwing exceptions.
    /// The filter can include criteria such as date ranges, working directories, models, and session ID patterns.
    /// </remarks>
    IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        SessionFilter? filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the most recent rate limit information emitted by Codex.
    /// </summary>
    /// <param name="noCache">When true, forces a fresh read from session logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest <see cref="RateLimits"/> if available; otherwise null.</returns>
    Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default);
}
