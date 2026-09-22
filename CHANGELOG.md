# Changelog

## Unreleased

- Remove a missing Avalonia DataGrid theme that prevented Release builds.
- Default monitoring to off for this revision and require a reviewed preview before starting.
- Reject overlapping/cyclic folders, linked paths and cross-volume automatic moves.
- Process watcher and scan requests through one cancellable, deduplicated queue.
- Record move intent/completion in a durable journal and protect undo with SHA-256 and source collision checks.
- Serialize settings writes and preserve the last committed configuration on failure.
- Fix the second-instance mutex ownership path and clarify the supported-platform claims.

No public tag has been created for this source revision.
