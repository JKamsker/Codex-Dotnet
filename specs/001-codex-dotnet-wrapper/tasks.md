---
description: "Task breakdown for Codex.DotNet CLI wrapper implementation"
---

# Tasks: Codex.DotNet CLI Wrapper

**Input**: `specs/001-codex-dotnet-wrapper/{plan.md,spec.md,data-model.md,quickstart.md,contracts/}`  
**Prerequisites**: Phase 1 design docs above are finalized.  
**Tests**: Unit and integration tests are required for core stories (US1-US5).  
**Organization**: [P] = can run in parallel (different files, no dependencies). Tasks are grouped by user story for independent delivery.

**Scope guard**: Tasks stay within the spec's scope (no advanced event filtering/transformations beyond FR-007/FR-008).
**Additional notes**: Avoid calling the real Codex CLI in unit tests and in testing overall. If absolute necessary - use the cheapest model/thinking budget as possible (gpt-5.1-codex-mini medium)

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] [INFRA] Create solution structure (`Codex.DotNet.sln`) with `src/Codex.DotNet/` and `tests/Codex.DotNet.Tests/` folders.
- [X] T002 [INFRA] Scaffold `src/Codex.DotNet/Codex.DotNet.csproj` targeting `net10.0` (optional multi-target `net8.0;net9.0`), enable nullable, implicit usings, and add package refs for `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`.
- [X] T003 [INFRA] Scaffold `tests/Codex.DotNet.Tests/Codex.DotNet.Tests.csproj` with xUnit + `coverlet.collector`, reference the main project, and add FluentAssertions (or equivalent) for assertions.

## Phase 2: Foundational (Blocking Prerequisites)

- [X] T010 [P] [INFRA] Implement public models/value objects in `src/Codex.DotNet/Public/Models/` (`SessionId`, `CodexModel`, `CodexReasoningEffort`, `CodexEvent` + derived event types, `EventStreamOptions`) and metadata types (`CodexSessionInfo`, `SessionFilter`), including validation helpers.
- [X] T011 [P] [INFRA] Implement option classes `CodexClientOptions` and `CodexSessionOptions` in `src/Codex.DotNet/Public/` with validation (timeouts, paths, required prompt/working directory) and defaults from plan.
- [X] T012 [INFRA] Define abstraction interfaces in `src/Codex.DotNet/Abstractions/` (`ICodexClient`, `ICodexSessionHandle`, `ICodexProcessLauncher`, `ICodexSessionLocator`, `IJsonlTailer`, `IJsonlEventParser`, `IFileSystem`, `ICodexPathProvider`, `ILoggerAdapter` optional).
- [X] T013 [P] [INFRA] Implement `RealFileSystem` and `DefaultCodexPathProvider` in `src/Codex.DotNet/Infrastructure/` to resolve Codex executable (platform-aware), sessions root, and basic file operations.
- [X] T014 [P] [INFRA] Implement `JsonlEventParser` in `src/Codex.DotNet/Infrastructure/JsonlEventParser.cs` to map all known event types, preserve unknown types, expose `RawPayload`, and skip malformed lines with logging (FR-007, FR-008, FR-020, FR-023).
- [X] T015 [INFRA] Implement `JsonlTailer` in `src/Codex.DotNet/Infrastructure/JsonlTailer.cs` to stream lines from append-only JSONL with `FileShare.ReadWrite`, polling interval, and support for `EventStreamOptions` (from beginning, timestamp, byte offset) with cancellation (FR-006, FR-021, FR-024).
- [X] T016 [P] [INFRA] Add test helpers in `tests/Codex.DotNet.Tests/TestHelpers/` (`InMemoryFileSystem`, `MockCodexProcessLauncher`, `TestJsonlGenerator`, `SampleEventFactory`) plus sample JSONL fixtures under `tests/Codex.DotNet.Tests/Fixtures/sample-session-logs/`.
- [X] T017 [P] [INFRA] Unit tests for models/options validation and `DefaultCodexPathProvider` in `tests/Codex.DotNet.Tests/Unit/`.
- [X] T018 [P] [INFRA] Unit tests for `JsonlEventParser` covering all event schemas, unknown event passthrough, and malformed line recovery in `tests/Codex.DotNet.Tests/Unit/JsonlEventParserTests.cs`.
- [X] T019 [INFRA] Unit tests for `JsonlTailer` in `tests/Codex.DotNet.Tests/Unit/JsonlTailerTests.cs` verifying tailing growth, timestamp/offset start positions, cancellation responsiveness, and concurrency-safe reads.

**Checkpoint**: Foundational abstractions, models, and file tailing/parsing are complete; user story work can proceed in parallel.

## Phase 3: User Story 1 - Start Session & Stream Events (P1)

