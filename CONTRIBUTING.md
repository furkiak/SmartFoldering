# Contributing

Use synthetic files in a temporary directory for bug reports and tests. Do not attach a real operation journal or settings file containing personal paths. Describe the source, destination, rule, expected preview, and observed result.

Run `dotnet test SmartFoldering.Tests/SmartFoldering.Tests.csproj -c Release` before a change. If behavior changes, add a test using temporary folders and a collision or failure fixture. Native UI changes also need a Windows check for preview, start/pause, watcher and scan, blocked rules, undo, tray, and settings failure. Distinguish automated coverage from that interactive check.
