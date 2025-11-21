using System.Diagnostics;
using System.Runtime.CompilerServices;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public.Models;
using Microsoft.Extensions.Logging;

namespace Codex.DotNet.Public;

/// <summary>
/// Represents a handle to a Codex session, providing access to metadata, event streaming,
/// and lifecycle operations for live or resumed sessions.
/// </summary>
public sealed class CodexSessionHandle : ICodexSessionHandle
{
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly Process? _process;
    private readonly ICodexProcessLauncher? _processLauncher;
    private readonly TimeSpan _processExitTimeout;
    private readonly ILogger<CodexSessionHandle> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexSessionHandle"/> class.
    /// </summary>
    /// <param name="info">Session metadata for this handle.</param>
    /// <param name="tailer">JSONL tailer used to read session logs.</param>
    /// <param name="parser">Event parser used to map lines to strongly-typed events.</param>
    /// <param name="process">Optional running Codex process for live sessions.</param>
    /// <param name="processLauncher">Process launcher used for graceful termination of live sessions.</param>
    /// <param name="processExitTimeout">Timeout for graceful process termination.</param>
    /// <param name="logger">Logger instance.</param>
    public CodexSessionHandle(
        CodexSessionInfo info,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        Process? process,
        ICodexProcessLauncher? processLauncher,
        TimeSpan processExitTimeout,
        ILogger<CodexSessionHandle> logger)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _tailer = tailer ?? throw new ArgumentNullException(nameof(tailer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _process = process;
        _processLauncher = processLauncher;
        _processExitTimeout = processExitTimeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public CodexSessionInfo Info { get; }

    /// <inheritdoc />
    public bool IsLive => !_disposed && _process is { HasExited: false };

    /// <inheritdoc />
    public IAsyncEnumerable<CodexEvent> GetEventsAsync(
        EventStreamOptions? options,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? EventStreamOptions.Default;
        var shouldFollow = effectiveOptions.Follow && IsLive;
        if (effectiveOptions.Follow != shouldFollow)
        {
            effectiveOptions = effectiveOptions with { Follow = shouldFollow };
        }

        var lineStream = _tailer.TailAsync(Info.LogPath, effectiveOptions, cancellationToken);
        var parsedStream = _parser.ParseAsync(lineStream, cancellationToken);

        return ApplyTimestampFilter(parsedStream, effectiveOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_process == null)
        {
            throw new InvalidOperationException("Cannot wait for exit on a non-live session.");
        }

        await _process.WaitForExitAsync(cancellationToken);
        return _process.ExitCode;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_process != null)
            {
                if (!_process.HasExited && _processLauncher != null)
                {
                    try
                    {
                        await _processLauncher.TerminateProcessAsync(
                            _process,
                            _processExitTimeout,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error terminating process for session {SessionId}", Info.Id);
                    }
                }

                try
                {
                    _process.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error disposing process for session {SessionId}", Info.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing process for session {SessionId}", Info.Id);
        }

        await Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CodexSessionHandle));
        }
    }

    private async IAsyncEnumerable<CodexEvent> ApplyTimestampFilter(
        IAsyncEnumerable<CodexEvent> events,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (options.AfterTimestamp.HasValue)
            {
                if (evt.Timestamp <= options.AfterTimestamp.Value)
                {
                    continue;
                }
            }

            yield return evt;
        }
    }
}
