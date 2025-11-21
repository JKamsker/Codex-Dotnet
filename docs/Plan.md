Here’s a concrete plan for a **new .NET 10 library** that cleanly wraps the Codex CLI and its JSONL session logs.

I’ll call it **`Codex.DotNet`** in this plan, but you can rename it however you like.

---

## 1. Goals & constraints

**What the library must do**

* Start Codex via `exec`, **capture the session id**, and then:

  * **Stream events from the JSONL session file only** (ignore “human” stdout where possible).
* **Start new sessions** and **resume existing ones** (by ID / log file).
* Expose a **stream of structured events**, ideally as `IAsyncEnumerable<T>`.
* Be **cross‑platform** (Windows + *nix) and **testable** (no hard-coded file system or process logic).
* Provide a **modern .NET experience**:

  * Records, `IAsyncEnumerable<T>`, options pattern, DI-friendly.
  * Async throughout, with `CancellationToken`.

**Non‑goals (for v1)**

* Not a full “Codex SDK” – it’s a **CLI/session wrapper**, not a REST client.
* No GUI; just a clean programmatic surface.
* No assumption that we control Codex’s internal schema 100%; we’ll handle unknown events robustly.

---

## 2. High-level architecture

Pipeline for a *live* session:

```text
CodexClient.StartSessionAsync()
    ↓
ICodexCli / CodexProcessLauncher
    ↓ (exec, resume, etc.)
Codex CLI process
    ↓
Session JSONL file (.codex/sessions/YYYY/MM/DD/rollout-...-<sessionId>.jsonl)
    ↓
JsonlTailer (IAsyncEnumerable<string>)
    ↓
JsonlEventParser (IAsyncEnumerable<CodexEvent>)
    ↓
User code (await foreach)
```

For a *resumed* session:

```text
CodexClient.ResumeSessionAsync(sessionId/logPath)
    ↓
ICodexSessionLocator → find the JSONL file
    ↓
JsonlTailer + JsonlEventParser
    ↓
User code (await foreach)
```

We’ll build this as a set of small, testable services under one public façade: `CodexClient`.

---

## 3. Project layout

Single main project, optionally plus a test project:

```text
src/
  Codex.DotNet/
    Codex.DotNet.csproj
    Public/
      CodexClient.cs
      CodexClientOptions.cs
      CodexSessionHandle.cs
      CodexSessionOptions.cs
      CodexSessionInfo.cs
      CodexEvent.cs & derived types
      SessionFilter.cs
    Infrastructure/
      CodexProcessLauncher.cs
      CodexSessionLocator.cs
      JsonlTailer.cs
      JsonlEventParser.cs
      DefaultCodexPathProvider.cs
      RealFileSystem.cs
    Abstractions/
      ICodexClient.cs
      ICodexProcessLauncher.cs
      IFileSystem.cs
      ICodexPathProvider.cs
      ICodexSessionLocator.cs
      IJsonlTailer.cs
      IJsonlEventParser.cs
      ILoggerAdapter.cs (optional)
tests/
  Codex.DotNet.Tests/
```

Target frameworks: `net10.0` (and optionally multi‑target `net8.0/net9.0` for compatibility).

---

## 4. Public API design

### 4.1 Core concepts

#### `CodexSessionInfo`

Metadata about a session:

```csharp
public sealed record CodexSessionInfo(
    string Id,
    string LogPath,
    DateTimeOffset CreatedAt,
    string? RepoRoot,
    string? Model,
    string? HumanLabel // e.g. first user_message summary, optional
);
```

#### `CodexSessionOptions` (for starting sessions)

```csharp
public sealed record CodexSessionOptions
{
    public required string RepoRoot { get; init; }
    public required string Prompt { get; init; }

    public string Model { get; init; } = "gpt-5.1-codex";
    public ReasoningEffort ReasoningEffort { get; init; } = ReasoningEffort.Medium;

    /// Additional CLI options (e.g. ["--cheap-model", "--no-tui"])
    public IReadOnlyList<string> AdditionalOptions { get; init; } = Array.Empty<string>();

    /// Optional override of the codex binary path
    public string? CodexBinaryPath { get; init; }
}
```

`ReasoningEffort` is exactly like your existing enum:

```csharp
public enum ReasoningEffort { Low, Medium, High }
```

#### `CodexClientOptions`

Global configuration:

