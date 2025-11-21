# Feature Specification: Codex.DotNet - CLI Wrapper Library

**Feature Branch**: `001-codex-dotnet-wrapper`  
**Created**: November 20, 2025  
**Status**: Draft  
**Input**: User description: "Create a .NET 10 library that wraps the Codex CLI and streams structured events from JSONL session logs"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start New Codex Session and Stream Events (Priority: P1)

A developer wants to programmatically start a new Codex session from their .NET application and receive real-time structured events as Codex processes their prompt.

**Why this priority**: This is the core value proposition - enabling programmatic access to Codex sessions with structured event streaming. Without this, the library has no primary use case.

**Independent Test**: Can be fully tested by starting a session with a simple prompt and verifying that at least one structured event (session metadata, user message, agent message) is received through the async stream.

**Acceptance Scenarios**:

1. **Given** a valid working directory (repository root) and prompt, **When** the user starts a new session, **Then** a session identifier is returned for tracking
2. **Given** a started session, **When** the user observes the session activity, **Then** structured events (session metadata, user messages, agent responses) are delivered as they occur
3. **Given** a prompt requesting code generation, **When** Codex processes the request, **Then** the activity stream includes reasoning steps and generated content
4. **Given** a completed session, **When** the user reviews all activity, **Then** token usage information is included showing input and output token counts

---

### User Story 2 - Resume Existing Session from Log (Priority: P2)

A developer wants to replay or continue analyzing a previous Codex session by loading its JSONL log file without restarting Codex.

**Why this priority**: Essential for debugging, auditing, and building tools that analyze Codex interactions. Enables offline analysis and replay capabilities.

**Independent Test**: Can be tested by providing a valid session ID or log file path and verifying that all historical events can be enumerated from the beginning of the session.

**Acceptance Scenarios**:

1. **Given** a valid session ID from a previous session, **When** the user resumes that session, **Then** access to all historical activity is provided
2. **Given** a resumed session, **When** the user requests activity from the beginning, **Then** all events from the original session are replayed in chronological order
3. **Given** a direct session log file path, **When** the user opens that file, **Then** the session can be accessed without needing the session ID
4. **Given** a non-existent session ID, **When** the user attempts to resume, **Then** a clear error message indicates the session was not found

---

### User Story 3 - List and Filter Historical Sessions (Priority: P3)

A developer wants to discover and enumerate past Codex sessions, filtering by date range, working directory (repository root), or other metadata.

**Why this priority**: Supports building management and auditing tools. Less critical than core session interaction but valuable for production tooling.

**Independent Test**: Can be tested by creating multiple sessions and verifying that session listing returns accurate metadata for each session with optional filtering applied.

**Acceptance Scenarios**:

1. **Given** multiple historical sessions exist, **When** the user requests a session list, **Then** session information is returned with accurate metadata (ID, timestamp, working directory)
2. **Given** a date range filter, **When** the user applies that filter to the session list, **Then** only sessions within the date range are returned
3. **Given** a working directory filter, **When** the user applies the filter, **Then** only sessions from that working directory are returned
4. **Given** no sessions exist, **When** the user requests a session list, **Then** an empty list is returned without errors

---

### User Story 4 - Configure Codex Execution Options (Priority: P2)

A developer wants to customize Codex execution parameters such as model selection, reasoning effort level, and additional CLI options.

**Why this priority**: Required for real-world usage where different scenarios need different configurations (e.g., cheap models for testing, high reasoning effort for complex tasks).

**Independent Test**: Can be tested by starting sessions with different configuration options and verifying they are passed correctly to the Codex CLI (via process arguments or resulting behavior).

**Acceptance Scenarios**:

1. **Given** configuration specifying Model="gpt-5.1-codex-mini", **When** the session starts, **Then** the Codex process is invoked with the correct model parameter
2. **Given** ReasoningEffort configured as high, **When** the session starts, **Then** the config parameter model_reasoning_effort=high is passed to Codex
3. **Given** AdditionalOptions containing ["--no-tui"], **When** the session starts, **Then** the option is included in the CLI invocation
4. **Given** a custom CodexBinaryPath in options, **When** the session starts, **Then** that specific executable is used instead of the default

---

### User Story 5 - Handle Cancellation and Cleanup (Priority: P2)

A developer wants to cancel a running Codex session gracefully and ensure all resources (processes, file handles) are properly cleaned up.

**Why this priority**: Critical for production reliability and resource management. Prevents resource leaks and enables responsive applications.

