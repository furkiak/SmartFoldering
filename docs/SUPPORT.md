# Support and release gates

| Area | Evidence for this source revision |
| --- | --- |
| Windows Release source build | Builds locally after removing a missing XAML theme reference. |
| Planner, journal, queue and settings | 19 tests pass on Windows, including a real 64 MiB file, rename, duplicate scans, undo and stopped watcher. |
| Native main and Settings UI | Opened on Windows 11 Pro 26200; an overlapping folder rule was rejected in Preview and Start remained paused. Existing settings SHA-256 did not change. |
| Native moves and second instance | Two disposable-profile Preview/Start/Pause/Undo cycles passed on Windows 11. Destination collision and unmatched file hashes were preserved. Second instance showed a warning and exited without ApplicationException. |
| Separate profiles and Quit | Absolute --data-directory keeps settings/journal/logs and instance lock separate. Native Settings Save left the normal startup entry unchanged. Quit closed the worker and process. |
| Tray and registry startup | Full tray reopen and the normal profile's sign-in startup still require direct checks. |
| macOS and Linux | Experimental source scope only; no supported package or watcher/permission test. |
| Public release | Clean-machine install/upgrade, another account, real kernel watcher-buffer overflow, downloaded current package and remote CI remain open. |

For a release, verify same-volume and denied-permission cases on a clean Windows account, stop during a locked-file wait, watcher overflow recovery, and crash recovery from an intent-only journal. Preserve the previous published package and a copy of `%APPDATA%\SmartFoldering` for rollback. Do not advertise a platform or data-loss guarantee without a test on that platform and filesystem.
