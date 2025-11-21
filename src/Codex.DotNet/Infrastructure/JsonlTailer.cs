using System.Runtime.CompilerServices;
using System.Text;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codex.DotNet.Infrastructure;

/// <summary>
/// Default implementation of JSONL file tailer.
/// </summary>
/// <remarks>
/// This implementation provides file tailing functionality similar to 'tail -f',
/// reading new lines as they are written to a file with support for concurrent
/// read access while another process is writing.
/// </remarks>
public sealed class JsonlTailer : IJsonlTailer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<JsonlTailer> _logger;
    private readonly CodexClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonlTailer"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The client options for polling configuration.</param>
    public JsonlTailer(
        IFileSystem fileSystem,
        ILogger<JsonlTailer> logger,
        IOptions<CodexClientOptions> options)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> TailAsync(
        string filePath,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath), "File path cannot be null or whitespace.");

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (!_fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException(
                $"The JSONL file does not exist: {filePath}",
                filePath);
        }

        _logger.LogDebug("Starting to tail file: {FilePath}", filePath);

        // Open the file with shared read/write access to allow concurrent writing
        FileStream? fileStream = null;
        StreamReader? reader = null;

        try
        {
            fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Apply starting position based on options
            await ApplyStartingPositionAsync(reader, fileStream, options, cancellationToken);

            // Read lines from the file
            await foreach (var line in ReadLinesAsync(reader, fileStream, filePath, options, cancellationToken))
            {
                yield return line;
            }
        }
        finally
        {
            reader?.Dispose();
            fileStream?.Dispose();
            _logger.LogDebug("Stopped tailing file: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Applies the starting position to the stream based on EventStreamOptions.
    /// </summary>
    private async Task ApplyStartingPositionAsync(
        StreamReader reader,
        FileStream fileStream,
        EventStreamOptions options,
        CancellationToken cancellationToken)
    {
        // Handle byte offset
        if (options.FromByteOffset.HasValue)
        {
            var offset = options.FromByteOffset.Value;
            _logger.LogDebug("Seeking to byte offset: {Offset}", offset);

            fileStream.Seek(offset, SeekOrigin.Begin);
            reader.DiscardBufferedData();
            return;
        }

        // Handle timestamp filter
        if (options.AfterTimestamp.HasValue)
        {
            _logger.LogDebug("Filtering events after timestamp: {Timestamp}", options.AfterTimestamp.Value);
            // Note: Timestamp filtering is handled at the parser level, not here
            // The tailer reads from the beginning and the parser filters
            return;
        }

        // FromBeginning is the default - no seek needed
        if (!options.FromBeginning)
        {
            // If not from beginning and no other option specified, seek to end
            _logger.LogDebug("Seeking to end of file");
            fileStream.Seek(0, SeekOrigin.End);
            reader.DiscardBufferedData();
        }
    }

    /// <summary>
    /// Reads lines from the file, polling for new content when EOF is reached.
    /// </summary>
    private async IAsyncEnumerable<string> ReadLinesAsync(
        StreamReader reader,
        FileStream fileStream,
        string filePath,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pollInterval = _options.TailPollInterval;
        long lastKnownLength = fileStream.Length;

        while (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);

            if (line != null)
            {
                // Successfully read a line
                yield return line;
                lastKnownLength = Math.Max(lastKnownLength, fileStream.Position);
                continue;
            }

            if (!options.Follow)
            {
                _logger.LogTrace("Follow disabled; stopping tail for {FilePath}", filePath);
                yield break;
            }

            // Reached EOF - check if file has grown
            var currentLength = _fileSystem.GetFileSize(filePath);

            if (currentLength > lastKnownLength)
            {
                // File has grown - discard buffered data and continue reading
                _logger.LogTrace("File grew from {OldSize} to {NewSize} bytes", lastKnownLength, currentLength);
                reader.DiscardBufferedData();
                lastKnownLength = currentLength;
                continue;
            }

            if (currentLength < lastKnownLength)
            {
                // File was truncated or replaced
                _logger.LogWarning(
                    "File size decreased from {OldSize} to {NewSize} bytes - file may have been truncated or replaced",
                    lastKnownLength,
                    currentLength);

                // Seek to beginning of new content
                fileStream.Seek(0, SeekOrigin.Begin);
                reader.DiscardBufferedData();
                lastKnownLength = currentLength;
                continue;
            }

            // No new data - wait before polling again
            _logger.LogTrace("No new data, waiting {Interval}ms before next poll", pollInterval.TotalMilliseconds);

            try
            {
                await Task.Delay(pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested during delay
                yield break;
            }
        }
    }
}
