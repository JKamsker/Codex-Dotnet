# Data Model: Codex.DotNet

**Feature**: Codex.DotNet - CLI Wrapper Library  
**Date**: November 20, 2025  
**Phase**: Phase 1 - Design & Contracts

---

## Overview

This document defines the core data structures and entities used in the Codex.DotNet library. All entities are designed as immutable records where possible to ensure thread-safety and predictable behavior.

---

## Public Models

### SessionId (Value Object)

Represents a unique Codex session identifier with validation.

**Properties**:
- `Value` (string, required): The session identifier string (opaque; typically UUID today but not guaranteed)

**Static Members**:
- `Parse(string value)`: Creates a SessionId from a string
- `TryParse(string value, out SessionId sessionId)`: Safe parsing with validation
- `implicit operator SessionId(string value)`: Allows implicit conversion from string
- `implicit operator string(SessionId sessionId)`: Allows implicit conversion to string

**Validation**:
- Cannot be empty or whitespace
- SHOULD be a UUID if produced by current Codex CLI, but the type must accept any non-whitespace string to remain compatible with future formats

**Usage**:
```csharp
// Implicit conversion from string
SessionId sessionId = "019aa2a5-849a-7433-ab64-6b3f51c2fc43";

// Explicit parsing (UUIDs are accepted but not required)
var sessionId = SessionId.Parse("019aa2a5-849a-7433-ab64-6b3f51c2fc43");

// Safe parsing
if (SessionId.TryParse(input, out var sessionId))
{
    // Use sessionId
}

// Implicit conversion to string
string idString = sessionId;
Console.WriteLine($"Session: {sessionId}"); // ToString() automatically called
```

**Design Rationale**: Value object provides type safety, preventing accidental string mixing, while keeping validation flexible for future ID shapes. Implicit conversions maintain ergonomic API usage.

---

### CodexSessionInfo

Represents metadata about a Codex session.

**Properties**:
- `Id` (SessionId, required): Unique session identifier
- `LogPath` (string, required): Absolute path to the JSONL session log file
- `CreatedAt` (DateTimeOffset, required): Session creation timestamp
- `WorkingDirectory` (string, nullable): Working directory path for the session
- `Model` (CodexModel, nullable): Codex model identifier
- `HumanLabel` (string, nullable): Optional human-readable session description

**Validation Rules**:
- `Id` must be a valid SessionId
- `LogPath` must be a valid absolute file path
- `CreatedAt` must not be default value

**Relationships**:
- Referenced by `CodexSessionHandle` to provide session metadata
- Created by `CodexSessionLocator` when discovering sessions

**Usage**:
```csharp
var info = new CodexSessionInfo(
    Id: "019aa2a5-849a-7433-ab64-6b3f51c2fc43", // Implicit conversion from string
    LogPath: @"C:\Users\...\sessions\2025\11\20\rollout-2025-11-20T20-02-27-019aa2a5.jsonl",
    CreatedAt: DateTimeOffset.Parse("2025-11-20T20:02:27Z"),
    WorkingDirectory: @"C:\Projects\MyRepo",
    Model: CodexModel.Parse("gpt-5.1-codex"),
    HumanLabel: "Generate test scenarios"
);
```

---

### CodexSessionOptions

Configuration for starting a new Codex session.

**Properties**:
- `WorkingDirectory` (string, required): Working directory for the session context
- `Prompt` (string, required): User prompt to send to Codex
- `Model` (CodexModel, optional): Model identifier (default: CodexModel.Default)
- `ReasoningEffort` (CodexReasoningEffort, optional): Reasoning effort level (default: CodexReasoningEffort.Medium)
- `AdditionalOptions` (IReadOnlyList<string>, optional): Additional CLI flags
- `CodexBinaryPath` (string, nullable): Override Codex executable path

**Validation Rules**:
- `WorkingDirectory` must exist as a directory
- `Prompt` must not be empty or whitespace
- `Model` must be a valid CodexModel instance if provided
- `CodexBinaryPath` must exist as a file if provided

**Default Values**:
- `Model`: CodexModel.Default ("gpt-5.1-codex")
- `ReasoningEffort`: CodexReasoningEffort.Medium
- `AdditionalOptions`: Empty list

**Usage**:
```csharp
var options = new CodexSessionOptions
{
    WorkingDirectory = @"C:\Projects\MyRepo",
    Prompt = "Generate integration tests for the API controller",
    Model = CodexModel.Parse("gpt-5.1-codex"),
    ReasoningEffort = CodexReasoningEffort.High,
    AdditionalOptions = new[] { "--no-tui" }
};
```

---

### CodexClientOptions

Global configuration for the Codex client.

