# Implement DDuck private modules against the prepared public shell

## Task and boundaries

Implement the DDuck host/module split in `Z:\DDuck`, using the completed public shell in `Z:\dduck-public` and the current MOM/DhogNav repositories as working references. Complete the implementation, offline verification, and one private package at DDuck's existing version, **0.0.0.3**. Do not commit, stage, push, sync, publish, install, reload a plugin, or operate any live client.

All maintained code must live in Git repositories. DDuck implementation, build fixtures, configuration and tests belong in `Z:\DDuck`; public host code belongs in `Z:\dduck-public`. Shared numbered scripts and Python tooling already belong in `Z:\mom`. `Z:\!cryptography\DDuck` is only private identity storage and generated build/release material. Do not copy source there to build from it. The explicitly requested launcher is `Z:\dduckprivate.bat`.

Re-read Git status and current files before editing. Another CLI is actively changing DDuck; this preparation deliberately made no edits to `Z:\DDuck`. Preserve all current feature changes. The two accidentally sent Sasquatch/Chimera strategy prompts were withdrawn in this chat and are not part of this migration.

Use Git for ordinary tracked-source recovery. Preserve the established release workflow's external recovery safeguards. Stage 5's selected exact-file backup belongs under `Z:\srcbackups\<repo-leaf>\<timestamp>`. Add no blanket source snapshot, extra report, tracking system, or unrelated persistent machinery. Update DDuck's existing `CHANGELOG.md` and relevant README guidance rather than creating parallel documents.

## Already prepared

`Z:\dduck-public` is a newly initialized local Git repository with no commits or remote. Its complete source includes `Plugin.cs`, `IntroductionWindow.cs`, `IModule.cs`, `ModuleLoader.cs`, `ModulePackage.cs`, `PrivateManifest.cs`, `BuildInfo.cs`, `TrustAnchor.cs`, `DDuck.csproj`, `DDuck.json`, the original icon, and a focused offline host-contract regression. Everything is unstaged.

The host implements both `/dduck` and `/dd`, distinct main/config UI forwarding, all three access IPC endpoints, authenticated module loading, JSON/carrier binding, and safe registration rollback/disposal. `PrivateManifest.cs` was copied byte-for-byte from the working public host. This is a complete shell, not an introduction-only plugin.

The original DDuck AGPL-3.0-or-later `LICENSE` and third-party notices were retained; the public package calls the latter `THIRD-PARTY-NOTICES.txt`. DDuck's implementation repository still owns `THIRD_PARTY_NOTICES.md`. Do not replace DDuck's license with MOM/DhogNav's proprietary text.

A distinct RSA-3072 publisher identity already exists at:

```text
Z:\!cryptography\DDuck\publisher-private.pem
```

It was created with the shared workflow's existing protected-directory permission handling. `dduck-public\TrustAnchor.cs` contains its matching public key. Reuse this exact pair. Do not rotate, replace with another plugin's identity, copy the private key into a repo/package, or print it. Missing identity must fail with a restore instruction.

No publication destination was inferred: public `repo.json` is intentionally `[]`, and no remote repository/release exists as a result of this work. Package-manager DDuck support is not yet verified.

## Fixed host and package contract

| Item | Required value |
| --- | --- |
| Plugin/internal identity | `DDuck` |
| Display name | `Deep Ducking` |
| Public assembly / namespace | `DDuck` / `DDuck.PublicShell` |
| Public CLR ABI | `1.0.0.0` |
| Initial public release/FileVersion | `0.0.0.3` |
| Initial private project version | `0.0.0.3` |
| Private implementation assembly | `DDuck.Private` |
| Signed carrier assembly | `DDuck.Access` |
| Encrypted resource | `DDuck.Payload` |
| Embedded JSON resource | `DDuck.AccessManifest` |
| Installation directory | `GetPluginConfigDirectory()/tasks` |
| Installed files | `DDuck.Access.dll`, `DDuck.json` |
| Package name | `DDuck-v<private-version>.zip` |

The ZIP must contain exactly those two installed files at its root. The sidecar must match the JSON embedded inside the signed carrier byte-for-byte:

```json
{
  "SchemaVersion": 1,
  "InternalName": "DDuck",
  "AssemblyVersion": "0.0.0.3",
  "PublicFacingVersion": "0.0.0.3",
  "DalamudApiLevel": 15
}
```

Derive `AssemblyVersion` from `xapr/private/DDuck.Private.csproj` and `PublicFacingVersion` from `Z:\dduck-public\DDuck.csproj`. Future private versions are independent. Do not add timestamp revisions, `BuildRevision`, `PublicFacingRevision`, or a generated private-release metadata file as a version authority.

