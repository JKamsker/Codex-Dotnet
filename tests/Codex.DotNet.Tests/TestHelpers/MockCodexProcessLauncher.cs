using System.Diagnostics;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ICodexProcessLauncher for testing purposes.
/// </summary>
/// <remarks>
/// This implementation returns fake Process objects and captures session options
/// for test verification without actually launching Codex processes.
/// </remarks>
public class MockCodexProcessLauncher : ICodexProcessLauncher
{
    private readonly List<SessionStartCapture> _capturedStarts = new();
    private readonly List<ProcessTerminationCapture> _capturedTerminations = new();

    /// <summary>
    /// Gets or sets whether StartSessionAsync should simulate a failure.
    /// </summary>
    public bool SimulateStartFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception to throw when simulating a start failure.
    /// </summary>
    public Exception? StartFailureException { get; set; }

    /// <summary>
    /// Gets or sets the exit code to return from TerminateProcessAsync.
    /// </summary>
    public int TerminateExitCode { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether TerminateProcessAsync should simulate a failure.
    /// </summary>
    public bool SimulateTerminateFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception to throw when simulating a termination failure.
    /// </summary>
    public Exception? TerminateFailureException { get; set; }

    /// <summary>
    /// Gets or sets the delay before StartSessionAsync completes.
    /// </summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the delay before TerminateProcessAsync completes.
    /// </summary>
    public TimeSpan TerminateDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the list of captured session start invocations.
    /// </summary>
    public IReadOnlyList<SessionStartCapture> CapturedStarts => _capturedStarts;

    /// <summary>
    /// Gets the list of captured process termination invocations.
    /// </summary>
    public IReadOnlyList<ProcessTerminationCapture> CapturedTerminations => _capturedTerminations;

    /// <summary>
    /// Creates a new instance of MockCodexProcessLauncher.
    /// </summary>
    public MockCodexProcessLauncher()
    {
    }

    /// <summary>
    /// Resets all captured data and configuration to defaults.
    /// </summary>
    public void Reset()
    {
        _capturedStarts.Clear();
        _capturedTerminations.Clear();
        SimulateStartFailure = false;
        StartFailureException = null;
        TerminateExitCode = 0;
        SimulateTerminateFailure = false;
        TerminateFailureException = null;
        StartDelay = TimeSpan.Zero;
        TerminateDelay = TimeSpan.Zero;
    }

    /// <inheritdoc />
    public async Task<Process> StartSessionAsync(
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        // Capture the invocation
        var capture = new SessionStartCapture
        {
            Options = options,
            ClientOptions = clientOptions,
            Timestamp = DateTimeOffset.UtcNow
        };
        _capturedStarts.Add(capture);

        // Simulate delay if configured
        if (StartDelay > TimeSpan.Zero)
        {
            await Task.Delay(StartDelay, cancellationToken);
        }

        // Simulate failure if configured
        if (SimulateStartFailure)
        {
            throw StartFailureException ?? new InvalidOperationException("Simulated start failure");
        }

        // Return a mock process
        // Note: We cannot create a real Process instance without actually starting a process,
        // so we return null and tests should handle this appropriately.
        // In a real test, you might use a test process like "cmd.exe" or "ping" on Windows
        var process = CreateMockProcess();
        return process;
    }

    /// <inheritdoc />
    public Task<Process> ResumeSessionAsync(
        SessionId sessionId,
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken)
    {
        // For testing, reuse StartSessionAsync behavior.
        return StartSessionAsync(options, clientOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> TerminateProcessAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        // Capture the invocation
        var capture = new ProcessTerminationCapture
        {
            Process = process,
            Timeout = timeout,
            Timestamp = DateTimeOffset.UtcNow
        };
        _capturedTerminations.Add(capture);

        // Simulate delay if configured
        if (TerminateDelay > TimeSpan.Zero)
        {
            await Task.Delay(TerminateDelay, cancellationToken);
        }

        // Simulate failure if configured
        if (SimulateTerminateFailure)
        {
            throw TerminateFailureException ?? new InvalidOperationException("Simulated termination failure");
        }

        // Return configured exit code
        return TerminateExitCode;
    }

    /// <summary>
    /// Creates a mock process for testing.
    /// </summary>
    /// <remarks>
    /// This creates a real process that does nothing and exits immediately.
    /// On Windows, it uses "cmd.exe /c exit 0". On Unix, it uses "true".
    /// </remarks>
    private static Process CreateMockProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "sh",
            Arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c true",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    /// <summary>
    /// Represents a captured session start invocation.
    /// </summary>
    public class SessionStartCapture
    {
        public required CodexSessionOptions Options { get; init; }
        public required CodexClientOptions ClientOptions { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }

    /// <summary>
    /// Represents a captured process termination invocation.
    /// </summary>
    public class ProcessTerminationCapture
    {
        public required Process Process { get; init; }
        public required TimeSpan Timeout { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}
