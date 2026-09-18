using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using VariaChestFocus;
using UnityEngine;

int passed = 0;
void Check(string name, Action test)
{
    test();
    Console.WriteLine("PASS " + name);
    passed++;
}
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
ItemDrop.ItemData Item(string name, int stack = 1, ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.Material)
{
    var go = new GameObject { name = name };
    var data = new ItemDrop.ItemData { m_dropPrefab = go, m_stack = stack, m_gridPos = new Vector2i(0, 1) };
    data.m_shared.m_name = name; data.m_shared.m_itemType = type;
    go.Drop = new ItemDrop { gameObject = go, m_itemData = data };
    return data;
}
object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
ChestFocusConfigSnapshot Config(int moves = 60) => new() { Enabled = true, ProtectHotbar = true, SortRange = 16, MaxChests = 40, MaxMoves = moves };
Container Chest(string prefab, int capacity = 100, float distance = 0)
{
    var container = new Container(); container.m_inventory.Capacity = capacity;
    container.transform.position = new Vector3 { x = distance };
    ContainerRegistry.Register(container);
    var settings = new ChestSettings(); settings.AllowedPrefabs.Add(prefab);
    ChestSettingsStore.TrySet(container, settings);
    return container;
}
void Remove(params Container[] containers) { foreach (var c in containers) ContainerRegistry.Unregister(c); }

Check("quick-sort excludes tombstones without affecting ordinary storage", () => {
    var grave = new Container { Grave = new TombStone() };
    ContainerRegistry.Register(grave);
    var chest = Chest("Wood");
    var player = new Player(); player.Inventory.Items.Add(Item("Wood", 5));
    Assert(!ContainerRegistry.TryGetContainer(grave.m_inventory, out _));
    Assert(QuickSort.Run(player, Config()) == 1);
    Assert(grave.m_inventory.Items.Count == 0 && grave.Saves == 0 && grave.Loads == 0);
    Assert(chest.m_inventory.Items.Sum(item => item.m_stack) == 5);
    Remove(chest, grave);
});

Check("paint budget includes mipmaps and recovers capacity after release", () => {
    Assert(PaintMemoryBudget.TextureBytes(1,1) == 4);
    Assert(PaintMemoryBudget.TextureBytes(3,5) == 72);
    Assert(PaintMemoryBudget.TextureBytes(1024,1024) == 5592404);
    var budget=new PaintMemoryBudget();
    long texture=PaintMemoryBudget.TextureBytes(1024,1024);
    Assert(!budget.TryReserve(texture,0) && budget.UsedBytes==0);
    Assert(budget.TryReserve(texture,texture*2));
    Assert(budget.TryReserve(texture,texture*2));
    Assert(!budget.TryReserve(texture,texture*2));
    budget.Release(texture); Assert(budget.TryReserve(texture,texture*2));
    Assert(!budget.TryReserve(4,texture)); // Lowering a limit preserves existing leases.
    budget.Clear(); Assert(budget.UsedBytes==0 && budget.TryReserve(texture,texture));
});

