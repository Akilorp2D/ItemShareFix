# Development

## Prerequisites

- .NET SDK capable of building the solution and running the `net8.0` MSTest project;
- a local Risk of Rain 2 installation;
- BepInEx installed in that game profile so the plugin project can reference the required runtime assemblies.

The plugin project resolves the game path from either the MSBuild property `RiskOfRain2Dir` or `ROR2_GAME_DIR`.

## Build and test

Standard toolchain:

```text
dotnet restore ItemShareFix.sln
dotnet build ItemShareFix.sln -c Release --no-restore -warnaserror
dotnet test tests/ItemShareFix.Core.Tests/ItemShareFix.Core.Tests.csproj -c Release --no-build
```

If the game directory is not supplied through `ROR2_GAME_DIR`, pass it as an MSBuild property:

```text
dotnet build ItemShareFix.sln -c Release --no-restore -warnaserror -p:RiskOfRain2Dir="C:\path\to\Risk of Rain 2"
```

No PowerShell is required or supported by the release workflow.

## Deterministic build settings

`Directory.Build.props` enables deterministic/CI compilation and maps the repository root to `/_/` through standard Roslyn/MSBuild `PathMap`. This is intended to remove developer-machine source-root drift from emitted debug/source paths.

The presence of deterministic properties is **not** by itself proof of cross-root byte identity. Release validation should build from two clean roots and compare exact DLL/PDB/package hashes before classifying the build as byte-reproducible.

## Repository hygiene

Do not commit:

- `bin/`, `obj/`, `.vs/`, IDE user state;
- local game/runtime DLLs;
- `TestResults/`, TRX, coverage, runtime logs;
- private QA evidence, handoff ZIPs, operator kits, or internal iteration packages;
- secrets/tokens or local absolute machine paths.

## Release package layout

The intended Thunderstore package root is:

```text
manifest.json
README.md
CHANGELOG.md
LICENSE
icon.png
plugins/ItemShareFix/ItemShareFix.dll
plugins/ItemShareFix/ItemShareFix.Core.dll
```

Create the package only from a fresh validated Release build. Smoke-test the exact archive bytes intended for upload.
