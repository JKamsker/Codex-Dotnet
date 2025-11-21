# Quick Start Guide: Codex.DotNet

Get started with Codex.DotNet CLI wrapper library in minutes.

---

## Installation

### NuGet Package (when published)

```bash
dotnet add package Codex.DotNet
```

### Build from Source

```bash
git clone https://github.com/yourorg/Codex-Dotnet.git
cd Codex-Dotnet
dotnet build src/Codex.DotNet/Codex.DotNet.csproj
```

---

## Prerequisites

- .NET 10 SDK or later
- Codex CLI installed and accessible in PATH
- Valid Codex authentication/configuration

---

## Basic Usage

### 1. Start a New Session and Stream Events

```csharp
using System.Threading;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

// Create client with defaults (uses PATH / default sessions root)
await using var client = new CodexClient();

// Configure session
var options = new CodexSessionOptions
{
    WorkingDirectory = @"C:\Projects\MyRepo", // required, must exist
    Prompt = "Generate integration tests for the Countries controller",
    Model = CodexModel.Parse("gpt-5.1-codex"),
    ReasoningEffort = CodexReasoningEffort.Medium
};

// Start session
await using var session = await client.StartSessionAsync(options, CancellationToken.None);

Console.WriteLine($"Session started: {session.Info.Id}");

// Stream events as they occur
await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    switch (evt)
    {
        case UserMessageEvent userMsg:
            Console.WriteLine($"USER: {userMsg.Text}");
            break;
            
        case AgentReasoningEvent reasoning:
            Console.WriteLine($"THINKING: {reasoning.Text}");
            break;
            
        case AgentMessageEvent agentMsg:
            Console.WriteLine($"AGENT: {agentMsg.Text}");
            break;
            
        case TokenCountEvent tokens:
            Console.WriteLine($"Tokens - In: {tokens.InputTokens}, Out: {tokens.OutputTokens}");
            break;
    }
}
```

### 2. Resume a Previous Session

```csharp
using System.Threading;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

await using var client = new CodexClient();

// Resume by session ID
SessionId sessionId = "019aa2a5-849a-7433-ab64-6b3f51c2fc43";
await using var session = await client.ResumeSessionAsync(sessionId, CancellationToken.None);

Console.WriteLine($"Resumed session from {session.Info.CreatedAt}");

// Replay all events
await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.Type}");
}
```

### 2b. Replay All Events from a Log File

```csharp
using System.Threading;
using Codex.DotNet.Public;

await using var client = new CodexClient();

// Attach directly to a log file (no Codex process launched)
var logPath = @"C:\Users\you\.codex\sessions\2025\11\20\rollout-2025-11-20T20-02-27-019aa2a5.jsonl";
await using var session = await client.AttachToLogAsync(logPath, CancellationToken.None);

// Replay everything from the beginning
var replayOptions = new EventStreamOptions(FromBeginning: true, Follow: false);
await foreach (var evt in session.GetEventsAsync(replayOptions, CancellationToken.None))
{
    Console.WriteLine($"[REPLAY] {evt.Type} at {evt.Timestamp}");
}
```

### 3. List Historical Sessions

```csharp
using System.Threading;
using Codex.DotNet.Public;

await using var client = new CodexClient();

// List all sessions
await foreach (var sessionInfo in client.ListSessionsAsync(filter: null, CancellationToken.None))
{
    Console.WriteLine($"{sessionInfo.Id} - {sessionInfo.CreatedAt} - {sessionInfo.WorkingDirectory}");
}

// List with filter
var filter = new SessionFilter(
    FromDate: DateTimeOffset.Now.AddDays(-7),
    WorkingDirectory: @"C:\Projects\MyRepo");

await foreach (var sessionInfo in client.ListSessionsAsync(filter, CancellationToken.None))
{
    Console.WriteLine($"Recent session: {sessionInfo.Id}");
}
```

---

## Configuration

### Global Client Configuration

```csharp
using Codex.DotNet.Public;

var clientOptions = new CodexClientOptions
{
    // Custom Codex executable location
    CodexExecutablePath = @"C:\Tools\codex.exe",

    // Custom sessions directory
    SessionsRootDirectory = @"D:\CodexLogs",

    // Timeouts
    StartTimeout = TimeSpan.FromSeconds(60),
    ProcessExitTimeout = TimeSpan.FromSeconds(10),

    // Polling interval for file tailing
    TailPollInterval = TimeSpan.FromMilliseconds(500)
};

await using var client = new CodexClient(clientOptions);
```

### Dependency Injection Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Codex.DotNet.Public;
using Codex.DotNet.Abstractions;

var services = new ServiceCollection();

services.AddCodexClient(options =>
{
    options.CodexExecutablePath = @"C:\Tools\codex.exe";
    options.TailPollInterval = TimeSpan.FromMilliseconds(200);
});

var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<ICodexClient>();
```

---

## Common Patterns

### Pattern 1: Start Session and Wait for Completion

```csharp
await using var session = await client.StartSessionAsync(options, CancellationToken.None);

// Stream events
await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    // Process events...
}

