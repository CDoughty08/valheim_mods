# Chest Focus regression checks

Run `dotnet run --project tests/VariaChestFocus.Tests` from the repository root.

The runner tests the mod's code with game API stubs. It checks
serialization, categories, catalog refresh, transfer guards, sorting conservation,
save batching, hover summaries/access guards, AzuEPI API selection, and live item/slot favorite protection (including character isolation and API failures). It uses the real Harmony instruction helpers.
It does not simulate Unity lifecycle, networking, native inventory internals, or apply
Harmony patches to a running game.

Material cleanup tests use the mod's material owner, debris component, and fragment
transpiler with renderer and texture-cache stubs. They cover last-chest removal,
multiple fragment owners, recolor snapshots, fallback dye, plugin unload, idempotent release,
and preserving another mod's material replacement. The optional installed-game check also
verifies the material-assignment call site in `Destructible.CreateFragments`. These checks
do not execute GPU allocation, Unity's native destruction order, or in-game rendering.

Optional positional arguments are the installed `assembly_valheim.dll` path and AzuEPI
DLL path. These add read-only metadata checks for the patched bulk-transfer call sites,
game method signatures, and AzuEPI delegate signature. No game assemblies are copied.

Compile against the installed game without deploying:

```powershell
dotnet build VariaChestFocus/VariaChestFocus.csproj -c Release -p:DeployMod=false
```

In-game checks: drag an excluded item out onto an occupied player slot; rearrange old
excluded chest contents; use Place stacks with mixed allowed/excluded items; sort into
nearly full chests; verify ward and AzuEPI protections; reopen after save/reload; repeat
with a second client to check ownership changes. Inspect the BepInEx log for patch errors.

UI follow-up checks: hover ellipsized names and page while a tooltip is visible; verify
Items/No icon/Pinned/Missing views retain selections; open Clear filters, cancel, then
confirm; verify categories/pins clear while priority and contents remain; change chests
with a confirmation pending; test controller focus and UI scaling at smaller resolutions.

World hover checks: inspect filtered, unrestricted, and Never-priority chests; verify
unrestricted hover says "after filtered chests" and only Never says "excluded".
Verify long category selections stay compact and pinned counts update.
Check denied wards/private chests and settings changed by another player. Confirm the
summary fits beneath vanilla interaction hints alongside other installed hover mods.

Color checks: choose each palette swatch on wooden, reinforced, and black metal chests;
check open/closed lids and damaged models; verify a neighboring chest stays unchanged.
Choose Original, toggle the mod off/on, and reload the world. Copy/Paste must include
color; Clear filters must preserve it. A color-only chest must participate after
filtered chests, including with an old IncludeUnsetChests=false config. Verify second-client updates, ownership
changes and denied access. Test the taller panel at small UI resolutions and check
custom chest shaders. These rendering/networking checks require the running game;
the automated suite covers tint persistence, parsing, copying and ownership guards.

Dye rendering checks: compare saved Red against Original in daylight, torchlight, and
darkness on wooden, reinforced and black metal chests. Check recognizable hue, retained
grain, metal reflections, transparent/cutout parts, snow, damage and hammer highlights.
White should give a pale finish; Original must restore the original texture and color.
Recolor repeatedly, unload/reload an area and disable the mod; check logs for texture
preparation failures and confirm other chests/materials are unaffected. Automated checks
cover the luminance curve; GPU readback, gamma handling, material cleanup and the final
appearance require in-game verification.

Paint checks (0.1.12): reselect the new Teal swatch on a wooden chest and
compare it against Original in daylight and torchlight. The side handles and front
ring rim should retain their natural color, while the wood inside the ring stays
painted. Check stronger dark grain and original surface relief with reduced gloss,
including the lid in both open and closed positions. Repeat on the
ironchest and strongbox atlases, checking metal bands and seams. Verify opaque/alpha
regions, rain/snow and hammer highlights still behave normally. Check both old saved
RGB values (unchanged) and revised palette values. Recolor several matching chests to
different/same shades; reset one and unload/reload the area while the others retain
their paint. Unknown chest materials should continue to use dye fallback. The runner
checks production paint blending, timber/metal coverage, side-handle and front-ring
UV exclusions, the colored ring interior, grain contrast and highlight differences;
it does not execute GPU rendering or shader lighting.

