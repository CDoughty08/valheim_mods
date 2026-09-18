AI disclaimer: AI tools were used in the development of this mod.

# VariaFood

Change how long food lasts, how much health, stamina, and eitr it gives, and how quickly those stats regenerate.

- Disable food decay (full HP / stamina / eitr until expiry)
- Duration multiplier
- Separate multipliers for health, stamina, and eitr
- Health regen follows vanilla food regen (× multiplier)
- Stamina / eitr regen can scale with the food's **stat boost** or its health-regen stat
- Per-stat regen mode: **PerTick** (every 10s) or **PerSecond** (smooth; tick amount ÷ 10)
  - Defaults: health PerTick, stamina/eitr PerSecond + StatBoost
- Food tooltips show separate Healing / Stamina regen / Eitr regen lines with `/tick` or `/sec`
- **4 solid-food slots + 1 drink-only slot** (5 HUD icons); configurable counts under `Slots`
- Drink slots show a muted mead/flask watermark so empty drink slots stay recognizable

## Extra food / drink slots

With `ExtraFoodSlots = true` (default):

- Solid food uses up to `FoodSlotCount` slots (default **4**)
- `DrinkSlotCount` sets the number of drink slots (default **1**). Solid food cannot use these slots or replace drinks
- Empty drink slots keep a faint vanilla mead icon in the background (active drinks draw on top)
- Meads and potions with no food stats show their status effect's remaining time. `DrinkSlotDuration` is used when the drink has neither a food duration nor a timed effect. These timers are unaffected by `DurationMultiplier` or the world's food drain rate
- Potion icons do not grant extra regeneration or count toward food-eaten statistics
- Drinking while the drink slot is already full still applies the potion — it just is not shown in the bar
- Potion icons do not use food capacity and are cleared when you reload your character. Vomiting does not remove them. Drinks with food stats are saved normally and appear before potion icons when display space is limited
- Slot-count changes apply immediately to gameplay and the HUD, including server-synced settings. Reducing capacity preserves already-eaten foods until expiry; only slots within the new limits are displayed
- Do **not** run alongside Fatty or other food-slot expanders
- Turning `ExtraFoodSlots` off with 4–5 foods already saved keeps those entries until they expire (HUD shows the first three)

`ExtraDrinkKeywords` accepts comma-separated whole-word tokens for modded drinks.

## Stamina / eitr StatBoost scaling

With `StaminaRegenScale = StatBoost` and a multiplier of **1.0**, stamina regenerates at **2.5% of the food's stamina boost per second**:

| Food | Stamina | Regen |
|------|---------|-------|
| Grilled Neck | 8 | 0.2/sec |
| Honey | 35 | 0.875/sec |
| Roasted Crust Pie (Ashlands) | 100 | 2.5/sec |

Three foods that each give 100 stamina provide **7.5/sec** in total. Lower the multiplier if that feels too generous. Eitr uses the same calculation.

Configure in-game with **Azumatt Official BepInEx Configuration Manager** (F1), or edit `BepInEx/config/com.varia.food.cfg`.

## ServerSync (optional)

By default each player uses their own config. To enforce server settings:

1. Install the mod on the dedicated server **and** all clients
2. Set `LockConfiguration = true` on the server

With lock off (default), the mod stays client-side even if the server also has it. Launch with **crossplay disabled** for BepInEx.

## Development

Build without deploying to Gale: `dotnet build VariaFood/VariaFood.csproj -c Release -p:DeployMod=false`.

Run tests: `dotnet run --project tests/VariaFood.Tests`. See the [test README](../tests/VariaFood.Tests/README.md) for coverage and in-game checks.

## License

MIT — see [LICENSE](../LICENSE).