```csharp
public sealed class CodexClientOptions
{
    public string? CodexExecutablePath { get; set; } // default: "codex"/"codex.cmd"
    public string? SessionsRootDirectory { get; set; } // default: %USERPROFILE%/.codex/sessions
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ProcessExitTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan TailPollInterval { get; set; } = TimeSpan.FromMilliseconds(200);
}
```

### 4.2 Events model

We’ll represent each JSONL record as a `CodexEvent`:

```csharp
public abstract record CodexEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public string Type { get; init; } = default!;
    public JsonElement RawPayload { get; init; } // or JsonNode
}
```

Type-specific derived records for the common event kinds we see in your logs:

```csharp
public sealed record SessionMetaEvent : CodexEvent
{
    public string SessionId { get; init; } = default!;
    public string? Cwd { get; init; }
}

public sealed record TurnContextEvent : CodexEvent
{
    public string? ApprovalPolicy { get; init; }
    public string? SandboxPolicyType { get; init; }
    // Additional fields as needed
}

public sealed record UserMessageEvent : CodexEvent
{
    public string Text { get; init; } = default!;
}

public sealed record AgentMessageEvent : CodexEvent
{
    public string Text { get; init; } = default!;
}

public sealed record AgentReasoningEvent : CodexEvent
{
    public string Text { get; init; } = default!;
}

public sealed record TokenCountEvent : CodexEvent
{
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? ReasoningTokens { get; init; }
}
```

For unknown or rarely-used types we can keep them as `UnknownCodexEvent : CodexEvent`.

**Mapping rule:** `JsonlEventParser` inspects `type`, then:

* For `session_meta`, it creates `SessionMetaEvent` and sets `SessionId` from `payload.id`.
* For others, map fields if they’re present; otherwise store the payload in `RawPayload` so callers can dig into it.

### 4.3 Session handle & client

#### `CodexSessionHandle`

Represents an active or resumed session:

```csharp
public sealed class CodexSessionHandle : IAsyncDisposable
{
    public CodexSessionInfo Info { get; }

    /// Live process for new sessions; null for purely “resumed from log” sessions.
    public bool IsLive => _process != null && !_process!.HasExited;

    // Main entry point for streaming events:
    public IAsyncEnumerable<CodexEvent> GetEventsAsync(
        EventStreamOptions? options = null,
        CancellationToken cancellationToken = default);

    /// Write to Codex stdin in “exec -” mode (for live sessions only)
    public Task SendInputAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// Wait for Codex process to exit (no-op for resumed sessions)
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    public ValueTask DisposeAsync();
}
```

`EventStreamOptions` controls where to start:

```csharp
public sealed record EventStreamOptions
{
    /// Start reading from the beginning of the file (default: true)
    public bool FromBeginning { get; init; } = true;

    /// If not from beginning, start after this timestamp or byte offset
    public DateTimeOffset? AfterTimestamp { get; init; }
    public long? FromByteOffset { get; init; }
}
```

#### `ICodexClient` & `CodexClient`

```csharp
public interface ICodexClient : IAsyncDisposable
{
    Task<CodexSessionHandle> StartSessionAsync(
        CodexSessionOptions options,
        CancellationToken cancellationToken = default);

    Task<CodexSessionHandle> ResumeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<CodexSessionHandle> AttachToLogAsync(
        string logPath,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        SessionFilter? filter = null,
        CancellationToken cancellationToken = default);
}
```

`SessionFilter` might filter by date range, repo root, or model.

`CodexClient` is the default concrete implementation.

---

## 5. How we start Codex & capture the session id

We’ll generalize what you already have in `CodexOrchestrator` and `CodexProcessRunner`.

### 5.1 CLI argument construction

For a **new session**, we replicate your pattern:

```text
codex exec [additional options] --cd <repoRoot> --model <model> \
  --config model_reasoning_effort=<low|medium|high> -
```

Notes:

* The final `-` means “read prompt from stdin”, as in your orchestrator.
* Additional options from `CodexSessionOptions.AdditionalOptions` are inserted after `exec`.

For a **resume**:

```text
codex exec [additional options] --cd <repoRoot> resume <sessionId> -
```

(`CodexSessionOptions` for resume might not need `Prompt`; or we add a `CodexResumeOptions` record separately.)

### 5.2 Process launching

Encapsulated in `ICodexProcessLauncher`:

```csharp
public interface ICodexProcessLauncher
{
    Task<(Process Process, string? StdoutBuffer, string? StderrBuffer)> 
        StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}
```

