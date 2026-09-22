# Operation journal and recovery

The app stores `settings.json`, `settings.json.bak`, `operations.jsonl`, and local log files under `%APPDATA%\SmartFoldering`. The journal has one JSON event per line: `intent`, `moved`, `failed`, or `undone`. It includes source/destination paths and a SHA-256 hash, so treat it as private. The app does not automatically delete old journal entries.

If power fails during a move, close the app before inspecting the two paths and the journal. An `intent` may exist without a `moved` event. If the source is absent, the destination exists, and its bytes match the hash, the paused app can attempt an undo. If the destination was changed or the old source path is occupied, undo refuses. Back up the involved files before any manual repair.

Settings are written to a unique temporary file, flushed and replaced atomically on the local filesystem while keeping the previous version as `.bak`. A malformed current settings file can be recovered from a valid backup; if neither parses, the original files are left untouched and startup stops. Journal corruption blocks new moves before any file is touched. Atomic rename and the journal do not guarantee recovery from every disk, driver, power or filesystem failure. Automatic moves are limited to the same volume to avoid copy/delete semantics across volumes.
