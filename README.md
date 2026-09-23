# Deep Ducking public host

This repository contains the complete DDuck public host: introduction, authenticated module loader, manifest validation, and access IPC. Open it with `/dduck` or `/dd`. Automation source remains in `Z:\DDuck`; the public shell builds independently.

The public version is **0.0.0.3**. Its CLR assembly identity is **1.0.0.0**, the stable module ABI. Private module versions advance independently.

## Build

With .NET 10 and the Dalamud API 15 development references installed:

```powershell
dotnet build Z:\dduck-public\DDuck.csproj -c Release -p:Platform=x64
```

The public package is `bin\x64\Release\DDuck\latest.zip`. It contains only the host, manifest, icon, README, license and notices. Signing keys and private automation source are not build inputs.

Run the offline host contract regression with:

```powershell
dotnet run --project Z:\dduck-public\tests\HostContract\HostContract.csproj -c Release
```

## Module access

The host exposes `DDuck.Access.Directory.v1`, `DDuck.Access.Validate.v1` and `DDuck.Access.Refresh.v1`. Modules install as `tasks\DDuck.Access.dll` and `tasks\DDuck.json` beneath DDuck's plugin configuration directory. The JSON must exactly match the manifest inside the authenticated carrier. Public release numbers do not restrict private module versions.

Keep the host enabled while installing access updates. Use a package manager with DDuck access support and follow the package's installation instructions. Click **Check installed access** to load an installed update. Package-manager integration and live client behavior have not been verified here.

The implementation migration is described in [DDUCK-IMPLEMENTATION-HANDOFF.md](DDUCK-IMPLEMENTATION-HANDOFF.md). After that migration, `Z:\dduckprivate.bat` builds the real private package. It reports the missing prerequisite until the DDuck private project exists.

No publication destination is configured. `repo.json` is intentionally empty until a real public release location is selected. No remote repository was created by this preparation.

## Community and license

Join [The Dumpster Fire on Discord](https://discord.gg/VsXqydsvpu) or [support McVaxius on Ko-fi](https://ko-fi.com/mcvaxius).

DDuck's existing AGPL-3.0-or-later license and third-party notices are retained. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
