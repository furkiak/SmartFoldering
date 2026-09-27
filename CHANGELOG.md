# Changelog

## Unreleased

### Local candidate 0.3.0-preview.1

- Add Light/Dark and 100/150/200% actual font-size preferences with visible profile settings and atomic persistence; retain legacy Dark/100 defaults.
- Keep appearance-only saves separate from preview approval, committed/pending rule edits, monitoring and RulesChanging events. Preserve unsupported appearance data and unknown top-level fields.
- Use opaque palette surfaces, stronger control boundaries and explicit keyboard focus; disable decorative transitions. Reflow actions, wrap paths and use resizable scrollable windows with standard frames.
- Write the normal Windows startup entry only when its checkbox changes; keep separate profiles isolated and report partial appearance/startup save failures explicitly.
- Pass all 40 behavior tests with real Windows kernel overflow/ACL/ReadOnly cases; detect invalid-preference and preview-revision mutations. New appearance native acceptance remains pending; old screenshots show the earlier layout.

- Retain an overflow rescan requested while another full scan is active; keep queued requests coalesced without losing events that arrive after the active file-list snapshot.
- Verify actual Windows kernel watcher overflow with the production 64 KiB buffer, lost notification recovery, unchanged file hashes and one journal result per file.
- Add opt-in owned-fixture Windows ACL and read-only replacement-denial tests; preserve settings/backup/committed state and restore permissions before cleanup.
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