The public loader accepts existing schema-1 and schema-2 envelopes, authenticates RSA-PSS/SHA-256 before decrypting AES-GCM, checks ABI/resources/bounds, and clears plaintext/key buffers. Keep those safeguards, exact dependency identity checks, collectible loading, and cleanup. No numeric public-release restrictions may be introduced. Current shared Stage 4 intentionally emits a schema-1 envelope for compatibility; do not unnecessarily change its transport for MOM/DhogNav.

IPC supplied by the public shell:

```text
DDuck.Access.Directory.v1    Func<string>
DDuck.Access.Validate.v1     Func<byte[], bool>
DDuck.Access.Refresh.v1      Func<bool>
```

`Validate` authenticates/decrypts carrier bytes and clears the plaintext; JSON binding is separately enforced by the loader and package installer. `Refresh` reloads installed access and reports success. Endpoints are registered before initial module loading.

## Convert the existing DDuck entry point

Use the actual current `Z:\DDuck\DDuck\Plugin.cs`; do not replace it wholesale with another plugin's file.

1. Convert `DDuck.Plugin` from `IDalamudPlugin` into `DDuck.PublicShell.IModule`. It must remain parameterless-constructible without accessing game services. Move the current heavy constructor initialization into `Initialize(IDalamudPluginInterface pluginInterface, string hostVersion)`.
2. Guard against repeated initialization/disposed instances. Inject the existing `[PluginService]` members using the host interface (`pluginInterface.Inject(this)`), verify injection succeeded, and only then initialize diagnostics, config, adapters, native save observer, runtime, coordinator, IPC, windows, and DTR. Preserve all current services and initialization ordering dependencies. Adjust readonly/get-only fields carefully for delayed initialization.
3. The public shell owns command handlers and `UiBuilder.Draw/OpenMainUi/OpenConfigUi`. Remove duplicate registrations/removals from the private module. Retain private ownership of framework updates, existing business IPC, DTR, windows, diagnostics and automation resources.
4. Implement `OpenMainWindow()` by preserving current `OpenMainUi()` behavior. Implement `Draw()` by drawing DDuck's existing `WindowSystem`. Make `OnCommand(string command, string arguments)` public while retaining every existing command. Route `config` to existing `OpenConfigUi()`/`mainWindow.OpenSettings()` because the host sends `OnCommand("/dduck", "config")` for Dalamud's config action. Both aliases and their argument strings must continue to work.
5. Make disposal safe after any partial initialization and idempotent. Unsubscribe framework/business IPC callbacks and release every acquired resource even when another disposal or logging call fails. The loader calls `Dispose()` if `Initialize` throws. Callbacks must not run automation after disposal, and stale static references must not retain an unloaded module.
6. Preserve DDuck behavior and state: FULL STOP, bound attempts/save slots, campaign/reload continuation, party ownership, native dispatch protections, DTR, diagnostics, integrations, and current feature work. No automation policy change is requested by this migration.

The fixed interface is already in the public repo:

```csharp
public interface IModule : IDisposable
{
    void Initialize(IDalamudPluginInterface pluginInterface, string hostVersion);
    void OpenMainWindow();
    void OnCommand(string command, string arguments);
    void Draw();
}
```

Configuration compatibility needs a real regression: old saved type identity can be `DDuck.Configuration, DDuck`, whereas the migrated class will live in `DDuck.Private`. Check Dalamud's config deserialization and DDuck's `GetPluginConfig()`/`Configuration.Save()` path. An old config must load and round-trip without losing saved runs, stop flags, preferences, or ownership. Never silently substitute defaults and save over a file that failed to deserialize. Use synthetic representative configs offline, not live client configs. Keep serialized/reflection/IPC identities stable through protection.

If adding the same user-triggered **Check Updates** integration as the working private plugins, use `Z:\dhognav\DhogNav\AccessUpdateUi.cs` as the reference: a click invokes `AethertekPluginManager.CheckUpdates.v1` with `("DDuck", clipboardText)` and displays the returned status. Preserve the missing-APM handling. Do not add polling, startup update requests, direct network downloads, or automatic client actions. Verify the package manager recognizes DDuck before claiming end-to-end installation support; this preparation did not locate or modify APM.

## Add DDuck's repository-owned build inputs

Follow current `Z:\dhognav\Directory.Build.props` and `Z:\dhognav\xapr`, while preserving DDuck-specific resources/global usings, unsafe support, language level, target framework, and license.

