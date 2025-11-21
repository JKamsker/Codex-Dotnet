using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using Microsoft.Extensions.Logging;

namespace Codex.DotNet.Infrastructure;

/// <summary>
/// Default implementation of Codex session locator.
/// </summary>
/// <remarks>
/// This implementation provides functionality for discovering and locating Codex session files,
/// including polling for new sessions, finding specific session logs, and enumerating
/// sessions with optional filtering.
/// </remarks>
public sealed class CodexSessionLocator : ICodexSessionLocator
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CodexSessionLocator> _logger;

    // Poll interval for waiting for new session files
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    // Regex patterns for session file matching
    private static readonly Regex SessionFilePattern = new(
        @"^(rollout-.*\.jsonl|.*-[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.jsonl)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexSessionLocator"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileSystem"/> or <paramref name="logger"/> is null.
    /// </exception>
    public CodexSessionLocator(
        IFileSystem fileSystem,
        ILogger<CodexSessionLocator> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> WaitForNewSessionFileAsync(
        string sessionsRoot,
        DateTimeOffset startTime,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timeout must be a positive TimeSpan.",
                nameof(timeout));
        }

        _logger.LogDebug(
            "Waiting for new session file in {Directory} created after {StartTime}",
            sessionsRoot,
            startTime);

        // Snapshot existing files before launch to avoid picking old sessions
        var baseline = CaptureSessionSnapshot(sessionsRoot);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startTimeUtc = startTime.UtcDateTime;

        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                // Search for .jsonl files created after startTime and not in baseline
                var newSessionFile = FindNewSessionFile(sessionsRoot, startTimeUtc, baseline);

                if (newSessionFile != null)
                {
                    _logger.LogInformation(
                        "Found new session file: {Path}",
                        newSessionFile);
                    return newSessionFile;
                }

                // Wait before polling again
                await Task.Delay(DefaultPollInterval, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            throw new TimeoutException(
                $"No new session file was created within the timeout period of {timeout.TotalSeconds:F1} seconds.");
        }

        // Should not reach here, but just in case
        throw new TimeoutException(
            $"No new session file was created within the timeout period of {timeout.TotalSeconds:F1} seconds.");
    }

    /// <inheritdoc />
    public async Task<string> FindSessionLogAsync(
        SessionId sessionId,
        string sessionsRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException(
                "Session ID cannot be empty or whitespace.",
                nameof(sessionId));
        }

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        _logger.LogDebug(
            "Searching for session log file for session ID: {SessionId} in {Directory}",
            sessionId,
            sessionsRoot);

        // Search for files matching the pattern *-{sessionId}.jsonl
        var searchPattern = $"*-{sessionId.Value}.jsonl";

        try
        {
            var matchingFiles = _fileSystem.GetFiles(sessionsRoot, searchPattern).ToArray();

            if (matchingFiles.Length == 0)
            {
                throw new FileNotFoundException(
                    $"No session log file found for session ID: {sessionId}. Searched in: {sessionsRoot}",
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
            _logger.LogDebug("Found session log file: {Path}", logPath);

            var validated = await ValidateLogFileAsync(logPath, cancellationToken).ConfigureAwait(false);
            return validated;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(
                ex,
                "Error searching for session log file for session ID: {SessionId}",
                sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> WaitForSessionLogByIdAsync(
        SessionId sessionId,
        string sessionsRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                var path = await FindSessionLogAsync(sessionId, sessionsRoot, timeoutCts.Token).ConfigureAwait(false);
                return path;
            }
            catch (FileNotFoundException)
            {
                // Not there yet; wait and retry
                try
                {
                    await Task.Delay(DefaultPollInterval, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        throw new TimeoutException(
            $"No session log file found for session ID: {sessionId} within {timeout.TotalSeconds:F1} seconds.");
    }

    /// <inheritdoc />
    public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(logFilePath);

        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException(
                "Log file path cannot be empty or whitespace.",
                nameof(logFilePath));
        }

        if (!_fileSystem.FileExists(logFilePath))
        {
            throw new FileNotFoundException(
                $"Session log file not found: {logFilePath}",
                logFilePath);
        }

        try
        {
            // Ensure the file can be opened for read access
            using var stream = _fileSystem.OpenRead(logFilePath);
            _ = stream.Length; // touch stream to surface access errors
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
        {
            _logger.LogError(
                ex,
                "Unable to open session log file: {Path}",
                logFilePath);
            throw;
        }

        return Task.FromResult(logFilePath);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        string sessionsRoot,
        SessionFilter? filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        _logger.LogDebug(
            "Enumerating sessions in {Directory} with filter: {Filter}",
            sessionsRoot,
            filter?.ToString() ?? "none");

        foreach (var filePath in EnumerateSessionFiles(sessionsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var createdAtUtc = TryGetCreationTimeUtc(filePath);

            // Quick pre-filter on creation time if available to avoid opening files that
            // obviously fall outside the requested range.
            if (filter != null && createdAtUtc.HasValue)
            {
                var createdAt = new DateTimeOffset(createdAtUtc.Value, TimeSpan.Zero);

                if (filter.FromDate.HasValue && createdAt < filter.FromDate.Value)
                {
                    _logger.LogTrace("Skipping {FilePath} - created before FromDate", filePath);
                    continue;
                }

                if (filter.ToDate.HasValue && createdAt > filter.ToDate.Value)
                {
                    _logger.LogTrace("Skipping {FilePath} - created after ToDate", filePath);
                    continue;
                }
            }

            // Try to parse session metadata from the file
            CodexSessionInfo? sessionInfo = null;

            try
            {
                sessionInfo = await ParseSessionInfoAsync(filePath, createdAtUtc, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error parsing session info from file: {FilePath}, skipping",
                    filePath);
                continue;
            }

            if (sessionInfo == null)
            {
                _logger.LogTrace("No valid session info found in file: {FilePath}, skipping", filePath);
                continue;
            }

            // Apply filter if provided
            if (filter != null && !MatchesFilter(sessionInfo, filter))
            {
                _logger.LogTrace(
                    "Session {SessionId} does not match filter, skipping",
                    sessionInfo.Id);
                continue;
            }

            yield return sessionInfo;
        }
    }

    /// <summary>
    /// Captures the set of session files that exist prior to starting Codex.
    /// </summary>
    /// <param name="sessionsRoot">The sessions root directory.</param>
    /// <returns>A snapshot of existing session file paths.</returns>
    private HashSet<string> CaptureSessionSnapshot(string sessionsRoot)
    {
        var snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var existingFiles = _fileSystem.GetFiles(sessionsRoot, "*.jsonl");
            foreach (var file in existingFiles)
            {
                var fileName = Path.GetFileName(file);
                if (SessionFilePattern.IsMatch(fileName))
                {
                    snapshot.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing pre-launch session snapshot in {Directory}", sessionsRoot);
        }

        return snapshot;
    }

    /// <summary>
    /// Finds a new session file created after the specified start time that was not present in the baseline snapshot.
    /// </summary>
    /// <param name="sessionsRoot">The sessions root directory.</param>
    /// <param name="startTimeUtc">The start time in UTC.</param>
    /// <param name="baseline">The snapshot of files present before launch.</param>
    /// <returns>The path to a new session file, or null if none found.</returns>
    private string? FindNewSessionFile(string sessionsRoot, DateTime startTimeUtc, HashSet<string> baseline)
    {
        try
        {
            var jsonlFiles = _fileSystem.GetFiles(sessionsRoot, "*.jsonl");

            var candidates = new List<(string Path, DateTime CreatedUtc)>();

            foreach (var filePath in jsonlFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);

                    // Check if the file name matches the expected pattern
                    if (!SessionFilePattern.IsMatch(fileName))
                    {
                        continue;
                    }

                    if (baseline.Contains(filePath))
                    {
                        continue;
                    }

                    // Check creation time
                    var creationTimeUtc = _fileSystem.GetFileCreationTimeUtc(filePath);

                    if (creationTimeUtc >= startTimeUtc)
                    {
                        candidates.Add((filePath, creationTimeUtc));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error checking file: {FilePath}", filePath);
                }
            }

            var earliest = candidates
                .OrderBy(c => c.CreatedUtc)
                .FirstOrDefault();

            if (earliest != default)
            {
                _logger.LogTrace(
                    "Found candidate session file: {FilePath} (created: {CreationTime})",
                    earliest.Path,
                    earliest.CreatedUtc);
                return earliest.Path;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for new session files in: {Directory}", sessionsRoot);
        }

        return null;
    }

    /// <summary>
    /// Parses session information from a session log file.
    /// </summary>
    /// <param name="filePath">The path to the session log file.</param>
    /// <param name="creationTimeUtc">
    /// Optional file creation time to use when the log does not include a session_meta timestamp.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A CodexSessionInfo instance if the file contains valid session metadata, otherwise null.
    /// </returns>
    private async Task<CodexSessionInfo?> ParseSessionInfoAsync(
        string filePath,
        DateTime? creationTimeUtc,
        CancellationToken cancellationToken)
    {
        SessionId? sessionId = null;
        DateTimeOffset? createdAt = null;
        string? workingDirectory = null;
        CodexModel? model = null;

        // Read the file and look for session_meta event
        using var stream = _fileSystem.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Check if this is a session_meta event
                if (root.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetString() == "session_meta")
                {
                    // Extract timestamp
                    if (root.TryGetProperty("timestamp", out var timestampElement))
                    {
                        createdAt = timestampElement.GetDateTimeOffset();
                    }

                    // Extract session metadata from payload
                    if (root.TryGetProperty("payload", out var payload))
                    {
                        if (payload.TryGetProperty("id", out var idElement))
                        {
                            var idString = idElement.GetString();
                            if (!string.IsNullOrWhiteSpace(idString))
                            {
                                sessionId = SessionId.Parse(idString);
                            }
                        }

                        if (payload.TryGetProperty("cwd", out var cwdElement))
                        {
                            workingDirectory = cwdElement.GetString();
                        }

                        if (payload.TryGetProperty("model", out var modelElement))
                        {
                            var modelString = modelElement.GetString();
                            if (!string.IsNullOrWhiteSpace(modelString) &&
                                CodexModel.TryParse(modelString, out var parsedModel))
                            {
                                model = parsedModel;
                            }
                        }
                    }

                    // We found the session_meta event, we can stop reading
                    break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogTrace(ex, "Error parsing JSON line in file: {FilePath}", filePath);
                // Continue to next line
            }
        }

        // If we found a valid session ID, create the session info
        if (!sessionId.HasValue)
        {
            sessionId = TryExtractSessionIdFromFilePath(filePath);

            if (!sessionId.HasValue)
            {
                return null;
            }
        }

        var effectiveCreatedAt = createdAt
            ?? (creationTimeUtc.HasValue
                ? new DateTimeOffset(creationTimeUtc.Value, TimeSpan.Zero)
                : DateTimeOffset.UtcNow);

        return new CodexSessionInfo(
            Id: sessionId.Value,
            LogPath: filePath,
            CreatedAt: effectiveCreatedAt,
            WorkingDirectory: workingDirectory,
            Model: model);
    }

    /// <summary>
    /// Determines whether a session matches the specified filter criteria.
    /// </summary>
    /// <param name="sessionInfo">The session information to check.</param>
    /// <param name="filter">The filter criteria.</param>
    /// <returns>True if the session matches the filter; otherwise, false.</returns>
    private bool MatchesFilter(CodexSessionInfo sessionInfo, SessionFilter filter)
    {
        // Check date range
        if (filter.FromDate.HasValue && sessionInfo.CreatedAt < filter.FromDate.Value)
        {
            return false;
        }

        if (filter.ToDate.HasValue && sessionInfo.CreatedAt > filter.ToDate.Value)
        {
            return false;
        }

        // Check working directory
        if (!string.IsNullOrWhiteSpace(filter.WorkingDirectory) &&
            !string.Equals(sessionInfo.WorkingDirectory, filter.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check model
        if (filter.Model.HasValue)
        {
            if (!sessionInfo.Model.HasValue)
            {
                return false;
            }

            if (!string.Equals(sessionInfo.Model.Value.Value, filter.Model.Value.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check session ID pattern
        if (!string.IsNullOrWhiteSpace(filter.SessionIdPattern))
        {
            if (!MatchesPattern(sessionInfo.Id.Value, filter.SessionIdPattern))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a value matches a pattern (supports wildcards).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="pattern">The pattern to match against (supports * and ? wildcards).</param>
    /// <returns>True if the value matches the pattern; otherwise, false.</returns>
    private bool MatchesPattern(string value, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    private IEnumerable<string> EnumerateSessionFiles(string sessionsRoot)
    {
        IEnumerable<string> files;

        try
        {
            files = _fileSystem.GetFiles(sessionsRoot, "*.jsonl");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate session files in {Directory}", sessionsRoot);
            yield break;
        }

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            if (!SessionFilePattern.IsMatch(fileName))
            {
                _logger.LogTrace("Skipping non-session file {FilePath}", file);
                continue;
            }

            yield return file;
        }
    }

    private DateTime? TryGetCreationTimeUtc(string filePath)
    {
        try
        {
            return _fileSystem.GetFileCreationTimeUtc(filePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
        {
            _logger.LogTrace(ex, "Unable to read creation time for {FilePath}", filePath);
            return null;
        }
    }

    private SessionId? TryExtractSessionIdFromFilePath(string filePath)
    {
        try
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return null;
            }

            var lastDash = fileNameWithoutExtension.LastIndexOf('-');
            if (lastDash < 0 || lastDash == fileNameWithoutExtension.Length - 1)
            {
                return null;
            }

            var candidate = fileNameWithoutExtension[(lastDash + 1)..];
            return SessionId.Parse(candidate);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract session id from file name {FilePath}", filePath);
            return null;
        }
    }
}
