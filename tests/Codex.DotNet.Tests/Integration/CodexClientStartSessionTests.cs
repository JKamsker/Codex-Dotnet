using System.Diagnostics;
using System.IO;
using System;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Xunit;

namespace Codex.DotNet.Tests.Integration;

public class CodexClientStartSessionTests
{
    private static readonly ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public async Task StartSessionAsync_StreamsEventsInOrder_AndReturnsSessionId()
    {
        // Arrange
        var startTimeout = TimeSpan.FromSeconds(2);
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var clientOptions = Options.Create(new CodexClientOptions { StartTimeout = startTimeout });
        var sessionOptions = new CodexSessionOptions(workingDirectory: workingDirectory, prompt: "hello world");

        var sessionId = SessionId.Parse("session-123");
        var escapedWorkingDirectory = workingDirectory.Replace("\\", "\\\\");
        var lines = new[]
        {
            $"{{\"timestamp\":\"2025-11-20T22:00:00Z\",\"type\":\"session_meta\",\"payload\":{{\"id\":\"{sessionId}\",\"cwd\":\"{escapedWorkingDirectory}\"}}}}",
            """{"timestamp":"2025-11-20T22:00:01Z","type":"user_message","payload":{"message":"hello world"}}""",
            """{"timestamp":"2025-11-20T22:00:02Z","type":"agent_reasoning","payload":{"text":"thinking"}}""",
            """{"timestamp":"2025-11-20T22:00:03Z","type":"agent_message","payload":{"message":"hi!"}}""",
            """{"timestamp":"2025-11-20T22:00:04Z","type":"token_count","payload":{"input_tokens":10,"output_tokens":5,"reasoning_output_tokens":2}}"""
        };

        using var process = FakeProcessLauncher.CreateLongLivedProcess();

        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator("C:\\sessions\\rollout-session-123.jsonl");
        var tailer = new FakeTailer(lines);
        var parser = new Codex.DotNet.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<Codex.DotNet.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        try
        {
            // Act
            var sw = Stopwatch.StartNew();
            await using var handle = await client.StartSessionAsync(sessionOptions);
            sw.Stop();

            // Assert session
            Assert.Equal(sessionId, handle.Info.Id);
            Assert.True(handle.IsLive);
            // Assert events order
            var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();
            Assert.Equal(5, events.Count);
            Assert.Collection(events,
                e => Assert.IsType<SessionMetaEvent>(e),
                e => Assert.IsType<UserMessageEvent>(e),
                e => Assert.IsType<AgentReasoningEvent>(e),
                e => Assert.IsType<AgentMessageEvent>(e),
                e => Assert.IsType<TokenCountEvent>(e));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartSessionAsync_Fails_WhenProcessExitsBeforeSessionMeta()
    {
        // Arrange
        var clientOptions = Options.Create(new CodexClientOptions { StartTimeout = TimeSpan.FromSeconds(2) });
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var sessionOptions = new CodexSessionOptions(workingDirectory, "prompt");

        using var process = FakeProcessLauncher.CreateShortProcess(); // exits almost immediately

        var launcher = new FakeProcessLauncher(process);
        var locator = new FakeSessionLocator("C:\\sessions\\rollout-session-123.jsonl");
        var tailer = new FakeTailer(Array.Empty<string>()); // no session_meta
        var parser = new Codex.DotNet.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<Codex.DotNet.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        // Act + Assert
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartSessionAsync(sessionOptions));
            Assert.Contains("session_meta", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartSessionAsync_Fails_WhenSessionLogNotFound()
    {
        // Arrange
        var clientOptions = Options.Create(new CodexClientOptions { StartTimeout = TimeSpan.FromSeconds(1) });
        var workingDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}")).FullName;
        var sessionOptions = new CodexSessionOptions(workingDirectory, "prompt");

        var launcher = new FakeProcessLauncher(FakeProcessLauncher.CreateLongLivedProcess());
        var locator = new FakeSessionLocator(throwOnWait: true);
        var tailer = new FakeTailer(Array.Empty<string>());
        var parser = new Codex.DotNet.Infrastructure.JsonlEventParser(LoggerFactory.CreateLogger<Codex.DotNet.Infrastructure.JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var logger = LoggerFactory.CreateLogger<CodexClient>();

        var client = new CodexClient(clientOptions, launcher, locator, tailer, parser, pathProvider, logger, LoggerFactory);

        try
        {
            // Act + Assert
            await Assert.ThrowsAnyAsync<Exception>(() => client.StartSessionAsync(sessionOptions));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private sealed class FakeProcessLauncher : ICodexProcessLauncher
    {
        private readonly Process _process;

        public FakeProcessLauncher(Process process)
        {
            _process = process;
        }

        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(_process);
        }

        public Task<Process> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CodexClientOptions clientOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(_process);
        }

        public Task<int> TerminateProcessAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            return Task.FromResult(process.ExitCode);
        }

        public static Process CreateLongLivedProcess()
        {
            // On Windows, use "cmd /c ping -n 30 127.0.0.1" to stay alive for a bit
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping -n 30 127.0.0.1 >NUL",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
            var p = Process.Start(psi)!;
            return p;
        }

        public static Process CreateShortProcess()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var p = Process.Start(psi)!;
            return p;
        }
    }

    private sealed class FakeSessionLocator : ICodexSessionLocator
    {
        private readonly string _path;
        private readonly bool _throwOnWait;

        public FakeSessionLocator(string path)
        {
            _path = path;
        }

        public FakeSessionLocator(bool throwOnWait)
        {
            _throwOnWait = throwOnWait;
            _path = string.Empty;
        }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_throwOnWait)
            {
                throw new TimeoutException("No session file");
            }

            return Task.FromResult(_path);
        }

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(_path);
        }

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(logFilePath);
        }

        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(string sessionsRoot, SessionFilter? filter, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeTailer : IJsonlTailer
    {
        private readonly IReadOnlyList<string> _lines;

        public FakeTailer(IEnumerable<string> lines)
        {
            _lines = lines.ToList();
        }

        public async IAsyncEnumerable<string> TailAsync(string filePath, EventStreamOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var line in _lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
        }
    }

    private sealed class FakePathProvider : ICodexPathProvider
    {
        private readonly string _sessionsRoot;

        public FakePathProvider(string sessionsRoot)
        {
            _sessionsRoot = sessionsRoot;
        }

        public string GetCodexExecutablePath(string? overridePath) =>
            overridePath ?? "codex.exe";

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            overrideDirectory ?? _sessionsRoot;

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            Path.Combine(sessionsRoot ?? _sessionsRoot, $"rollout-{sessionId}.jsonl");
    }
}