Extended storage checks: use palette and custom colors on the barrel, black metal
chest, all three Yuleklapp variants and all three green pot shapes. Barrel staves/lid
and black-metal wood panels should recolor while hoops and the green metal frame
retain their natural appearance. Gift ribbons should keep their original color and
printed light/dark wrapping details should remain readable. Pots should retain ceramic
sheen and relief, including damaged models and LOD transitions. Check open/closed
lids, snow overlays, daylight/torchlight, Original/reset, remote recoloring, unloading,
and mod disable/re-enable. Red/cracked pots and unknown materials retain dye fallback.
Automated checks cover the exact atlas profiles, timber/metal and wrapping/ribbon
separation, green/damaged glaze, finish settings, and the snow-overlay exclusion rule.

Debris checks (0.1.12): deconstruct and damage-destroy colored wooden/reinforced/personal
and black metal chests, barrels, gifts and green pots. Repeat with the last colored
container in the area, multiple matching colors, and a fallback-dye material. Fragments
should keep their selected finish until vanilla despawns them, with no pink surfaces.
Verify this on the owner and another client, with lids open/closed and damaged pot
fragment roots. Recolor/reset neighboring storage while fragments exist, then leave the
area; check resource cleanup and logs. Plugin unload while debris is visible should
restore vanilla materials without invalid references. Check the result in game.

Quick-sort checks: Low filtered must beat Critical unrestricted, with priority and
distance ordering within each group. Fill a matching chest and verify overflow and
unmatched items reach unrestricted storage. Repeat with Never, ownership/access denial,
and chest/move limits; saved inventory totals must remain unchanged by transfers.

Quick-sort 0.1.13: after upgrading from plain G, confirm H sorts and G only opens the
radial menu. Other custom bindings (including modified G) must survive the upgrade;
manual rebindings after the first migration must survive restart. With AzuEPI, Alt-click
an item type and an inventory slot; quick-sort must preserve all stacks of that type
and the item in that slot while storing unprotected items. Unfavorite and repeat, then
switch characters to verify favorites are per character. These input/migration checks
require the running plugin; automated sorting checks use favorites API stubs.

Quick-slot 0.1.14: remove favorites from items in AzuEPI's quick slots and confirm H
keeps them while sorting spare stacks from normal inventory. Move an item out of its
quick slot and repeat; it should now sort unless equipped or favorited. Automated
checks cover multiple unfavorited quick-slot items when grid lookup returns false,
spare-stack overflow, changed quick-slot contents, and quick-slot API failure.

Hotbar 0.1.15: confirm QuickSort → ProtectHotbar defaults to true in F1 and H leaves
slots 1–8 untouched. Disable it and sort again; unprotected hotbar items should store.
Re-enable it without restarting and verify protection returns. The setting is local
even with server configuration locking. Automated checks cover all eight positions,
regular inventory, unnumbered columns in wider inventories, switching the setting off,
and retaining equipped/AzuEPI/favorite protection when hotbar sorting is enabled.

Custom picker checks: open Custom from both Original and a saved color, drag every edge
of the shade square, adjust all three sliders, and compare Current/New swatches. Apply,
reopen and confirm the shade/hex survive. Cancel and close the inventory while editing;
neither should save the preview. Switch chests with the picker open and lose ownership
before Apply. Test valid/invalid hex, including black and white; gameplay shortcuts must
stay blocked while typing. Confirm the underlying panel cannot be edited through the
modal, controller/keyboard slider adjustments and navigation work, and scaling keeps
all controls visible. Run alongside the palette multiplayer and reload checks above.

Version 0.1.16 adds checks for tombstone exclusion and mip-inclusive paint memory accounting. In game, lower Appearance → MaxPaintTextureMiB, add distinct colors and check the dye fallback; existing painted debris must stay valid. Destroy/unload the plugin with the settings/color panel open and confirm its controls disappear without saving.