Set explicit properties in `Z:\DDuck\Directory.Build.props`:

```text
RepositoryRoot   = Z:/DDuck/
PublicHostRoot   = Z:/dduck-public/
CryptoToolsRoot  = Z:/mom/
CryptoWorkspace  = Z:/!cryptography/DDuck/
```

Prefer repository-relative definitions for the first three, as in DhogNav. Redirect generated intermediates, caches, tests and private outputs into the DDuck workspace; do not redirect source. Preserve the existing full-development batch output and runtime companions.

Create/adapt these DDuck-owned inputs:

- `xapr/private/DDuck.Private.csproj`: version `0.0.0.3`, assembly `DDuck.Private`, explicit implementation source/resources from `$(RepositoryRoot)DDuck`, reference to `$(AccessHostAssembly)` with `Private="false"`, no distributable `IDalamudPlugin`, no packager, and import `$(CryptoToolsRoot)xapr/PrivateBuildIdentity.targets`. Keep the missing-reference error. Exclude public Plugin/loader/interface definitions from this assembly.
- `xapr/build-reference/BuildReference.csproj` and `ReferenceServices.cs`: adapt the same named DhogNav files. Build assembly `DDuck`, ABI `1.0.0.0`, linking `IModule`, loader, decoder, manifest and trust directly from `$(PublicHostRoot)`. The reference must have `PrivateBuildReference=true` and no `IDalamudPlugin` entry point/UI. `ReferenceServices.cs` supplies the log stub and version from file metadata.
- `xapr/carrier/Carrier.csproj` and `Carrier.cs`: adapt the exact current files, substituting DDuck identities/resources. Embed DDuck's original `LICENSE` and `THIRD_PARTY_NOTICES.md` from its repository (the source filename differs from DhogNav). Import the shared identity targets so no new revision metadata is emitted.
- `xapr/tool-lock.json`: reuse the established Obfuscar 2.2.50 tool lock/protection profile and existing verified dependency handling. Preserve serialized member names, service injection, module ABI, existing IPC names and resources. Do not add an independent protector/downloader implementation.
- `tests/PackageVerifier`: adapt the existing verifier to DDuck. Preserve its signature/decryption, JSON binding, no-revision, build-reference, ABI and loader lifecycle checks. Replace DhogNav-specific feature/type assertions with applicable DDuck coverage; simply replacing namespaces is insufficient.
- Adapt existing workflow/version-helper suites and any required lifecycle/Check Updates regressions. Existing DDuck feature tests remain in `DDuck.Regression`; preserve their source links and run them after migration.

Suggested `Z:\DDuck\xapr\project.json`:

```json
{
  "internal_name": "DDuck",
  "display_name": "Deep Ducking",
  "host_namespace": "DDuck.PublicShell",
  "public_project": "DDuck/DDuck.csproj",
  "public_manifest": "DDuck/DDuck.json",
  "public_icon": "DDuck/icon.png",
  "public_readme": "README.md",
  "build_info": "DDuck/BuildInfo.cs",
  "trust_anchor": "DDuck/TrustAnchor.cs",
  "private_project": "xapr/private/DDuck.Private.csproj",
  "github_repository": "",
  "repository_manifest": "DDuck.json",
  "repository_icon": "icon.png",
  "github_token_environment": "DDUCK_GITHUB_TOKEN",
  "discord_webhook_environment": "DDUCK_DISCORD_WEBHOOK",
  "public_release_description": "Deep Ducking public host.",
  "repository_feed": "repo.json",
  "signing_directory": "Z:/!cryptography/DDuck",
  "source_root": "Z:/DDuck",
  "public_source_root": "Z:/dduck-public",
  "workspace_root": "Z:/!cryptography/DDuck"
}
```

`public_project` and similar aliases retain the workflow's source-recovery archive layout. `workflow.input_path()` maps them to the real public repository; do not create duplicate host source under `Z:\DDuck\DDuck` to satisfy these strings. Keep publication configuration empty until a real destination is chosen.

## Extend the existing shared selectors, preserve the full build

In `Z:\mom\xapr\workflow.py`, add exact selector `DDuck` to `select_project` validation/help and `entry` argparse choices. In `Z:\mom\xapr\private_version.py`, add it to `entry` choices. The existing repository lookup via `name.lower()` locates `Z:\DDuck` on Windows. Keep one numbered-script/Python implementation; do not fork or relocate it. Update existing usage guidance to include DDuck and re-run MOM/DhogNav isolation tests after these shared changes. Their current versions may have advanced; never reset them from this handoff.

