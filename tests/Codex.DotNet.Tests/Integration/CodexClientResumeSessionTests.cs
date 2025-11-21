using System.Runtime.CompilerServices;
using Codex.DotNet.Abstractions;
using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using Codex.DotNet.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Codex.DotNet.Tests.Integration;

public class CodexClientResumeSessionTests
{
    private static readonly ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public async Task ResumeSessionAsync_ReplaysAllEvents_FromBeginning()
    {
        // Arrange
        var baseTime = DateTimeOffset.Parse("2025-11-20T22:00:00Z");
        var sessionId = SessionId.Parse("session-123");
        var logPath = "C:\\sessions\\rollout-session-123.jsonl";

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateUserMessage("hello", baseTime.AddSeconds(1)),
            TestJsonlGenerator.GenerateAgentReasoning("thinking", baseTime.AddSeconds(2)),
            TestJsonlGenerator.GenerateAgentMessage("hi!", baseTime.AddSeconds(3)),
            TestJsonlGenerator.GenerateTokenCount(10, 5, 2, baseTime.AddSeconds(4))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act
        await using var handle = await client.ResumeSessionAsync(sessionId);
        var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();

        // Assert
        Assert.False(handle.IsLive);
        Assert.Equal(logPath, handle.Info.LogPath);
        Assert.Equal(sessionId, handle.Info.Id);
        Assert.Equal(5, events.Count);
        Assert.Collection(events,
            e => Assert.IsType<SessionMetaEvent>(e),
            e => Assert.IsType<UserMessageEvent>(e),
            e => Assert.IsType<AgentReasoningEvent>(e),
            e => Assert.IsType<AgentMessageEvent>(e),
            e => Assert.IsType<TokenCountEvent>(e));
    }

    [Fact]
    public async Task AttachToLogAsync_AllowsReplayByPath()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-path");
        var logPath = "C:\\sessions\\rollout-session-path.jsonl";
        var baseTime = DateTimeOffset.Parse("2025-11-20T23:00:00Z");

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateAgentMessage("attached", baseTime.AddSeconds(1))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act
        await using var handle = await client.AttachToLogAsync(logPath);
        var events = await handle.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None).ToListAsync();

        // Assert
        Assert.False(handle.IsLive);
        Assert.Equal(logPath, handle.Info.LogPath);
        Assert.Equal(sessionId, handle.Info.Id);
        Assert.Equal(2, events.Count);
        Assert.IsType<SessionMetaEvent>(events[0]);
        Assert.IsType<AgentMessageEvent>(events[1]);
    }

    [Fact]
    public async Task ResumeSessionAsync_HonorsAfterTimestampFilter()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-filter");
        var logPath = "C:\\sessions\\rollout-session-filter.jsonl";
        var baseTime = DateTimeOffset.Parse("2025-11-20T23:30:00Z");

        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, "C:\\repos\\demo", baseTime),
            TestJsonlGenerator.GenerateUserMessage("first", baseTime.AddSeconds(1)),
            TestJsonlGenerator.GenerateAgentMessage("second", baseTime.AddSeconds(2))
        };

        var options = Options.Create(new CodexClientOptions());
        var locator = new StubSessionLocator(logPath);
        var tailer = new FakeTailer(lines);
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        var filterOptions = new EventStreamOptions(FromBeginning: false, AfterTimestamp: baseTime.AddSeconds(1));

        // Act
        await using var handle = await client.ResumeSessionAsync(sessionId);
        var events = await handle.GetEventsAsync(filterOptions, CancellationToken.None).ToListAsync();

        // Assert
        var agentMessage = Assert.Single(events);
        var agentMessageEvent = Assert.IsType<AgentMessageEvent>(agentMessage);
        Assert.Equal("second", agentMessageEvent.Text);
    }

    [Fact]
    public async Task ResumeSessionAsync_ThrowsWhenSessionNotFound()
    {
        // Arrange
        var sessionId = SessionId.Parse("missing-session");
        var locator = new StubSessionLocator(path: string.Empty, throwOnFind: true);
        var options = Options.Create(new CodexClientOptions());
        var tailer = new FakeTailer(Array.Empty<string>());
        var parser = new JsonlEventParser(LoggerFactory.CreateLogger<JsonlEventParser>());
        var pathProvider = new FakePathProvider("C:\\sessions");
        var client = new CodexClient(options, processLauncher: null, locator, tailer, parser, pathProvider, LoggerFactory.CreateLogger<CodexClient>(), LoggerFactory);

        // Act + Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => client.ResumeSessionAsync(sessionId));
    }

    private sealed class StubSessionLocator : ICodexSessionLocator
    {
        private readonly string _path;
        private readonly bool _throwOnFind;

        public StubSessionLocator(string path, bool throwOnFind = false)
        {
            _path = path;
            _throwOnFind = throwOnFind;
        }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken)
        {
            if (_throwOnFind)
            {
                throw new FileNotFoundException("Session not found", sessionId.Value);
            }

            return Task.FromResult(_path);
        }

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new FileNotFoundException("Invalid path", logFilePath);
            }

            return Task.FromResult(logFilePath);
        }

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_throwOnFind)
            {
                throw new FileNotFoundException("Session not found", sessionId.Value);
            }
            return Task.FromResult(_path);
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
