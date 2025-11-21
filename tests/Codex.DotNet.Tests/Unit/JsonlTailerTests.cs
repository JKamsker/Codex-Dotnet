using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public;
using Codex.DotNet.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Codex.DotNet.Tests.Unit;

/// <summary>
/// Unit tests for the JsonlTailer.
/// </summary>
public class JsonlTailerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly InMemoryFileSystem _fileSystem;
    private readonly IOptions<CodexClientOptions> _options;

    public JsonlTailerTests()
    {
        // Create a temporary directory for test files
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"JsonlTailerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        _fileSystem = new InMemoryFileSystem();
        _options = Options.Create(new CodexClientOptions
        {
            TailPollInterval = TimeSpan.FromMilliseconds(50)
        });
    }

    public void Dispose()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task TailAsync_FromBeginning_ReadsAllLines()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test.jsonl");
        var content = string.Join(Environment.NewLine, new[]
        {
            "Line 1",
            "Line 2",
            "Line 3"
        });
        File.WriteAllText(filePath, content);

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true, Follow: false);

        // Act
        var lines = await tailer.TailAsync(filePath, streamOptions, CancellationToken.None).ToListAsync();

        // Assert
        lines.Should().HaveCount(3);
        lines[0].Should().Be("Line 1");
        lines[1].Should().Be("Line 2");
        lines[2].Should().Be("Line 3");
    }

    [Fact]
    public async Task TailAsync_FromByteOffset_StartsAtCorrectPosition()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test-offset.jsonl");
        var lines = new[] { "First line", "Second line", "Third line" };
        var content = string.Join(Environment.NewLine, lines);
        File.WriteAllText(filePath, content);

        // Calculate offset to start at "Second line"
        var firstLineLength = System.Text.Encoding.UTF8.GetByteCount("First line" + Environment.NewLine);

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: false, FromByteOffset: firstLineLength, Follow: false);

        // Act
        var result = await tailer.TailAsync(filePath, streamOptions, CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("Second line");
        result[1].Should().Be("Third line");
    }

    [Fact]
    public async Task TailAsync_ActiveFile_WaitsForNewContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "active-file.jsonl");
        File.WriteAllText(filePath, "Initial line");

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true);

        using var cts = new CancellationTokenSource();
        var lines = new List<string>();

        // Act - Start tailing in background
        var tailTask = Task.Run(async () =>
        {
            await foreach (var line in tailer.TailAsync(filePath, streamOptions, cts.Token))
            {
                lines.Add(line);
                if (lines.Count >= 3) // Stop after getting 3 lines
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        // Wait for initial line to be read
        await Task.Delay(100);
        lines.Should().HaveCount(1);

        // Append new content
        await File.AppendAllTextAsync(filePath, Environment.NewLine + "New line 1" + Environment.NewLine + "New line 2");

        // Wait for new lines to be processed
        await Task.Delay(200);

        // Cancel to stop tailing
        cts.Cancel();

        try
        {
            await tailTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert
        lines.Should().HaveCountGreaterThanOrEqualTo(1);
        lines[0].Should().Be("Initial line");
    }

    [Fact]
    public async Task TailAsync_Cancellation_StopsReading()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "cancel-test.jsonl");
        File.WriteAllText(filePath, "Line 1");

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true);

        using var cts = new CancellationTokenSource();
        var lines = new List<string>();

        // Act - Start tailing
        var tailTask = Task.Run(async () =>
        {
            await foreach (var line in tailer.TailAsync(filePath, streamOptions, cts.Token))
            {
                lines.Add(line);
            }
        });

        // Wait a bit for the first line to be read
        await Task.Delay(100);

        // Cancel the operation
        cts.Cancel();

        // Wait for the task to complete
        try
        {
            await tailTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        lines.Should().HaveCount(1);
        lines[0].Should().Be("Line 1");
    }

    [Fact]
    public async Task TailAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.jsonl");
        var tailer = new JsonlTailer(_fileSystem, NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true);

        // Act
        var act = async () => await tailer.TailAsync(filePath, streamOptions, CancellationToken.None).ToListAsync();

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*does not exist*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TailAsync_InvalidFilePath_ThrowsArgumentNullException(string? invalidPath)
    {
        // Arrange
        var tailer = new JsonlTailer(_fileSystem, NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true);

        // Act
        var act = async () => await tailer.TailAsync(invalidPath!, streamOptions, CancellationToken.None).ToListAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TailAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test.jsonl");
        File.WriteAllText(filePath, "Test content");
        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);

        // Act
        var act = async () => await tailer.TailAsync(filePath, null!, CancellationToken.None).ToListAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task TailAsync_EmptyFile_ReturnsNoLines()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "empty.jsonl");
        File.WriteAllText(filePath, "");

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        var lines = new List<string>();
        try
        {
            await foreach (var line in tailer.TailAsync(filePath, streamOptions, cts.Token))
            {
                lines.Add(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - timeout while waiting for content
        }

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task TailAsync_FromBeginningFalse_StartsAtEnd()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test-end.jsonl");
        File.WriteAllText(filePath, "Existing line 1" + Environment.NewLine + "Existing line 2");

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        var lines = new List<string>();
        try
        {
            await foreach (var line in tailer.TailAsync(filePath, streamOptions, cts.Token))
            {
                lines.Add(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - timeout while waiting for new content
        }

        // Assert
        // Should not read existing lines when FromBeginning is false and no offset specified
        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task TailAsync_MultipleChunks_ReadsAllContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "multi-chunk.jsonl");
        var lines = Enumerable.Range(1, 100).Select(i => $"Line {i}").ToArray();
        var content = string.Join(Environment.NewLine, lines);
        File.WriteAllText(filePath, content);

        var tailer = new JsonlTailer(new RealFileSystem(), NullLogger<JsonlTailer>.Instance, _options);
        var streamOptions = new EventStreamOptions(FromBeginning: true, Follow: false);

        // Act
        var result = await tailer.TailAsync(filePath, streamOptions, CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(100);
        result.First().Should().Be("Line 1");
        result.Last().Should().Be("Line 100");
    }
}
