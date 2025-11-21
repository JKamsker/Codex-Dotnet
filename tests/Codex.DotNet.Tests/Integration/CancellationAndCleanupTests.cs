using System.Diagnostics;
using System.Runtime.CompilerServices;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using Codex.DotNet.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Codex.DotNet.Tests.Integration;

public class CancellationAndCleanupTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly ILogger<CodexSessionHandle> _handleLogger;
    private readonly CodexClientOptions _clientOptions;

    public CancellationAndCleanupTests()
    {
        _handleLogger = _loggerFactory.CreateLogger<CodexSessionHandle>();
        _clientOptions = new CodexClientOptions
        {
            ProcessExitTimeout = TimeSpan.FromSeconds(1),
            TailPollInterval = TimeSpan.FromMilliseconds(50)
        };
    }

    public void Dispose()
    {
        // best-effort: stop any stray processes from helper
        try
        {
            Process.GetProcessesByName("cmd").Where(p => p.StartInfo.Arguments.Contains("ping 127.0.0.1")).ToList()
                .ForEach(p => { try { p.Kill(entireProcessTree: true); } catch { } });
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task GetEventsAsync_CancellationStopsStreaming()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var sessionId = SessionId.Parse("cancel-session");
        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "/tmp", baseTime),
            TestJsonlGenerator.GenerateUserMessage("first", baseTime.AddSeconds(1)),
            TestJsonlGenerator.GenerateAgentMessage("second", baseTime.AddSeconds(2))
        };

        var tailer = new DelayedFakeTailer(lines, perLineDelay: TimeSpan.FromMilliseconds(30));
        var parser = new JsonlEventParser(_loggerFactory.CreateLogger<JsonlEventParser>());
        var info = new CodexSessionInfo(sessionId, "test-log.jsonl", baseTime, "/tmp", null);

        await using var handle = new CodexSessionHandle(
            info,
            tailer,
            parser,
            process: null,
            processLauncher: null,
            processExitTimeout: _clientOptions.ProcessExitTimeout,
            _handleLogger);

        using var cts = new CancellationTokenSource();
        var received = new List<CodexEvent>();

        // Act
        try
        {
            await foreach (var evt in handle.GetEventsAsync(EventStreamOptions.Default, cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 2)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected due to cancellation
        }

        // Assert
        received.Should().HaveCount(2);
        received[0].Should().BeOfType<SessionMetaEvent>();
        received[1].Should().BeOfType<UserMessageEvent>();
    }

    [Fact]
    public async Task DisposeAsync_TerminatesLiveProcessWithinTimeout()
    {
        // Arrange
        using var longRunning = StartLongRunningProcess();
        var processLauncher = new CodexProcessLauncher(
            new DefaultCodexPathProvider(new RealFileSystem(), _loggerFactory.CreateLogger<DefaultCodexPathProvider>()),
            _loggerFactory.CreateLogger<CodexProcessLauncher>());

        var info = new CodexSessionInfo(SessionId.Parse("live-session"), "live-log.jsonl", DateTimeOffset.UtcNow, "/tmp", null);
        await using var handle = new CodexSessionHandle(
            info,
            new JsonlTailer(new RealFileSystem(), _loggerFactory.CreateLogger<JsonlTailer>(), Options.Create(_clientOptions)),
            new JsonlEventParser(_loggerFactory.CreateLogger<JsonlEventParser>()),
            longRunning,
            processLauncher,
            _clientOptions.ProcessExitTimeout,
            _handleLogger);

        var sw = Stopwatch.StartNew();
        var pid = longRunning.Id;

        // Act
        await handle.DisposeAsync();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        bool exited = false;
        try
        {
            var proc = Process.GetProcessById(pid);
            exited = proc.HasExited;
        }
        catch (ArgumentException)
        {
            exited = true; // process not found => terminated
        }

        exited.Should().BeTrue();
    }

    [Fact]
    public async Task DisposedHandle_RejectsFurtherOperations()
    {
        // Arrange
        var info = new CodexSessionInfo(SessionId.Parse("disposed"), "log.jsonl", DateTimeOffset.UtcNow, "/tmp", null);
        await using var handle = new CodexSessionHandle(
            info,
            new JsonlTailer(new RealFileSystem(), _loggerFactory.CreateLogger<JsonlTailer>(), Options.Create(_clientOptions)),
            new JsonlEventParser(_loggerFactory.CreateLogger<JsonlEventParser>()),
            process: null,
            processLauncher: null,
            processExitTimeout: _clientOptions.ProcessExitTimeout,
            _handleLogger);

        await handle.DisposeAsync();

        // Act + Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync());

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await handle.WaitForExitAsync(CancellationToken.None));
    }

    private static Process StartLongRunningProcess()
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 30 >nul",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
            : new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"sleep 30\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();
        return process;
    }

    private sealed class DelayedFakeTailer : IJsonlTailer
    {
        private readonly IReadOnlyList<string> _lines;
        private readonly TimeSpan _perLineDelay;

        public DelayedFakeTailer(IReadOnlyList<string> lines, TimeSpan perLineDelay)
        {
            _lines = lines;
            _perLineDelay = perLineDelay;
        }

        public async IAsyncEnumerable<string> TailAsync(
            string filePath,
            EventStreamOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var line in _lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_perLineDelay, cancellationToken);
                yield return line;
            }

            if (options.Follow)
            {
                // hold open until cancelled to mimic live tailing
                while (true)
                {
                    await Task.Delay(_perLineDelay, cancellationToken);
                }
            }
        }
    }
}
