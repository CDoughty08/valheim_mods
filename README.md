# Varia Valheim mods

BepInEx mods for Valheim 1.0+. Install them through [Gale](https://github.com/kesomannen/gale) or another mod manager that supports Thunderstore packages.

## Mods

| Mod | Description |
|-----|-------------|
| [VariaFood](VariaFood/) | Configurable food duration, HP/stamina/eitr, regen, tooltips, no-decay |
| [VariaWeight](VariaWeight/) | Weightlifting skill: XP from moving under load; configurable carry bonus curve |
| [VariaTracking](VariaTracking/) | Creature tracking skill, map markers and awareness progression |
| [VariaChestFocus](VariaChestFocus/) | Per-container filters, quick-sort and chest colors |

## Requirements

- Valheim 1.0+ (Steam)
- [Gale](https://thunderstore.io/) profile with [BepInExPack Valheim 5.4.2351](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- Optional: [Official BepInEx Configuration Manager 19.0.0](https://valheim.hexium.gg/mods/Azumatt/Official_BepInEx_ConfigurationManager) (F1)
- Optional for ChestFocus: [AzuEPI 2.6.0](https://valheim.hexium.gg/mods/Azumatt/AzuExtendedPlayerInventory) (equipment, quick-slot and favorite protection)
- **Crossplay disabled** when using BepInEx mods

## Build

```powershell
dotnet build VariaFood\VariaFood.csproj -c Release
```

Default Steam and Gale paths are in `Directory.Build.props`. To use different paths, copy `Environment.props.example` to `Environment.props` and edit it.

All four projects share the same controls. Release builds package independently of Gale and deploy only when the profile exists and `DeployMod=true`. Debug builds neither deploy nor package automatically.

```powershell
dotnet build VariaWeight/VariaWeight.csproj -c Release -p:DeployMod=false
```

This produces the DLL and a zip under `bin/Release/`. Add `-p:PackMod=false` to build just the DLL. The older `DeployToGaleOnBuild=false` option also disables deployment.

## Development

Each mod has its own folder and targets .NET Framework 4.8, BepInEx 5, and HarmonyX. Builds use game assemblies from `VALHEIM_INSTALL` and publicize them locally. Shared build settings are in `Directory.Build.props`; packaging and deployment targets are in `Shared/ModPackaging.targets`.

### Dependency baseline (2026-09-26)

Build package versions are shared by all four mods and the regression runners through `Directory.Build.props`:

| Dependency | Version | Role |
|------------|---------|------|
| [BepInEx.Core](https://nuget.bepinex.dev/v3/package/bepinex.core/index.json) | 5.4.21 | Latest published stable BepInEx 5 NuGet API; runtime pack is 5.4.2351 (BepInEx 5.4.23.5) |
| [HarmonyX](https://www.nuget.org/packages/HarmonyX/2.16.1) | 2.16.1 | Compile reference; BepInEx supplies Harmony at runtime |
| [AssemblyPublicizer](https://www.nuget.org/packages/BepInEx.AssemblyPublicizer.MSBuild/0.4.3) | 0.4.3 | Latest stable build tool; excludes 0.5 beta releases |
| [ILRepack](https://www.nuget.org/packages/ILRepack.Lib.MSBuild.Task/2.0.48) | 2.0.48 | Merges ServerSync into each mod |
| [ServerSync](https://github.com/blaxxun-boop/ServerSync/releases/tag/v1.20) | 1.20 | Latest release; vendored DLL verified against the upstream SHA-256 |

AzuEPI and Configuration Manager remain optional integrations, with current releases on Hexium. Their deprecated Thunderstore listings contain older versions. ChestFocus's existing slot and favorite API checks pass against the installed AzuEPI 2.6.0 DLL. No AzuEPI or Configuration Manager DLL is bundled in Varia packages.

For a new mod, copy an existing project's structure, use a `Varia` name and a lowercase `com.varia.*` plugin GUID, and include a manifest, README, and icon. Keep the version in the project file, `PluginVersion`, and `manifest.json` in sync. Gale folders use `$(VariaAuthor)-$(VariaModName)`.

Keep game assemblies, decompiled source, build output, local `Environment.props` files, and BepInEx configs out of version control. Local source dumps belong in the ignored `_decompile/` folder.

All mod projects exclude HarmonyX's transitive `MonoMod.Backports` compiler reference
through `Directory.Build.targets`. BepInEx 5 does not ship this library; allowing it
into compilation can silently make interpolated messages require it at runtime.
Before deploying, run the [release dependency checks](tests/VariaPackaging.Tests/README.md)
on the built DLLs or release ZIPs as well as the behavior tests.

## License

MIT — see [LICENSE](LICENSE).
