using UnityEngine;
using VariaChestFocus;

internal static class SortingChecks
{
    private static void Assert(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    private static ChestFocusConfigSnapshot Config(int moves = 60) => new()
        { Enabled = true, ProtectHotbar = true, SortRange = 16, MaxChests = 40, MaxMoves = moves };

    private static ItemDrop.ItemData Item(string name, int stack = 1, int x = 0, int y = 0)
    {
        var item = new ItemDrop.ItemData
            { m_dropPrefab = new GameObject { name = name }, m_stack = stack, m_gridPos = new Vector2i(x, y) };
        item.m_shared.m_name = name;
        item.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
        return item;
    }

    private sealed class Storage : IDisposable
    {
        private readonly List<Container> _chests = new();
        public Container Add(string filter = null, ChestPriority priority = ChestPriority.Medium,
            int capacity = 100, float distance = 0)
        {
            var chest = new Container();
            chest.m_inventory.Capacity = capacity;
            chest.transform.position = new Vector3 { x = distance };
            ContainerRegistry.Register(chest);
            var settings = new ChestSettings { Priority = priority };
            if (filter != null) settings.AllowedPrefabs.Add(filter);
            ChestSettingsStore.TrySet(chest, settings);
            _chests.Add(chest);
            return chest;
        }
        public void Dispose() { foreach (var chest in _chests) ContainerRegistry.Unregister(chest); }
    }

    private static int Total(Container chest) => chest.m_inventory.Items.Sum(item => item.m_stack);

    internal static void Run(Action<string, Action> check)
    {
        check("container sort compacts the live grid and preserves every item and stack", () => {
            using var storage = new Storage();
            var chest = storage.Add("Wood", ChestPriority.Never);
            chest.InUse = true; // The button operates on the player's open chest.
            chest.m_inventory.Width = 2;
            chest.m_inventory.Height = 3;
            var wood = Item("Wood", 4, 1, 2);
            var iron = Item("Iron", 6, 0, 2); iron.m_quality = 2;
            iron.m_customData["mod-enchantment"] = "keep me";
            var food = Item("Food", 2, 1, 1); food.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            var moreWood = Item("Wood", 3, 0, 1);
            chest.m_inventory.Items.AddRange(new[] { wood, iron, moreWood, food });
            Assert(ContainerSort.Run(new Player(), chest));
            Assert(chest.m_inventory.GetItemAt(0, 0) == food && chest.m_inventory.GetItemAt(1, 0) == iron);
            Assert(chest.m_inventory.GetItemAt(0, 1) == wood && chest.m_inventory.GetItemAt(1, 1) == moreWood);
            Assert(Total(chest) == 15 && chest.m_inventory.Items.Count == 4 && iron.m_customData["mod-enchantment"] == "keep me");
            Assert(chest.Saves == 1 && chest.Loads == 0 && chest.SavedTotal == 15);
            Assert(ContainerSort.Run(new Player(), chest) && chest.Saves == 1, "Already sorted is a no-op");
        });

        check("container sort refuses denied access and invalid or overfull live grids without mutation", () => {
            using var storage = new Storage();
            var chest = storage.Add();
            var item = Item("Wood", 4, 1, 1); chest.m_inventory.Items.Add(item);
            void Blocked()
            {
                Assert(!ContainerSort.Run(new Player(), chest));
                Assert(item.m_gridPos.x == 1 && item.m_gridPos.y == 1 && chest.Saves == 0);
            }
            chest.Accessible = false; Blocked(); chest.Accessible = true;
            chest.m_nview.Owner = false; Blocked(); chest.m_nview.Owner = true;
            chest.m_nview.Valid = false; Blocked(); chest.m_nview.Valid = true;
            chest.m_loading = true; Blocked(); chest.m_loading = false;
            chest.Grave = new TombStone(); Blocked(); chest.Grave = null;
            chest.m_rootObjectOverride = new object(); Blocked(); chest.m_rootObjectOverride = null;
            chest.m_checkGuardStone = true; PrivateArea.Allowed = false;
            try { Blocked(); } finally { PrivateArea.Allowed = true; chest.m_checkGuardStone = false; }
            VariaChestFocusPlugin.IsModEnabled = false;
            try { Blocked(); } finally { VariaChestFocusPlugin.IsModEnabled = true; }
            chest.m_inventory.Width = 0; Blocked(); chest.m_inventory.Width = 1;
            chest.m_inventory.Height = 1; chest.m_inventory.Items.Add(Item("Stone")); Blocked();
        });

        check("chest sorting scans closed nearby chests and leaves player inventory untouched", () => {
            using var storage = new Storage();
            var source = storage.Add(priority: ChestPriority.Critical);
            var dest = storage.Add(priority: ChestPriority.Low, distance: 8);
            ChestSettingsStore.TrySet(dest, new ChestSettings { Priority = ChestPriority.Low, CategoryFlags = CategoryId.Wood });
            source.m_inventory.Items.Add(Item("Wood", 8));
            var player = new Player(); var carried = Item("Wood", 12); player.Inventory.Items.Add(carried);
            Assert(QuickSort.RunChests(player, Config()) == 1);
            Assert(Total(source) == 0 && Total(dest) == 8 && player.Inventory.Items.Single() == carried && carried.m_stack == 12);
            Assert(source.Saves == 1 && dest.Saves == 1 && source.SavedTotal == 0 && dest.SavedTotal == 8);
            Assert(source.Loads == 1 && dest.Loads == 1);
            Assert(QuickSort.RunChests(player, Config()) == 0);
        });

        check("area-sort buttons include only the current open chest and retain its live inventory", () => {
            foreach (bool openSource in new[] { true, false })
            {
                using var storage = new Storage();
                var source = storage.Add(); var dest = storage.Add("Wood");
                var occupied = storage.Add("Wood", ChestPriority.Critical);
                occupied.InUse = true; occupied.m_inventory.Items.Add(Item("Wood", 11));
                source.m_inventory.Items.Add(Item("Wood", 8));
                var open = openSource ? source : dest; open.InUse = true;
                var liveInventory = open.m_inventory;
                var player = new Player(); var carried = Item("Wood", 12); player.Inventory.Items.Add(carried);
                Assert(QuickSort.RunChests(player, Config()) == 0, "Hotkeys still skip occupied chests");
                int openLoads = open.Loads;
                Assert(QuickSort.RunChests(player, Config(), open) == 1);
                Assert(Total(source) == 0 && Total(dest) == 8 && Total(occupied) == 11);
                Assert(open.Loads == openLoads && open.m_inventory == liveInventory);
                Assert(occupied.Saves == 0 && occupied.Loads == 0);
                Assert(source.Saves == 1 && dest.Saves == 1 && source.SavedTotal == 0 && dest.SavedTotal == 8);
                Assert(player.Inventory.Items.Single() == carried && carried.m_stack == 12);
            }
        });

        check("an open chest still requires access and ownership and obeys exclusions and caps", () => {
            foreach (bool openSource in new[] { true, false })
            {
                using var storage = new Storage();
                var source = storage.Add(); var dest = storage.Add("Wood");
                source.m_inventory.Items.Add(Item("Wood", 3));
                var open = openSource ? source : dest; open.InUse = true;
                void Blocked(ChestFocusConfigSnapshot cfg)
                {
                    Assert(QuickSort.RunChests(new Player(), cfg, open) == 0);
                    Assert(Total(source) == 3 && Total(dest) == 0 && source.Saves == 0 && dest.Saves == 0);
                    Assert(open.Loads == 0);
                }
                open.Accessible = false; Blocked(Config()); open.Accessible = true;
                open.m_nview.Owner = false; Blocked(Config()); open.m_nview.Owner = true;
                open.m_nview.Valid = false; Blocked(Config()); open.m_nview.Valid = true;
                open.m_loading = true; Blocked(Config()); open.m_loading = false;
                open.Grave = new TombStone(); Blocked(Config()); open.Grave = null;
                open.m_rootObjectOverride = new object(); Blocked(Config()); open.m_rootObjectOverride = null;
                open.transform.position = new Vector3 { x = 17 }; Blocked(Config()); open.transform.position = new Vector3();
                open.m_checkGuardStone = true; PrivateArea.Allowed = false;
                try { Blocked(Config()); } finally { PrivateArea.Allowed = true; open.m_checkGuardStone = false; }
                var settings = ChestSettingsStore.Get(open).Clone();
                settings.Priority = ChestPriority.Never; ChestSettingsStore.TrySet(open, settings); Blocked(Config());
                settings.Priority = ChestPriority.Medium; ChestSettingsStore.TrySet(open, settings);
                var cfg = Config(); cfg.Enabled = false; Blocked(cfg);
                cfg = Config(); cfg.MaxChests = 1; Blocked(cfg);
                Assert(QuickSort.RunChests(new Player(), Config(), open) == 1 && open.Loads == 0);
            }
        });

        check("area-sort move limits save partial work in the current open chest", () => {
            using var storage = new Storage();
            var source = storage.Add(); source.InUse = true;
            var dest = storage.Add("Wood", capacity: 5);
            var overflow = storage.Add("Wood", distance: 1);
            source.m_inventory.Items.Add(Item("Wood", 10));
            Assert(QuickSort.RunChests(new Player(), Config(1), source) == 1);
            Assert(Total(source) == 5 && Total(dest) == 5 && Total(overflow) == 0 && source.Loads == 0);
            Assert(source.Saves == 1 && dest.Saves == 1 && source.SavedTotal == 5);
            Assert(QuickSort.RunChests(new Player(), Config(), source) == 1 && Total(overflow) == 5);
        });

        check("chest sorting uses priority then distance with partial overflow and conservation", () => {
            using var storage = new Storage();
            var source = storage.Add("Wood", ChestPriority.Low);
            var high = storage.Add("Wood", ChestPriority.Critical, 5, 9);
            var near = storage.Add("Wood", ChestPriority.High, 7, 2);
            var far = storage.Add("Wood", ChestPriority.High, 100, 5);
            source.m_inventory.Items.Add(Item("Wood", 20));
            Assert(QuickSort.RunChests(new Player(), Config()) == 3);
            Assert(Total(source) == 0 && Total(high) == 5 && Total(near) == 7 && Total(far) == 8);
            Assert(new[] { source, high, near, far }.All(c => c.Saves == 1 && c.SavedTotal == Total(c)));
            Assert(QuickSort.RunChests(new Player(), Config()) == 0, "Overflow must remain stable");
        });

        check("equally suitable chests stay put even when the player changes position", () => {
            using var storage = new Storage();
            var a = storage.Add("Wood", distance: 1); var b = storage.Add("Wood", distance: 8);
            a.m_inventory.Items.Add(Item("Wood", 3)); b.m_inventory.Items.Add(Item("Wood", 6));
            var player = new Player();
            Assert(QuickSort.RunChests(player, Config()) == 0);
            player.transform.position = new Vector3 { x = 9 };
            Assert(QuickSort.RunChests(player, Config()) == 0 && a.Saves == 0 && b.Saves == 0);
        });

        check("excluded contents can leave a filtered chest for a lower-priority catch-all", () => {
            using var storage = new Storage();
            var source = storage.Add("Wood", ChestPriority.Critical);
            var catchAll = storage.Add(priority: ChestPriority.Low);
            source.m_inventory.Items.AddRange(new[] { Item("Stone", 4), Item("Wood", 3) });
            Assert(QuickSort.RunChests(new Player(), Config()) == 1);
            Assert(source.m_inventory.Items.Single().m_shared.m_name == "Wood" && Total(catchAll) == 4);
            Assert(QuickSort.RunChests(new Player(), Config()) == 0);
        });

        check("unrestricted storage consolidates toward higher priority without filter matches", () => {
            using var storage = new Storage();
            var source = storage.Add(priority: ChestPriority.Low);
            var dest = storage.Add(priority: ChestPriority.High);
            source.m_inventory.Items.Add(Item("Stone", 4));
            Assert(QuickSort.RunChests(new Player(), Config()) == 1 && Total(dest) == 4);
        });

        check("Never chests are excluded as both sources and destinations", () => {
            using var storage = new Storage();
            var never = storage.Add("Wood", ChestPriority.Never);
            var source = storage.Add(); var dest = storage.Add("Wood");
            never.m_inventory.Items.Add(Item("Wood", 8)); source.m_inventory.Items.Add(Item("Wood", 3));
            Assert(QuickSort.RunChests(new Player(), Config()) == 1);
            Assert(Total(never) == 8 && never.Saves == 0 && never.Loads == 0 && Total(dest) == 3);
            Assert(QuickSort.RunChests(new Player(), Config()) == 0);
        });

        check("chest sorting checks range, access, wards, ownership and use on both ends", () => {
            foreach (bool guardSource in new[] { true, false })
            {
                using var storage = new Storage();
                var source = storage.Add(); var dest = storage.Add("Wood");
                source.m_inventory.Items.Add(Item("Wood", 3));
                var guarded = guardSource ? source : dest;
                void Blocked()
                {
                    Assert(QuickSort.RunChests(new Player(), Config()) == 0);
                    Assert(Total(source) == 3 && Total(dest) == 0 && source.Saves == 0 && dest.Saves == 0);
                }
                guarded.Accessible = false; Blocked(); guarded.Accessible = true;
                guarded.m_nview.Owner = false; Blocked(); guarded.m_nview.Owner = true;
                guarded.m_nview.Valid = false; Blocked(); guarded.m_nview.Valid = true;
                guarded.InUse = true; Blocked(); guarded.InUse = false;
                guarded.m_loading = true; Blocked(); guarded.m_loading = false;
                guarded.Grave = new TombStone(); Blocked(); guarded.Grave = null;
                guarded.m_rootObjectOverride = new object(); Blocked(); guarded.m_rootObjectOverride = null;
                guarded.transform.position = new Vector3 { x = 17 }; Blocked(); guarded.transform.position = new Vector3();
                guarded.m_checkGuardStone = true; PrivateArea.Allowed = false;
                try { Blocked(); } finally { PrivateArea.Allowed = true; guarded.m_checkGuardStone = false; }
                Assert(QuickSort.RunChests(new Player(), Config()) == 1);
            }
        });

        check("chest sorting respects disabled state and chest and move caps", () => {
            using var storage = new Storage();
            var source = storage.Add(); var dest = storage.Add("Wood", capacity: 5);
            var overflow = storage.Add("Wood", distance: 1);
            source.m_inventory.Items.Add(Item("Wood", 10));
            var cfg = Config(); cfg.Enabled = false;
            Assert(QuickSort.RunChests(new Player(), cfg) == 0 && source.Loads == 0);
            cfg.Enabled = true; cfg.MaxChests = 2;
            Assert(QuickSort.RunChests(new Player(), cfg) == 0 && source.Loads == 0);
            Assert(QuickSort.RunChests(new Player(), Config(1)) == 1);
            Assert(Total(source) == 5 && Total(dest) == 5 && Total(overflow) == 0);
            Assert(source.Saves == 1 && dest.Saves == 1 && overflow.Saves == 0);
            Assert(QuickSort.RunChests(new Player(), Config()) == 1 && Total(overflow) == 5);
        });

        check("chest sorting saves both sides of partial work and recovers after transfer failure", () => {
            using var storage = new Storage();
            var source = storage.Add(); var dest = storage.Add("Wood", capacity: 5);
            var failure = storage.Add("Wood", distance: 1); failure.m_inventory.ThrowOnMove = true;
            source.m_inventory.Items.Add(Item("Wood", 10));
            bool threw = false;
            try { QuickSort.RunChests(new Player(), Config()); } catch (InvalidOperationException) { threw = true; }
            Assert(threw && source.SavedTotal == 5 && dest.SavedTotal == 5 && source.Saves == 1 && dest.Saves == 1);
            failure.m_inventory.ThrowOnMove = false;
            Assert(QuickSort.RunChests(new Player(), Config()) == 1 && Total(failure) == 5);
        });

        check("chest sorting still saves the other end if source or destination serialization fails", () => {
            foreach (bool failSource in new[] { true, false })
            {
                using var storage = new Storage();
                var source = storage.Add(); var dest = storage.Add("Wood");
                source.m_inventory.Items.Add(Item("Wood", 3));
                var failed = failSource ? source : dest; failed.ThrowOnSave = true;
                bool threw = false;
                try { QuickSort.RunChests(new Player(), Config()); } catch (InvalidOperationException) { threw = true; }
                Assert(threw && source.Saves == 1 && dest.Saves == 1 && Total(source) + Total(dest) == 3);
                failed.ThrowOnSave = false;
                source.m_inventory.Items.Add(Item("Wood", 2));
                Assert(QuickSort.RunChests(new Player(), Config()) == 1 && Total(dest) == 5);
            }
        });
    }
}