**Properties**:
- `CodexExecutablePath` (string, nullable): Path to codex executable
- `SessionsRootDirectory` (string, nullable): Root directory for session logs
- `StartTimeout` (TimeSpan, optional): Timeout for session start (default: 30s)
- `ProcessExitTimeout` (TimeSpan, optional): Timeout for process termination (default: 5s)
- `TailPollInterval` (TimeSpan, optional): Interval for log file polling (default: 200ms)

**Default Values**:
- `CodexExecutablePath`: Auto-detected ("codex" on Unix, "codex.cmd" on Windows)
- `SessionsRootDirectory`: `%USERPROFILE%/.codex/sessions`
- `StartTimeout`: 30 seconds
- `ProcessExitTimeout`: 5 seconds
- `TailPollInterval`: 200 milliseconds

**Validation Rules**:
- All TimeSpan values must be positive
- `TailPollInterval` should be >= 50ms for performance
- `CodexExecutablePath` must be executable if specified

**Usage**:
```csharp
var options = new CodexClientOptions
{
    CodexExecutablePath = @"C:\Tools\codex.exe",
    SessionsRootDirectory = @"D:\CodexLogs",
    StartTimeout = TimeSpan.FromSeconds(60),
    TailPollInterval = TimeSpan.FromMilliseconds(500)
};
```

---

### CodexModel (Value Object)

Represents a Codex model identifier with parsing and validation.

**Properties**:
- `Value` (string, required): The model identifier string

**Static Members**:
- `Default`: Returns CodexModel for "gpt-5.1-codex"
- `Parse(string value)`: Creates a CodexModel from a string
- `TryParse(string value, out CodexModel model)`: Safe parsing

**Predefined Models** (for convenience, not exhaustive):
- `CodexModel.Gpt51Codex`: "gpt-5.1-codex"
- `CodexModel.Gpt51CodexMini`: "gpt-5.1-codex-mini"

**Usage**:
```csharp
// Using predefined
var model = CodexModel.Gpt51Codex;

// Parsing custom model (future-proof)
var model = CodexModel.Parse("gpt-6.0-codex");

// Safe parsing
if (CodexModel.TryParse("custom-model", out var model))
{
    // Use model
}
```

**Design Rationale**: Value object pattern allows users to specify any model string without requiring library updates when new models are released.

---

### CodexReasoningEffort (Value Object)

Represents Codex reasoning effort level with extensibility for custom values.

**Properties**:
- `Value` (string, required): The effort level identifier

**Static Members**:
- `Low`: Minimal reasoning, faster responses ("low")
- `Medium`: Balanced reasoning, default ("medium")
- `High`: Maximum reasoning, thorough analysis ("high")
- `Parse(string value)`: Creates from string
- `TryParse(string value, out CodexReasoningEffort effort)`: Safe parsing

**Mapping to CLI**:
- Generates `--config model_reasoning_effort={Value}`

**Usage**:
```csharp
// Using predefined
var effort = CodexReasoningEffort.High;

// Custom effort level (if Codex adds new levels)
var effort = CodexReasoningEffort.Parse("extreme");
```

**Design Rationale**: Value object pattern allows extensibility without library updates when Codex introduces new reasoning effort levels.

---

### EventStreamOptions

Configuration for streaming events from a session.

**Properties**:
- `FromBeginning` (bool, optional): Start from beginning of log (default: true)
- `AfterTimestamp` (DateTimeOffset, nullable): Start after specific timestamp
- `FromByteOffset` (long, nullable): Start at specific byte position

**Validation Rules**:
- If `FromBeginning` is false, either `AfterTimestamp` or `FromByteOffset` should be provided
- `FromByteOffset` must be non-negative if specified

**Usage**:
```csharp
// Read all events from beginning
var options1 = new EventStreamOptions { FromBeginning = true };

// Read events after specific time
var options2 = new EventStreamOptions 
{ 
    FromBeginning = false,
    AfterTimestamp = DateTimeOffset.Parse("2025-11-20T20:05:00Z")
};

// Resume from byte position
var options3 = new EventStreamOptions 
{ 
    FromBeginning = false,
    FromByteOffset = 1024
};
```

---

### SessionFilter

Criteria for filtering session lists.

**Properties**:
- `FromDate` (DateTimeOffset, nullable): Include sessions after this date
- `ToDate` (DateTimeOffset, nullable): Include sessions before this date
- `WorkingDirectory` (string, nullable): Filter by working directory path
- `Model` (CodexModel, nullable): Filter by model identifier
- `SessionIdPattern` (string, nullable): Wildcard pattern for session ID matching

**Validation Rules**:
- If both `FromDate` and `ToDate` are specified, `FromDate` must be before `ToDate`

**Usage**:
```csharp
var filter = new SessionFilter
{
    FromDate = DateTimeOffset.Now.AddDays(-7),
    ToDate = DateTimeOffset.Now,
    WorkingDirectory = @"C:\Projects\MyRepo"
};
```

---

## Event Models

### CodexEvent (Abstract Base)

Base type for all Codex events.