Default implementation:

* Sets:

  * `RedirectStandardInput = true`
  * `RedirectStandardOutput = true` (for debugging, not for main data)
  * `RedirectStandardError = true`
  * `UseShellExecute = false`, `CreateNoWindow = true`.
* Writes the prompt to stdin.
* Optionally captures a **small** rolling buffer of stdout/stderr (for debugging & error messages) but we don’t parse them for events.

### 5.3 Finding the right JSONL file

We need `ICodexSessionLocator`:

```csharp
public interface ICodexSessionLocator
{
    Task<string> WaitForNewSessionFileAsync(
        DateTimeOffset after,
        CancellationToken cancellationToken);

    Task<string?> FindSessionFileByIdAsync(
        string sessionId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        SessionFilter? filter,
        CancellationToken cancellationToken);
}
```

Default implementation:

* Uses `CodexClientOptions.SessionsRootDirectory` (defaults to `%USERPROFILE%/.codex/sessions` like your `ProductionPathProvider`).
* For **WaitForNewSessionFileAsync**:

  * Snapshot all `.jsonl` files before launching Codex.
  * After start, poll with your configured interval for new `.jsonl` files created after `after`.
  * Return the path to the first new file found.
* For **FindSessionFileByIdAsync**:

  * Use the filename pattern `*<sessionId>.jsonl` (your logs are `rollout-<timestamp>-<sessionId>.jsonl`).
  * Optionally verify by opening the file and checking the first `session_meta` event’s `payload.id`.

---

## 6. JSONL tailing & event pipeline

We treat Codex logs as **JSONL / NDJSON**: one valid JSON object per line, separated by newlines. That’s exactly what JSON Lines / NDJSON is designed for: log-style streaming of structured data. ([jsonlines.org][1])

### 6.1 Tailer: `JsonlTailer`

```csharp
public sealed class JsonlTailer : IJsonlTailer
{
    public async IAsyncEnumerable<string> TailAsync(
        string path,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // open FileStream(FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        // seek to options.FromByteOffset or 0
        // read lines in a loop using StreamReader.ReadLineAsync
        // when EOF reached, poll for new data & continue
    }
}
```

Implementation details:

* Open file with `FileShare.ReadWrite` so Codex can keep appending.
* `ReadLineAsync` will return `null` at EOF; in that case:

  * `await Task.Delay(pollInterval)` and then check if the file length has grown.
  * If grown: reset the `StreamReader` buffer (`DiscardBufferedData` + reposition the base stream) and continue reading.
* Use UTF‑8 encoding.
* Each line is one complete JSON object, as per JSON Lines/NDJSON conventions, so we can parse each line independently. ([jsonltools.com][2])

### 6.2 Parser: `JsonlEventParser`

```csharp
public sealed class JsonlEventParser : IJsonlEventParser
{
    public async IAsyncEnumerable<CodexEvent> ParseAsync(
        IAsyncEnumerable<string> lines,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // Log & skip malformed line
                continue;
            }

            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString() ?? "unknown";
            var ts = root.GetProperty("timestamp").GetDateTimeOffset();

            var payload = root.TryGetProperty("payload", out var payloadEl)
                ? payloadEl.Clone()
                : default;

            var evt = MapToConcreteEvent(type, ts, payload, root);
            yield return evt;
        }
    }

    private static CodexEvent MapToConcreteEvent(
        string type,
        DateTimeOffset ts,
        JsonElement payload,
        JsonElement root)
    {
        return type switch
        {
            "session_meta" => new SessionMetaEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload,
                SessionId = payload.GetProperty("id").GetString()!,
                Cwd = payload.TryGetProperty("cwd", out var cwd) ? cwd.GetString() : null
            },
            "user_message" => new UserMessageEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload,
                Text = payload.GetProperty("message").GetString() ?? string.Empty
            },
            "agent_message" => new AgentMessageEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload,
                Text = payload.GetProperty("message").GetString() ?? string.Empty
            },
            "agent_reasoning" => new AgentReasoningEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload,
                Text = payload.GetProperty("text").GetString() ?? string.Empty
            },
            "token_count" => new TokenCountEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload,
                InputTokens = payload.TryGetProperty("input_tokens", out var i)
                    ? i.GetInt32() : null,
                OutputTokens = payload.TryGetProperty("output_tokens", out var o)
                    ? o.GetInt32() : null,
                ReasoningTokens = payload.TryGetProperty("reasoning_output_tokens", out var r)
                    ? r.GetInt32() : null
            },
            _ => new UnknownCodexEvent
            {
                Timestamp = ts,
                Type = type,
                RawPayload = payload
            }
        };
    }
}
```

