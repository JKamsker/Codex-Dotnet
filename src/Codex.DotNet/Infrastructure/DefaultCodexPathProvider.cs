using System.Runtime.InteropServices;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public.Models;
using Microsoft.Extensions.Logging;

namespace Codex.DotNet.Infrastructure;

/// <summary>
/// Default implementation of Codex path resolution with platform detection.
/// </summary>
/// <remarks>
/// This implementation automatically detects the platform and resolves
/// appropriate paths for the Codex executable and session directories.
/// </remarks>
public sealed class DefaultCodexPathProvider : ICodexPathProvider
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<DefaultCodexPathProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCodexPathProvider"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultCodexPathProvider(
        IFileSystem fileSystem,
        ILogger<DefaultCodexPathProvider> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string GetCodexExecutablePath(string? overridePath)
    {
        // If override path is provided, validate and use it
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!_fileSystem.FileExists(overridePath))
            {
                throw new FileNotFoundException(
                    $"The specified Codex executable path does not exist: {overridePath}",
                    overridePath);
            }

            _logger.LogDebug("Using custom Codex executable path: {Path}", overridePath);
            return overridePath;
        }

        // Auto-detect based on platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ResolveWindowsExecutablePath();
        }
        else
        {
            return ResolveUnixExecutablePath();
        }
    }

    /// <inheritdoc />
    public string GetSessionsRootDirectory(string? overrideDirectory)
    {
        // If override directory is provided, validate and use it
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            if (!_fileSystem.DirectoryExists(overrideDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"The specified sessions root directory does not exist: {overrideDirectory}");
            }

            _logger.LogDebug("Using custom sessions root directory: {Path}", overrideDirectory);
            return overrideDirectory;
        }

        // Return the default sessions directory
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException(
                "Unable to determine user profile directory. " +
                "The USERPROFILE environment variable may not be set.");
        }

        var sessionsDir = Path.Combine(userProfile, ".codex", "sessions");

        _logger.LogDebug("Default sessions directory: {Path}", sessionsDir);

        return sessionsDir;
    }

    /// <inheritdoc />
    public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot)
    {
        var rootDir = sessionsRoot ?? GetSessionsRootDirectory(null);

        if (!_fileSystem.DirectoryExists(rootDir))
        {
            throw new DirectoryNotFoundException(
                $"The sessions directory does not exist: {rootDir}. " +
                "Verify that Codex CLI is properly installed and has been run at least once.");
        }

        // Search for files matching the pattern *-{sessionId}.jsonl
        var searchPattern = $"*-{sessionId.Value}.jsonl";

        _logger.LogDebug(
            "Searching for session log files matching pattern: {Pattern} in {Directory}",
            searchPattern,
            rootDir);

        var matchingFiles = _fileSystem.GetFiles(rootDir, searchPattern).ToArray();

        if (matchingFiles.Length == 0)
        {
            throw new FileNotFoundException(
                $"No session log file found for session ID: {sessionId}. " +
                $"Searched in: {rootDir}",
                searchPattern);
        }

        if (matchingFiles.Length > 1)
        {
            _logger.LogWarning(
                "Multiple session log files found for session ID {SessionId}. Using the first match: {Path}",
                sessionId,
                matchingFiles[0]);
        }

        var logPath = matchingFiles[0];
        _logger.LogDebug("Resolved session log path: {Path}", logPath);

        return logPath;
    }

    /// <summary>
    /// Resolves the Codex executable path on Windows systems.
    /// </summary>
    /// <returns>The path to the Codex executable.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the executable cannot be found.</exception>
    /// <remarks>
    /// Checks for "codex.cmd" first, then falls back to "codex.exe".
    /// </remarks>
    private string ResolveWindowsExecutablePath()
    {
        // Try to find codex.cmd or codex.exe in PATH
        var codexCmd = FindExecutableInPath("codex.cmd");
        if (codexCmd != null)
        {
            _logger.LogDebug("Found Codex executable: {Path}", codexCmd);
            return codexCmd;
        }

        var codexExe = FindExecutableInPath("codex.exe");
        if (codexExe != null)
        {
            _logger.LogDebug("Found Codex executable: {Path}", codexExe);
            return codexExe;
        }

        throw new FileNotFoundException(
            "Codex CLI executable not found. Searched for 'codex.cmd' and 'codex.exe' in PATH. " +
            "Please ensure Codex CLI is installed and accessible in your PATH environment variable.");
    }

    /// <summary>
    /// Resolves the Codex executable path on Unix-like systems (Linux, macOS).
    /// </summary>
    /// <returns>The path to the Codex executable.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the executable cannot be found.</exception>
    private string ResolveUnixExecutablePath()
    {
        var codex = FindExecutableInPath("codex");
        if (codex != null)
        {
            _logger.LogDebug("Found Codex executable: {Path}", codex);
            return codex;
        }

        throw new FileNotFoundException(
            "Codex CLI executable not found. Searched for 'codex' in PATH. " +
            "Please ensure Codex CLI is installed and accessible in your PATH environment variable.");
    }

    /// <summary>
    /// Searches for an executable in the system PATH.
    /// </summary>
    /// <param name="executableName">The name of the executable to find.</param>
    /// <returns>The full path to the executable, or null if not found.</returns>
    private string? FindExecutableInPath(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            _logger.LogWarning("PATH environment variable is not set or empty");
            return null;
        }

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path, executableName);
                if (_fileSystem.FileExists(fullPath))
                {
                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                // Log and continue - some PATH entries might be invalid
                _logger.LogTrace(ex, "Error checking path entry: {Path}", path);
            }
        }

        return null;
    }
}
