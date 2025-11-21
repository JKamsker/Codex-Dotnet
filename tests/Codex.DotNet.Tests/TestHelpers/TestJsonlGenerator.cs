using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Tests.TestHelpers;

/// <summary>
/// Helper class to generate test JSONL content for Codex events.
/// </summary>
/// <remarks>
/// This class provides methods to generate properly formatted JSONL strings
/// for various Codex event types, useful for testing event parsing and processing.
/// </remarks>
public static class TestJsonlGenerator
{
    /// <summary>
    /// Generates a session_meta JSONL event.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cwd">The current working directory.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <param name="model">Optional model identifier to include in metadata.</param>
    /// <returns>A JSONL string representing the session_meta event.</returns>
    public static string GenerateSessionMeta(SessionId sessionId, string cwd, DateTimeOffset? timestamp = null, string? model = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["id"] = sessionId.Value,
            ["cwd"] = cwd
        };

        if (!string.IsNullOrWhiteSpace(model))
        {
            payload["model"] = model;
        }

        var eventData = new
        {
            type = "session_meta",
            timestamp = ts.ToString("o"),
            payload
        };

        return JsonSerializer.Serialize(
            eventData,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    /// <summary>
    /// Generates a user_message JSONL event.
    /// </summary>
    /// <param name="text">The user's message text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A JSONL string representing the user_message event.</returns>
    public static string GenerateUserMessage(string text, DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var eventData = new
        {
            type = "user_message",
            timestamp = ts.ToString("o"),
            payload = new
            {
                message = text
            }
        };

        return JsonSerializer.Serialize(eventData);
    }

    /// <summary>
    /// Generates an agent_message JSONL event.
    /// </summary>
    /// <param name="text">The agent's message text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A JSONL string representing the agent_message event.</returns>
    public static string GenerateAgentMessage(string text, DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var eventData = new
        {
            type = "agent_message",
            timestamp = ts.ToString("o"),
            payload = new
            {
                message = text
            }
        };

        return JsonSerializer.Serialize(eventData);
    }

    /// <summary>
    /// Generates an agent_reasoning JSONL event.
    /// </summary>
    /// <param name="text">The agent's reasoning text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A JSONL string representing the agent_reasoning event.</returns>
    public static string GenerateAgentReasoning(string text, DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var eventData = new
        {
            type = "agent_reasoning",
            timestamp = ts.ToString("o"),
            payload = new
            {
                text
            }
        };

        return JsonSerializer.Serialize(eventData);
    }

    /// <summary>
    /// Generates a token_count JSONL event.
    /// </summary>
    /// <param name="inputTokens">The number of input tokens.</param>
    /// <param name="outputTokens">The number of output tokens.</param>
    /// <param name="reasoningTokens">The number of reasoning tokens.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A JSONL string representing the token_count event.</returns>
    public static string GenerateTokenCount(
        int inputTokens,
        int outputTokens,
        int reasoningTokens,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var eventData = new
        {
            type = "token_count",
            timestamp = ts.ToString("o"),
            payload = new
            {
                input_tokens = inputTokens,
                output_tokens = outputTokens,
                reasoning_output_tokens = reasoningTokens
            }
        };

        return JsonSerializer.Serialize(eventData);
    }

    /// <summary>
    /// Generates a turn_context JSONL event.
    /// </summary>
    /// <param name="approvalPolicy">The approval policy.</param>
    /// <param name="sandboxPolicyType">The sandbox policy type.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A JSONL string representing the turn_context event.</returns>
    public static string GenerateTurnContext(
        string approvalPolicy,
        string sandboxPolicyType,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var eventData = new
        {
            type = "turn_context",
            timestamp = ts.ToString("o"),
            payload = new
            {
                approval_policy = approvalPolicy,
                sandbox_policy_type = sandboxPolicyType
            }
        };

        return JsonSerializer.Serialize(eventData);
    }

    /// <summary>
    /// Generates a complete JSONL session with multiple events.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cwd">The current working directory.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="agentMessage">The agent's response message.</param>
    /// <param name="includeReasoning">Whether to include reasoning events.</param>
    /// <param name="includeTokens">Whether to include token count events.</param>
    /// <returns>A complete JSONL string with multiple events separated by newlines.</returns>
    public static string GenerateSession(
        SessionId sessionId,
        string cwd,
        string userMessage,
        string agentMessage,
        bool includeReasoning = false,
        bool includeTokens = false)
    {
        var baseTime = DateTimeOffset.UtcNow;
        var lines = new List<string>
        {
            GenerateSessionMeta(sessionId, cwd, baseTime),
            GenerateTurnContext("auto", "none", baseTime.AddMilliseconds(10)),
            GenerateUserMessage(userMessage, baseTime.AddMilliseconds(20))
        };

        if (includeReasoning)
        {
            lines.Add(GenerateAgentReasoning("Analyzing the user's request...", baseTime.AddMilliseconds(30)));
        }

        lines.Add(GenerateAgentMessage(agentMessage, baseTime.AddMilliseconds(40)));

        if (includeTokens)
        {
            lines.Add(GenerateTokenCount(100, 50, includeReasoning ? 25 : 0, baseTime.AddMilliseconds(50)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
