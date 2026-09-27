# Support and release gates

| Area | Evidence for this source revision |
| --- | --- |
| Windows Release source build | Builds locally after removing a missing XAML theme reference. |
| Planner, journal, queue and settings | 40 tests pass with no skips in the opt-in Windows run: 37 ordinary cases plus real kernel overflow, ACL denial and read-only settings denial. All previous 22 cases remain. |
| Appearance candidate | Light/Dark and 100/150/200% font sizes use atomic profile settings without changing preview revision or monitoring. Source token contrast minima pass; native keyboard/focus/200% layout acceptance is pending. See APPEARANCE.md. |
| Kernel overflow recovery | Production 64 KiB watcher raised InternalBufferOverflowException; individual sentinel events were lost, then a retained full rescan moved all files once with unchanged SHA-256. No forced/fallback scan or fake error event was used. |
| Settings permission failures | Owned-directory ACL CreateFiles denial and ReadOnly current-file replacement failure preserved main/backup/revision/current/committed state; permissions restored and later save succeeded. Native Preview/Start also displayed access denied and left monitoring off, files unchanged and journal absent. |
| Earlier native main and Settings UI | Prior layout opened on Windows 11 Pro 26200; an overlapping folder rule was rejected in Preview and Start remained paused. Existing settings SHA-256 did not change. These screenshots precede appearance controls. |
| Native moves and second instance | Two disposable-profile Preview/Start/Pause/Undo cycles passed on Windows 11. Destination collision and unmatched file hashes were preserved. Second instance showed a warning and exited without ApplicationException. |
| Separate profiles and Quit | Absolute --data-directory keeps settings/journal/logs and instance lock separate. Native Settings Save left the normal startup entry unchanged. Quit closed the worker and process. |
| Tray and registry startup | Full tray reopen and the normal profile's sign-in startup still require direct checks. |
| macOS and Linux | Experimental source scope only; no supported package or watcher/permission test. |
| Public release | Clean-machine install/upgrade, another account, downloaded current package and remote CI remain open. |

For a release, verify same-volume and denied-permission cases on a clean Windows account, stop during a locked-file wait, watcher overflow recovery, and crash recovery from an intent-only journal. Preserve the previous published package and a copy of `%APPDATA%\SmartFoldering` for rollback. Do not advertise a platform or data-loss guarantee without a test on that platform and filesystem.

See [Windows integration reproduction](WINDOWS_INTEGRATION.md) for the explicit root, real-error preconditions and cleanup scope. The local tests cover this Windows/filesystem session; broader release environments require separate evidence.