**Independent Test**: Can be tested by starting a session, triggering cancellation mid-execution, and verifying the process terminates and file handles are released.

**Acceptance Scenarios**:

1. **Given** a running session, **When** the user cancels the operation, **Then** the activity stream stops delivering new events
2. **Given** a cancelled session, **When** the user closes the session, **Then** the Codex process is terminated gracefully (if running)
3. **Given** a closed session, **When** the user attempts to access session activity, **Then** an appropriate error is raised indicating the session is no longer available
4. **Given** a long-running session with file I/O, **When** cleanup occurs, **Then** no file handles remain locked

---


### Edge Cases

- What happens when the Codex CLI executable is not found or not accessible?
  - Validation occurs before process launch; clear error message provided including expected location
- What happens when Codex process exits unexpectedly during session start?
  - Captured stderr/stdout is analyzed; detailed exception thrown with process output for debugging
- What happens when the session log file contains malformed data?
  - Parser logs the error and skips the malformed content, continuing with valid data
- What happens when multiple sessions start simultaneously and create log files at nearly the same time?
  - Session locator uses timestamp and unique session ID in filename to differentiate; each session correctly identifies its own log file
- What happens when a user tries to resume a session that is currently being written to by an active Codex process?
  - File is opened for concurrent read access, allowing monitoring of new content as it's written
- What happens when the sessions directory doesn't exist?
  - Appropriate exception thrown during session location with guidance on verifying Codex installation
- What happens when a session log file is deleted while being tailed?
  - FileStream detects the deletion; exception propagated to caller with context about the missing file
- What happens when reading from a session that never received a session_meta event?
  - Timeout occurs during session start; descriptive exception thrown indicating incomplete session initialization


## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST start new Codex CLI sessions programmatically by executing the codex binary with appropriate arguments
- **FR-002**: System MUST capture the session identifier from Codex output by reading the session_meta event from the JSONL log file and representing it as a validated SessionId value object (opaque string; default validation must allow non-empty strings and SHOULD accept UUIDs without requiring them)
- **FR-003**: System MUST locate the correct JSONL session log file by monitoring the configured sessions directory for new files created after session start
- **FR-004**: System MUST stream structured events from JSONL log files as a continuous sequence of strongly-typed event objects
- **FR-005**: System MUST parse JSONL (newline-delimited JSON) format where each line is a complete JSON object representing one event
- **FR-006**: System MUST tail active JSONL files by polling for new content when EOF is reached, allowing real-time event streaming from running sessions
- **FR-007**: System MUST map the core event types documented in `contracts/` (currently session_meta, user_message, token_count) to strongly-typed classes; additional known event types (agent_message, agent_reasoning, turn_context) SHOULD be mapped when corresponding schemas are available
- **FR-008**: System MUST preserve unknown event types with their complete data accessible for future compatibility
- **FR-009**: System MUST resume existing sessions by SessionId value object by locating the corresponding log file and streaming its events
- **FR-010**: System MUST attach to sessions by direct log file path, enabling analysis of archived or copied log files
- **FR-011**: System MUST support configuring Codex execution options including model identifier, reasoning effort level, working directory path, and additional CLI arguments
- **FR-012**: System MUST pass user prompts to Codex CLI through the appropriate input channel in interactive mode
- **FR-013**: System MUST handle cancellation tokens throughout all async operations, stopping event streams and process execution when cancelled
- **FR-014**: System MUST clean up resources (processes, file handles) when a session is closed
- **FR-015**: System MUST validate Codex executable existence and accessibility before attempting to launch processes
- **FR-016**: System MUST provide session metadata including SessionId value object, log file path, creation timestamp, working directory, and model identifier
- **FR-017**: System MUST list historical sessions from the sessions directory with optional filtering by date range, working directory, or model
- **FR-018**: System MUST be cross-platform supporting Windows, Linux, and macOS with appropriate process launching for each OS
- **FR-019**: System MUST allow dependency injection of all core services for testability and extensibility
- **FR-020**: System MUST handle malformed log entries by logging and skipping them without halting the event stream
- **FR-021**: System MUST support concurrent read access to log files while Codex is actively writing to them
- **FR-022**: System MUST provide configurable timeouts for session start, process exit, and file polling intervals
- **FR-023**: System MUST expose complete event data on all event types for accessing fields not mapped to standard properties
- **FR-024**: System MUST support EventStreamOptions allowing users to start reading from beginning, specific timestamp, or byte offset
- **FR-025**: System MUST track live vs resumed sessions, indicating whether an active Codex process is associated with the session handle

