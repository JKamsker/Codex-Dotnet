using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using Codex.DotNet.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Linq;
using Xunit;

namespace Codex.DotNet.Tests.Integration;

public sealed class CodexClientListSessionsTests : IAsyncDisposable
{
    private static readonly NullLoggerFactory LoggerFactory = NullLoggerFactory.Instance;
    private readonly string _sessionsRoot;

    public CodexClientListSessionsTests()
    {
        _sessionsRoot = Path.Combine(Path.GetTempPath(), $"codex-sessions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sessionsRoot);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsAllSessionsWithMetadata()
    {
        // Arrange
        var firstTime = new DateTimeOffset(2025, 11, 19, 10, 0, 0, TimeSpan.Zero);
        var secondTime = new DateTimeOffset(2025, 11, 20, 12, 30, 0, TimeSpan.Zero);

        var sessionA = SessionId.Parse("session-a");
        var sessionB = SessionId.Parse("session-b");

        var logA = CreateSessionLog(sessionA, firstTime, @"C:\repos\alpha", "gpt-5.1-codex");
        var logB = CreateSessionLog(sessionB, secondTime, @"C:\repos\beta", "gpt-5.1-codex-mini");

        await using var client = CreateClient();

        // Act
        var sessions = await client.ListSessionsAsync().ToListAsync();

        // Assert
        Assert.Equal(2, sessions.Count);

        var fromA = sessions.Single(s => s.Id == sessionA);
        Assert.Equal(logA, fromA.LogPath);
        Assert.Equal(firstTime, fromA.CreatedAt);
        Assert.Equal(@"C:\repos\alpha", fromA.WorkingDirectory);
        Assert.Equal(CodexModel.Parse("gpt-5.1-codex"), fromA.Model);

        var fromB = sessions.Single(s => s.Id == sessionB);
        Assert.Equal(logB, fromB.LogPath);
        Assert.Equal(secondTime, fromB.CreatedAt);
        Assert.Equal(@"C:\repos\beta", fromB.WorkingDirectory);
        Assert.Equal(CodexModel.Parse("gpt-5.1-codex-mini"), fromB.Model);
    }

    [Fact]
    public async Task ListSessionsAsync_AppliesDateRangeAndModelFilter()
    {
        // Arrange
        var early = new DateTimeOffset(2025, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2025, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2025, 11, 21, 18, 0, 0, TimeSpan.Zero);

        CreateSessionLog(SessionId.Parse("early"), early, "/work/one", "gpt-5.1-codex");
        var target = CreateSessionLog(SessionId.Parse("middle"), middle, "/work/one", "gpt-5.1-codex-mini");
        CreateSessionLog(SessionId.Parse("late"), late, "/work/one", "gpt-5.1-codex-mini");

        var filter = new SessionFilter(
            FromDate: new DateTimeOffset(2025, 11, 17, 0, 0, 0, TimeSpan.Zero),
            ToDate: new DateTimeOffset(2025, 11, 19, 23, 59, 59, TimeSpan.Zero),
            Model: CodexModel.Parse("gpt-5.1-codex-mini"));

        await using var client = CreateClient();

        // Act
        var sessions = await client.ListSessionsAsync(filter).ToListAsync();

        // Assert
        var match = Assert.Single(sessions);
        Assert.Equal(target, match.LogPath);
        Assert.Equal("middle", match.Id.Value);
        Assert.Equal(middle, match.CreatedAt);
        Assert.Equal(CodexModel.Parse("gpt-5.1-codex-mini"), match.Model);
    }

    [Fact]
    public async Task ListSessionsAsync_FiltersByWorkingDirectory()
    {
        // Arrange
        var targetWorkingDir = "/home/user/project-alpha";
        var otherWorkingDir = "/home/user/project-beta";

        CreateSessionLog(SessionId.Parse("alpha-1"), DateTimeOffset.UtcNow, targetWorkingDir, "gpt-5.1-codex");
        CreateSessionLog(SessionId.Parse("alpha-2"), DateTimeOffset.UtcNow.AddMinutes(1), targetWorkingDir, "gpt-5.1-codex-mini");
        CreateSessionLog(SessionId.Parse("beta-1"), DateTimeOffset.UtcNow.AddMinutes(2), otherWorkingDir, "gpt-5.1-codex-mini");

        var filter = SessionFilter.ForWorkingDirectory(targetWorkingDir);

        await using var client = CreateClient();

        // Act
        var sessions = await client.ListSessionsAsync(filter).ToListAsync();

        // Assert
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal(targetWorkingDir, s.WorkingDirectory, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsEmptyWhenDirectoryIsEmpty()
    {
        // Arrange
        await using var client = CreateClient();

        // Act
        var sessions = await client.ListSessionsAsync().ToListAsync();

        // Assert
        Assert.Empty(sessions);
    }

    private CodexClient CreateClient()
    {
        var options = Options.Create(new CodexClientOptions
        {
            SessionsRootDirectory = _sessionsRoot
        });

        return new CodexClient(
            options,
            processLauncher: null,
            sessionLocator: null,
            tailer: null,
            parser: null,
            pathProvider: null,
            logger: LoggerFactory.CreateLogger<CodexClient>(),
            loggerFactory: LoggerFactory);
    }

    private string CreateSessionLog(SessionId sessionId, DateTimeOffset timestamp, string workingDirectory, string? model)
    {
        var datePath = Path.Combine(
            _sessionsRoot,
            timestamp.Year.ToString("D4"),
            timestamp.Month.ToString("D2"),
            timestamp.Day.ToString("D2"));

        Directory.CreateDirectory(datePath);

        var filePath = Path.Combine(datePath, $"rollout-{timestamp:HHmmss}-{sessionId.Value}.jsonl");
        var lines = new[]
        {
            TestJsonlGenerator.GenerateSessionMeta(sessionId, workingDirectory, timestamp, model),
            TestJsonlGenerator.GenerateUserMessage("hello", timestamp.AddSeconds(1))
        };

        File.WriteAllLines(filePath, lines);
        File.SetCreationTimeUtc(filePath, timestamp.UtcDateTime);

        return filePath;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_sessionsRoot))
            {
                Directory.Delete(_sessionsRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures in test teardown.
        }

        return ValueTask.CompletedTask;
    }
}
