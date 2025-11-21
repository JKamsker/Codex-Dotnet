````markdown
# Implementation Plan: Codex.DotNet - CLI Wrapper Library

**Branch**: `001-codex-dotnet-wrapper` | **Date**: November 20, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-codex-dotnet-wrapper/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Create a .NET 10 library that wraps the Codex CLI and provides programmatic access to Codex sessions through structured event streaming from JSONL session logs. The library enables developers to start sessions, resume historical sessions, stream real-time events, and manage Codex processes without direct CLI interaction. Core capabilities include process management, JSONL file tailing, event parsing, session discovery, and cross-platform support.

`StartSessionAsync` yields a live session handle (`IsLive == true`); `ResumeSessionAsync` / `AttachToLogAsync` yield log-only handles (`IsLive == false`) that replay/tail existing JSONL without launching a Codex process.

## Technical Context

**Language/Version**: .NET 10 (target framework: net10.0, optionally multi-target net8.0/net9.0 for compatibility)  
**Primary Dependencies**: 
  - System.Text.Json (built-in) for JSON parsing
  - Microsoft.Extensions.Logging.Abstractions (optional) for logging integration
  - Microsoft.Extensions.DependencyInjection.Abstractions (optional) for DI support
  - Microsoft.Extensions.Options (optional) for options pattern support  
**Storage**: Read-only access to Codex CLI session logs at `%USERPROFILE%/.codex/sessions/YYYY/MM/DD/rollout-<timestamp>-<sessionId>.jsonl`  
**Testing**: xUnit or NUnit for unit and integration tests, with mock abstractions for file system and process operations  
**Target Platform**: Cross-platform (Windows, Linux, macOS) with .NET 10 runtime  
**Project Type**: Single class library project with test project  
**Performance Goals (targets on a reference dev machine with local SSD)**: 
  - First event within 5 seconds of session start
  - Event streaming latency under 500ms from log file write
  - Handle 100+ events/second without dropping events
  - Session resumption of 1000 events under 2 seconds  
**Constraints**: 
  - No external Codex CLI modification or bundling
  - Must support concurrent file access (reading while Codex writes)
  - Cross-platform process launching with OS-specific differences
  - Must be testable without running actual Codex processes  
**Scale/Scope**: 
  - Single library exposing 5-10 public types
  - 10-15 internal service implementations
  - Support for 6+ recognized event types with extensibility for unknown types
  - Configuration-driven with dependency injection support

**Terminology**: Use `WorkingDirectory` (a.k.a. repository root) consistently across the spec, plan, data model, and tasks; align API surface to the same term.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Status**: ✅ PASSED - Constitution file is placeholder template only, no specific principles to validate against.

Since the constitution.md file contains only template placeholders with no project-specific principles defined, there are no constitutional requirements to verify. The project proceeds with standard .NET library best practices:

- Single library project (not organizational-only)
- Independently testable via abstraction interfaces
- Clear purpose: Codex CLI wrapper with event streaming
- No unnecessary architectural complexity
- Standard dependency injection patterns
- Cross-platform compatibility via .NET abstractions

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Codex.DotNet/
│   ├── Codex.DotNet.csproj
│   ├── Public/
│   │   ├── CodexClient.cs
│   │   ├── CodexClientOptions.cs
│   │   ├── CodexSessionHandle.cs
│   │   ├── CodexSessionOptions.cs
│   │   ├── CodexSessionInfo.cs
│   │   ├── Models/
│   │   │   ├── CodexEvent.cs
│   │   │   ├── SessionMetaEvent.cs
│   │   │   ├── UserMessageEvent.cs
│   │   │   ├── AgentMessageEvent.cs
│   │   │   ├── AgentReasoningEvent.cs
│   │   │   ├── TokenCountEvent.cs
│   │   │   ├── TurnContextEvent.cs
│   │   │   ├── UnknownCodexEvent.cs
│   │   │   ├── SessionId.cs
│   │   │   ├── CodexModel.cs
│   │   │   ├── CodexReasoningEffort.cs
│   │   │   ├── SessionFilter.cs
│   │   │   └── EventStreamOptions.cs
│   ├── Infrastructure/
│   │   ├── CodexProcessLauncher.cs
│   │   ├── CodexSessionLocator.cs
│   │   ├── JsonlTailer.cs
│   │   ├── JsonlEventParser.cs
│   │   ├── DefaultCodexPathProvider.cs
│   │   └── RealFileSystem.cs
│   └── Abstractions/
│       ├── ICodexClient.cs
│       ├── ICodexProcessLauncher.cs
│       ├── ICodexSessionLocator.cs
│       ├── IJsonlTailer.cs
│       ├── IJsonlEventParser.cs
│       ├── ICodexPathProvider.cs
│       ├── IFileSystem.cs
│       └── ILoggerAdapter.cs (optional)