(You’d adjust property names once you’ve fully documented Codex’s payload schema.)

### 6.3 Wiring into `CodexSessionHandle`

`CodexSessionHandle.GetEventsAsync` becomes a *thin wrapper*:

```csharp
public IAsyncEnumerable<CodexEvent> GetEventsAsync(
    EventStreamOptions? options = null,
    CancellationToken cancellationToken = default)
{
    var effectiveOptions = options ?? new EventStreamOptions();
    var lines = _jsonlTailer.TailAsync(Info.LogPath, effectiveOptions, cancellationToken);
    return _eventParser.ParseAsync(lines, cancellationToken);
}
```

This is a straightforward and idiomatic async stream: user code consumes it with `await foreach`, as in the .NET async streams guidance. ([Microsoft Learn][3])

---

## 7. Session lifecycle: start, resume, attach

### 7.1 Starting a new session

`CodexClient.StartSessionAsync` orchestrates:

1. Capture timestamp `startTime = DateTimeOffset.UtcNow`.
2. Ask `ICodexSessionLocator.WaitForNewSessionFileAsync(startTime, ct)` **in parallel** with starting the process.
3. Build `ProcessStartInfo` (using `CodexSessionOptions`).
4. Launch Codex via `ICodexProcessLauncher`.
5. Feed the prompt into stdin.
6. Wait until:

   * We have a **session log path** from the locator, and
   * We have read at least one line from that file whose `type` is `session_meta`, so we get `sessionId`.
7. If the process exits early **before** we see a `session_meta`, read captured stderr/stdout buffer and throw a descriptive exception.
8. Construct `CodexSessionInfo` from the `SessionMetaEvent`, log path and metadata (repo root, model).
9. Return `CodexSessionHandle` that holds:

   * The live `Process` (for new sessions).
   * The log path.
   * A reference to `JsonlTailer` and `JsonlEventParser`.

Internally:

```csharp
public async Task<CodexSessionHandle> StartSessionAsync(
    CodexSessionOptions options,
    CancellationToken cancellationToken)
{
    var startTime = DateTimeOffset.UtcNow;

    var startInfo = _startInfoBuilder.BuildExecuteStartInfo(options);
    var sessionFileTask = _sessionLocator.WaitForNewSessionFileAsync(startTime, cancellationToken);
    var (process, _, _) = await _processLauncher.StartAsync(startInfo, cancellationToken);

    // write prompt to stdin
    await process.StandardInput.WriteAsync(options.Prompt);
    await process.StandardInput.FlushAsync();
    process.StandardInput.Close();

    var logPath = await sessionFileTask;

    // read session_meta
    var meta = await GetSessionMetaAsync(logPath, cancellationToken);
    var info = new CodexSessionInfo(
        meta.SessionId,
        logPath,
        meta.Timestamp,
        options.RepoRoot,
        options.Model,
        humanLabel: null);

    return new CodexSessionHandle(info, process, _jsonlTailer, _eventParser, _options, _logger);
}
```

`GetSessionMetaAsync` is a helper that reads from the start of the JSONL until it finds `SessionMetaEvent`.

### 7.2 Resuming by session id

```csharp
public async Task<CodexSessionHandle> ResumeSessionAsync(
    string sessionId,
    CancellationToken cancellationToken)
{
    var logPath = await _sessionLocator.FindSessionFileByIdAsync(sessionId, cancellationToken)
        ?? throw new InvalidOperationException($"Session '{sessionId}' not found");

    var meta = await GetSessionMetaAsync(logPath, cancellationToken);
    var info = new CodexSessionInfo(
        meta.SessionId,
        logPath,
        meta.Timestamp,
        repoRoot: meta.Cwd,    // or parse from payload if present
        model: null,
        humanLabel: null);

    // No live process – this is a view over the log file only
    return new CodexSessionHandle(info, process: null, _jsonlTailer, _eventParser, _options, _logger);
}
```

If you later decide to support “resume with Codex CLI actually running again”, you can add:

```csharp
Task<CodexSessionHandle> ResumeWithProcessAsync(string sessionId, CodexSessionOptions options, CancellationToken ct);
```

