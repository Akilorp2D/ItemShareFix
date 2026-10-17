# Build from source

Use a .NET SDK capable of building `netstandard2.1` and running the `net8.0` MSTest
project. Release 1.2.0 was verified with **.NET SDK 10.0.400**. Exact binary
reproduction should use that SDK and the same game/mod reference assemblies.

Required local references:

- Risk of Rain 2 1.5.0: `Risk of Rain 2_Data/Managed`;
- BepInEx core assemblies: the game's or your mod-manager profile's `BepInEx/core`.

The source archive does not contain game, BepInEx or third-party mod DLLs. Point
MSBuild to your legitimate installation. NuGet dependencies are restored normally.

From the directory containing `ItemShareFix.sln`, for example:

```text
dotnet restore ItemShareFix.sln
dotnet build ItemShareFix.sln -c Release --no-restore -warnaserror -p:RiskOfRain2Dir="C:/Games/Risk of Rain 2" -p:BepInExCoreDir="C:/ModProfiles/MyProfile/BepInEx/core"
dotnet test tests/ItemShareFix.Core.Tests/ItemShareFix.Core.Tests.csproj -c Release --no-build --no-restore
```

Outputs:

```text
src/ItemShareFix.Plugin/bin/Release/netstandard2.1/ItemShareFix.dll
src/ItemShareFix.Core/bin/Release/netstandard2.1/ItemShareFix.Core.dll
```

Install both DLLs together. The `tests/ItemShareFix.Core.Tests/Fixtures` directory
contains required regression test data, including historical source contracts;
these files are test fixtures, not development reports. Keep them when running
the tests. The tests discover the source root through `ItemShareFix.sln`.

The archive contains source, tests, resources and public documentation only.
Development reports, agent instructions/skills, logs and build outputs are supplied
separately or excluded.