**Properties**:
- `Timestamp` (DateTimeOffset, required): Event timestamp
- `Type` (string, required): Event type identifier
- `RawPayload` (JsonElement, required): Complete event data

**Usage**: Never instantiated directly; use derived types.

---

### SessionMetaEvent

Session initialization event (first event in every session).

**Inherits**: CodexEvent

**Additional Properties**:
- `SessionId` (SessionId, required): Unique session identifier
- `Cwd` (string, nullable): Working directory

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:27.123Z",
  "type": "session_meta",
  "payload": {
    "id": "019aa2a5-849a-7433-ab64-6b3f51c2fc43",
    "cwd": "/home/user/projects/myrepo"
  }
}
```

---

### UserMessageEvent

User input submitted to Codex.

**Inherits**: CodexEvent

**Additional Properties**:
- `Text` (string, required): User message content

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:28.456Z",
  "type": "user_message",
  "payload": {
    "message": "Generate integration tests for the Countries controller"
  }
}
```

---

### AgentMessageEvent

Codex response (generated text, code, or explanation).

**Inherits**: CodexEvent

**Additional Properties**:
- `Text` (string, required): Agent response content

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:35.789Z",
  "type": "agent_message",
  "payload": {
    "message": "I'll create integration tests for the Countries controller..."
  }
}
```

---

### AgentReasoningEvent

Codex internal reasoning/thinking process.

**Inherits**: CodexEvent

**Additional Properties**:
- `Text` (string, required): Reasoning text content

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:30.123Z",
  "type": "agent_reasoning",
  "payload": {
    "text": "First, I need to understand the structure of the Countries controller..."
  }
}
```

---

### TokenCountEvent

Token usage statistics for a turn.

**Inherits**: CodexEvent

**Additional Properties**:
- `InputTokens` (int, nullable): Input token count
- `OutputTokens` (int, nullable): Output token count
- `ReasoningTokens` (int, nullable): Reasoning token count

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:40.456Z",
  "type": "token_count",
  "payload": {
    "input_tokens": 1234,
    "output_tokens": 567,
    "reasoning_output_tokens": 890
  }
}
```

---

### TurnContextEvent

Turn initialization with execution environment settings.

**Inherits**: CodexEvent

**Additional Properties**:
- `ApprovalPolicy` (string, nullable): Approval policy type
- `SandboxPolicyType` (string, nullable): Sandbox execution policy

**Source JSONL**:
```json
{
  "timestamp": "2025-11-20T20:02:29.789Z",
  "type": "turn_context",
  "payload": {
    "approval_policy": "auto",
    "sandbox_policy_type": "isolated"
  }
}
```

---

### UnknownCodexEvent

Fallback for unrecognized event types.

**Inherits**: CodexEvent

**Additional Properties**: None (uses `RawPayload` from base)

**Usage**: Ensures forward compatibility when Codex adds new event types. Consumers can access `RawPayload` to inspect unknown events.

---

## State Transitions

### Session Lifecycle

```
[Not Started]
     ↓ StartSessionAsync()
[Starting] ← Process launching, waiting for session_meta
     ↓ session_meta event received
[Active] ← Process running, events streaming
     ↓ Process exits OR DisposeAsync()
[Completed/Disposed]
```

### Resumed Session States

```
[Not Loaded]
     ↓ ResumeSessionAsync(sessionId)
[Loading] ← Locating log file
     ↓ Log file found
[Replaying] ← Streaming historical events
     ↓ All events streamed
[Completed]
```

---

## Entity Relationships

```
CodexClient
    ├── manages: CodexSessionHandle (1..*)
    └── uses: CodexClientOptions (1)

CodexSessionHandle
    ├── has: CodexSessionInfo (1)
    ├── produces: CodexEvent stream (0..*)
    └── configures via: EventStreamOptions (0..*)

CodexEvent (abstract)
    ├── SessionMetaEvent
    ├── UserMessageEvent
    ├── AgentMessageEvent
    ├── AgentReasoningEvent
    ├── TokenCountEvent
    ├── TurnContextEvent
    └── UnknownCodexEvent
```

---

## Implementation Notes

1. **Immutability**: All models use `record` types with `init`-only properties for thread-safety
2. **Validation**: Validation occurs at construction time or via explicit Validate() methods
3. **Serialization**: Models designed to work with System.Text.Json for configuration files
4. **Extensibility**: Unknown event types preserved via `RawPayload` JsonElement
5. **Testing**: All models include factory methods in test helpers for easy test data generation
6. **Schema tolerance**: JSON deserialization and validation must allow extra/unknown fields beyond the documented schemas (FR-008, FR-023) to stay forward-compatible with Codex additions

---

## Next Steps

With the data model defined, proceed to:
1. Create event schema contracts in `contracts/` directory
2. Define public API interfaces
3. Write quickstart guide with usage examples