### Key Entities

- **Session Information**: Represents session metadata including unique SessionId value object, log file path, creation timestamp, working directory path, model identifier, and optional human-readable label
- **Session Handle**: Represents an active or resumed session with capabilities for observing activity, checking status, providing input, and releasing resources
- **Activity Event**: Base concept for all event types containing timestamp, event type indicator, and full event data
- **Session Initialization Event**: Session startup event containing session ID, working directory, and configuration details
- **User Input Event**: User prompt or message submitted to Codex
- **Agent Response Event**: Codex-generated text, code, or explanations
- **Agent Reasoning Event**: Codex internal thinking process and decision-making steps
- **Token Usage Event**: Token consumption metrics including input tokens, output tokens, and reasoning tokens
- **Turn Context Event**: Turn initialization information including approval policy and execution environment settings
- **Session Configuration**: Settings for starting new sessions including working directory path, prompt, model identifier, reasoning effort, and additional execution options
- **Client Configuration**: Global settings including Codex executable location, sessions directory location, and timeout values
- **Session Filter**: Criteria for filtering session lists by date range, working directory (repository root), model, or other metadata

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developers can start a new Codex session and receive the first structured event within 5 seconds
- **SC-002**: Event streams deliver events with latency under 500ms from when they are written to the JSONL log file
- **SC-003**: Target: System handles at least 100 events per second when tailing an active session without dropping events on a reference developer machine (local SSD, not network storage)
- **SC-004**: Target: Session resumption from historical logs completes enumeration of 1000 events in under 2 seconds on a reference developer machine
- **SC-005**: Library successfully operates on Windows, Linux, and macOS without platform-specific code in user-facing APIs
- **SC-006**: 100% of recognized event types are mapped to strongly-typed classes with accessible properties
- **SC-007**: Malformed log entries (up to 10% of total entries) do not prevent processing of valid events
- **SC-008**: Resource cleanup completes within 2 seconds of closing a session, with no leaked processes or file handles
- **SC-009**: Session locator finds new session log files within 1 second of file creation in the sessions directory
- **SC-010**: Developers can write unit tests for Codex integration logic without executing real Codex processes by using injected test implementations

## Assumptions

- The Codex CLI is already installed and accessible on the user's system
- The Codex CLI outputs session logs in JSONL format to `%USERPROFILE%/.codex/sessions/YYYY/MM/DD/rollout-<timestamp>-<sessionId>.jsonl`
- The JSONL format follows JSON Lines specification where each line is a complete valid JSON object
- Session log files use UTF-8 encoding
- The `session_meta` event is always the first event in a session log file and contains the session ID
- Session identifiers are treated as opaque strings; current Codex CLI emits UUID-like values but future formats may differ
- Event types and payload schemas are consistent with the examples provided in the reference logs
- Users have read/write permissions to the Codex sessions directory
- .NET 10 (or compatible runtime) is available on the target system
- Codex CLI supports the `exec` command with `--cd`, `--model`, `--config`, and `resume` options as documented
- File system supports concurrent read/write operations (FileShare.ReadWrite)

## Dependencies

- .NET 10 SDK for development and target framework
- System.Text.Json for JSON parsing (built-in to .NET)
- Codex CLI must be installed externally (not bundled with library)
- Optional: Microsoft.Extensions.Logging.Abstractions for logging integration
- Optional: Microsoft.Extensions.DependencyInjection.Abstractions for DI support
- Optional: Microsoft.Extensions.Options for options pattern support

## Out of Scope

- This library does NOT provide a REST client or direct API access to Codex services
- This library does NOT include GUI or visualization components
- This library does NOT control or modify Codex CLI internal behavior or schemas
- This library does NOT manage Codex CLI installation, updates, or authentication
- This library does NOT provide conversation management features beyond session start/resume
- Resuming a session in v1 means replaying/tailing an existing JSONL log; the library does not spawn a new `codex exec resume` process when resuming.
- This library does NOT implement retry logic or error recovery for failed Codex executions (users handle this at application level)
- Advanced event filtering or transformation beyond basic type mapping is not included
- This library does NOT persist or cache session data beyond what Codex CLI already writes

## Notes

This specification is derived from a detailed technical plan that includes architecture decisions, implementation guidance, and migration strategies from existing tools. The library design follows modern patterns for asynchronous streaming, immutable data structures, configuration management, and testability.

The core design principle is to provide a clean programmatic wrapper around the Codex CLI without reimplementing Codex functionality, focusing on process management and structured event parsing from existing JSONL logs.
