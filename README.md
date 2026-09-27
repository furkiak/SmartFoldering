# SmartFoldering

**Preview and organize local files with explicit control.** SmartFoldering is a C#/.NET 9 Avalonia desktop app. It matches top-level files in selected source folders against extension or filename-keyword rules, then moves them to destination folders. This revision adds a read-only preview, a single processing queue, a persistent move journal, and a guarded one-step undo.

![SmartFoldering workflow diagram, not a screenshot](docs/workflow.svg)

> Preview source revision: 22 tests pass in the opt-in Windows run, including a real 64 MiB file, rename, guarded undo, production-buffer kernel overflow recovery, ACL access denial and read-only settings protection. A separate native profile completed Preview → Start → Pause → Undo twice, preserving a destination collision and the nonmatching file. A second instance was rejected cleanly. Windows sign-in startup, another Windows account and clean-machine installation remain unverified. The diagram is a workflow explanation.

![Windows 11 native preview of a disposable fixture, 27 September 2026](docs/windows-preview-2026-09-27.png)

The native check used disposable local files. The normal profile's settings and Windows startup entry stayed unchanged. File hashes and the durable `intent → moved → undone` journal were checked after each cycle. **Quit** stops the worker and closes the app; **Hide window** keeps it available in the tray.

## Quick start on Windows

Install the .NET 9 SDK and run from the repository root:

```powershell
dotnet run --project SmartFoldering.csproj
```

1. Add a source folder, such as a **test** `Downloads` folder. Add a destination folder and an extension rule such as `.pdf`.
2. Choose **Preview moves**. The proposed source and destination paths are displayed without moving a file.
3. Review the list, then choose **Start monitoring**. If rules change, monitoring pauses and another preview is required.
4. Choose **Pause monitoring** before **Undo last move**. Undo refuses to overwrite an existing source or move a destination whose SHA-256 differs from the recorded file.

If preview reports that a destination is the same as, inside, or above its source, edit the destination to a separate folder and run Preview again. Monitoring remains off; a failed preview cannot authorize a Start.

For a separate disposable or portable profile, launch with `--data-directory` and an absolute folder path. Settings, the move journal and logs stay in that directory; different profiles use separate instance locks. Windows sign-in startup is disabled for these profiles, so saving their Settings window cannot change the normal profile's registry entry. No existing settings are copied automatically.

```powershell
dotnet run --project SmartFoldering.csproj -- --data-directory E:\SmartFoldering-TestProfile
```

For source development: `dotnet test SmartFoldering.Tests/SmartFoldering.Tests.csproj -c Release`. There is no published installer for this revision. Start with disposable test files; the local native check does not prove every user's filesystem or permissions.

The three Windows integration tests require an explicitly selected disposable root via `SMARTFOLDERING_WINDOWS_TEST_ROOT`; otherwise they are skipped. See [actual kernel/permission checks and reproduction](docs/WINDOWS_INTEGRATION.md) for the command, owned-fixture cleanup and evidence limits.

## Example preview

With `C:\Test\Downloads\report.pdf` and rule `.pdf → C:\Test\Documents`:

```text
Preview: 1 proposed move
report.pdf → C:\Test\Documents\report.pdf
```

If `report.pdf` already exists at the destination, the plan proposes `report (1).pdf`. At execution, a new conflict or changed source causes a **skip and error**, not an overwrite. A watcher event and five-minute fallback scan enter the same single-reader queue. The scan does not recurse into subfolders. The app waits for exclusive access and stable file metadata before moving, but no watcher can guarantee the exact instant an external process has finished writing.

If Windows reports a watcher buffer overflow, a full scan is queued even when another scan is already processing its captured file list. Duplicate waiting requests are coalesced; one later scan can recover files whose individual notification was lost.

## Safety boundaries

- A destination cannot equal, contain, sit inside, or feed a watched source. Overlapping watched sources are rejected; these checks also prevent A→B→A cycles.
- Linked/junction paths and linked files are rejected. Automatic source and destination must be on the same volume. Temporary download suffixes such as `.part` and `.crdownload` are skipped.
- Each move writes an intent to `%APPDATA%\SmartFoldering\operations.jsonl`, then moves the file and writes a completion event. SHA-256 identifies the moved bytes for undo. A crash between move and completion leaves an intent that can be reconciled by the undo path if the destination still matches.
- Rule settings use unique temporary files, disk flush and an atomic replacement with `settings.json.bak`. A failed save reports an error and the running engine keeps the last committed rules. Editing rules pauses monitoring before the new settings are saved.
- Windows auto-start is a separate setting; it should be enabled only after the workflow has been checked on the user's machine.

Operation history can expose file names and paths. It is local to the OS user profile and is not uploaded by the source app. There is no automatic retention or secure erasure. Back up important data independently; a journal and one-step undo do not replace a backup. See [recovery and limits](docs/SAFETY.md).

## Scope and verification

The code uses cross-platform APIs in several places, but macOS and Linux builds, packaging, permissions, watcher behavior and UX have **not** been verified. Windows is the intended first release target. The source currently targets .NET 9; moving to a later runtime is a separate compatibility task. See [support gates](docs/SUPPORT.md), [changelog](CHANGELOG.md), [contributing](CONTRIBUTING.md) and [security reporting](SECURITY.md). Licensed under [MIT](LICENSE).

## Türkçe

SmartFoldering, kurallara göre yerel dosyaları taşımadan önce **önizleme** gösteren C# / Avalonia uygulamasıdır. Yeni sürümde izleme varsayılan olarak kapalıdır. Kaynak, hedef ve kuralı ekledikten sonra **Preview moves** ile yolları incele; **Start monitoring** ile başlat. Kaynak/hedef iç içe olursa önizleme açıklayıcı hata verir ve izleme başlamaz. Kurallar değişince izleme durur. **Undo last move** yalnızca izleme duraklatıldığında, kaynak yol boşsa ve hedef dosya taşınan dosyayla aynı SHA-256 değerindeyse çalışır. Windows 11'de ayrı geçici profilde iki gerçek taşıma/geri alma döngüsü, çakışan hedefi koruma ve ikinci örneğin temiz kapanması doğrulandı; kişisel ayarlar değişmedi. Açıkça seçilen geçici kökte Windows entegrasyonu dahil 22 test geçti: gerçek 64 KiB watcher taşmasında kaybolan bildirimler tam taramayla kurtarıldı; ACL ve read-only kayıt reddinde son sağlam JSON/yedek/ayarlar korundu. Üç entegrasyon testi kök belirtilmezse atlanır. **Quit** izlemeyi durdurup uygulamayı kapatır; **Hide window** tepside çalışmayı sürdürür. Native kayıt hatasında açıklayıcı erişim engeli mesajı göründü, izleme başlamadı ve hiçbir dosya taşınmadı. Başka hesap ve temiz makine kurulumu ayrı kontrollerdir.
