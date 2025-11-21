using System.IO;

namespace Codex.DotNet.Public;

/// <summary>
/// Represents configuration options for the Codex client.
/// </summary>
/// <remarks>
/// This class provides settings for configuring the Codex client behavior including
/// executable location, session storage, and various timeout and polling settings.
/// All properties are mutable to support flexible configuration patterns.
/// </remarks>
public class CodexClientOptions
{
    private static readonly TimeSpan MinimumTailPollInterval = TimeSpan.FromMilliseconds(50);

    private TimeSpan _startTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _processExitTimeout = TimeSpan.FromSeconds(5);
    private TimeSpan _tailPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the path to the Codex CLI executable.
    /// </summary>
    /// <remarks>
    /// When null, the client will attempt to locate the Codex executable using system PATH
    /// or platform-specific default locations.
    /// </remarks>
    public string? CodexExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the root directory where session data is stored.
    /// </summary>
    /// <remarks>
    /// When null, the default Codex session directory will be used (typically ~/.codex/sessions
    /// on Unix-like systems or %USERPROFILE%\.codex\sessions on Windows).
    /// </remarks>
    public string? SessionsRootDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum time to wait for the Codex process to start.
    /// </summary>
    /// <remarks>
    /// Default is 30 seconds. This timeout applies when starting a new Codex session
    /// and waiting for the process to become ready.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not positive.
    /// </exception>
    public TimeSpan StartTimeout
    {
        get => _startTimeout;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(StartTimeout),
                    value,
                    "Start timeout must be a positive TimeSpan.");

            _startTimeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum time to wait for the Codex process to exit gracefully.
    /// </summary>
    /// <remarks>
    /// Default is 5 seconds. This timeout applies when stopping a session and waiting
    /// for the process to terminate cleanly. After this timeout, the process may be
    /// forcefully terminated.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not positive.
    /// </exception>
    public TimeSpan ProcessExitTimeout
    {
        get => _processExitTimeout;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(ProcessExitTimeout),
                    value,
                    "Process exit timeout must be a positive TimeSpan.");

            _processExitTimeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the polling interval for tailing log files and event streams.
    /// </summary>
    /// <remarks>
    /// Default is 200 milliseconds. This controls how frequently the client checks
    /// for new events when monitoring a session's event stream. Lower values provide
    /// more real-time updates but increase CPU usage.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not positive.
    /// </exception>
    public TimeSpan TailPollInterval
    {
        get => _tailPollInterval;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(TailPollInterval),
                    value,
                    "Tail poll interval must be a positive TimeSpan.");

            if (value < MinimumTailPollInterval)
                throw new ArgumentOutOfRangeException(
                    nameof(TailPollInterval),
                    value,
                    $"Tail poll interval must be at least {MinimumTailPollInterval.TotalMilliseconds} ms to avoid excessive CPU usage.");

            _tailPollInterval = value;
        }
    }

    /// <summary>
    /// Creates a new instance of CodexClientOptions with default values.
    /// </summary>
    public CodexClientOptions()
    {
    }

    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new CodexClientOptions instance with the same values.</returns>
    public CodexClientOptions Clone() => new()
    {
        CodexExecutablePath = CodexExecutablePath,
        SessionsRootDirectory = SessionsRootDirectory,
        StartTimeout = StartTimeout,
        ProcessExitTimeout = ProcessExitTimeout,
        TailPollInterval = TailPollInterval
    };

    /// <summary>
    /// Validates the current configuration and throws exceptions if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is invalid (e.g., paths don't exist when they should).
    /// </exception>
    public void Validate()
    {
        // Validation logic can be expanded as needed
        if (StartTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Start timeout must be a positive TimeSpan.");

        if (ProcessExitTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Process exit timeout must be a positive TimeSpan.");

        if (TailPollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("Tail poll interval must be a positive TimeSpan.");

        if (TailPollInterval < MinimumTailPollInterval)
            throw new InvalidOperationException(
                $"Tail poll interval must be at least {MinimumTailPollInterval.TotalMilliseconds} ms.");

        if (!string.IsNullOrWhiteSpace(CodexExecutablePath) && !File.Exists(CodexExecutablePath))
        {
            throw new FileNotFoundException(
                $"CodexExecutablePath '{CodexExecutablePath}' does not exist.",
                CodexExecutablePath);
        }
    }
}
