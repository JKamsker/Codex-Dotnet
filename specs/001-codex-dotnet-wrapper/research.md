# Research Findings: Codex.DotNet Implementation

**Feature**: Codex.DotNet - CLI Wrapper Library  
**Date**: November 20, 2025  
**Phase**: Phase 0 - Research & Discovery

---

## 1. JSONL File Tailing Patterns in .NET

**Decision**: Use FileStream with FileShare.ReadWrite and polling-based growth detection with StreamReader

**Rationale**: 
- FileShare.ReadWrite allows concurrent read while Codex writes to the file
- FileSystemWatcher is unreliable for append operations (often doesn't fire for partial writes)
- Polling with configurable interval (200-500ms) provides predictable, testable behavior
- StreamReader.ReadLineAsync returns null at EOF, enabling clean polling loop

**Alternatives Considered**:
- **FileSystemWatcher**: Rejected due to unreliability for append operations, platform inconsistencies, and difficulty testing
- **Memory-mapped files**: Rejected as overkill for sequential line reading, adds complexity
- **Push-based events**: Rejected because polling is simpler and matches the append-only log pattern

**Implementation Notes**:
- Open with `FileMode.Open, FileAccess.Read, FileShare.ReadWrite`
- Use UTF-8 encoding for StreamReader
- When ReadLineAsync returns null, check if file size increased
- If size increased: call DiscardBufferedData() and continue reading
- Poll interval should be configurable via CodexClientOptions.TailPollInterval
- Reference: JSONL/NDJSON specification at jsonlines.org

---

## 2. Cross-Platform Process Management

**Decision**: Use ProcessStartInfo with runtime platform detection via RuntimeInformation.IsOSPlatform

**Rationale**:
- Built-in Process class handles cross-platform differences internally
- Platform detection needed only for executable name resolution (codex vs. codex.cmd)
- RedirectStandardInput/Output/Error provides clean communication channel
- Standard .NET approach, no additional dependencies

**Alternatives Considered**:
- **Platform-specific implementations**: Rejected as unnecessary, Process class abstracts well
- **Shell execution**: Rejected due to security concerns and escaping complexity
- **External process management libraries**: Rejected as overkill for basic needs

**Implementation Notes**:
- Windows: Check for "codex.cmd" then "codex.exe"
- Linux/macOS: Use "codex" directly
- Set `UseShellExecute = false, CreateNoWindow = true`
- Redirect stdin for prompt input, stdout/stderr for debugging (not main data source)
- Use Process.WaitForExitAsync with cancellation token
- Graceful shutdown: Close stdin, wait briefly, then Kill() if still running
- Reference pattern from Inspiration 1 (CodexProcessRunner)

---

## 3. Async Stream Best Practices

**Decision**: Implement IAsyncEnumerable<T> using async iterators with [EnumeratorCancellation] attribute

**Rationale**:
- C# async iterators provide clean syntax with yield return
- [EnumeratorCancellation] properly propagates cancellation token
- try-finally blocks ensure resource cleanup
- Standard pattern with excellent tooling support

**Alternatives Considered**:
- **Channel-based streaming**: Rejected as more complex, adds synchronization concerns
- **Reactive Extensions (Rx)**: Rejected as heavy dependency for simple streaming
- **Custom enumerator classes**: Rejected in favor of cleaner async iterator syntax

**Implementation Notes**:
```csharp
public async IAsyncEnumerable<CodexEvent> ParseAsync(
    IAsyncEnumerable<string> lines,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var line in lines.WithCancellation(cancellationToken))
    {
        // parse and yield
        yield return evt;
    }
}
```
- Use `.WithCancellation(ct)` when consuming other async enumerables
- Dispose resources in finally blocks
- Don't catch and swallow exceptions - let them propagate
- Reference: Microsoft Learn - Generate and consume async streams

---

## 4. Session Directory Monitoring

**Decision**: Snapshot directory before launch, then poll for new files with timestamp filter

**Rationale**:
- Simple, predictable, testable approach
- Avoids FileSystemWatcher reliability issues
- File creation timestamp provides clear "after start" filter
- Works reliably across platforms

**Alternatives Considered**:
- **FileSystemWatcher**: Rejected due to missed events, buffering issues, platform differences
- **Continuous polling**: Rejected in favor of snapshot + targeted search (more efficient)
- **Process output parsing**: Rejected as Codex doesn't output session file path

**Implementation Notes**:
- Before starting Codex: snapshot existing .jsonl files (GetFiles with pattern)
- Pass start timestamp to WaitForNewSessionFileAsync
- Poll sessions directory for files with CreationTimeUtc > startTime
- Use filename pattern: `*-{sessionId}.jsonl` or `rollout-*-*.jsonl`
- Timeout after configured duration (default 30 seconds)
- Return first matching new file found
- Pattern: `%USERPROFILE%/.codex/sessions/YYYY/MM/DD/rollout-<timestamp>-<sessionId>.jsonl`

---

## 5. JSON Parsing Error Recovery

**Decision**: Use JsonDocument.Parse per line with try-catch, log and skip malformed lines

**Rationale**:
- JsonDocument is fastest for read-only scenarios
- Per-line parsing with line-level error handling allows skipping bad lines
- Each JSONL line is independent, so recovery is clean
- Structured logging captures malformed data for debugging

**Alternatives Considered**:
- **JsonSerializer.Deserialize**: Rejected as slower and requires target type upfront
- **Fail on first error**: Rejected due to FR-020 requirement for resilience
- **Manual JSON parsing**: Rejected as reinventing the wheel, error-prone

**Implementation Notes**:
```csharp
try
{
    using var doc = JsonDocument.Parse(line);
    var root = doc.RootElement;
    // extract and map
}
catch (JsonException ex)
{
    _logger.LogWarning(ex, "Malformed JSON line: {Line}", line);
    continue; // skip this line
}
```
- Clone JsonElement for payload storage: `root.GetProperty("payload").Clone()`
- Use TryGetProperty for optional fields
- Map known event types via switch on "type" field
- Unknown types → UnknownCodexEvent with full payload preserved
- Reference: System.Text.Json documentation

---

## 6. Existing Codex CLI Behavior

**Decision**: Based on reference logs and original plan documentation

**Command syntax for new session**:
```bash
codex exec [--cd <repoRoot>] [--model <model>] [--config model_reasoning_effort=<low|medium|high>] [additional options] -
```
- Final `-` means read prompt from stdin
- Additional options: `--no-tui`, `--cheap-model`, etc.

**Command syntax for resume**:
```bash
codex exec [--cd <repoRoot>] resume <sessionId> -
```

**Session file location**:
- Path: `%USERPROFILE%/.codex/sessions/YYYY/MM/DD/rollout-<timestamp>-<sessionId>.jsonl`
- Format: JSONL (newline-delimited JSON), one event per line
- Encoding: UTF-8

**Event types observed** (from reference logs):
- `session_meta`: First event, contains `payload.id` (session ID), `payload.cwd`
- `turn_context`: Turn initialization with approval policy
- `user_message`: User prompt, `payload.message`
- `agent_message`: Codex response, `payload.message`
- `agent_reasoning`: Thinking text, `payload.text`
- `token_count`: Usage stats, `payload.{input_tokens, output_tokens, reasoning_output_tokens}`

**Rationale**: Based on actual log files in References/ExampleLogs/ and patterns from Inspiration 1 and 2

**Implementation Notes**:
- Model names: "gpt-5.1-codex", "gpt-5.1-codex-mini" (from plan)
- Reasoning effort: low, medium, high (mapped from enum)
- Session ID: Currently observed as UUID format in filename, but library must treat it as an opaque string to allow future formats
- First event is always session_meta - wait for this during startup
- Timestamp format in filename: ISO 8601 with milliseconds

---

## Summary

All research topics resolved with concrete decisions. No NEEDS CLARIFICATION items remain. The implementation can proceed to Phase 1 (Design & Contracts) with confidence in:

1. File tailing strategy using polling + FileShare.ReadWrite
2. Cross-platform process management via ProcessStartInfo
3. IAsyncEnumerable<T> streaming with proper cancellation
4. Session discovery via timestamp-filtered polling
5. Resilient per-line JSON parsing
6. Codex CLI command structure and event schemas

Next phase will create data-model.md defining entity schemas and contracts/ defining event structures.