which essentially calls `codex exec ... resume <sessionId> -` and does the same dance as `StartSessionAsync`.

### 7.3 Attaching directly to a log file

Useful for tooling that only cares about “playback” of an existing session:

```csharp
public Task<CodexSessionHandle> AttachToLogAsync(string logPath, CancellationToken cancellationToken)
{
    // validate path & file existence
    var meta = await GetSessionMetaAsync(logPath, cancellationToken);
    var info = new CodexSessionInfo(
        meta.SessionId,
        logPath,
        meta.Timestamp,
        repoRoot: meta.Cwd,
        model: null,
        humanLabel: null);

    return Task.FromResult(new CodexSessionHandle(info, process: null, _jsonlTailer, _eventParser, _options, _logger));
}
```

---

## 8. Streaming API surface (IAsyncEnumerable)

The user story is:

> “There should be a stream of output that the user can choose to receive (asyncenumerable?)”

We give them this:

```csharp
await using var client = new CodexClient(options);
await using var session = await client.StartSessionAsync(
    new CodexSessionOptions
    {
        RepoRoot = repoRoot,
        Prompt = "Generate integration test scenarios for Countries controller",
        Model = "gpt-5.1-codex",
        ReasoningEffort = ReasoningEffort.Medium
    },
    cancellationToken);

await foreach (var ev in session.GetEventsAsync().WithCancellation(cancellationToken))
{
    switch (ev)
    {
        case UserMessageEvent user:
            Console.WriteLine($"USER: {user.Text}");
            break;
        case AgentMessageEvent agent:
            Console.WriteLine($"AGENT: {agent.Text}");
            break;
        case AgentReasoningEvent thinking:
            Console.WriteLine($"THINKING: {thinking.Text}");
            break;
        case TokenCountEvent tokens:
            Console.WriteLine($"Tokens: in={tokens.InputTokens}, out={tokens.OutputTokens}");
            break;
    }
}
```

This uses idiomatic async streams (`IAsyncEnumerable<T>` + `await foreach`) as recommended in modern .NET for streaming data sources. ([Microsoft Learn][3])

---

## 9. Configuration & DI

To make it drop-in for your tools:

```csharp
services.AddOptions<CodexClientOptions>()
        .Bind(configuration.GetSection("Codex"));

services.AddSingleton<ICodexClient, CodexClient>();
services.AddSingleton<ICodexProcessLauncher, CodexProcessLauncher>();
services.AddSingleton<ICodexPathProvider, DefaultCodexPathProvider>();
services.AddSingleton<ICodexSessionLocator, CodexSessionLocator>();
services.AddSingleton<IJsonlTailer, JsonlTailer>();
services.AddSingleton<IJsonlEventParser, JsonlEventParser>();
services.AddSingleton<IFileSystem, RealFileSystem>();
```

