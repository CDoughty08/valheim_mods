using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace VariaChestFocus
{
    internal static class QuickSort
    {
        private static readonly List<Dest> DestScratch = new List<Dest>(64);
        private static readonly List<ItemDrop.ItemData> ItemScratch = new List<ItemDrop.ItemData>(64);
        private static readonly List<ChestItem> ChestItemScratch = new List<ChestItem>(256);
        private static readonly HashSet<Container> DirtyContainers = new HashSet<Container>();
        private static bool _running;

        // Inventory emits multiple change callbacks per move. Keep other callbacks intact,
        // but serialize each affected chest only once at the end of this synchronous sort.
        internal static bool DeferSave(Container container)
        {
            if (!_running || container.m_loading || !container.IsOwner()) return false;
            DirtyContainers.Add(container);
            return true;
        }

        private struct Dest
        {
            public Container Container;
            public Inventory Inventory;
            public ChestSettings Settings;
            public float DistSq;
            public bool Filtered;
        }

        private struct ChestItem
        {
            public Dest Source;
            public ItemDrop.ItemData Item;
        }

        // UI callers may pass only their currently open container. All other occupied
        // storage remains excluded, and hotkeys use the default closed-chest behavior.
        internal static int Run(Player player, in ChestFocusConfigSnapshot cfg, Container openContainer = null)
        {
            return Execute(player, cfg, betweenChests: false, openContainer);
        }

        internal static int RunChests(Player player, in ChestFocusConfigSnapshot cfg, Container openContainer = null)
        {
            return Execute(player, cfg, betweenChests: true, openContainer);
        }

        private static int Execute(Player player, in ChestFocusConfigSnapshot cfg, bool betweenChests, Container openContainer)
        {
            if (_running) return 0;
            _running = true;
            try
            {
                return RunCore(player, cfg, betweenChests, openContainer);
            }
            finally
            {
                try
                {
                    ExceptionDispatchInfo saveFailure = null;
                    foreach (Container container in DirtyContainers)
                    {
                        if (container != null && container.m_nview != null
                            && container.m_nview.IsValid() && container.m_nview.IsOwner())
                        {
                            try
                            {
                                container.Save();
                            }
                            catch (Exception error)
                            {
                                // One failed serialization must not discard the pending saves
                                // for every other chest that already sent or received items.
                                VariaChestFocusPlugin.Log?.LogWarning("Chest Focus: cannot save sorted chest: " + error);
                                if (saveFailure == null) saveFailure = ExceptionDispatchInfo.Capture(error);
                            }
                        }
                    }
                    saveFailure?.Throw();
                }
                finally
                {
                    DirtyContainers.Clear();
                    DestScratch.Clear();
                    ItemScratch.Clear();
                    ChestItemScratch.Clear();
                    ContainerRegistry.ClearNearby();
                    _running = false;
                }
            }
        }

        private static int RunCore(Player player, in ChestFocusConfigSnapshot cfg, bool betweenChests, Container openContainer)
        {
            if (player == null || !cfg.Enabled)
            {
                return 0;
            }

            Inventory playerInv = player.GetInventory();
            if (!betweenChests && playerInv == null)
            {
                return 0;
            }

            Vector3 pos = player.transform.position;
            // Include in-place recipe/build-table changes once per press.
            CategoryDefs.Invalidate();
            List<Container> nearby = ContainerRegistry.GetNearby(pos, cfg.SortRange);
            DestScratch.Clear();

            for (int i = 0; i < nearby.Count; i++)
            {
                Container container = nearby[i];
                if (!ContainerAccess.CanModify(player, container, allowInUse: container == openContainer))
                {
                    continue;
                }

                Inventory inv = container.GetInventory();
                if (inv == null)
                {
                    continue;
                }

                ChestSettings settings = ChestSettingsStore.Get(container);
                if (settings.Priority == ChestPriority.Never)
                {
                    continue;
                }

                DestScratch.Add(new Dest
                {
                    Container = container,
                    Inventory = inv,
                    Settings = settings,
                    DistSq = (container.transform.position - pos).sqrMagnitude,
                    Filtered = !settings.IsEmpty
                });
            }

            DestScratch.Sort(CompareDest);

            int maxChests = Mathf.Max(1, cfg.MaxChests);
            if (DestScratch.Count > maxChests)
            {
                DestScratch.RemoveRange(maxChests, DestScratch.Count - maxChests);
            }

            // Settings and distance determine eligibility without deserializing inventories.
            // Refresh only the selected destinations, before making any transfers.
            for (int i = 0; i < DestScratch.Count; i++)
            {
                Dest dest = DestScratch[i];
                // The visible grid already holds the current live inventory and item references.
                if (dest.Container != openContainer) dest.Container.Load();
                dest.Inventory = dest.Container.GetInventory();
                DestScratch[i] = dest;
            }

            if (betweenChests) return MoveChestItems(player, cfg, openContainer);

            ItemScratch.Clear();
            List<ItemDrop.ItemData> all = playerInv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData item = all[i];
                if (item == null
                    || cfg.ProtectHotbar && item.m_gridPos.y == 0 && item.m_gridPos.x >= 0 && item.m_gridPos.x < 8
                    || EpiCompat.IsProtectedPlayerItem(player, item))
                {
                    continue;
                }

                ItemScratch.Add(item);
            }

            int moves = 0;
            int maxMoves = Mathf.Max(1, cfg.MaxMoves);

            for (int i = 0; i < ItemScratch.Count && moves < maxMoves; i++)
            {
                ItemDrop.ItemData item = ItemScratch[i];
                if (item == null || item.m_stack <= 0)
                {
                    continue;
                }

                // Item may have been moved already.
                if (!playerInv.ContainsItem(item))
                {
                    continue;
                }

                for (int d = 0; d < DestScratch.Count && moves < maxMoves; d++)
                {
                    Dest dest = DestScratch[d];
                    if (dest.Inventory == null || !dest.Settings.Allows(item)
                        || !ContainerAccess.CanModify(player, dest.Container, allowInUse: dest.Container == openContainer))
                    {
                        continue;
                    }

                    int amount = item.m_stack;
                    // Allow filling the remaining space in an existing stack.
                    if (amount <= 0 || !dest.Inventory.CanAddItem(item, 1))
                    {
                        continue;
                    }

                    int before = item.m_stack;
                    dest.Inventory.MoveItemToThis(playerInv, item);
                    bool moved = !playerInv.ContainsItem(item) || item.m_stack < before;
                    if (moved)
                    {
                        moves++;
                        // Inventory's change callback queues the owning container for saving.
                        // A partial transfer can continue into the next matching chest.
                        if (!playerInv.ContainsItem(item) || item.m_stack <= 0)
                        {
                            break;
                        }
                    }
                }
            }

            return moves;
        }

        private static int MoveChestItems(Player player, in ChestFocusConfigSnapshot cfg, Container openContainer)
        {
            // Snapshot every source before any move. An item arriving in another chest
            // must not become new work during the same press.
            foreach (Dest source in DestScratch)
            {
                if (source.Inventory == null) continue;
                foreach (ItemDrop.ItemData item in source.Inventory.GetAllItems())
                    if (item != null) ChestItemScratch.Add(new ChestItem { Source = source, Item = item });
            }

            int moves = 0;
            int maxMoves = Mathf.Max(1, cfg.MaxMoves);
            foreach (ChestItem entry in ChestItemScratch)
            {
                if (moves >= maxMoves) break;
                Dest source = entry.Source;
                ItemDrop.ItemData item = entry.Item;
                if (item.m_stack <= 0 || !source.Inventory.ContainsItem(item)
                    || !ContainerAccess.CanModify(player, source.Container, allowInUse: source.Container == openContainer)) continue;

                bool belongsHere = source.Settings.Allows(item);
                foreach (Dest dest in DestScratch)
                {
                    if (moves >= maxMoves) break;
                    if (dest.Inventory == null || dest.Inventory == source.Inventory
                        || !dest.Settings.Allows(item)
                        || belongsHere && CompareRank(dest, source) >= 0
                        || !ContainerAccess.CanModify(player, dest.Container, allowInUse: dest.Container == openContainer)
                        || !dest.Inventory.CanAddItem(item, 1)) continue;

                    int before = item.m_stack;
                    dest.Inventory.MoveItemToThis(source.Inventory, item);
                    if (!source.Inventory.ContainsItem(item) || item.m_stack < before)
                    {
                        moves++;
                        // Both inventories issue change callbacks; both chests must be saved.
                        if (!source.Inventory.ContainsItem(item) || item.m_stack <= 0) break;
                    }
                }
            }
            return moves;
        }

        private static int CompareDest(Dest a, Dest b)
        {
            int rank = CompareRank(a, b);
            return rank != 0 ? rank : a.DistSq.CompareTo(b.DistSq);
        }

        private static int CompareRank(Dest a, Dest b)
        {
            // Dedicated storage always precedes catch-all storage, regardless of priority.
            if (a.Filtered != b.Filtered)
            {
                return a.Filtered ? -1 : 1;
            }

            // Distance chooses a destination, but is never a reason to empty an
            // equally suitable source chest when the player moves around the base.
            return ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority);
        }
    }
}
