# Deep Ducking public host

This repository contains the complete DDuck public host: introduction, authenticated module loader, manifest validation, and access IPC. Open it with `/dduck` or `/dd`. Automation source remains in `Z:\DDuck`; the public shell builds independently.

The public version is **0.2.2.0**. Its CLR assembly identity is **1.0.0.0**, the stable module ABI. Private module versions advance independently.

The introduction provides regular and compact layouts, shared colour and fourteen-language selectors, and managed host fonts. The language choices include Vietnamese, Brazilian Portuguese, Indonesian, Polish and Turkish. Its appearance preferences use the existing plugin configuration and are shared with the authenticated module while preserving private and unknown configuration fields. In regular mode, access instructions and the check button sit beside each other when space permits and stack in narrow windows. Community buttons use shared Discord and Ko-fi artwork; access-failure text wraps to the visible window width.

## Build

With the sibling `Z:\aethertekUI` checkout, pinned .NET SDK 10.0.201 and the Dalamud API 15 development references installed:

```powershell
. Z:\aethertekUI\eng\Enter-RepoEnv.ps1
dotnet build Z:\dduck-public\DDuck.csproj -c Release -p:Platform=x64 -p:OutputPath=Z:/!cryptography/DDuck/offline/public/Release/
```

The public package is `DDuck\latest.zip` beneath the selected output directory (normally `bin\x64\Release`). It contains the host, `AethertekUI.dll`, `AethertekUI.Dalamud.dll`, manifest, icon, README, license and notices. Localization resources are embedded in the host. Signing keys and private automation source are not build inputs; host-owned Dalamud assemblies and Windows fonts are excluded.

GitHub Actions uses `.github/workflows/build-release.yml` to build and release on pushes to `master`, `v*` tags, or a manual workflow run. It checks out the public host and AethertekUI in sibling directories using the consumer's read-only `AETHERTEKUI_DEPLOY_KEY`, installs .NET 10.0.201, downloads Dalamud references and passes that distribution to restore/build. It attaches `latest.zip` and `DDuck.json` to the versioned release, following the MOM/DhogNav public workflow.

The dependency checkout is pinned to published AethertekUI revision `6c193cf06ac67f954c549cafc2033ac0efdd630a`, which contains the required Hindi text host, window opacity and community icon APIs. When adopting a newer library API, update this existing ref after that library revision is published.

Version bumper H (`Z:\dhogbump\bump_gui_h.py`) includes a separate `DDuck-public` row. It updates the public project, BuildInfo, manifest and repository feed together while preserving CLR ABI `1.0.0.0` and DDuck's private version.

Run the offline host contract regression with:

```powershell
dotnet run --project Z:\dduck-public\tests\HostContract\HostContract.csproj -c Release
```

## Module access

The host exposes `DDuck.Access.Directory.v1`, `DDuck.Access.Validate.v1` and `DDuck.Access.Refresh.v1`. Modules install as `tasks\DDuck.Access.dll` and `tasks\DDuck.json` beneath DDuck's plugin configuration directory. The JSON must exactly match the manifest inside the authenticated carrier. Public release numbers do not restrict private module versions.

Keep the host enabled while installing access updates. Use a package manager with DDuck access support and follow the package's installation instructions. Click **Check installed access** to load an installed update. Package-manager integration and live client behavior have not been verified here.

The implementation migration is described in [DDUCK-IMPLEMENTATION-HANDOFF.md](DDUCK-IMPLEMENTATION-HANDOFF.md). `Z:\dduckprivate.bat` now builds the real private package from repository-owned implementation and an independent ABI reference. Loader error reporting is contained so a logging failure cannot prevent module disposal or context unloading. Offline verification does not establish live client or package-manager acceptance.

`repo.json` points to the latest public release in [McVaxius/dduck-public](https://github.com/McVaxius/dduck-public). Preparing the local workflow does not publish a release; it runs after an authorized push or manual GitHub dispatch.

## Community and license

Join [The Dumpster Fire on Discord](https://discord.gg/VsXqydsvpu) or [support McVaxius on Ko-fi](https://ko-fi.com/mcvaxius).

DDuck's existing AGPL-3.0-or-later license and third-party notices are retained. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
