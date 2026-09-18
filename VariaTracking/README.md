AI disclaimer: AI tools were used in the development of this mod.

# VariaTracking

Adds a **Tracking** skill that shows nearby creatures on the minimap and large map. You start with grey dots in a narrow cone ahead of you. As you level up, you can see farther, detect creatures behind you, and learn more about them.

## Features

- Earn **Tracking** XP while moving with creatures in range
- **Forward cone** that widens to a full circle at level **50** (default)
- Semi-transparent **range wedge / circle** on mini / large map
- Unlock hostility colors, star rings, boss colors, creature names, and tracking through unexplored areas as you level up
- Early names for **claimed trophies** (and trophy-less species by default)
- Crouch or stand still to extend your range; the bonus grows with your level
- Low-skill **position jitter** that tightens as you level
- Hover tooltips on the large map (M): real names or `???`
- Fog of war respected until the pierce unlock
- Adjust range, XP, and unlock levels in the config; optionally sync settings from the server

## Progression (defaults)

| Level | Unlock |
|------:|--------|
| 1 | Grey blips, ~70° cone, `???` names (trophy / trophyless exceptions) |
| 10+ | More dots, up to the configured MaxDots limits |
| 20 | Hostile vs passive colors |
| 30 | Starred rings |
| 50 | Full 360° awareness |
| 60 | Distinct boss color |
| 80 | Names for all detected creatures |
| 100 | Track creatures in unexplored areas |

**Range** grows from `MinRange` to `MaxRange`, with `Balance` controlling how much you gain at early or late levels. The cone widens from `ConeStartDegrees` to 360° by `ConeFullLevel`.

**Hunting posture:** crouch, or stand still briefly, for a range bonus: `1 + PostureFloor + (level/100)*PostureBonusAt100` (default ~+10% early, +35% at 100).

**Trophy study:** picking up a creature’s trophy can reveal its name before level 80.

## Defaults (range / XP)

| Setting | Default | Meaning |
|---------|---------|---------|
| Min range (level 0) | `40` | World meters |
| Max range (level 100) | `200` | World meters |
| Balance | `50%` | Linear range progression |
| Fog pierce level | `100` | Track creatures through unexplored map areas |
| XP rate | `0.25` | Skill XP per second while moving with trackable targets in range |

**Hostile** means a boss or a creature the game considers your enemy. Animals that only flee stay passive. Neutral creatures fighting other enemies do not become hostile just because they are alerted.

**Boss controls:** `ShowBoss` turns boss markers on or off at any level. `BossUnlockLevel` sets when they get a distinct color. Until then, bosses use grey or the hostile color, depending on your unlocks. `ShowPassive` and `ShowHostile` filter other creatures only after you unlock hostility colors.

**Updates:** `UpdateIntervalSeconds` controls how often the mod looks for nearby creatures. Creatures already found update every frame, including when they move out of range, die, or become tame. New arrivals may take until the next scan to appear. When there are more visible creatures than the dot limit allows, the nearest ones are shown.

**Modded trophies:** early names use each creature's current trophy drops, including drops added or changed by other mods.

Configure with **Azumatt Configuration Manager** (F1) or `BepInEx/config/com.varia.tracking.cfg`.

## ServerSync (optional)

By default each player uses their own config. To enforce server settings:

1. Install the mod on the dedicated server **and** all clients
2. Set `LockConfiguration = true` on the server

With lock off (default), the mod stays client-side even if the server also has it. Launch with **crossplay disabled** for BepInEx.

## Manual test checklist

Build without deploying or packaging: `dotnet build VariaTracking/VariaTracking.csproj -c Release -p:DeployMod=false -p:PackMod=false`.

Run tests: `dotnet run --project tests/VariaTracking.Tests/VariaTracking.Tests.csproj -c Release` (requires .NET 10 SDK). See the [test README](../tests/VariaTracking.Tests/README.md) for coverage.

- [ ] Level ~1: grey dots only inside forward wedge; hover shows `???`
- [ ] Turning: dots at cone edge don’t strobe (hysteresis)
- [ ] Crouch: range wedge grows smoothly
- [ ] Level 20+: passive/hostile colors
- [ ] Level 30+: star rings on 1★+
- [ ] Level 50: full circle instead of wedge
- [ ] Level 60+: bosses use boss color
- [ ] Claim trophy → that species names early; level 80 → all names
- [ ] Level 100: dots through unexplored fog
- [ ] Death applies the normal skill drain
- [ ] Neutral Dvergr fighting another creature stay passive; aggravated Dvergr become hostile
- [ ] At a fog / cone edge, jitter does not move an otherwise visible dot across that boundary
- [ ] Stand up or reduce range: previously scanned targets outside the new radius disappear immediately
- [ ] Switch characters / reconnect: XP, posture, and name knowledge do not carry over
- [ ] Disable ShowBoss with ShowHostile enabled: bosses disappear even before hostility unlock
- [ ] Set BossUnlockLevel below HostilityUnlockLevel: boss color unlocks at the configured boss level
- [ ] Two tracked creatures exchange distance order: the nearest dot wins the cap immediately
- [ ] Tame/kill the only tracked target: marker and XP eligibility stop before the next discovery scan

## License

MIT — see [LICENSE](../LICENSE).