tests/
├── Codex.DotNet.Tests/
│   ├── Codex.DotNet.Tests.csproj
│   ├── Unit/
│   │   ├── JsonlEventParserTests.cs
│   │   ├── JsonlTailerTests.cs
│   │   ├── CodexSessionLocatorTests.cs
│   │   ├── DefaultCodexPathProviderTests.cs
│   │   └── ProcessStartInfoBuilderTests.cs
│   ├── Integration/
│   │   ├── CodexClientStartSessionTests.cs
│   │   ├── CodexClientResumeSessionTests.cs
│   │   ├── CodexClientListSessionsTests.cs
│   │   └── EventStreamingTests.cs
│   ├── TestHelpers/
│   │   ├── InMemoryFileSystem.cs
│   │   ├── MockCodexProcessLauncher.cs
│   │   ├── TestJsonlGenerator.cs
│   │   └── SampleEventFactory.cs
│   └── Fixtures/
│       └── sample-session-logs/
│           ├── rollout-2025-11-20-session1.jsonl
│           └── rollout-2025-11-20-session2.jsonl
```

**Structure Decision**: Single library project structure chosen because:
- This is a focused CLI wrapper library with clear boundaries
- No frontend/backend split needed
- No separate API or mobile components
- All functionality cohesively grouped in one library
- Test project mirrors source structure with Unit/Integration separation
- Public API in `Public/` folder, implementation in `Infrastructure/`, contracts in `Abstractions/`

## Complexity Tracking

> **No constitutional violations identified - this section is empty**

The project follows straightforward .NET library patterns with no architectural complexity requiring justification. The design uses standard dependency injection, abstraction interfaces for testability, and well-established async streaming patterns (IAsyncEnumerable<T>).

---

## Phase 0: Research & Discovery

### Research Topics

Based on Technical Context unknowns and implementation requirements, the following research is needed:

#### 1. JSONL File Tailing Patterns in .NET
**Question**: What is the best approach for tailing a file that is actively being written to by another process?
**Why needed**: Core requirement FR-006 for real-time event streaming from active sessions
**Research areas**:
- FileStream with FileShare.ReadWrite behavior and limitations
- Polling intervals vs. FileSystemWatcher for append detection
- StreamReader buffer management when EOF reached
- Handling file growth detection and seeking
- Cross-platform file locking differences (Windows vs. Unix)

#### 2. Cross-Platform Process Management
**Question**: How to reliably launch and manage external processes across Windows, Linux, and macOS?
**Why needed**: FR-001 requires cross-platform Codex CLI execution
**Research areas**:
- ProcessStartInfo configuration for stdin/stdout/stderr redirection
- Platform-specific executable resolution (codex vs. codex.cmd on Windows)
- Process termination strategies (graceful vs. forced)
- Detecting process exit and capturing exit codes
- Environment variable handling across platforms

#### 3. Async Stream Best Practices
**Question**: What are the patterns for implementing robust IAsyncEnumerable<T> with cancellation?
**Why needed**: FR-004 requires streaming events as IAsyncEnumerable<T>
**Research areas**:
- Proper cancellation token propagation
- Exception handling in async iterators
- Resource cleanup in async enumerable implementations
- Backpressure handling when consumer is slow
- Testing strategies for async enumerables

#### 4. Session Directory Monitoring
**Question**: How to efficiently detect new files in a directory without polling everything?
**Why needed**: FR-003 requires locating new session files after Codex starts
**Research areas**:
- FileSystemWatcher reliability and limitations
- Efficient polling strategies as fallback
- Filename pattern matching with wildcards
- Handling race conditions when files are created
- Cross-platform directory watching differences

#### 5. JSON Parsing Error Recovery
**Question**: How to handle malformed JSON lines while continuing to parse subsequent valid lines?
**Why needed**: FR-020 requires resilient parsing that skips malformed entries
**Research areas**:
- JsonDocument vs. JsonSerializer for line-by-line parsing
- JsonException handling and recovery
- Partial line buffering when reading incrementally
- Performance of per-line parsing vs. batch parsing
- Validation strategies for event schema

#### 6. Existing Codex CLI Behavior
**Question**: What are the exact CLI arguments, output format, and session file patterns used by Codex?
**Why needed**: FR-001, FR-002, FR-011 require correct CLI invocation and session location
**Research areas**:
- Codex `exec` command arguments and flags
- Session log file naming convention and directory structure
- session_meta event schema and timing
- Resume command syntax and behavior
- Model names and reasoning effort configuration syntax

### Research Outputs

Research findings documented in [research.md](./research.md) with decisions on:
1. ✅ JSONL File Tailing: FileStream with FileShare.ReadWrite + polling
2. ✅ Cross-Platform Process Management: ProcessStartInfo with RuntimeInformation
3. ✅ Async Stream Best Practices: IAsyncEnumerable<T> with [EnumeratorCancellation]
4. ✅ Session Directory Monitoring: Snapshot + timestamp-filtered polling
5. ✅ JSON Parsing Error Recovery: Per-line JsonDocument.Parse with try-catch
6. ✅ Codex CLI Behavior: Command syntax, file patterns, event schemas

All NEEDS CLARIFICATION items resolved. Ready for Phase 1.

---

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](./data-model.md) for complete entity definitions including:
- **SessionId**: Value object for session identifiers with implicit conversions
- **CodexSessionInfo**: Session metadata record
- **CodexSessionOptions**: Configuration for starting sessions
- **CodexClientOptions**: Global client configuration
- **CodexModel**: Value object for Codex model identifiers with extensibility
- **CodexReasoningEffort**: Value object for reasoning effort levels with extensibility
- **CodexEvent**: Base event type with derived event classes
- **EventStreamOptions**: Stream reading configuration
- **SessionFilter**: Session list filtering criteria

### API Contracts

See [contracts/](./contracts/) directory for:
- Event schemas (JSON structure for each event type)
- Public API interface definitions
- Configuration object schemas

### Quick Start Guide

See [quickstart.md](./quickstart.md) for:
- Installation instructions
- Basic usage examples (start, resume, stream events)
- Configuration guide
- Common patterns and recipes

### Constitution Re-check

**Status**: ✅ PASSED - No changes to constitutional compliance

Phase 1 design maintains:
- Single focused library with clear responsibility
- All services exposed via abstraction interfaces
- Testable without external dependencies
- No unnecessary complexity introduced
- Standard .NET patterns throughout

---

## Phase 2: Task Breakdown

Phase 2 (task breakdown) is handled by the `/speckit.tasks` command and will generate `tasks.md`.

This plan document ends at Phase 1 completion as per the mode instructions.

---

## Appendix: Key Design Decisions

### 1. Event Streaming Architecture

**Pipeline**: 
```
CodexClient → Process Launch → Session Locator → JSONL File
                                                      ↓
