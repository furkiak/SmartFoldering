# Changelog

All notable changes to SmartFoldering are documented here.

## [2.0.0] - 2026-10-12

A rewrite: a new engine that is careful with your files, a new Liquid Glass interface, and tests.

### Added
- **Preview before anything moves.** A newly added folder starts switched off and shows exactly which files would go where; *Turn on sorting* starts it.
- **Undo.** Every move is kept in a history (`history.json`, last 500 moves) and can be put back from the activity list or the notification. A file that was put back is left alone for 24 hours.
- **Ready-made destinations** for Downloads (documents, images, videos, music, archives, installers, code), with folder names in the interface language.
- **Regular-expression rules** next to extension and name rules; extension rules accept lists (`pdf, docx`) and double extensions (`tar.gz`).
- **Rule editor with a file-name test** that also tells you when an earlier destination takes the file first, and **destination ordering** (first match wins) on the cards.
- **Activity list**, "sorted today" counter and a count of files still waiting for a download to finish.
- **Notifications** that batch several files into one card, offer *Undo* for a single file and never take keyboard focus.
- **Settings**: language (English / Turkish, switchable at runtime), theme (system / light / dark), wait time, full-scan interval, name-conflict policy (keep both / leave it), notifications, start with Windows.
- **Liquid Glass design** with a new app icon, light and dark themes.
- **Tests**: 42 engine tests against real folders (including a live `FileSystemWatcher`) and a headless UI self-test with 29 checks; both run in CI on every push.
- **Releases** built by GitHub Actions from a version tag: a self-contained single-file exe, a small framework-dependent exe and SHA-256 checksums.

### Fixed (problems in 1.x)
- Partial downloads were not recognised: a name rule could move a `.crdownload` or `.part` file while the browser was still writing it, and a browser's empty placeholder (`report.pdf` next to `report.pdf.part`) was moved too, leaving an empty duplicate behind. Partial downloads and their placeholders are now never moved.
- A file was moved as soon as it could be opened, even if a copy was still writing to it in bursts. A file now has to stay unchanged for the wait time and must not be open elsewhere.
- A destination pointing at the watched folder itself renamed files endlessly (`a (1).pdf`, `a (1) (1).pdf`…). Such a destination is now ignored.
- An extension rule written without the dot (`pdf`) never matched, and `tar.gz` could not be matched at all.
- A name rule also matched the extension, so a rule for "pdf" caught every PDF. Name rules now look at the name only.
- Bursts of new files could overflow the watcher and stay unsorted until the next five-minute scan. An overflow now triggers a scan at once.
- Office and LibreOffice lock files, `desktop.ini` and hidden or system files could be moved.
- The start-with-Windows entry kept pointing at the old location after the app was moved or updated.

### Changed
- Requires .NET 10 (the release exe bundles it).
- Settings from 1.x (`%APPDATA%\SmartFoldering\settings.json`) are converted on first start; sorting starts paused so you can check them. Quit version 1 before starting version 2.
- Serilog was replaced by a small built-in log that rotates at 512 KB.

## [1.0.0]

First version: watched folders, target folders with extension and keyword rules, tray icon and start with Windows.
