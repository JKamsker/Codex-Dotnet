using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public.Models;
using Codex.DotNet.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codex.DotNet.Tests.Unit;

/// <summary>
/// Unit tests for the JsonlEventParser.
/// </summary>
public class JsonlEventParserTests
{
    private readonly JsonlEventParser _parser;

    public JsonlEventParserTests()
    {
        _parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance);
    }

    [Fact]
    public async Task ParseAsync_SessionMetaEvent_ParsesCorrectly()
    {
        // Arrange
        var sessionId = SessionId.Parse("test-session-123");
        var cwd = "/home/user/project";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateSessionMeta(sessionId, cwd, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<SessionMetaEvent>().Subject;
        evt.SessionId.Should().Be(sessionId);
        evt.Cwd.Should().Be(cwd);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("session_meta");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_UserMessageEvent_ParsesCorrectly()
    {
        // Arrange
        var messageText = "Hello, Codex!";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateUserMessage(messageText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UserMessageEvent>().Subject;
        evt.Text.Should().Be(messageText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("user_message");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_AgentMessageEvent_ParsesCorrectly()
    {
        // Arrange
        var messageText = "I'll help you with that.";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentMessage(messageText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentMessageEvent>().Subject;
        evt.Text.Should().Be(messageText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("agent_message");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_AgentReasoningEvent_ParsesCorrectly()
    {
        // Arrange
        var reasoningText = "Analyzing the request structure...";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentReasoning(reasoningText, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<AgentReasoningEvent>().Subject;
        evt.Text.Should().Be(reasoningText);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("agent_reasoning");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_TokenCountEvent_ParsesCorrectly()
    {
        // Arrange
        var inputTokens = 100;
        var outputTokens = 50;
        var reasoningTokens = 25;
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTokenCount(inputTokens, outputTokens, reasoningTokens, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TokenCountEvent>().Subject;
        evt.InputTokens.Should().Be(inputTokens);
        evt.OutputTokens.Should().Be(outputTokens);
        evt.ReasoningTokens.Should().Be(reasoningTokens);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("token_count");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_TurnContextEvent_ParsesCorrectly()
    {
        // Arrange
        var approvalPolicy = "auto";
        var sandboxPolicyType = "none";
        var timestamp = DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTurnContext(approvalPolicy, sandboxPolicyType, timestamp);
        var lines = AsyncEnumerable.Repeat(jsonLine, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<TurnContextEvent>().Subject;
        evt.ApprovalPolicy.Should().Be(approvalPolicy);
        evt.SandboxPolicyType.Should().Be(sandboxPolicyType);
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.Type.Should().Be("turn_context");
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_UnknownEventType_CreatesUnknownCodexEvent()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var unknownJson = $@"{{
            ""type"": ""unknown_future_event"",
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""some_field"": ""some_value""
            }}
        }}";
        var lines = AsyncEnumerable.Repeat(unknownJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(1);
        var evt = events[0].Should().BeOfType<UnknownCodexEvent>().Subject;
        evt.Type.Should().Be("unknown_future_event");
        evt.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        evt.RawPayload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_SkipsLineAndContinues()
    {
        // Arrange
        var validLine = TestJsonlGenerator.GenerateUserMessage("Valid message");
        var malformedLine = "{ invalid json without closing brace";
        var anotherValidLine = TestJsonlGenerator.GenerateAgentMessage("Another valid message");

        var lines = new[] { validLine, malformedLine, anotherValidLine }.ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<UserMessageEvent>();
        events[1].Should().BeOfType<AgentMessageEvent>();
    }

    [Fact]
    public async Task ParseAsync_EmptyLines_AreSkipped()
    {
        // Arrange
        var validLine = TestJsonlGenerator.GenerateUserMessage("Test message");
        var lines = new[] { "", validLine, "   ", "\t", validLine }.ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(2);
        events.Should().AllBeOfType<UserMessageEvent>();
    }

    [Fact]
    public async Task ParseAsync_MissingTimestampField_SkipsEvent()
    {
        // Arrange
        var invalidJson = @"{
            ""type"": ""user_message"",
            ""payload"": {
                ""message"": ""Test""
            }
        }";
        var lines = AsyncEnumerable.Repeat(invalidJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MissingTypeField_SkipsEvent()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var invalidJson = $@"{{
            ""timestamp"": ""{timestamp:o}"",
            ""payload"": {{
                ""message"": ""Test""
            }}
        }}";
        var lines = AsyncEnumerable.Repeat(invalidJson, 1);

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_NullLines_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _parser.ParseAsync(null!).ToListAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("lines");
    }

    [Fact]
    public async Task ParseAsync_CompleteSession_ParsesAllEvents()
    {
        // Arrange
        var sessionId = SessionId.Parse("complete-session-test");
        var jsonl = TestJsonlGenerator.GenerateSession(
            sessionId,
            "/home/user/project",
            "User question",
            "Agent response",
            includeReasoning: true,
            includeTokens: true);

        var lines = jsonl.Split(Environment.NewLine).ToAsyncEnumerable();

        // Act
        var events = await _parser.ParseAsync(lines).ToListAsync();

        // Assert
        events.Should().HaveCount(6);
        events[0].Should().BeOfType<SessionMetaEvent>();
        events[1].Should().BeOfType<TurnContextEvent>();
        events[2].Should().BeOfType<UserMessageEvent>();
        events[3].Should().BeOfType<AgentReasoningEvent>();
        events[4].Should().BeOfType<AgentMessageEvent>();
        events[5].Should().BeOfType<TokenCountEvent>();
    }

    [Fact]
    public async Task ParseAsync_CancellationToken_StopsProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var infiniteLines = CreateInfiniteLines();

        // Act
        var events = new List<CodexEvent>();
        await foreach (var evt in _parser.ParseAsync(infiniteLines, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 3)
            {
                cts.Cancel();
            }
        }

        // Assert
        events.Should().HaveCount(3);
    }

    private static async IAsyncEnumerable<string> CreateInfiniteLines()
    {
        var index = 0;
        while (true)
        {
            yield return TestJsonlGenerator.GenerateUserMessage($"Message {index++}");
            await Task.Delay(1); // Simulate some delay
        }
    }
}
