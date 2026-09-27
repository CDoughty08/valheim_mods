# Varia release dependency checks

Use .NET 10 to inspect the actual merged DLLs or packaged ZIPs before deployment:

```powershell
dotnet run --project tests/VariaPackaging.Tests -- VariaFood/bin/Release/VariaFood.dll VariaTracking/bin/Release/VariaTracking.dll VariaWeight/bin/Release/VariaWeight.dll VariaChestFocus/bin/Release/VariaChestFocus.dll
```

Pass release ZIP paths instead to also check the five expected package files and
DLL/manifest version agreement. Every artifact is checked even if an earlier one
fails; any failure produces a nonzero exit code. Inspection reads metadata without
loading or executing plugin code and requires no game assemblies or NuGet packages.

The supported runtime assembly list represents the libraries these mods use from
Valheim, .NET Framework, and BepInEx 5. A new dependency must be deliberately supported
before changing that list. In particular, HarmonyX's transitive `MonoMod.Backports`
library must not become a runtime dependency through C# string interpolation.

These checks do not exercise Unity, plugin initialization, or Harmony patch ordering.
Run the behavior regression runners and in-game checks separately.
