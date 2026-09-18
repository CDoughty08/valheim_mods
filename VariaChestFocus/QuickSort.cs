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

        internal static int Run(Player player, in ChestFocusConfigSnapshot cfg)
        {
            if (_running) return 0;
            _running = true;
            try
            {
                return RunCore(player, cfg);
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
                                // for every other chest that already received player items.
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
                    ContainerRegistry.ClearNearby();
                    _running = false;
                }
            }
        }

        private static int RunCore(Player player, in ChestFocusConfigSnapshot cfg)
        {
            if (player == null || !cfg.Enabled)
            {
                return 0;
            }

            Inventory playerInv = player.GetInventory();
            if (playerInv == null)
            {
                return 0;
            }

            Vector3 pos = player.transform.position;
            // Include in-place recipe/build-table changes once per press.
            CategoryDefs.Invalidate();
            List<Container> nearby = ContainerRegistry.GetNearby(pos, cfg.SortRange);
            DestScratch.Clear();

            long playerId = Game.instance != null ? Game.instance.GetPlayerProfile().GetPlayerID() : 0L;

            for (int i = 0; i < nearby.Count; i++)
            {
                Container container = nearby[i];
                if (container == null || container.IsInUse())
                {
                    continue;
                }

                if (!container.CheckAccess(playerId)
                    || container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false))
                {
                    continue;
                }

                ZNetView nview = container.m_nview;
                if (nview == null || !nview.IsValid() || !nview.IsOwner())
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
                dest.Container.Load();
                dest.Inventory = dest.Container.GetInventory();
                DestScratch[i] = dest;
            }

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
                    if (dest.Inventory == null || !dest.Settings.Allows(item))
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

        private static int CompareDest(Dest a, Dest b)
        {
            // Dedicated storage always precedes catch-all storage, regardless of priority.
            if (a.Filtered != b.Filtered)
            {
                return a.Filtered ? -1 : 1;
            }

            int p = ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority);
            if (p != 0)
            {
                return p;
            }

            return a.DistSq.CompareTo(b.DistSq);
        }
    }
}
