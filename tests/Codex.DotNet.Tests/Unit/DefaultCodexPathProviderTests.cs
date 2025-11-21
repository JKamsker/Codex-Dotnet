using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public.Models;
using Codex.DotNet.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codex.DotNet.Tests.Unit;

/// <summary>
/// Unit tests for the DefaultCodexPathProvider.
/// </summary>
public class DefaultCodexPathProviderTests
{
    private readonly InMemoryFileSystem _fileSystem;
    private readonly DefaultCodexPathProvider _pathProvider;

    public DefaultCodexPathProviderTests()
    {
        _fileSystem = new InMemoryFileSystem();
        _pathProvider = new DefaultCodexPathProvider(_fileSystem, NullLogger<DefaultCodexPathProvider>.Instance);
    }

    [Fact]
    public void GetCodexExecutablePath_WithOverride_ReturnsOverride()
    {
        // Arrange
        var overridePath = @"C:\custom\path\codex.exe";
        _fileSystem.AddFile(overridePath, "fake executable");

        // Act
        var result = _pathProvider.GetCodexExecutablePath(overridePath);

        // Assert
        result.Should().Be(overridePath);
    }

    [Fact]
    public void GetCodexExecutablePath_WithNonExistentOverride_ThrowsFileNotFoundException()
    {
        // Arrange
        var overridePath = @"C:\nonexistent\codex.exe";

        // Act
        var act = () => _pathProvider.GetCodexExecutablePath(overridePath);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*does not exist*")
            .Which.FileName.Should().Be(overridePath);
    }

    [Fact]
    public void GetCodexExecutablePath_NoOverride_SearchesPATH()
    {
        // Arrange - Set up a fake PATH with codex.exe
        var pathDir = @"C:\Program Files\Codex";
        var codexPath = Path.Combine(pathDir, "codex.exe");
        _fileSystem.AddDirectory(pathDir);
        _fileSystem.AddFile(codexPath, "fake executable");

        // Mock PATH environment variable (note: this test validates the logic but can't fully test PATH resolution)
        // In a real scenario, the PATH would need to be set, but here we verify the file exists check

        // Act & Assert - Without override and with no PATH setup, it will throw
        var act = () => _pathProvider.GetCodexExecutablePath(null);
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*not found*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCodexExecutablePath_WithEmptyOrWhitespaceOverride_SearchesPATH(string? overridePath)
    {
        // Act
        var act = () => _pathProvider.GetCodexExecutablePath(overridePath);

        // Assert - Should attempt PATH search and throw when not found
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetSessionsRootDirectory_WithOverride_ReturnsOverride()
    {
        // Arrange
        var overrideDir = @"C:\custom\sessions";
        _fileSystem.AddDirectory(overrideDir);

        // Act
        var result = _pathProvider.GetSessionsRootDirectory(overrideDir);

        // Assert
        result.Should().Be(overrideDir);
    }

    [Fact]
    public void GetSessionsRootDirectory_WithNonExistentOverride_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var overrideDir = @"C:\nonexistent\sessions";

        // Act
        var act = () => _pathProvider.GetSessionsRootDirectory(overrideDir);

        // Assert
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public void GetSessionsRootDirectory_NoOverride_ReturnsDefault()
    {
        // Arrange - In a test environment, USERPROFILE should be available
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = Path.Combine(userProfile, ".codex", "sessions");

        // Act
        var result = _pathProvider.GetSessionsRootDirectory(null);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSessionsRootDirectory_WithEmptyOrWhitespaceOverride_ReturnsDefault(string? overrideDir)
    {
        // Arrange
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = Path.Combine(userProfile, ".codex", "sessions");

        // Act
        var result = _pathProvider.GetSessionsRootDirectory(overrideDir);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveSessionLogPath_ValidSessionId_ReturnsLogPath()
    {
        // Arrange
        var sessionId = SessionId.Parse("abc123");
        var sessionsRoot = @"C:\sessions";
        var expectedLogFile = Path.Combine(sessionsRoot, $"2024-01-01-{sessionId.Value}.jsonl");

        _fileSystem.AddDirectory(sessionsRoot);
        _fileSystem.AddFile(expectedLogFile, "log content");

        // Act
        var result = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        result.Should().Be(expectedLogFile);
    }

    [Fact]
    public void ResolveSessionLogPath_NonExistentSessionId_ThrowsFileNotFoundException()
    {
        // Arrange
        var sessionId = SessionId.Parse("nonexistent");
        var sessionsRoot = @"C:\sessions";

        _fileSystem.AddDirectory(sessionsRoot);

        // Act
        var act = () => _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*No session log file found*");
    }

    [Fact]
    public void ResolveSessionLogPath_NonExistentSessionsRoot_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var sessionId = SessionId.Parse("test-session");
        var sessionsRoot = @"C:\nonexistent";

        // Act
        var act = () => _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*sessions directory does not exist*");
    }

    [Fact]
    public void ResolveSessionLogPath_MultipleMatchingFiles_ReturnsFirst()
    {
        // Arrange
        var sessionId = SessionId.Parse("multi-match");
        var sessionsRoot = @"C:\sessions";
        var logFile1 = Path.Combine(sessionsRoot, $"2024-01-01-{sessionId.Value}.jsonl");
        var logFile2 = Path.Combine(sessionsRoot, $"2024-01-02-{sessionId.Value}.jsonl");

        _fileSystem.AddDirectory(sessionsRoot);
        _fileSystem.AddFile(logFile1, "log 1");
        _fileSystem.AddFile(logFile2, "log 2");

        // Act
        var result = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        result.Should().BeOneOf(logFile1, logFile2);
    }

    [Fact]
    public void ResolveSessionLogPath_SessionIdWithSpecialCharacters_Works()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-with-dashes-123");
        var sessionsRoot = @"C:\sessions";
        var logFile = Path.Combine(sessionsRoot, $"2024-{sessionId.Value}.jsonl");

        _fileSystem.AddDirectory(sessionsRoot);
        _fileSystem.AddFile(logFile, "log content");

        // Act
        var result = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        result.Should().Be(logFile);
    }

    [Fact]
    public void ResolveSessionLogPath_NullSessionsRoot_UsesDefault()
    {
        // Arrange
        var sessionId = SessionId.Parse("default-root-test");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultSessionsRoot = Path.Combine(userProfile, ".codex", "sessions");
        var logFile = Path.Combine(defaultSessionsRoot, $"2024-{sessionId.Value}.jsonl");

        _fileSystem.AddDirectory(defaultSessionsRoot);
        _fileSystem.AddFile(logFile, "log content");

        // Act
        var result = _pathProvider.ResolveSessionLogPath(sessionId, null);

        // Assert
        result.Should().Be(logFile);
    }

    [Fact]
    public void Constructor_NullFileSystem_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultCodexPathProvider(null!, NullLogger<DefaultCodexPathProvider>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileSystem");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultCodexPathProvider(_fileSystem, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void ResolveSessionLogPath_PatternMatchingWildcard_Works()
    {
        // Arrange
        var sessionId = SessionId.Parse("wildcard-test");
        var sessionsRoot = @"C:\sessions";
        // File matches pattern: *-{sessionId}.jsonl
        var logFile = Path.Combine(sessionsRoot, $"prefix-timestamp-{sessionId.Value}.jsonl");

        _fileSystem.AddDirectory(sessionsRoot);
        _fileSystem.AddFile(logFile, "log content");

        // Act
        var result = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);

        // Assert
        result.Should().Be(logFile);
    }
}
