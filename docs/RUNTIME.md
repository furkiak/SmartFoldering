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

The first working-tree .NET 10 Release run on 27 September 2026 passed **40/40**, zero failed/skipped, including the three real Windows kernel/permission cases. The initial self-contained output includes runtime **10.0.12** and produced no build warnings. The complete-graph test-project audit reported no known vulnerable packages. Repeat from a clean Git clone and record the package hash and actual native result for delivery. The native appearance evidence in APPEARANCE.md belongs to the earlier .NET 9 candidate. A successful restore or audit is not a native UI result, a reproduced exploit test, remote CI execution or a public release. No new release has been published.

## Türkçe

**0.3.0-preview.2** uygulama ve testleri .NET 10 LTS'ye taşır; Avalonia, profil biçimi ve dosya güvenliği davranışı korunur. Bilinen D-Bus bağımlılık açığı için **0.21.3** seçilir, uyarı bastırılmaz. Doğrudan ve dolaylı NuGet açıkları restore sırasında hata olur. Önceki .NET 9 test/native kanıtları tarihsel kayıttır; yeni runtime için temiz klon, gerçek dosya/izin/watcher ve ayrı profilde uygulama kontrolleri ayrıca kaydedilmelidir. Diğer işletim sistemleri, temiz makine ve yayın henüz doğrulanmış değildir.
