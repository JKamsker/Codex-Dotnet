using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for parsing JSONL event streams into strongly-typed Codex events.
/// </summary>
/// <remarks>
/// This interface provides functionality to convert raw JSON lines from Codex session logs
/// into strongly-typed <see cref="CodexEvent"/> instances. It handles event type discrimination
/// and deserialization, providing extensibility for unknown event types.
/// </remarks>
public interface IJsonlEventParser
{
    /// <summary>
    /// Parses an async stream of JSON lines into strongly-typed Codex events.
    /// </summary>
    /// <param name="lines">
    /// An async enumerable of JSON strings, where each string represents a single event.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the parsing operation.
    /// </param>
    /// <returns>
    /// An async enumerable that yields <see cref="CodexEvent"/> instances as they are parsed.
    /// Events are yielded in the order they appear in the input stream.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="lines"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method processes each JSON line and deserializes it into the appropriate
    /// <see cref="CodexEvent"/> subtype based on the event's "type" field:
    /// <list type="bullet">
    /// <item><description>session_meta -> <see cref="SessionMetaEvent"/></description></item>
    /// <item><description>user_message -> <see cref="UserMessageEvent"/></description></item>
    /// <item><description>agent_message -> <see cref="AgentMessageEvent"/></description></item>
    /// <item><description>agent_reasoning -> <see cref="AgentReasoningEvent"/></description></item>
    /// <item><description>token_count -> <see cref="TokenCountEvent"/></description></item>
    /// <item><description>turn_context -> <see cref="TurnContextEvent"/></description></item>
    /// <item><description>Unknown types -> <see cref="UnknownCodexEvent"/></description></item>
    /// </list>
    ///
    /// Invalid JSON lines or lines that cannot be parsed are logged and skipped without throwing exceptions.
    /// The parser preserves the raw JSON payload in each event's RawPayload property for extensibility.
    /// Empty or whitespace-only lines are skipped silently.
    /// </remarks>
    IAsyncEnumerable<CodexEvent> ParseAsync(
        IAsyncEnumerable<string> lines,
        CancellationToken cancellationToken);
}
