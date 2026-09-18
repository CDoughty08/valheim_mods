AI disclaimer: AI tools were used in the development of this mod.

# VariaWeight

Adds a **Weightlifting** skill. Carry items while moving to earn XP; heavier loads earn more. As the skill levels up, you can carry more weight.

## Defaults

| Setting | Default | Meaning |
|---------|---------|---------|
| Baseline carry bonus (level 0) | `0` | No bonus at skill 0 |
| Max carry bonus (level 100) | `300` | +300 at 100 (vanilla base 300 → 600) |
| Balance | `50%` | Linear progression |
| XP rate | `0.2` | At 300 carried weight, ~0.2 skill XP per second while moving |

**Balance** controls when you earn the carry bonus. At `50%`, each level adds the same amount. Higher values give more of the bonus at later levels; lower values give more of it early on.

Configure with **Azumatt Configuration Manager** (F1) or `BepInEx/config/com.varia.weight.cfg`.

## ServerSync (optional)

By default each player uses their own config. To enforce server settings:

1. Install the mod on the dedicated server **and** all clients
2. Set `LockConfiguration = true` on the server

With lock off (default), the mod stays client-side even if the server also has it. Launch with **crossplay disabled** for BepInEx.

## Version 0.0.4

XP accounts for time spent moving and changes in carried weight. Temporary skill bonuses no longer stop you from leveling the base skill. Switching characters clears any pending XP.

## Development

Build without deploying: `dotnet build VariaWeight/VariaWeight.csproj -c Release -p:DeployMod=false`. Add `-p:PackMod=false` to skip packaging.

Weight and Tracking share the movement XP code. Run its tests with `dotnet run --project tests/VariaTracking.Tests`; see the [test README](../tests/VariaTracking.Tests/README.md) for details.

## License

MIT — see [LICENSE](../LICENSE).