- [X] T030 [US1] Implement `CodexProcessLauncher` in `src/Codex.DotNet/Infrastructure/CodexProcessLauncher.cs` to start `codex exec -` with stdin prompt piping, capture process handles, and bubble stderr for diagnostics; ensure validation of executable (FR-001, FR-012, FR-015).
- [X] T031 [US1] Implement `CodexSessionLocator` start-path discovery in `src/Codex.DotNet/Infrastructure/CodexSessionLocator.cs` using pre-launch snapshot + timestamp filter to find new log file within `StartTimeout` (FR-003, FR-021).
- [X] T032 [US1] Implement `CodexSessionHandle` in `src/Codex.DotNet/Public/CodexSessionHandle.cs` to wrap session metadata, expose `IsLive`, `GetEventsAsync`, `WaitForExitAsync`, and dispose logic wiring `JsonlTailer` + `JsonlEventParser`.
- [X] T033 [US1] Implement `CodexClient.StartSessionAsync` in `src/Codex.DotNet/Public/CodexClient.cs` orchestrating options validation, process launch, session file location, session_meta extraction, and return of `CodexSessionHandle`.
- [X] T034 [P] [US1] Integration tests in `tests/Codex.DotNet.Tests/Integration/CodexClientStartSessionTests.cs` using mocked launcher/logs to verify session ID capture, event stream ordering (session_meta -> user_message -> agent_reasoning -> agent_message -> token_count), and first-event timing respects `StartTimeout` (SC-001/004).
- [X] T035 [P] [US1] Error-path tests for start failures (missing executable, process exits before session_meta, log file not found) in `tests/Codex.DotNet.Tests/Integration/CodexClientStartSessionTests.cs`.

**MVP line (US1 complete)**: Tasks T001–T035 deliver the minimum viable product (start session + live streaming). Tasks below this line are post-MVP.

## Phase 4: User Story 2 - Resume Existing Session (P2)

- [X] T040 [US2] Extend `CodexSessionLocator` to resolve existing logs by `SessionId` and by explicit file path, including verification of file existence and readability (FR-009, FR-010).
- [X] T041 [US2] Implement `CodexClient.ResumeSessionAsync` and `AttachToLogAsync` in `src/Codex.DotNet/Public/CodexClient.cs` to create non-live `CodexSessionHandle` using existing logs, honoring `EventStreamOptions` starting positions.
- [X] T042 [P] [US2] Integration tests in `tests/Codex.DotNet.Tests/Integration/CodexClientResumeSessionTests.cs` covering full replay from beginning, resume by path, and error on unknown session ID (acceptance scenarios 1-4).

## Phase 5: User Story 4 - Configure Codex Execution Options (P2)

- [X] T050 [P] [US4] Implement argument construction in `CodexProcessLauncher` for model (`--model`), reasoning effort (`--config model_reasoning_effort=<val>`), working directory (`--cd`), additional CLI options, and custom binary path override (FR-011, FR-018).
- [X] T051 [US4] Strengthen options validation in `CodexClientOptions`/`CodexSessionOptions` (positive timeouts, poll interval minimums, required prompt/working directory) with clear error messages (edge cases section).
- [X] T052 [P] [US4] Unit tests in `tests/Codex.DotNet.Tests/Unit/ProcessStartInfoBuilderTests.cs` verifying argument mapping for models, reasoning effort, `--no-tui`/additional options, and binary path selection on Windows vs. Unix.
- [X] T053 [P] [US4] Add DI registration conveniences (optional) in `src/Codex.DotNet/Public/ServiceCollectionExtensions.cs` wiring abstractions to implementations for Microsoft.Extensions.DependencyInjection (FR-019).

## Phase 6: User Story 5 - Cancellation & Cleanup (P2)

- [X] T060 [US5] Propagate cancellation tokens through start/resume/event streaming paths (`CodexClient`, `CodexSessionHandle`, `JsonlTailer`) ensuring prompt exit on cancellation (FR-013).
- [X] T061 [US5] Implement graceful shutdown in `CodexSessionHandle.DisposeAsync`/`CodexProcessLauncher` with stdin close, timed wait, forced kill fallback, and file handle disposal; track disposed state to block further reads (FR-014, FR-025).
- [X] T062 [P] [US5] Integration tests in `tests/Codex.DotNet.Tests/Integration/CancellationAndCleanupTests.cs` verifying event stream stops on cancellation, process termination within `ProcessExitTimeout`, and disposed handles reject new operations (edge cases 1-4).

## Phase 7: User Story 3 - List & Filter Historical Sessions (P3)

- [X] T070 [US3] Implement session discovery in `CodexSessionLocator` to enumerate JSONL logs under sessions root, parse metadata (timestamp, working directory, model when available), and support concurrent directory access (FR-017, FR-021).
- [X] T071 [US3] Implement `CodexClient.ListSessionsAsync` in `src/Codex.DotNet/Public/CodexClient.cs` returning `IAsyncEnumerable<CodexSessionInfo>` with `SessionFilter` applied (date range, working directory, model, ID pattern) (FR-010, FR-017, FR-024).
- [X] T072 [P] [US3] Integration tests in `tests/Codex.DotNet.Tests/Integration/CodexClientListSessionsTests.cs` covering multiple sessions, date/model filters, working-directory filter, and empty-directory behavior (acceptance scenarios 1-4).

## Phase 8: Polish & Cross-Cutting

- [X] T080 [P] [DOC] Update `specs/001-codex-dotnet-wrapper/quickstart.md` and any public README samples to match final API surface and option names.
- [X] T081 [P] [QA] Add XML documentation comments for public API types/members and enable `GenerateDocumentationFile` in `Codex.DotNet.csproj`.
- [X] T082 [QA] Add GitHub-friendly build/test script or CI stub (if repo uses CI) to run `dotnet test` across target frameworks.
