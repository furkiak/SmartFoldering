# Changelog

## Unreleased

- Allow a separate absolute data directory for isolated/portable profiles, including profile-specific logs and instance locks; keep their Settings window away from the normal Windows startup entry.
- Exercise a real 64 MiB file, temporary-download rename, duplicate fallback requests, guarded undo and stopped watcher behavior on Windows.
- Complete disposable native move/pause/undo and second-instance checks; add an accessible Quit action and keep preview status accurate during monitoring.
- Keep the recent operation list scrollable and remove nullable initialization warnings without disabling compiler checks.

- Show invalid-rule details in the preview panel and keep Start paused without a misleading retry prompt.
- Give the Windows app its own icon and clarify what the sign-in startup setting does.
- Remove a missing Avalonia DataGrid theme that prevented Release builds.
- Default monitoring to off for this revision and require a reviewed preview before starting.
- Reject overlapping/cyclic folders, linked paths and cross-volume automatic moves.
- Process watcher and scan requests through one cancellable, deduplicated queue.
- Record move intent/completion in a durable journal and protect undo with SHA-256 and source collision checks.
- Serialize settings writes and preserve the last committed configuration on failure.
- Fix the second-instance mutex ownership path and clarify the supported-platform claims.

No public tag has been created for this source revision.
