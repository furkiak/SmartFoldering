# Support and release gates

| Area | Evidence for this source revision |
| --- | --- |
| Windows Release source build | Builds locally after removing a missing XAML theme reference. |
| Planner, journal, queue and settings | Synthetic temporary-folder tests pass on Windows. |
| Native main and Settings UI | Opened on Windows 11 Pro 26200; an overlapping folder rule was rejected in Preview and Start remained paused. Existing settings SHA-256 did not change. |
| Tray, registry startup, mutex dialog, real file moves | Still require an isolated desktop scenario; no user's files were moved in this UI check. |
| macOS and Linux | Experimental source scope only; no supported package or watcher/permission test. |
| Public release | Blocked on native UI, clean-machine install/upgrade, and downloaded package check. |

For a release, verify same-volume and denied-permission cases on a clean Windows account, stop during a locked-file wait, watcher overflow recovery, and crash recovery from an intent-only journal. Preserve the previous published package and a copy of `%APPDATA%\SmartFoldering` for rollback. Do not advertise a platform or data-loss guarantee without a test on that platform and filesystem.
