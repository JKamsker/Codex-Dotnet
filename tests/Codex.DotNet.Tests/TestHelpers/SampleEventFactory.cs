using System.Text.Json;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Tests.TestHelpers;

/// <summary>
/// Factory methods for creating test event objects.
/// </summary>
/// <remarks>
/// This class provides convenient methods to create properly constructed CodexEvent instances
/// for testing purposes, with realistic timestamps and properly constructed JsonElement payloads.
/// </remarks>
public static class SampleEventFactory
{
    /// <summary>
    /// Creates a SessionMetaEvent with the specified parameters.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cwd">The current working directory.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed SessionMetaEvent.</returns>
    public static SessionMetaEvent CreateSessionMetaEvent(
        SessionId sessionId,
        string cwd,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateSessionMeta(sessionId, cwd, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new SessionMetaEvent
        {
            Timestamp = ts,
            Type = "session_meta",
            RawPayload = root.Clone(),
            SessionId = sessionId,
            Cwd = cwd
        };
    }

    /// <summary>
    /// Creates a UserMessageEvent with the specified text.
    /// </summary>
    /// <param name="text">The user's message text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed UserMessageEvent.</returns>
    public static UserMessageEvent CreateUserMessageEvent(
        string text,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateUserMessage(text, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new UserMessageEvent
        {
            Timestamp = ts,
            Type = "user_message",
            RawPayload = root.Clone(),
            Text = text
        };
    }

    /// <summary>
    /// Creates an AgentMessageEvent with the specified text.
    /// </summary>
    /// <param name="text">The agent's message text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed AgentMessageEvent.</returns>
    public static AgentMessageEvent CreateAgentMessageEvent(
        string text,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentMessage(text, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new AgentMessageEvent
        {
            Timestamp = ts,
            Type = "agent_message",
            RawPayload = root.Clone(),
            Text = text
        };
    }

    /// <summary>
    /// Creates an AgentReasoningEvent with the specified text.
    /// </summary>
    /// <param name="text">The agent's reasoning text.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed AgentReasoningEvent.</returns>
    public static AgentReasoningEvent CreateAgentReasoningEvent(
        string text,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateAgentReasoning(text, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new AgentReasoningEvent
        {
            Timestamp = ts,
            Type = "agent_reasoning",
            RawPayload = root.Clone(),
            Text = text
        };
    }

    /// <summary>
    /// Creates a TokenCountEvent with the specified token counts.
    /// </summary>
    /// <param name="inputTokens">The number of input tokens.</param>
    /// <param name="outputTokens">The number of output tokens.</param>
    /// <param name="reasoningTokens">The number of reasoning tokens.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed TokenCountEvent.</returns>
    public static TokenCountEvent CreateTokenCountEvent(
        int inputTokens,
        int outputTokens,
        int reasoningTokens,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTokenCount(inputTokens, outputTokens, reasoningTokens, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new TokenCountEvent
        {
            Timestamp = ts,
            Type = "token_count",
            RawPayload = root.Clone(),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens
        };
    }

    /// <summary>
    /// Creates a TurnContextEvent with the specified policies.
    /// </summary>
    /// <param name="approvalPolicy">The approval policy.</param>
    /// <param name="sandboxPolicyType">The sandbox policy type.</param>
    /// <param name="timestamp">Optional timestamp (defaults to current UTC time).</param>
    /// <returns>A fully constructed TurnContextEvent.</returns>
    public static TurnContextEvent CreateTurnContextEvent(
        string approvalPolicy,
        string sandboxPolicyType,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var jsonLine = TestJsonlGenerator.GenerateTurnContext(approvalPolicy, sandboxPolicyType, ts);
        var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new TurnContextEvent
        {
            Timestamp = ts,
            Type = "turn_context",
            RawPayload = root.Clone(),
            ApprovalPolicy = approvalPolicy,
            SandboxPolicyType = sandboxPolicyType
        };
    }

    /// <summary>
    /// Creates a complete set of events for a typical session interaction.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cwd">The current working directory.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="agentMessage">The agent's response message.</param>
    /// <param name="includeReasoning">Whether to include reasoning events.</param>
    /// <param name="includeTokens">Whether to include token count events.</param>
    /// <returns>A list of CodexEvent instances representing a complete interaction.</returns>
    public static List<CodexEvent> CreateSessionEvents(
        SessionId sessionId,
        string cwd,
        string userMessage,
        string agentMessage,
        bool includeReasoning = false,
        bool includeTokens = false)
    {
        var baseTime = DateTimeOffset.UtcNow;
        var events = new List<CodexEvent>
        {
            CreateSessionMetaEvent(sessionId, cwd, baseTime),
            CreateTurnContextEvent("auto", "none", baseTime.AddMilliseconds(10)),
            CreateUserMessageEvent(userMessage, baseTime.AddMilliseconds(20))
        };

        if (includeReasoning)
        {
            events.Add(CreateAgentReasoningEvent("Analyzing the user's request...", baseTime.AddMilliseconds(30)));
        }

        events.Add(CreateAgentMessageEvent(agentMessage, baseTime.AddMilliseconds(40)));

        if (includeTokens)
        {
            events.Add(CreateTokenCountEvent(100, 50, includeReasoning ? 25 : 0, baseTime.AddMilliseconds(50)));
        }

        return events;
    }
}