// Wait for Codex process to exit
if (session.IsLive)
{
    var exitCode = await session.WaitForExitAsync();
    Console.WriteLine($"Codex exited with code {exitCode}");
}
```

### Pattern 2: Start Reading from Specific Timestamp

```csharp
await using var session = await client.ResumeSessionAsync(sessionId, CancellationToken.None);

var streamOptions = EventStreamOptions.FromTimestamp(DateTimeOffset.Parse("2025-11-20T20:05:00Z"), follow: false);

await foreach (var evt in session.GetEventsAsync(streamOptions, CancellationToken.None))
{
    // Only events after 20:05:00...
}
```

### Pattern 3: Cancellation Support

```csharp
using var cts = new CancellationTokenSource();

// Cancel after 30 seconds
cts.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    await using var session = await client.StartSessionAsync(options, cts.Token);
    
    await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, cts.Token))
    {
        // Process until cancelled...
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Session cancelled");
}
```

### Pattern 4: Collect All Events into List

```csharp
await using var session = await client.ResumeSessionAsync(sessionId, CancellationToken.None);

var events = await session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None)
    .ToListAsync(); // Requires System.Linq.Async

Console.WriteLine($"Collected {events.Count} events");
```

### Pattern 5: Filter Events by Type

```csharp
using Codex.DotNet.Public.Models;

await using var session = await client.StartSessionAsync(options, CancellationToken.None);

// Only agent messages
await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    if (evt is AgentMessageEvent agentMsg)
    {
        Console.WriteLine(agentMsg.Text);
    }
}
```

---

## Error Handling

### Handling Missing Codex Executable

```csharp
try
{
    await using var client = new CodexClient();
    await using var session = await client.StartSessionAsync(options, CancellationToken.None);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Codex executable not found"))
{
    Console.WriteLine("Please install Codex CLI or specify CodexExecutablePath");
    Console.WriteLine($"Expected location: {ex.Message}");
}
```

### Handling Session Not Found

```csharp
try
{
    await using var session = await client.ResumeSessionAsync("invalid-id", CancellationToken.None);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Session") && ex.Message.Contains("not found"))
{
    Console.WriteLine($"Session does not exist: {ex.Message}");
}
```

### Handling Malformed Events

Event parser automatically skips malformed lines and logs warnings. No action needed in most cases:

```csharp
// The library handles malformed data gracefully
await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    // Only valid events are yielded
    // Malformed lines are logged and skipped
}
```

---

## Testing

### Unit Testing with Mocks

```csharp
using Codex.DotNet.Abstractions;
using Codex.DotNet.Public.Models;
using Moq;
using Xunit;

public class MyCodexServiceTests
{
    [Fact]
    public async Task Should_Process_Events()
    {
        // Arrange
        var mockClient = new Mock<ICodexClient>();
        var mockSession = new Mock<ICodexSessionHandle>();
        
        var testEvents = new[]
        {
            new UserMessageEvent { /* ... */ },
            new AgentMessageEvent { /* ... */ }
        };
        
        mockSession
            .Setup(s => s.GetEventsAsync(default, default))
            .Returns(testEvents.ToAsyncEnumerable());
        
        mockClient
            .Setup(c => c.StartSessionAsync(It.IsAny<CodexSessionOptions>(), default))
            .ReturnsAsync(mockSession.Object);
        
        var service = new MyCodexService(mockClient.Object);
        
        // Act
        var result = await service.GenerateTestsAsync("prompt");
        
        // Assert
        Assert.NotNull(result);
    }
}
```

---

## Performance Tips

1. **Adjust Polling Interval**: Lower `TailPollInterval` for lower latency, higher for reduced CPU usage
   ```csharp
   options.TailPollInterval = TimeSpan.FromMilliseconds(100); // Low latency
   options.TailPollInterval = TimeSpan.FromMilliseconds(1000); // Lower CPU
   ```

2. **Use Cancellation Tokens**: Always pass cancellation tokens for responsive cancellation
   ```csharp
   await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, ct))
   ```

3. **Filter Events Early**: Process only needed event types to reduce memory pressure
   ```csharp
   await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, ct))
   {
       if (evt is not AgentMessageEvent) continue;
       // Process only agent messages
   }
   ```

4. **Resume from Byte Offset**: For large logs, resume from known position
   ```csharp
   var options = new EventStreamOptions(FromByteOffset: lastProcessedOffset);
   ```

---

## Next Steps

- Read [data-model.md](./data-model.md) for detailed entity documentation
- Explore [contracts/](./contracts/) for event schemas
- See [plan.md](./plan.md) for architecture and design decisions
- Check [spec.md](./spec.md) for complete requirements

---

## Troubleshooting

### Issue: "Codex executable not found"
**Solution**: Ensure Codex CLI is installed and in PATH, or specify `CodexExecutablePath` in options.

### Issue: "Session not found"
**Solution**: Verify session ID is correct and session log exists in the sessions directory.

### Issue: "Session start timeout"
**Solution**: Increase `StartTimeout` in `CodexClientOptions` or check Codex CLI functionality.

### Issue: Events not streaming in real-time
**Solution**: Reduce `TailPollInterval` for more frequent polling. Ensure Codex process is still running.

### Issue: High CPU usage
**Solution**: Increase `TailPollInterval` to reduce polling frequency.

---

## Support

For issues, questions, or contributions, see the project repository.
