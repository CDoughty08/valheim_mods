# VariaFood regression checks

Run `dotnet run --project tests/VariaFood.Tests` from the repository root.

This net48 console runner tests the food code and applies its HarmonyX
patches to game API stubs. It covers expiry, force refreshes, duration
and stat multipliers, pending-eat cleanup on exceptions, regeneration, live potion
timers, category limits, HUD slot mapping, classification, tooltips, and load cleanup.

It does not run Unity, render the HUD, simulate networking, or validate other mods'
Harmony patch ordering. Build separately against the installed game:

```powershell
dotnet build VariaFood/VariaFood.csproj -c Release -p:DeployMod=false
```

In-game checks:

- Drink before eating; confirm the icon and timer appear on the flask watermark.
- Eat four foods; let multiple foods expire together and confirm all timers advance.
- Try 1 food + 1 drink and 6 foods + 2 drinks after recreating the HUD.
- Use a potion with reduced food drain; confirm its timer follows the status effect.
- Set healing to zero and inspect a food tooltip; test continuous healing while injured.
- Save/reload with a potion and a stat-bearing drink; only the potion entry should disappear.
- Check the BepInEx log for patch errors, including with your normal modpack.

HUD tests cover changing slot counts, potion placement, grouped and flat layouts, toggling the feature, and cleanup. They also check that potion icons stay separate from food entries and clear on load or when disabled. Rendering, Unity object cleanup, and server configuration timing still need in-game checks.
