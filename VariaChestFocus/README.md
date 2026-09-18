AI disclaimer: AI tools were used in the development of this mod.

# VariaChestFocus

Choose what belongs in each chest, then press **H** to sort your inventory into nearby storage. Set allowed categories or individual items, choose which chests fill first, and give each chest a color.

## Features

- **Per-chest settings** saved with each container
- **Chest colors**: ten palette swatches, a **Custom** color picker, and **Original** to restore the chest's appearance; saved per chest and shown to other clients running this version of the mod
- **Categories** (wood, ores, food, trophies, etc.) and a **searchable item browser** for vanilla and modded items
- **Pin items**: adds every item type currently in the chest to that chest’s allowlist (categories unchanged); useful after stocking a chest by hand
- **Clear** / **Copy** / **Paste** settings between chests
- Open settings with the icon button to the left of **Place stacks**
- **Quick-sort order**: filtered chests first, then unrestricted chests; Critical → High → Medium → Low within each group, nearest first on ties. **Never** excludes either kind.
- **Hover summary**: see the chest's priority, allowed categories, and pinned item count before opening it. Hidden when you don't have access.
- **Quick-sort** keybind (default `H`): moves player inventory into nearby allowed chests, preserving equipped items and AzuEPI favorites
- **Hotbar protection**: numbered slots **1–8** are skipped by default; toggle `QuickSort → ProtectHotbar` to allow sorting from them
- Filters apply when putting items in; other mods, such as AzuCraftyBoxes, can still take items out
- Caps for large bases: range, max chests, max moves per press
- Saved filters for missing modded items stay until you remove them

## Compatibility

- **AzuContainerSizes** (size changes) — uses the container's live inventory
- **AzuCraftyBoxes** — inbound filter only
- **AzuEPI** — quick-sort skips equipment slots, quick slots, favorited item types, and items in favorited inventory slots. Spare stacks in normal inventory can still be sorted unless favorited. Changes to favorites take effect on the next press. If the mod cannot read AzuEPI's protections, sorting pauses and logs a warning.
- **AzuAutoStore** — its global rules and this mod's per-chest filters both apply to deposits. Use different keybinds if you use both quick-sort functions.

Ship and cart storage is skipped. Crossplay must be **disabled** for BepInEx.

## Config

`BepInEx/config/com.varia.chestfocus.cfg` or Azumatt Configuration Manager (F1):

| Setting | Default | Notes |
|---------|---------|--------|
| Enabled | true | Master toggle |
| SortKeybind | H | Quick-sort |
| ProtectHotbar | true | Skip numbered hotbar slots 1–8; personal setting, not server-synced |
| SortRange | 16 | Meters |
| MaxChests | 40 | Cap per press |
| MaxMoves | 60 | Cap per press |

Optional ServerSync: set `LockConfiguration = true` on the server to sync shared settings.

Version 0.1.15 protects the numbered **1–8** hotbar slots by default. In F1, set **QuickSort → ProtectHotbar** to `false` to allow quick-sort to store items from those slots. Equipped items, AzuEPI equipment/quick slots, and favorites stay protected. Changes apply to the next quick-sort press.

Version 0.1.13 changes the default quick-sort key to **H**, leaving **G** for Valheim's radial menu. On the first launch after upgrading, an existing plain `G` binding migrates to `H`; other shortcuts stay as configured. This migration runs once, so later manual rebindings are preserved.

## Usage

1. Open a chest → **Chest Focus icon** left of Place stacks
2. Set priority, toggle categories, search/toggle items
3. Optionally **Pin** what’s already inside
   and choose a **Color** swatch or **Custom** at the bottom of the panel
4. Close the panel (auto-saves if you own the chest)
5. Press **H** (or your bind) near storage to auto-store

An empty filter allows any item. Quick-sort tries filtered chests first, then puts remaining items into unrestricted chests. For example, a **Low** priority filtered chest fills before a **Critical** unrestricted chest. Within each group, higher priority comes first, with the nearest chest winning ties. Set priority to **Never** to exclude a chest from quick-sort. The old `IncludeUnsetChests` option no longer affects this order.

## Chest colors

Choose red, orange, gold, green, teal, blue, purple, pink, white, or charcoal, or make a custom color. The finish depends on the container:

