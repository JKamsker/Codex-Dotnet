using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Codex.DotNet.Tests.Unit;

public class ResponseItemEventTests
{
    private readonly JsonlEventParser _parser = new(NullLogger<JsonlEventParser>.Instance);

    [Fact]
    public async Task ParsesReasoningResponseItem_WithSummaryText()
    {
        var line = """{"timestamp":"2025-11-21T10:53:36.569Z","type":"response_item","payload":{"type":"reasoning","summary":[{"type":"summary_text","text":"**Planning read-only exploration**"}],"content":null,"encrypted_content":"gAAAAA..."}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("reasoning");
        response.Payload.SummaryTexts.Should().ContainSingle("**Planning read-only exploration**");
        response.Payload.FunctionCall.Should().BeNull();
        response.Payload.MessageTextParts.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ParsesMessageResponseItem_WithTextParts()
    {
        var line = """{"timestamp":"2025-11-21T10:53:37Z","type":"response_item","payload":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Hello there"}]}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("message");
        response.Payload.MessageRole.Should().Be("assistant");
        response.Payload.MessageTextParts.Should().ContainSingle("Hello there");
        response.Payload.SummaryTexts.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ParsesFunctionCallResponseItem_WithArguments()
    {
        var line = """{"timestamp":"2025-11-21T10:53:38Z","type":"response_item","payload":{"type":"function_call","name":"shell_command","arguments":{"command":"ls"},"call_id":"call_123"}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("function_call");
        response.Payload.FunctionCall.Should().NotBeNull();
        response.Payload.FunctionCall!.Name.Should().Be("shell_command");
        response.Payload.FunctionCall.ArgumentsJson.Should().Contain("ls");
        response.Payload.FunctionCall.CallId.Should().Be("call_123");
    }

    private async Task<CodexEvent> ParseSingleAsync(string line)
    {
        var singleLine = GetSingleLineAsync(line);
        await foreach (var evt in _parser.ParseAsync(singleLine))
        {
            return evt;
        }
        throw new InvalidOperationException("No event parsed.");
    }

    private static async IAsyncEnumerable<string> GetSingleLineAsync(string line)
    {
        yield return line;
        await Task.CompletedTask;
    }
}
