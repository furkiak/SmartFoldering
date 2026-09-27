# .NET 10 local candidate

Version **0.3.0-preview.2** moves the application and its tests from `net9.0` to `net10.0`. The Windows CI source requests the stable .NET 10 SDK. `global.json` accepts stable .NET 10 feature bands starting at 10.0.100; it does not select .NET 11 or a preview SDK. Avalonia remains pinned to 11.2.7, with the existing styles and profile/journal formats unchanged.

Microsoft's [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), checked 27 September 2026, lists .NET 9 end of support as 10 November 2026 and .NET 10 LTS as 14 November 2028. Runtime patches still need monthly review; an LTS label does not make an old self-contained package current.

The original dependency audit reported **Tmds.DBus.Protocol 0.20.0**, brought in by Avalonia, against [GHSA-xrw6-gwf8-vvr9](https://github.com/tmds/Tmds.DBus/security/advisories/GHSA-xrw6-gwf8-vvr9). The maintainer backported the fix to **0.21.3**. This project directly selects that version rather than suppressing the warning. `Directory.Build.props` audits the complete dependency graph and treats NU1901–NU1904 as errors for the app and test project.

```powershell
dotnet --version
dotnet list SmartFoldering.csproj package --vulnerable --include-transitive
$env:SMARTFOLDERING_WINDOWS_TEST_ROOT = Join-Path (Get-Location) 'TestResults\windows-fixtures-net10'
dotnet test SmartFoldering.Tests/SmartFoldering.Tests.csproj -c Release --logger 'trx;LogFileName=net10.trx' --results-directory TestResults\net10
dotnet publish SmartFoldering.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o TestResults\publish-net10
```

Inspect `SmartFoldering.runtimeconfig.json` and the publish output to identify the exact bundled runtime patch. Keep the earlier ZIP and a copy of the entire profile before upgrading. The runtime change does not authorize monitoring: start with an absolute disposable `--data-directory`, inspect Preview, Start, Pause and Undo, and compare file bytes and the journal. Test appearance saving while the watcher is active and restart persistence separately. Normal-profile migration, fresh-machine installation, another OS user, Linux D-Bus interaction and macOS are separate release checks.

The first working-tree .NET 10 Release run on 27 September 2026 passed **40/40**, zero failed/skipped, including the three real Windows kernel/permission cases. The subsequent clean-clone and exact-final-package results are recorded below. For a future package, repeat the clean-clone checks and record its new hash and native result. The native appearance evidence in APPEARANCE.md belongs to the earlier .NET 9 candidate. A successful restore or audit is not a native UI result, a reproduced exploit test, remote CI execution or a public release. No new release has been published.

## Dated final-package validation

On 27 September 2026 a clean clone of source commit
`f1c8d6c63f95deeba83266cb1aecaf772bfcc100` passed **40/40 tests**, zero failures
or skips, including the three actual Windows kernel/permission cases. The
self-contained Windows x64 publish contained runtime **10.0.12** and reported
zero warnings. App/test direct-and-transitive audits reported no known vulnerable
packages. Restoring the old D-Bus dependency made restore fail with NU1903; the
source was restored before the successful checks.

The accepted package is `SmartFoldering-0.3.0-preview.2-f1c8d6c-win-x64-complete.zip`
(46,675,315 bytes), SHA-256
`7BC8CD2F3FC448956F8009D3110F3C4F20E603AE82993D8C5A6F1B3F0B304BC1`.
The exact ZIP opened paused at Light/200 in an owned synthetic profile. Preview
showed one Unicode filename without changing source bytes; Start moved it
byte-exact. Native Settings Save switched to Dark/200 while retaining the active
watcher and an unknown setting. A later synthetic PDF moved once without Scan
now or a new Preview. Pause/Undo restored that file and wrote one undone record.
Quit and restart retained Dark/200 with monitoring paused; both owned test
processes exited. The three known normal-profile file hashes and Run entry
digest stayed unchanged. No personal files were copied.

The dated validation records are retained with the local release candidate,
outside this source repository:

- `SmartFoldering-package-f1c8d6c-net10.json` binds the code commit, ZIP,
  clean-test/publish proof and native result.
- `Runtime-net10-2026-09-27\SmartFoldering-f1c8d6c\native-result.json` records
  the exact-package observations and remaining boundaries.

These results apply to the identified code commit and ZIP; later documentation
changes do not constitute another runtime validation. The wider `2b41133` .NET 9 font/minimum-window/ReadOnly
matrix remains historical. Selected Light/200 and Dark/200 flows do not prove
the full new-runtime failure/accessibility matrix. ReadOnly UI retry, tray/startup,
DPI/screen readers, Linux D-Bus/API, macOS, another account, fresh-machine setup,
remote CI, push and public release remain open.

## Türkçe

**0.3.0-preview.2** uygulama ve testleri .NET 10 LTS'ye taşır; Avalonia, profil biçimi ve dosya güvenliği davranışı korunur. Bilinen D-Bus bağımlılık açığı için **0.21.3** seçilir, uyarı bastırılmaz. Doğrudan ve dolaylı NuGet açıkları restore sırasında hata olur. `f1c8d6c` temiz klonda 40/40 test, sıfır atlama/uyarı ve runtime 10.0.12 içeren kesin ZIP yukarıdaki hash ile kaydedildi. Aynı ZIP'in seçili Preview/Start, aktif watcher'da görünüm kaydı, sonra taşıma/Pause/Undo ve duraklatılmış yeniden açılışı 27 Eylül 2026'da geçti. Bu belge commit'i yeni derleme/paket kabulü değildir; eski .NET 9 geniş matrisi tarihsel kalır. Tam yeni-runtime hata/ReadOnly/DPI/ekran okuyucu, diğer OS, başka hesap, tepsi/startup, temiz makine, uzak CI, push ve yayın kapıları açıktır.