Check("settings round-trip escaped delimiters and backslashes", () => {
    var settings = new ChestSettings { Priority = ChestPriority.Critical, CategoryFlags = CategoryId.Ores | CategoryId.Food };
    settings.AllowedPrefabs.UnionWith(new[] { "Wood", "Mod,Item", "Mod|Item", @"Mod\,|Item", @"Mod\\", "尾巴" });
    Assert(ChestSettings.TryParse(settings.Serialize(), out var parsed));
    Assert(parsed.AllowedPrefabs.SetEquals(settings.AllowedPrefabs));
    Assert(parsed.Priority == settings.Priority && parsed.CategoryFlags == settings.CategoryFlags);
});
Check("legacy settings remain readable", () => {
    Assert(ChestSettings.TryParse("v1|P:3|C:4|I:Wood,Iron", out var parsed));
    Assert(parsed.Priority == ChestPriority.High && parsed.AllowedPrefabs.SetEquals(new[] { "Wood", "Iron" }));
});
Check("custom hex input validates before becoming a saved tint", () => {
    foreach (string text in new[] { "#12aBcD", "12ABCD", "  #12abcd  " })
    {
        Assert(ChestPalette.TryParseHex(text, out int rgb) && rgb == 0x12ABCD);
        var c = new Container();
        Assert(ChestSettingsStore.TrySet(c, new ChestSettings { TintRgb = rgb }));
        ChestSettingsStore.Invalidate(c);
        Assert(ChestSettingsStore.Get(c).TintRgb == rgb);
        Assert(ChestPalette.NameOf(rgb) == "#12ABCD");
    }
    foreach (string text in new[] { null, "", "#", "#123", "#12345", "#1234567", "#GGGGGG", "-12345", "12 345", "red", "#FFFFFF00" })
        Assert(!ChestPalette.TryParseHex(text, out _), "Invalid hex accepted: " + text);
    Assert(ChestPalette.TryParseHex("#000000", out int black) && black == 0);
    Assert(ChestPalette.TryParseHex("#FFFFFF", out int white) && white == 0xFFFFFF);
});
Check("tints survive save, cache invalidation and clipboard without configuring sorting", () => {
    var c = new Container(); var settings = new ChestSettings { TintRgb = 0x709FE0 };
    Assert(settings.IsEmpty && !settings.HasConfiguration && settings.Allows(Item("Wood")));
    Assert(ChestSettingsStore.TrySet(c, settings));
    Assert(c.m_nview.Zdo.GetString(ChestSettingsStore.ZdoKey, "").Contains("|T:709FE0"));
    ChestSettingsStore.Invalidate(c);
    Assert(ChestSettingsStore.Get(c).TintRgb == settings.TintRgb);
    ChestSettingsStore.Copy(settings); settings.TintRgb = -1;
    var pasted = ChestSettingsStore.PasteClone(); Assert(pasted.TintRgb == 0x709FE0);
    pasted.CategoryFlags = CategoryId.Wood; pasted.AllowedPrefabs.Add("Wood"); pasted.ClearFilters();
    Assert(pasted.IsEmpty && pasted.TintRgb == 0x709FE0);
    Assert(ChestSettingsStore.TrySet(c, settings));
    Assert(c.m_nview.Zdo.GetString(ChestSettingsStore.ZdoKey, "") == "");
});
Check("legacy and malformed tint data keep the original appearance", () => {
    foreach (string suffix in new[] { "", "|T:FFFFFFF", "|T:GGGGGG", "|T:-00001", "|T:123", "|T:" })
    {
        Assert(ChestSettings.TryParse("v1|P:3|C:4|I:Wood" + suffix, out var settings));
        Assert(!settings.HasTint && settings.Priority == ChestPriority.High && settings.AllowedPrefabs.Contains("Wood"));
    }
    foreach (int rgb in new[] { 0, 0xFFFFFF, 0x00ABCD })
    {
        Assert(ChestSettings.TryParse(new ChestSettings { TintRgb = rgb }.Serialize(), out var parsed));
        Assert(parsed.TintRgb == rgb);
    }
});
Check("remote tint changes are read without writes and nonowners cannot recolor", () => {
    var c = new Container(); c.m_nview.Owner = false;
    Assert(!ChestSettingsStore.TrySet(c, new ChestSettings { TintRgb = 0x123456 }));
    Assert(c.m_nview.Zdo.Writes == 0);
    c.m_nview.Zdo.Set(ChestSettingsStore.ZdoKey, "v1|P:2|C:0|I:|T:ABCDEF");
    Assert(ChestSettingsStore.Get(c).TintRgb == 0xABCDEF);
    c.m_nview.Zdo.Set(ChestSettingsStore.ZdoKey, "");
    Assert(!ChestSettingsStore.Get(c).HasTint && c.m_nview.Zdo.Writes == 2);
});
Check("canonical serialization and unchanged ZDO writes", () => {
    var c = new Container(); var a = new ChestSettings(); var b = new ChestSettings();
    a.AllowedPrefabs.UnionWith(new[] { "Wood", "Iron" }); b.AllowedPrefabs.UnionWith(new[] { "Iron", "Wood" });
    Assert(a.Serialize() == b.Serialize());
    ChestSettingsStore.TrySet(c, a); ChestSettingsStore.TrySet(c, b);
    Assert(c.m_nview.Zdo.Writes == 1);
    a.AllowedPrefabs.Clear(); Assert(ChestSettingsStore.Get(c).AllowedPrefabs.Count == 2);
});
Check("remote settings changes invalidate cached values", () => {
    var c = Chest("Wood"); var next = new ChestSettings { CategoryFlags = CategoryId.Stone };
    c.m_nview.Zdo.Set(ChestSettingsStore.ZdoKey, next.Serialize());
    Assert(ChestSettingsStore.Get(c).CategoryFlags == CategoryId.Stone); Remove(c);
});
Check("food and curated categories overlap", () => {
    var meat = Item("CookedMeat", type: ItemDrop.ItemData.ItemType.Consumable); meat.m_shared.m_food = 40;
    Assert(CategoryDefs.Matches(meat, CategoryId.Food)); Assert(CategoryDefs.Matches(meat, CategoryId.Meat));
    Assert(CategoryDefs.Matches(Item("Barley"), CategoryId.Cooking));
    Assert(CategoryDefs.Matches(Item("Barley"), CategoryId.Seeds));
    Assert(!CategoryDefs.Matches(Item("Wood"), CategoryId.Misc));
});
Check("modded valuables and cooking ingredients use game data", () => {
    var gem = Item("ModGem"); gem.m_shared.m_value = 10;
    Assert(CategoryDefs.Matches(gem, CategoryId.Valuables));
    var meal = Item("ModMeal", type: ItemDrop.ItemData.ItemType.Consumable); meal.m_shared.m_foodEitr = 20;
    var ingredient = Item("ModIngredient");
    ObjectDB.instance = new();
    ObjectDB.instance.m_recipes.Add(new Recipe { m_item = meal.m_dropPrefab.Drop,
        m_resources = new[] { new Piece.Requirement { m_resItem = ingredient.m_dropPrefab.Drop } } });
    Assert(CategoryDefs.Matches(ingredient, CategoryId.Cooking));
    ObjectDB.instance = new(); Assert(!CategoryDefs.Matches(ingredient, CategoryId.Cooking));
});
Check("catalog follows database replacement and localization", () => {
    ObjectDB.instance = new(); ObjectDB.instance.m_items.Add(Item("First").m_dropPrefab); ItemCatalog.EnsureBuilt();
    ObjectDB.instance = new(); ObjectDB.instance.m_items.Add(Item("Second").m_dropPrefab); ItemCatalog.EnsureBuilt();
    Assert(ItemCatalog.All.Single().PrefabName == "Second");
    Localization.instance.Language = "French"; ItemCatalog.EnsureBuilt();
    Assert(ItemCatalog.All.Single().DisplayName.StartsWith("French:"));
});
Check("catalog supports more than 512 items and zero result limits", () => {
    ObjectDB.instance = new();
    for (int i = 0; i < 600; i++) ObjectDB.instance.m_items.Add(Item("Item" + i).m_dropPrefab);
    var results = new List<CatalogEntry>(); ItemCatalog.Search("", CategoryId.None, results, int.MaxValue);
    Assert(results.Count == 600); ItemCatalog.Search("", CategoryId.None, results, 0); Assert(results.Count == 0);
});
Check("browser groups iconless items without dropping saved pins", () => {
    ObjectDB.instance = new(); var visible = Item("Visible"); visible.m_shared.m_icons = new[] { new Sprite() };
    var hidden = Item("NoIcon"); ObjectDB.instance.m_items.AddRange(new[] { visible.m_dropPrefab, hidden.m_dropPrefab });
    ItemCatalog.Invalidate(); var settings = new ChestSettings(); settings.AllowedPrefabs.UnionWith(new[] { "NoIcon", "RemovedMod" });
    var results = new List<CatalogEntry>();
    ItemCatalog.Browse("", CatalogView.Items, settings, results); Assert(results.Count == 1 && results[0].PrefabName == "Visible");
    ItemCatalog.Browse("", CatalogView.NoIcon, settings, results); Assert(results.Count == 1 && results[0].PrefabName == "NoIcon");
    ItemCatalog.Browse("", CatalogView.Pinned, settings, results); Assert(results.Count == 1 && results[0].PrefabName == "NoIcon");
    ItemCatalog.Browse("", CatalogView.Missing, settings, results); Assert(results.Count == 1 && results[0].PrefabName == "RemovedMod");
    Assert(settings.AllowedPrefabs.SetEquals(new[] { "NoIcon", "RemovedMod" }));
    ItemCatalog.Browse("nonexistent", CatalogView.NoIcon, settings, results); Assert(results.Count == 0);
});
Check("icon lookup uses a populated variant after an empty first icon", () => {
    var item = Item("VariantItem"); var icon = new Sprite(); item.m_shared.m_icons = new[] { null, icon };
    Assert(ReferenceEquals(ItemUtil.GetIcon(item), icon));
});
Check("explicit invalidation catches in-place catalog changes", () => {
    ObjectDB.instance = new(); ObjectDB.instance.m_items.Add(Item("Before").m_dropPrefab); ItemCatalog.EnsureBuilt();
    ObjectDB.instance.m_items[0] = Item("After").m_dropPrefab; ItemCatalog.Invalidate(); ItemCatalog.EnsureBuilt();
    Assert(ItemCatalog.All.Single().PrefabName == "After");
});
Check("build tables classify modded plants, meals, and meads", () => {
    ObjectDB.instance = new(); var tool = Item("BuildTool"); var table = new PieceTable(); tool.m_shared.m_buildPieces = table;
    ObjectDB.instance.m_items.Add(tool.m_dropPrefab);
    var seed = Item("ModSeed"); var raw = Item("ModRaw"); var meal = Item("ModCooked", type: ItemDrop.ItemData.ItemType.Consumable); meal.m_shared.m_food = 20;
    var brew = Item("ModMeadBase"); var drink = Item("ModMead");
    var plant = new GameObject(); plant.Components.Add(new Plant()); plant.Components.Add(new Piece { m_resources = new[] { new Piece.Requirement { m_resItem = seed.m_dropPrefab.Drop } } });
    var stove = new GameObject(); stove.Components.Add(new CookingStation { m_conversion = new() { new() { m_from = raw.m_dropPrefab.Drop, m_to = meal.m_dropPrefab.Drop } } });
    var barrel = new GameObject(); barrel.Components.Add(new Fermenter { m_conversion = new() { new() { m_from = brew.m_dropPrefab.Drop, m_to = drink.m_dropPrefab.Drop } } });
    table.m_pieces.AddRange(new[] { plant, stove, barrel }); CategoryDefs.Invalidate();
    Assert(CategoryDefs.Matches(seed, CategoryId.Seeds)); Assert(CategoryDefs.Matches(raw, CategoryId.Cooking));
    Assert(CategoryDefs.Matches(brew, CategoryId.Potions)); Assert(CategoryDefs.Matches(drink, CategoryId.Potions));
    plant.Components.Clear(); CategoryDefs.Invalidate(); Assert(!CategoryDefs.Matches(seed, CategoryId.Seeds));
});
Check("metal conversions include mod ores without classifying kiln fuel as ore", () => {
    ObjectDB.instance = new(); var tool = Item("Hammer"); var table = new PieceTable(); tool.m_shared.m_buildPieces = table;
    ObjectDB.instance.m_items.Add(tool.m_dropPrefab);
    var ore = Item("ModOre"); var metal = Item("ModMetal"); var wood = Item("ModWood"); var coal = Item("ModCoal");
    var furnace = new GameObject(); furnace.Components.Add(new Smelter { m_conversion = new() {
        new() { m_from = Item("CopperOre").m_dropPrefab.Drop, m_to = Item("Copper").m_dropPrefab.Drop },
        new() { m_from = ore.m_dropPrefab.Drop, m_to = metal.m_dropPrefab.Drop } } });
    var kiln = new GameObject(); kiln.Components.Add(new Smelter { m_conversion = new() { new() { m_from = wood.m_dropPrefab.Drop, m_to = coal.m_dropPrefab.Drop } } });
    table.m_pieces.AddRange(new[] { furnace, kiln }); CategoryDefs.Invalidate();
    Assert(CategoryDefs.Matches(ore, CategoryId.Ores)); Assert(CategoryDefs.Matches(metal, CategoryId.Metals));
    Assert(!CategoryDefs.Matches(wood, CategoryId.Ores)); Assert(!CategoryDefs.Matches(coal, CategoryId.Metals));
});
Check("category cache follows added and replaced build-tool registrations", () => {
    ObjectDB.instance = new();
    var seed = Item("LateModSeed");
    Assert(!CategoryDefs.Matches(seed, CategoryId.Seeds));
    var tool = Item("LateCultivator");
    var plant = new GameObject();
    plant.Components.Add(new Plant());
    plant.Components.Add(new Piece { m_resources = new[] { new Piece.Requirement { m_resItem = seed.m_dropPrefab.Drop } } });
    tool.m_shared.m_buildPieces = new PieceTable { m_pieces = new() { plant } };
    ObjectDB.instance.m_items.Add(tool.m_dropPrefab);
    Assert(CategoryDefs.Matches(seed, CategoryId.Seeds), "Adding a build tool must refresh derived categories without a recipe change");
    ObjectDB.instance.m_items = new() { Item("ReplacementTool").m_dropPrefab };
    Assert(!CategoryDefs.Matches(seed, CategoryId.Seeds), "A replacement item list with the same count must invalidate old build tables");
});
Check("category cache follows replacement recipes with the same count", () => {
    ObjectDB.instance = new();
    var ingredient = Item("LateIngredient");
    var meal = Item("LateMeal", type: ItemDrop.ItemData.ItemType.Consumable); meal.m_shared.m_food = 20;
    ObjectDB.instance.m_recipes.Add(new Recipe { m_item = meal.m_dropPrefab.Drop,
        m_resources = new[] { new Piece.Requirement { m_resItem = ingredient.m_dropPrefab.Drop } } });
    Assert(CategoryDefs.Matches(ingredient, CategoryId.Cooking));
    ObjectDB.instance.m_recipes = new() { new Recipe { m_item = meal.m_dropPrefab.Drop, m_resources = Array.Empty<Piece.Requirement>() } };
    Assert(!CategoryDefs.Matches(ingredient, CategoryId.Cooking));
});
Check("build-table categories remain available without a recipe list", () => {
    ObjectDB.instance = new() { m_recipes = null };
    var brew = Item("TableOnlyMeadBase"); var tool = Item("TableOnlyHammer");
    var fermenter = new GameObject();
    fermenter.Components.Add(new Fermenter { m_conversion = new() { new() { m_from = brew.m_dropPrefab.Drop } } });
    tool.m_shared.m_buildPieces = new PieceTable { m_pieces = new() { fermenter } };
    ObjectDB.instance.m_items.Add(tool.m_dropPrefab);
    Assert(CategoryDefs.Matches(brew, CategoryId.Potions));
});
Check("outbound swap rejects reverse deposit before removing either item", () => {
    var c = Chest("Wood"); var wood = Item("Wood"); c.m_inventory.Items.Add(wood);
    var player = new Inventory(); player.Items.Add(Item("Stone"));
    object[] args = { new InventoryGrid { m_inventory = player }, c.m_inventory, wood, 1, new Vector2i(0, 1), true };
    Assert(!(bool)Invoke(typeof(InventoryGridFilterPatches), "DropItemPrefix", args));
    Assert(!(bool)args[5]); Assert(c.m_inventory.ContainsItem(wood) && player.Items.Count == 1); Remove(c);
});
Check("existing disallowed items can be rearranged within the same chest", () => {
    var c = Chest("Wood"); var stone = Item("Stone");
    object[] args = { new InventoryGrid { m_inventory = c.m_inventory }, c.m_inventory, stone, 1, new Vector2i(0, 0), false };
    Assert((bool)Invoke(typeof(InventoryGridFilterPatches), "DropItemPrefix", args));
    Assert((bool)Invoke(typeof(InventoryFilterPatches), "MoveItemAmountPrefix", c.m_inventory, c.m_inventory, stone, false)); Remove(c);
});
Check("bulk-transfer wrappers block deposits without changing source stacks", () => {
    var c = Chest("Wood"); var stone = Item("Stone", 20);
    Assert(!(bool)Invoke(typeof(BulkTransferFilterPatches), "AddAllowed", c.m_inventory, stone));
    Assert(!(bool)Invoke(typeof(BulkTransferFilterPatches), "AddAllowedAt", c.m_inventory, stone, 20, 0, 0, false));
    Assert(stone.m_stack == 20 && c.m_inventory.Items.Count == 0); Remove(c);
});
Check("bulk transpiler replaces both AddItem call signatures", () => {
    var add = AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData) });
    var addAt = AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });
    var code = new[] { new CodeInstruction(OpCodes.Callvirt, add), new CodeInstruction(OpCodes.Call, addAt), new CodeInstruction(OpCodes.Ret) };
    var output = ((IEnumerable<CodeInstruction>)Invoke(typeof(BulkTransferFilterPatches), "Transpiler", (object)code)).ToArray();
    Assert(((MethodInfo)output[0].operand).Name == "AddAllowed" && ((MethodInfo)output[1].operand).Name == "AddAllowedAt");
    Assert(output[0].opcode == OpCodes.Call && output[2].opcode == OpCodes.Ret);
});
Check("hotbar protection preserves all numbered slots while sorting regular inventory", () => {
    var player = new Player();
    var hotbar = Enumerable.Range(0, 8).Select(x => { var item = Item("Wood", x + 1); item.m_gridPos = new Vector2i(x, 0); return item; }).ToArray();
    player.Inventory.Items.AddRange(hotbar);
    player.Inventory.Items.Add(Item("Wood", 4));
    // A wider modded first row must not turn unnumbered columns into hotbar slots.
    var extraColumn = Item("Wood", 2); extraColumn.m_gridPos = new Vector2i(8, 0); player.Inventory.Items.Add(extraColumn);
    var c = Chest("Wood");
    try
    {
        Assert(QuickSort.Run(player, Config()) == 2);
        Assert(player.Inventory.Items.SequenceEqual(hotbar));
        Assert(hotbar.Select(i => i.m_stack).SequenceEqual(Enumerable.Range(1, 8)));
        Assert(c.m_inventory.Items.Sum(i => i.m_stack) == 6);
        var cfg = Config(); cfg.ProtectHotbar = false;
        Assert(QuickSort.Run(player, cfg) == 8 && player.Inventory.Items.Count == 0);
        Assert(c.m_inventory.Items.Sum(i => i.m_stack) == 42);
        var replacement = Item("Wood"); replacement.m_gridPos = new Vector2i(7, 0); player.Inventory.Items.Add(replacement);
        cfg.ProtectHotbar = true;
        Assert(QuickSort.Run(player, cfg) == 0 && player.Inventory.Items.Single() == replacement);
    }
    finally { Remove(c); }
});
Check("quick-sort fills partial capacity across multiple chests without loss", () => {
    var a = Chest("Wood", 5); var b = Chest("Wood", 7, 1); var player = new Player(); player.Inventory.Items.Add(Item("Wood", 20));
    Assert(QuickSort.Run(player, Config()) == 2);
    Assert(player.Inventory.Items.Sum(i => i.m_stack) == 8);
    Assert(a.m_inventory.Items.Sum(i => i.m_stack) == 5 && b.m_inventory.Items.Sum(i => i.m_stack) == 7);
    Assert(a.Saves == 1 && b.Saves == 1); Remove(a, b);
});
Check("quick-sort batches repeated saves and respects move cap", () => {
    var c = Chest("Wood"); var player = new Player(); player.Inventory.Items.AddRange(new[] { Item("Wood", 3), Item("Wood", 4), Item("Wood", 5) });
    Assert(QuickSort.Run(player, Config(2)) == 2); Assert(c.Saves == 1 && player.Inventory.Items.Count == 1); Remove(c);
});
Check("quick-sort enforces wards, privacy, ownership, and in-use state", () => {
    var c = Chest("Wood"); var player = new Player(); player.Inventory.Items.Add(Item("Wood"));
    c.m_checkGuardStone = true; PrivateArea.Allowed = false; Assert(QuickSort.Run(player, Config()) == 0);
    PrivateArea.Allowed = true; c.Accessible = false; Assert(QuickSort.Run(player, Config()) == 0);
    c.Accessible = true; c.m_nview.Owner = false; Assert(QuickSort.Run(player, Config()) == 0);
    c.m_nview.Owner = true; c.InUse = true; Assert(QuickSort.Run(player, Config()) == 0); Remove(c);
});
Check("quick-sort flushes successful moves and recovers after an exception", () => {
    var a = Chest("Wood", 5); var b = Chest("Wood", 5, 1); b.m_inventory.ThrowOnMove = true;
    var player = new Player(); player.Inventory.Items.Add(Item("Wood", 10));
    bool threw = false;
    try { QuickSort.Run(player, Config()); } catch (InvalidOperationException) { threw = true; }
    Assert(threw && a.Saves == 1 && player.Inventory.Items.Single().m_stack == 5);
    b.m_inventory.ThrowOnMove = false; Assert(QuickSort.Run(player, Config()) == 1); Remove(a, b);
});
Check("a failed deferred save does not abandon other changed chests", () => {
    for (int failing = 0; failing < 2; failing++)
    {
        var chests = new[] { Chest("Wood", 5), Chest("Wood", 5, 1) };
        chests[failing].ThrowOnSave = true;
        var player = new Player(); player.Inventory.Items.Add(Item("Wood", 10));
        bool threw = false;
        try { QuickSort.Run(player, Config()); } catch (InvalidOperationException) { threw = true; }
        Assert(threw, "Serialization failures must remain visible");
        Assert(chests.All(c => c.Saves == 1), "Every changed chest must receive a save attempt");
        Assert(chests[1 - failing].SavedTotal == 5 && player.Inventory.Items.Count == 0);
        chests[failing].ThrowOnSave = false;
        chests[failing].m_inventory.Capacity = 10;
        player.Inventory.Items.Add(Item("Wood"));
        Assert(QuickSort.Run(player, Config()) == 1, "Save failure must not leave the sorter locked");
        Remove(chests);
    }
});
Check("quick-sort applies priority before distance and honors Never", () => {
    var near = Chest("Wood"); var far = Chest("Wood", 100, 5); var never = Chest("Wood");
    var high = ChestSettingsStore.Get(far).Clone(); high.Priority = ChestPriority.High; ChestSettingsStore.TrySet(far, high);
    var excluded = ChestSettingsStore.Get(never).Clone(); excluded.Priority = ChestPriority.Never; ChestSettingsStore.TrySet(never, excluded);
    var cfg = Config(); cfg.MaxChests = 1; var player = new Player(); player.Inventory.Items.Add(Item("Wood", 10));
    Assert(QuickSort.Run(player, cfg) == 1 && far.m_inventory.Items.Count == 1);
    Assert(near.m_inventory.Items.Count == 0 && never.m_inventory.Items.Count == 0);
    Assert(far.Loads == 1 && near.Loads == 0 && never.Loads == 0,
        "Never and chests beyond MaxChests must not deserialize inventories");
    Remove(near, far, never);
});
Check("unrestricted destinations participate by default and equipped items remain protected", () => {
    var c = new Container(); ContainerRegistry.Register(c); var player = new Player(); var wood = Item("Wood");
    player.Inventory.Items.Add(wood); wood.m_equipped = true;
    Assert(QuickSort.Run(player, Config()) == 0); wood.m_equipped = false;
    Assert(QuickSort.Run(player, Config()) == 1 && c.m_inventory.Items.Count == 1); Remove(c);
});
Check("Low filtered precedes Critical unrestricted with overflow and unmatched items conserved", () => {
    var dedicated = Chest("Wood", 5, 8); var fallback = Chest("Wood");
    var low = ChestSettingsStore.Get(dedicated).Clone(); low.Priority = ChestPriority.Low;
    ChestSettingsStore.TrySet(dedicated, low);
    ChestSettingsStore.TrySet(fallback, new ChestSettings { Priority = ChestPriority.Critical, TintRgb = 0xE06060 });
    var player = new Player(); player.Inventory.Items.AddRange(new[] { Item("Wood", 8), Item("Stone", 4) });
    Assert(QuickSort.Run(player, Config()) == 3 && player.Inventory.Items.Count == 0);
    Assert(dedicated.m_inventory.Items.Single().m_stack == 5);
    Assert(fallback.m_inventory.Items.Sum(i => i.m_stack) == 7);
    Assert(dedicated.Saves == 1 && fallback.Saves == 1); Remove(dedicated, fallback);
});
Check("filtered grouping applies before MaxChests cap including category-only filters", () => {
    var filtered = Chest("Wood", 100, 8); var fallback = Chest("Wood");
    ChestSettingsStore.TrySet(filtered, new ChestSettings { Priority = ChestPriority.Low, CategoryFlags = CategoryId.Wood });
    ChestSettingsStore.TrySet(fallback, new ChestSettings { Priority = ChestPriority.Critical });
    var player = new Player(); player.Inventory.Items.Add(Item("Wood", 3));
    var cfg = Config(); cfg.MaxChests = 1;
    Assert(QuickSort.Run(player, cfg) == 1 && filtered.m_inventory.Items.Single().m_stack == 3);
    Assert(fallback.m_inventory.Items.Count == 0); Remove(filtered, fallback);
});
Check("unrestricted priority precedes distance and nearest breaks equal-priority ties", () => {
    var near = Chest("Wood"); var far = Chest("Wood", 100, 7);
    ChestSettingsStore.TrySet(near, new ChestSettings { Priority = ChestPriority.High });
    ChestSettingsStore.TrySet(far, new ChestSettings { Priority = ChestPriority.Critical });
    var player = new Player(); player.Inventory.Items.Add(Item("Wood"));
    var cfg = Config(); cfg.MaxChests = 1;
    Assert(QuickSort.Run(player, cfg) == 1 && far.m_inventory.Items.Count == 1 && near.m_inventory.Items.Count == 0);
    ChestSettingsStore.TrySet(far, new ChestSettings { Priority = ChestPriority.High });
    player.Inventory.Items.Add(Item("Stone"));
    Assert(QuickSort.Run(player, cfg) == 1 && near.m_inventory.Items.Count == 1); Remove(near, far);
});
Check("Never excludes unrestricted chests too", () => {
    var c = Chest("Wood"); ChestSettingsStore.TrySet(c, new ChestSettings { Priority = ChestPriority.Never });
    var player = new Player(); player.Inventory.Items.Add(Item("Wood"));
    Assert(QuickSort.Run(player, Config()) == 0 && player.Inventory.Items.Count == 1); Remove(c);
});
Check("dye preparation lifts dark detail without washing out black or clipping white", () => {
    Assert(Math.Abs(ChestDyeMath.NeutralAlbedo(0, 0, 0)) < 0.0001);
    Assert(Math.Abs(ChestDyeMath.NeutralAlbedo(1, 1, 1) - 1) < 0.0001);
    float previous = 0;
    for (int i = 1; i <= 100; i++)
    {
        float source = i / 100f;
        float gray = ChestDyeMath.NeutralAlbedo(source, source, source);
        Assert(gray > previous && gray >= source - 0.0001 && gray <= 1.0001);
        previous = gray;
    }
    // Representative dark timber retains increasing grain contrast after neutralization.
    float dark = ChestDyeMath.NeutralAlbedo(0.14f, 0.08f, 0.04f);
    float light = ChestDyeMath.NeutralAlbedo(0.42f, 0.28f, 0.12f);
    Assert(dark > 0 && light > dark && light < 1);
});
Check("paint preserves neutral hardware, metal masks and deep seams", () => {
    Assert(ChestDyeMath.PaintCoverage(0.5f, 0.5f, 0.5f, 0) == 0);
    Assert(ChestDyeMath.PaintCoverage(0.4f, 0.25f, 0.12f, 1) == 0);
    Assert(ChestDyeMath.PaintCoverage(0.025f, 0.015f, 0.005f, 0) == 0);
    Assert(ChestDyeMath.PaintCoverage(0.4f, 0.25f, 0.12f, 0) > 0.99f);
    Assert(ChestDyeMath.PaintChannel(0.45f, 0.1f, 0.8f, 0) == 0.45f);
    Assert(ChestDyeMath.TimberMask("woodchest_d", 0.615f, 0.9f) == 0);
    Assert(ChestDyeMath.TimberMask("woodchest_d", 0.2f, 0.5f) == 1);
    Assert(ChestDyeMath.TimberMask("ironchest_d", 0.615f, 0.9f) == 1);
});
Check("paint coverage gives dark timber visible color while preserving grain and selected hue", () => {
    float dark = ChestDyeMath.PaintGrain(0.18f, 0.1f, 0.05f);
    float light = ChestDyeMath.PaintGrain(0.6f, 0.4f, 0.2f);
    Assert(dark >= 0.45f && light > dark * 1.7f && light <= 0.96f);
    float red = ChestDyeMath.PaintChannel(0.18f, 0.15f, dark, 1);
    float green = ChestDyeMath.PaintChannel(0.1f, 0.75f, dark, 1);
    Assert(green > 0.35f && Math.Abs(red / green - 0.2f) < 0.0001);
    Assert(ChestDyeMath.PaintChannel(0.5f, 0, dark, 1) == 0);
});
Check("wood chest front ring stays exposed without unpainting its timber center", () => {
    // Sample the top, upper bend, side, lower bend and bottom of the inspected rim UVs.
    foreach (var uv in new[] { (0.02f, 0.684f), (0.05f, 0.674f), (0.062f, 0.644f),
        (0.046f, 0.608f), (0.02f, 0.599f) })
        Assert(ChestDyeMath.TimberMask("woodchest_d", uv.Item1, uv.Item2) < 0.1f);
    Assert(ChestDyeMath.TimberMask("woodchest_d", 0.03f, 0.64f) == 1);
    Assert(ChestDyeMath.TimberMask("woodchest_d", 0.08f, 0.64f) == 1);
    Assert(ChestDyeMath.TimberMask("woodchest_d", 0.02f, 0.71f) == 1);
    Assert(ChestDyeMath.TimberMask("ironchest_d", 0.062f, 0.644f) == 1);
    Assert(ChestDyeMath.TimberMask("strongbox_d", 0.062f, 0.644f) == 1);
});
Check("paint grain preserves highlight differences and a restrained midtone", () => {
    float previous = ChestDyeMath.PaintGrain(0, 0, 0);
    for (int i = 1; i <= 100; ++i)
    {
        float value = i / 100f;
        float grain = ChestDyeMath.PaintGrain(value, value, value);
        Assert(grain > previous && grain < 0.96f);
        previous = grain;
    }
    float midtone = ChestDyeMath.PaintGrain(0.3f, 0.3f, 0.3f);
    Assert(midtone > 0.70f && midtone < 0.76f);
});
Check("storage profiles cover all inspected variants without capturing unrelated textures", () => {
    foreach (string atlas in new[] { "woodchest_d", "ironchest_d", "strongbox_d", "barrel_d" })
        Assert(ChestPaintProfile.Get(atlas) == ChestFinish.Timber);
    Assert(ChestPaintProfile.Get("BlackMetalChest_D") == ChestFinish.FramedTimber);
    foreach (string atlas in new[] { "Julklapp_d", "Julklapp_1_d", "Julklapp_2_d" })
        Assert(ChestPaintProfile.Get(atlas) == ChestFinish.Wrapping);
    foreach (string atlas in new[] { "ceramicpotsgreen_d", "ceramicpotsbrokengreen_d" })
        Assert(ChestPaintProfile.Get(atlas) == ChestFinish.Ceramic);
    foreach (string atlas in new[] { "modded_barrel_d", "BlackMetalChest_silver_D", "ceramicpotsred_d", null })
        Assert(ChestPaintProfile.Get(atlas) == ChestFinish.None);
});
Check("barrel and black-metal wood accept color while their metal stays exposed", () => {
    foreach (string atlas in new[] { "barrel_d", "BlackMetalChest_D" })
    {
        var finish = ChestPaintProfile.Get(atlas);
        Assert(ChestPaintProfile.Coverage(finish, atlas, 0.4f, 0.25f, 0.12f, 0, 0.8f, 0.5f) > 0.99f);
        Assert(ChestPaintProfile.Coverage(finish, atlas, 0.4f, 0.25f, 0.12f, 1, 0.3f, 0.5f) == 0);
        Assert(ChestPaintProfile.Coverage(finish, atlas, 0.4f, 0.4f, 0.4f, 0, 0.3f, 0.5f) == 0);
    }
});
Check("gift paper recolors white red and green while every variant preserves ribbons", () => {
    var paper = new[] { (0.7f, 0.7f, 0.7f), (0.75f, 0.2f, 0.15f), (0.35f, 0.65f, 0.15f) };
    var ribbons = new[] { 0.63f, 0.84f, 1f };
    for (int i = 0; i < paper.Length; ++i)
    {
        var p = paper[i];
        Assert(ChestPaintProfile.Coverage(ChestFinish.Wrapping, "", p.Item1, p.Item2, p.Item3, 0, 0, 0) == 1);
        Assert(ChestPaintProfile.Coverage(ChestFinish.Wrapping, "", 0.8f, 0.3f, 0.05f, ribbons[i], 0, 0) == 0);
    }
    float dark = ChestPaintProfile.Grain(ChestFinish.Wrapping, 0.35f, 0.35f, 0.35f);
    float light = ChestPaintProfile.Grain(ChestFinish.Wrapping, 0.75f, 0.75f, 0.75f);
    Assert(light - dark > 0.3f);
});
Check("green glaze and damaged glaze accept hue while retaining dark cracks and texture", () => {
    foreach (string atlas in new[] { "ceramicpotsgreen_d", "ceramicpotsbrokengreen_d" })
    {
        var finish = ChestPaintProfile.Get(atlas);
        Assert(ChestPaintProfile.Coverage(finish, atlas, 0.22f, 0.28f, 0.20f, 0, 0, 0) == 1);
        Assert(ChestPaintProfile.Coverage(finish, atlas, 0.01f, 0.02f, 0.01f, 0, 0, 0) == 0);
        float grain = ChestPaintProfile.Grain(finish, 0.22f, 0.28f, 0.20f);
        Assert(grain > 0.5f && grain < 0.6f);
        Assert(ChestDyeMath.PaintChannel(0.28f, 0, grain, 1) == 0);
        Assert(ChestPaintProfile.Grain(finish, 0.3f, 0.36f, 0.28f) > grain);
    }
});
Check("storage finish rules retain ceramic sheen metal noise and separate snow overlays", () => {
    Assert(ChestPaintProfile.Gloss(ChestFinish.Timber, 0.235f) == 0.12f);
    Assert(ChestPaintProfile.Gloss(ChestFinish.FramedTimber, 0.3f) == 0.12f);
    Assert(ChestPaintProfile.ValueNoise(ChestFinish.FramedTimber, 1f) == 1f);
    Assert(ChestPaintProfile.Gloss(ChestFinish.Ceramic, 0.19f) == 0.19f);
    Assert(ChestPaintProfile.Gloss(ChestFinish.Ceramic, 0.13f) == 0.13f);
    Assert(ChestPaintProfile.Gloss(ChestFinish.Wrapping, 0f) == 0f);
    Assert(ChestPaintProfile.ValueNoise(ChestFinish.Ceramic, 0.5f) == 0.5f);
    Assert(ChestPaintProfile.PreserveMaterial("Valheim/Snow Mesh"));
    Assert(!ChestPaintProfile.PreserveMaterial("Custom/Piece"));
});
Check("quick-sort respects live per-character item and slot favorites", () => {
    BepInEx.Bootstrap.Chainloader.PluginInfos["Azumatt.AzuExtendedPlayerInventory"] = new();
    typeof(EpiCompat).GetField("_resolved", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
    var player = new Player { Id = 42 };
    var favorites = AzuEPI.Game.Favoriting.UserConfig.GetPlayerConfig(player.Id);
    favorites.Items.Add("Wood"); favorites.Slots.Add(new Vector2i(3, 1));
    var wood = Item("Wood", 10); var otherWood = Item("Wood", 5); otherWood.m_gridPos = new Vector2i(1, 1);
    var stone = Item("Stone", 4); stone.m_gridPos = new Vector2i(3, 1);
    var resin = Item("Resin", 3); resin.m_gridPos = new Vector2i(2, 1);
    player.Inventory.Items.AddRange(new[] { wood, otherWood, stone, resin });
    var dedicated = Chest("Wood"); var fallback = new Container(); ContainerRegistry.Register(fallback);
    try
    {
        Assert(QuickSort.Run(player, Config()) == 1);
        Assert(player.Inventory.Items.SequenceEqual(new[] { wood, otherWood, stone }));
        Assert(wood.m_stack == 10 && otherWood.m_stack == 5 && stone.m_stack == 4);
        Assert(dedicated.m_inventory.Items.Count == 0 && fallback.m_inventory.Items.Single().m_shared.m_name == "Resin");
        Assert(!EpiCompat.IsProtectedPlayerItem(new Player { Id = 43 }, wood), "Favorites must use the sorting character's ID");
        favorites.Items.Clear(); favorites.Slots.Clear();
        Assert(QuickSort.Run(player, Config()) == 3, "Removing favorites must take effect on the next press");
        Assert(player.Inventory.Items.Count == 0);
        Assert(dedicated.m_inventory.Items.Sum(i => i.m_stack) + fallback.m_inventory.Items.Sum(i => i.m_stack) == 22);
    }
    finally { favorites.Items.Clear(); favorites.Slots.Clear(); Remove(dedicated, fallback); }
});
Check("unfavorited AzuEPI quick-slot items survive sorting even without grid classification", () => {
    var player = new Player();
    var quickWood = Item("Wood", 10); quickWood.m_gridPos = new Vector2i(0, 5);
    var quickStone = Item("Stone", 7); quickStone.m_gridPos = new Vector2i(1, 5);
    var spareWood = Item("Wood", 6); spareWood.m_gridPos = new Vector2i(0, 1);
    player.Inventory.Items.AddRange(new[] { quickWood, quickStone, spareWood });
    AzuEPI.API.QuickSlotItems.AddRange(new[] { quickWood, quickStone });
    AzuEPI.API.SuppressGridLookup = true;
    var dedicated = Chest("Wood", 3); var fallback = new Container(); ContainerRegistry.Register(fallback);
    try
    {
        Assert(QuickSort.Run(player, Config()) == 2);
        Assert(player.Inventory.Items.SequenceEqual(new[] { quickWood, quickStone }));
        Assert(quickWood.m_stack == 10 && quickStone.m_stack == 7);
        Assert(dedicated.m_inventory.Items.Single().m_stack == 3 && fallback.m_inventory.Items.Single().m_stack == 3,
            "Only the spare stack should sort, including overflow into unrestricted storage");
        AzuEPI.API.QuickSlotItems.Remove(quickWood); quickWood.m_gridPos = new Vector2i(2, 1);
        Assert(QuickSort.Run(player, Config()) == 1, "Moving an item out of a quick slot must allow sorting it next time");
        Assert(player.Inventory.Items.Single() == quickStone);
    }
    finally { AzuEPI.API.QuickSlotItems.Clear(); AzuEPI.API.SuppressGridLookup = false; Remove(dedicated, fallback); }
});
Check("disabling hotbar protection retains equipment, AzuEPI slots and favorite protection", () => {
    var player = new Player { Id = 44 };
    var equipped = Item("Sword"); equipped.m_equipped = true; equipped.m_gridPos = new Vector2i(0, 0);
    var favorite = Item("Wood"); favorite.m_gridPos = new Vector2i(1, 0);
    var quick = Item("Stone"); quick.m_gridPos = new Vector2i(0, 5);
    var ordinary = Item("Resin"); ordinary.m_gridPos = new Vector2i(7, 0);
    player.Inventory.Items.AddRange(new[] { equipped, favorite, quick, ordinary });
    var favorites = AzuEPI.Game.Favoriting.UserConfig.GetPlayerConfig(player.Id); favorites.Items.Add("Wood");
    AzuEPI.API.QuickSlotItems.Add(quick);
    var c = new Container(); ContainerRegistry.Register(c);
    try
    {
        var cfg = Config(); cfg.ProtectHotbar = false;
        Assert(QuickSort.Run(player, cfg) == 1);
        Assert(player.Inventory.Items.SequenceEqual(new[] { equipped, favorite, quick }));
        Assert(c.m_inventory.Items.Single().m_shared.m_name == "Resin");
    }
    finally { favorites.Items.Clear(); AzuEPI.API.QuickSlotItems.Clear(); Remove(c); }
});
Check("quick-slot API failure leaves inventory untouched", () => {
    var player = new Player(); var wood = Item("Wood", 10); player.Inventory.Items.Add(wood);
    var c = Chest("Wood"); AzuEPI.API.ThrowQuickSlots = true;
    try
    {
        Assert(QuickSort.Run(player, Config()) == 0);
        Assert(player.Inventory.Items.Single() == wood && wood.m_stack == 10 && c.m_inventory.Items.Count == 0);
    }
    finally
    {
        AzuEPI.API.ThrowQuickSlots = false;
        typeof(EpiCompat).GetField("_failed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
        Remove(c);
    }
});
Check("favorite API failures protect inventory and missing methods fail closed", () => {
    var player = new Player(); var wood = Item("Wood"); player.Inventory.Items.Add(wood);
    var c = Chest("Wood");
    var method = typeof(EpiCompat).GetField("_isFavorite", BindingFlags.NonPublic | BindingFlags.Static);
    var saved = method.GetValue(null);
    try
    {
        method.SetValue(null, null);
        Assert(QuickSort.Run(player, Config()) == 0 && player.Inventory.Items.Contains(wood));
        method.SetValue(null, saved);
        AzuEPI.Game.Favoriting.UserConfig.Throw = true;
        Assert(QuickSort.Run(player, Config()) == 0 && player.Inventory.Items.Contains(wood));
        Assert(c.m_inventory.Items.Count == 0);
    }
    finally
    {
        method.SetValue(null, saved); AzuEPI.Game.Favoriting.UserConfig.Throw = false;
        typeof(EpiCompat).GetField("_failed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
        Remove(c);
    }
});
Check("AzuEPI resolver checks both APIs and protects equipment/quick slots", () => {
    BepInEx.Bootstrap.Chainloader.PluginInfos["Azumatt.AzuExtendedPlayerInventory"] = new();
    typeof(EpiCompat).GetField("_resolved", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
    var player = new Player(); var item = Item("Wood"); item.m_gridPos = new Vector2i(2, 4);
    Assert(EpiCompat.IsProtectedPlayerItem(player, item)); item.m_gridPos = new Vector2i(2, 0);
    Assert(!EpiCompat.IsProtectedPlayerItem(player, item)); item.m_equipped = true;
    Assert(EpiCompat.IsProtectedPlayerItem(player, item)); item.m_equipped = false; AzuEPI.API.Throw = true;
    Assert(EpiCompat.IsProtectedPlayerItem(player, item));
});
Check("hover distinguishes unrestricted filters from quick-sort exclusion", () => {
    var settings = new ChestSettings { Priority = ChestPriority.High };
    string text = ChestHoverSummary.Get(settings);
    Assert(text.Contains("Priority: High") && text.Contains("Allows: all items") && text.Contains("after filtered chests"));
    Assert(!text.Contains("excluded"));
    settings.Priority = ChestPriority.Never;
    Assert(ChestHoverSummary.Get(settings).Contains("excluded (Never priority)"));
});
Check("hover bounds category details and refreshes displayed values", () => {
    var settings = new ChestSettings { CategoryFlags = CategoryId.Wood | CategoryId.Stone | CategoryId.Ores | CategoryId.Food };
    settings.AllowedPrefabs.Add("Wood");
    string text = ChestHoverSummary.Get(settings);
    Assert(text.Contains("Wood, Stone +2 more categories + 1 pinned item") && !text.Contains("excluded"));
    settings.CategoryFlags = CategoryId.Food; settings.AllowedPrefabs.Add("Iron");
    Assert(ChestHoverSummary.Get(settings).Contains("Allows: Food + 2 pinned items"));
    settings.CategoryFlags = (CategoryId)(1UL << 63); settings.AllowedPrefabs.Clear();
    Assert(ChestHoverSummary.Get(settings).Contains("unrecognized categories"));
});
Check("hover preserves vanilla text and follows remote settings without writes", () => {
    var c = new Container(); c.m_nview.Owner = false;
    string original = "Chest\nOpen"; string text = original;
    ChestHoverPatch.Postfix(c, ref text);
    Assert(text.StartsWith(original + "\n") && text.Contains("Allows: all items"));
    var settings = new ChestSettings { Priority = ChestPriority.Critical, CategoryFlags = CategoryId.Ores };
    c.m_nview.Zdo.Set(ChestSettingsStore.ZdoKey, settings.Serialize());
    int writes = c.m_nview.Zdo.Writes; text = original;
    ChestHoverPatch.Postfix(c, ref text);
    Assert(text.Contains("Priority: Critical") && text.Contains("Allows: Ores"));
    Assert(c.m_nview.Zdo.Writes == writes && c.Loads == 0);
});
Check("hover respects disabled mod, access restrictions and unsupported containers", () => {
    var c = new Container();
    void Unchanged() { string text = "Vanilla hover"; ChestHoverPatch.Postfix(c, ref text); Assert(text == "Vanilla hover"); }
    c.Accessible = false; Unchanged(); c.Accessible = true;
    c.m_checkGuardStone = true; PrivateArea.Allowed = false;
    try { Unchanged(); } finally { PrivateArea.Allowed = true; c.m_checkGuardStone = false; }
    c.m_rootObjectOverride = new object(); Unchanged(); c.m_rootObjectOverride = null;
    VariaChestFocusPlugin.IsModEnabled = false;
    try { Unchanged(); } finally { VariaChestFocusPlugin.IsModEnabled = true; }
});
VisualLifetimeChecks.Run(Check);
if (args.Length > 0)
{
    Check("installed game bulk-transfer call sites match the transpiler", () => {
        using var game = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[0]);
        var inventory = game.MainModule.Types.Single(t => t.Name == "Inventory");
        foreach (string name in new[] { "StackAll", "MoveAll" })
        {
            var calls = inventory.Methods.Single(m => m.Name == name).Body.Instructions
                .Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>()
                .Where(m => m.DeclaringType.Name == "Inventory" && m.Name == "AddItem").ToArray();
            Assert(calls.Count(m => m.Parameters.Count == 1) == 1);
            Assert(calls.Count(m => m.Parameters.Count == 5) == (name == "MoveAll" ? 1 : 0));
        }
    });
    Check("installed game fragment creation shares materials at the patched call site", () => {
        using var game = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[0]);
        var fragments = game.MainModule.Types.Single(t => t.Name == "Destructible").Methods.Single(m => m.Name == "CreateFragments");
        Assert(fragments.IsStatic && fragments.Parameters.Select(p => p.ParameterType.FullName)
            .SequenceEqual(new[] { "UnityEngine.GameObject", "System.Boolean" }));
        var calls = fragments.Body.Instructions.Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().ToArray();
        Assert(calls.Count(m => m.DeclaringType.Name == "Renderer" && m.Name == "set_sharedMaterials") == 1);
        Assert(calls.Any(m => m.DeclaringType.Name == "Renderer" && m.Name == "get_sharedMaterials"));
    });
    Check("installed game patch targets and transfer overloads exist", () => {
        using var game = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[0]);
        Assert(game.MainModule.Types.Single(t => t.Name == "ObjectDB").Methods.Any(m => m.Name == "UpdateRegisters"));
        var container = game.MainModule.Types.Single(t => t.Name == "Container");
        foreach (string name in new[] { "Awake", "OnDestroyed", "OnContainerChanged", "Load", "Save", "GetHoverText" })
            Assert(container.Methods.Any(m => m.Name == name));
        var grid = game.MainModule.Types.Single(t => t.Name == "InventoryGrid");
        Assert(grid.Methods.Single(m => m.Name == "DropItem").Parameters.Select(p => p.Name)
            .SequenceEqual(new[] { "fromInventory", "item", "amount", "pos" }));
        var inventory = game.MainModule.Types.Single(t => t.Name == "Inventory");
        Assert(inventory.Methods.Any(m => m.Name == "MoveItemToThis" && m.Parameters.Count == 2));
        Assert(inventory.Methods.Any(m => m.Name == "MoveItemToThis" && m.Parameters.Count == 5));
    });
}
if (args.Length > 1)
{
    Check("installed AzuEPI exposes the exact slot delegate signature", () => {
        using var epi = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[1]);
        var api = epi.MainModule.Types.Single(t => t.FullName == "AzuEPI.API");
        var quickItems = api.Methods.Single(m => m.Name == "GetQuickSlotsItems");
        Assert(quickItems.IsPublic && quickItems.IsStatic && quickItems.Parameters.Count == 0);
        Assert(quickItems.ReturnType is Mono.Cecil.GenericInstanceType list
            && list.ElementType.FullName == "System.Collections.Generic.List`1"
            && list.GenericArguments.Single().Name == "ItemData");
        var method = api.Methods.Single(m => m.Name == "TryGetSlotIndexAtGridPos");
        Assert(method.IsStatic && method.IsPublic && method.ReturnType.FullName == "System.Boolean");
        Assert(method.Parameters.Select(p => p.ParameterType.FullName)
            .SequenceEqual(new[] { "Inventory", "Vector2i", "System.Int32&" }));
        var favorites = epi.MainModule.Types.Single(t => t.FullName == "AzuEPI.Game.Favoriting.UserConfig");
        var get = favorites.Methods.Single(m => m.Name == "GetPlayerConfig");
        Assert(get.IsPublic && get.IsStatic && get.ReturnType.FullName == favorites.FullName);
        Assert(get.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Int64" }));
        var check = favorites.Methods.Single(m => m.Name == "IsItemNameOrSlotFavorited");
        Assert(check.IsPublic && !check.IsStatic && check.ReturnType.FullName == "System.Boolean");
        Assert(check.Parameters.Count == 1 && check.Parameters[0].ParameterType.Name == "ItemData");
    });
}
Console.WriteLine($"{passed} regression checks passed.");