User Code ← CodexEvent ← JsonlEventParser ← JsonlTailer
```

**Rationale**: Clean separation of concerns with each component testable independently

### 2. Abstraction Strategy

All external interactions abstracted behind interfaces:
- `IFileSystem` for file operations (testable with in-memory implementation)
- `ICodexProcessLauncher` for process management (mockable)
- `ICodexSessionLocator` for file discovery (deterministic in tests)
- `IJsonlTailer` for file reading (controllable timing in tests)

**Rationale**: Enables unit testing without running Codex CLI or touching real file system

### 3. Configuration Pattern

Using Options pattern with three configuration levels:
1. **CodexClientOptions**: Global settings (executable path, sessions directory, timeouts)
2. **CodexSessionOptions**: Per-session settings (repo, prompt, model, reasoning effort)
3. **EventStreamOptions**: Per-stream settings (start position, filters)

**Rationale**: Standard .NET configuration approach, integrates with DI containers

### 4. Error Handling Strategy

- **Validation errors**: Thrown immediately with clear messages (e.g., executable not found)
- **Runtime errors**: Propagated through async streams, include context
- **Malformed data**: Logged and skipped, don't halt processing
- **Resource cleanup**: Always executed via try-finally or IAsyncDisposable
- **Process exit fallback**: If the Codex process does not exit within `ProcessExitTimeout`, the library attempts a forced kill and logs a warning to avoid orphaned processes

**Rationale**: Fail fast on configuration issues, resilient to runtime data issues

### 5. Cross-Platform Compatibility

- Process launching: Platform detection for executable name
- File paths: Use Path.Combine and Path.DirectorySeparatorChar
- Environment variables: Use Environment.GetFolderPath for user home
- File locking: FileShare.ReadWrite works across platforms

**Rationale**: Minimize platform-specific code, leverage .NET abstractions

---

## Implementation Readiness

✅ **Research Complete**: All technical unknowns resolved  
✅ **Design Complete**: Data model, contracts, and architecture defined  
✅ **Documentation Complete**: Quickstart guide created  
✅ **Constitution Compliant**: No violations identified  
✅ **Ready for Tasks**: Phase 2 task breakdown can proceed

**Next Step**: Run `/speckit.tasks` to generate detailed implementation task breakdown.
````