Stage 4 must independently build the DDuck ABI/loader reference, private implementation, protected carrier, and signed ZIP. It must not invoke Stage 1/2, require public binaries, or consult public candidate/selection pointers by default. Source collection must resolve each physical repository owner, exclude signing storage/other plugins, preserve historical receipt verification, and use existing external release recovery.

`Z:\dduck.bat` currently runs:

```bat
dotnet build Z:\dduck\DDuck.sln -c Debug -p:Platform=x64
```

Preserve its full-development build behavior and its existing output location. After `DDuck.Plugin` becomes an `IModule`, the development project needs a host entry point: selectively merge the MOM/DhogNav `LocalDevBuild` pattern, link public host sources from the public repo, include DDuck implementation only for that mode, and define `LOCAL_DEV_BUILD`. The prepared loader already contains `new global::DDuck.Plugin()` in that branch. Pass `-p:LocalDevBuild=true` from the batch file when adopting this pattern. Ensure only one `IDalamudPlugin` entry point exists. Do not accidentally turn the full batch build into a shell-only build or let private automation enter the public release package.

`Z:\dduckprivate.bat` already checks for DDuck's profile/private project, invokes the shared Stage 4 command below, preserves its exit code, and pauses. Its prerequisite failure was verified. Once this migration is implemented, the operator can simply run it:

```bat
Z:\dduckprivate.bat
```

Underlying noninteractive command:

```powershell
python -B "Z:\mom\4. Private DLL.py" --project DDuck --no-pause
```

Stage 5 preview, after adding the selector:

```powershell
python -B "Z:\mom\5. Private DLL Version.py" --project DDuck --version 0.0.0.3 --dry-run --no-pause
```

Do not bump versions as part of this migration. Preserve preview, invalid-input rejection, unchanged-version/cancellation behavior, atomic replacement, and the selected exact-file external backup for real future edits.

## Verification already completed here

- Public Release and Debug builds both succeeded with zero warnings/errors using .NET 10 and installed Dalamud API 15 references.
- `DDuck.dll` CLR identity is `DDuck, Version=1.0.0.0`; FileVersion/ProductVersion are `0.0.0.3`.
- `bin\x64\Release\DDuck\latest.zip` contains exactly `DDuck.dll`, `DDuck.json`, `icon.png`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`. Bytes match the built DLL/current companions; manifest version is correct; no private key is present.
- `PrivateManifest.cs` is byte-identical to the working DhogNav public contract.
- `tests/HostContract` passed **100 checks**, both with an ephemeral test publisher (host rejects it) and with DDuck's actual publisher identity (host accepts it). Real parser tests cover schema 1/2, independent versions, altered/missing/mismatched JSON, wrong identity, duplicate fields, carrier version mismatch, corrupted signatures, wrong keys, invalid schema/ABI, oversized packages, and legacy sidecar behavior. Host tests cover both aliases, collision ownership, config/main forwarding, IPC-before-load, partial construction, cleanup failures, and inert disposed callbacks.
- The focused suite stubs Dalamud/UI and module lifecycle. It does not prove real DDuck module initialization, collectible dependency loading, config migration, APM support, or live behavior.
- The private launcher reports the missing migration prerequisite and returns exit code 1. No real DDuck private package was built in this shell-preparation task.

Re-run the existing public check with:

```powershell
dotnet run --project Z:\dduck-public\tests\HostContract\HostContract.csproj -c Release
```

## Complete offline verification in the implementation task

Build and verify one real `DDuck-v0.0.0.3.zip`, including PackageVerifier and real loader initialization/failure/disposal/dependency checks against relocated inputs. Exercise Stage 4 with public outputs/pointers absent; establish that public artifacts remain unchanged. Verify the ABI build reference cannot be distributed as a plugin, all sidecar/security cases still fail closed, arbitrary private versions remain valid, and no new revision metadata is present.

Run the adapted workflow/version suites for DDuck and regression checks for both existing profiles, including source/profile isolation, invalid input, cancellation, unchanged versions, failed writes and legacy receipts. Run `DDuck.Regression` and configuration compatibility tests. Run the full `Z:\dduck.bat` flow and verify its expected DLL/runtime companions. Run the standalone public build and inspect its six-file ZIP again after any host edits. Finish by running `Z:\dduckprivate.bat` against the completed migration and verifying its real two-file private ZIP.

Inspect Git diff/status for all touched repos, preserve unrelated changes, and report exact artifact paths and remaining unverified behavior. No live test, installation, package-manager mutation, remote creation, commit or sync is authorized by this handoff.
