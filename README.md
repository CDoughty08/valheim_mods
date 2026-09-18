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
- [Gale](https://thunderstore.io/) profile with `denikson-BepInExPack_Valheim`
- Optional: Azumatt Official BepInEx Configuration Manager (F1)
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

For a new mod, copy an existing project's structure, use a `Varia` name and a lowercase `com.varia.*` plugin GUID, and include a manifest, README, and icon. Keep the version in the project file, `PluginVersion`, and `manifest.json` in sync. Gale folders use `$(VariaAuthor)-$(VariaModName)`.

Keep game assemblies, decompiled source, build output, local `Environment.props` files, and BepInEx configs out of version control. Local source dumps belong in the ignored `_decompile/` folder.

## License

MIT — see [LICENSE](LICENSE).