And a simple extension method:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCodexClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CodexClientOptions>(
            configuration.GetSection("Codex"));

        // register dependencies (as above)
        return services;
    }
}
```

---

## 10. Error handling & diagnostics

Key failure cases and how to handle them:

1. **Codex binary not found / not executable**

   * Validate path before launching (like your `ValidateCodexBinary` in `CodexOrchestrator`).
   * Throw `InvalidOperationException("Codex executable not found at ...")`.

2. **Process times out starting**

   * `StartSessionAsync` uses `CodexClientOptions.StartTimeout`.
   * If the process doesn’t create a session file or print `session_meta` in time:

     * Try to kill the process.
     * Throw `TimeoutException("Codex session did not start within ...")`.

3. **Session file not found**

   * `CodexSessionLocator` throws a detailed `InvalidOperationException`:

     * “Could not find Codex session file under {SessionsRootDirectory}...”
   * Suggest verifying Codex installation in the exception message.

4. **JSON parse errors**

   * `JsonlEventParser` logs and **skips** the problematic line.
   * Optionally raises a `CodexEventParsingException` if you want strict mode.

5. **User cancels**

   * All async streams accept `CancellationToken`.
   * Tailer and parser both respect cancellation and stop yielding.

6. **Live session cancellation**

   * `CodexSessionHandle.DisposeAsync` tries to gracefully kill the process (best-effort) and closes file handles.

All errors should be logged via `ILogger` (wrapped as `ILoggerAdapter` if you want to avoid direct dependency on `Microsoft.Extensions.Logging` in the core assembly).

---

## 11. Testing strategy

You’ve already got good patterns in the reference projects (`InMemoryFileSystem`, `MockCodexProcessRunner`). We’ll reuse them:

* **Unit tests**

  * `JsonlEventParser` with real sample lines from your logs.
  * `JsonlTailer` using an in‑memory file (temp file) that gets appended to during the test.
  * `CodexSessionLocator` with a fake file system tree under a fake `.codex/sessions`.

* **Integration tests**

  * `CodexClient.StartSessionAsync` using a fake `ICodexProcessLauncher` that:

    * Writes a synthetic JSONL file (like your `CreateSampleJsonl` helpers).
    * Pretends to be a Codex process but never actually spawns anything.

* **Smoke tests (optional)**

  * On your machine, a test that runs real `codex exec` in a sandbox repo and asserts that:

    * A session file is created.
    * `StartSessionAsync` returns a handle with non-null `Info.Id`.
    * Events stream includes at least one `UserMessageEvent` and `AgentMessageEvent`.

---

## 12. Migration path from your existing tools

### From **Pandocs.Tool.AI.Orchestration** (Inspiration 1)

Current flow:

* `CodexOrchestrator.ExecuteAsync` → shells out to Codex, handles callbacks & retries, returns `CodexSession` with `SessionId` and raw output.

New flow:

* Replace direct `CodexOrchestrator` usage with `ICodexClient`.
* Builder:

```csharp
var sessionOptions = new CodexSessionOptions
{
    RepoRoot = repoRoot,
    Prompt = prompt,
    Model = settings.CheapModel ? "gpt-5.1-codex-mini" : settings.Model,
    ReasoningEffort = ReasoningEffort.Medium,
    AdditionalOptions = settings.ParseCodexOptions()
};
await using var session = await _codexClient.StartSessionAsync(sessionOptions, ct);
```

* Use `session.Info.Id` instead of parsing stdout or the JSONL yourself.
* If you still want text callbacks (`ThinkingCallback`, `OutputCallback`), wrap the `GetEventsAsync` stream and forward `AgentReasoningEvent.Text` / `AgentMessageEvent.Text` to your existing callbacks.

### From **CodexAuthManager** (Inspiration 2)

Current flow:

* `CodexProcessRunner.RunCodexWithStatusAsync` launches Codex, then searches `.codex/sessions`, reads entire JSONL into memory.
* `CodexTuiService` parses usage stats from that string.

New flow:

* Replace those two concepts with:

  * `ICodexClient` + `CodexUsageStatsService` built on top of `IAsyncEnumerable<CodexEvent>`.
* Example:

```csharp
await using var session = await _codexClient.StartSessionAsync(options, ct);

var stats = await CodexUsageStats.FromEventsAsync(
    session.GetEventsAsync(ct),
    identityId,
    ct);
```

Where `CodexUsageStats.FromEventsAsync` is a high-level helper that looks for `token_count` and quota-related events in the JSONL stream.

---

## 13. Implementation checklist

A practical ordering for building this library:

1. **Core abstractions**

   * `ICodexClient`, `CodexSessionOptions`, `CodexSessionInfo`, event types.
2. **File system & path provider**

   * `IFileSystem`, `RealFileSystem`, `ICodexPathProvider`, `DefaultCodexPathProvider`.
3. **Session locator**

   * Implementation of `ICodexSessionLocator` with `.codex/sessions` traversal and “latest file” logic similar to your `CodexProcessRunner`.
4. **JsonlTailer** (with tests around growing files).
5. **JsonlEventParser** (tests using your example logs).
6. **CodexProcessLauncher** (OS detection based on `RuntimeInformation` like in `CodexProcessRunner`).
7. **CodexClient.StartSessionAsync / ResumeSessionAsync / AttachToLogAsync**.
8. **DI & options helpers**.
9. **Migration adapters** (optional):

   * `ICodexOrchestrator` implementation that delegates to `ICodexClient`.
   * `CodexTuiService` built over `ICodexClient`.

---

If you’d like, next step could be: I can sketch **actual C# stubs** for these types (interfaces + records + a skeletal `CodexClient`) so you have a ready-to-fill solution structure.

[1]: https://jsonlines.org/?utm_source=chatgpt.com "JSON Lines"
[2]: https://jsonltools.com/ndjson-format-specification?utm_source=chatgpt.com "NDJSON Specification: Newline Delimited JSON Format ..."
[3]: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream?utm_source=chatgpt.com "Generate and consume async streams using C# and .NET"
