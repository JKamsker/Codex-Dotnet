# Event Schemas: Codex.DotNet

This directory contains JSON schemas and examples for all Codex event types.

## Event Structure

All events follow the same top-level structure:

```json
{
  "timestamp": "2025-11-20T20:02:27.123Z",
  "type": "event_type_name",
  "payload": {
    // Event-specific fields
  }
}
```

## Event Types

Currently included schemas:
- [session_meta.json](./session_meta.json) - Session initialization
- [user_message.json](./user_message.json) - User input
- [token_count.json](./token_count.json) - Token usage

Planned (not yet checked in; referenced by FR-007 for future mapping):
- agent_message.json - Codex response
- agent_reasoning.json - Codex reasoning
- turn_context.json - Turn initialization

## Schema Format

Each `.json` file contains:
1. Schema definition (JSON Schema draft-07)
2. Example events
3. Field descriptions

## Usage

These schemas document the expected structure of events parsed from Codex JSONL logs. Treat them as a *lower bound*: parsers must tolerate extra fields and unknown event types to remain forward-compatible (FR-008, FR-023). Use them for:
- Understanding event structure
- Generating test data
- Validating parser implementations
- Documentation reference
