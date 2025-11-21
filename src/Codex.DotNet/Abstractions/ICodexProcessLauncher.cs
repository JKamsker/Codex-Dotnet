using System.Diagnostics;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for launching and managing Codex CLI processes.
/// </summary>
/// <remarks>
/// This interface provides methods for starting new Codex sessions as separate processes
/// and terminating them gracefully or forcefully. Implementations should handle process
/// lifecycle management, including proper cleanup and error handling.
/// </remarks>
public interface ICodexProcessLauncher
{
    /// <summary>
    /// Starts a new Codex CLI process with the specified session and client options.
    /// </summary>
    /// <param name="options">The session configuration options.</param>
    /// <param name="clientOptions">The client configuration options.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the start operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous start operation. The task result contains
    /// the started <see cref="Process"/> instance.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="clientOptions"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Codex executable cannot be found or the process fails to start.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method constructs the appropriate command-line arguments based on the provided options,
    /// starts the Codex CLI process, and returns the Process instance for monitoring and control.
    /// The process is started with redirected standard input/output/error streams for interaction.
    /// The caller is responsible for managing the lifetime of the returned Process.
    /// </remarks>
    Task<Process> StartSessionAsync(
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts the Codex CLI in resume mode for a given session id.
    /// </summary>
    /// <param name="sessionId">The existing session to resume.</param>
    /// <param name="options">Session options (working directory, prompt, model, flags).</param>
    /// <param name="clientOptions">Client-wide options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The started Codex process.</returns>
    Task<Process> ResumeSessionAsync(
        SessionId sessionId,
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Terminates a Codex CLI process gracefully, with forceful termination as a fallback.
    /// </summary>
    /// <param name="process">The process to terminate.</param>
    /// <param name="timeout">
    /// The maximum time to wait for graceful termination before forcing termination.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the termination operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous termination operation. The task result contains
    /// the exit code of the process, or -1 if the process was forcefully terminated.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="process"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the process has already exited or is in an invalid state.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method first attempts to terminate the process gracefully by sending a termination signal.
    /// If the process does not exit within the specified <paramref name="timeout"/>, it will be forcefully killed.
    /// The method waits for the process to exit completely before returning.
    /// If the process has already exited, this method returns immediately with the exit code.
    /// </remarks>
    Task<int> TerminateProcessAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