| Storage | Color treatment |
| --- | --- |
| Wooden chest | Matte colored wood, preserving side handles and the front ring rim; wood inside the ring stays colored |
| Reinforced and personal chests | Matte colored wood with exposed metal fittings |
| Barrel | Colored staves and lid with natural metal hoops |
| Black metal chest | Colored wood panels beneath the original green metal frame; frame color, metallic reflections and noise retained |
| Yuleklapp, all three variants | Colored wrapping with printed light/dark pattern and original ribbons |
| Green pots, all three shapes | Colored ceramic glaze with original sheen and relief, including damaged material variants |

Paint keeps wood grain visible, while wrapping and ceramic retain their gloss. Lighting, rain, and snow still affect the chest's appearance. Other storage materials, including red or cracked pots and modded textures, use a simpler dye finish that may also tint metal fittings. Materials that cannot be recolored stay unchanged.

Broken chest pieces keep the color the chest had when it broke. Recoloring nearby chests does not change those pieces. Unloading the plugin restores their original appearance.

If you're upgrading from before 0.1.12, **reselect a palette swatch to use its revised color**. Saved colors stay as they were. **White** gives pale paint; **Original** restores the chest's original appearance. Copy/Paste includes color, and Clear filters keeps it. Setting only a color leaves a chest unrestricted.

Colors are saved with the world and appear on other clients running this version of the mod, usually within about half a second after the game syncs the chest. Daylight and torchlight rendering still need in-game verification; see the [color checks](../tests/VariaChestFocus.Tests/README.md).

## Item filters

The browser has **Items**, **Pinned**, **No icon**, and **Missing** views. Items without icons have their own view because some are internal creature equipment, while others are usable modded items. You can still select them. Hover an item for its full name, prefab identifier, and whether it is allowed. **PIN** marks an item you selected; **CAT** marks one allowed by a category.

**Clear filters** asks for confirmation before removing category selections and pinned items. It keeps the chest's contents, priority, and color.

Categories can overlap: cooked meat can match both **Meat** and **Food**, and barley can match both **Seeds** and **Cooking**. Categories use the game's item stats and recipes, with built-in lists for materials the game does not clearly identify.

Quick-sort respects wards and private access, and skips chests in use. Your client must own the chest before sorting into it; opening a skipped chest requests ownership. Partial stacks can fill multiple chests, with each successful transfer counting against `MaxMoves`.

Filters apply to inventory moves, drag swaps, and **Place stacks**. You can still rearrange items already inside a chest after changing its filters. Some mods insert items without checking whether the chest accepts them and may bypass the filters; report those if you run into them.

## Picking a custom color

Choose **Custom** beside the palette. Use the rainbow **Hue** slider, then click or drag inside the shade square. **Color strength** and **Brightness** sliders offer another way to adjust the shade. Compare the **Current** and **New** tint swatches, then press **Apply color** to save. **Cancel**, closing the inventory, or switching chests discards the unconfirmed selection. The preview shows the tint color; chest textures and lighting affect its appearance in the world.

For an exact color, enter six hex digits such as `#709FE0` (the `#` is optional). Invalid or incomplete entries disable Apply until corrected or replaced using the picker. Hex entry captures gameplay keybinds just like item search. Sliders also support left/right adjustments through the UI's keyboard/controller navigation, with up/down moving between picker controls. Custom colors use the same saved settings and Copy/Paste behavior as palette colors.

## Development

Build without deploying to Gale: `dotnet build VariaChestFocus/VariaChestFocus.csproj -c Release -p:DeployMod=false`.

Test instructions and in-game checks are in the [test README](../tests/VariaChestFocus.Tests/README.md).

## Version 0.1.16

Quick-sort skips tombstones. Unloading the plugin closes its settings panels and restores chest appearances.

`Appearance → MaxPaintTextureMiB` limits memory used by painted color textures to **64 MiB** by default. When that limit is reached, new colors use the simpler dye finish, which may also tint metal fittings. Existing painted chests and debris keep their appearance. Lowering the limit takes effect as old textures are released. Set it to `0` to use dye for new colors, or raise it for a base with many different chest colors. This is a local setting and does not include source masks or shared dye textures.

## License

MIT — see [LICENSE](../LICENSE).
