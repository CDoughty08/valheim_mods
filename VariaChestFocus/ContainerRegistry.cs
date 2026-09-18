using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VariaChestFocus
{
    internal static class ContainerRegistry
    {
        private static readonly ConditionalWeakTable<Inventory, Container> ByInventory =
            new ConditionalWeakTable<Inventory, Container>();

        // Container.OnDestroyed is damage destruction, not Unity's scene-unload callback.
        private static readonly List<WeakReference<Container>> All = new List<WeakReference<Container>>(128);
        private static readonly List<Container> NearbyScratch = new List<Container>(64);

        internal static void Register(Container container)
        {
            if (container == null)
            {
                return;
            }

            // Shared root ZDO (cart/ship) — skip until child-keyed storage exists.
            if (container.m_rootObjectOverride != null || container.GetComponent<TombStone>() != null)
            {
                return;
            }

            Inventory inventory = container.GetInventory();
            if (inventory == null)
            {
                return;
            }

            bool registered = ByInventory.TryGetValue(inventory, out Container existing) && existing == container;
            ByInventory.Remove(inventory);
            ByInventory.Add(inventory, container);

            if (!registered)
            {
                All.Add(new WeakReference<Container>(container));
            }
        }

        internal static void Unregister(Container container)
        {
            if (container == null)
            {
                return;
            }

            Inventory inventory = container.m_inventory;
            if (inventory != null)
            {
                ByInventory.Remove(inventory);
            }

            All.RemoveAll(reference => !reference.TryGetTarget(out Container target) || target == null || target == container);
            ChestSettingsStore.Invalidate(container);
        }

        internal static bool TryGetContainer(Inventory inventory, out Container container)
        {
            container = null;
            if (inventory == null)
            {
                return false;
            }

            return ByInventory.TryGetValue(inventory, out container) && container != null;
        }

        internal static List<Container> GetNearby(Vector3 position, float range)
        {
            NearbyScratch.Clear();
            float rangeSq = range * range;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (!All[i].TryGetTarget(out Container container) || container == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if ((container.transform.position - position).sqrMagnitude <= rangeSq)
                {
                    NearbyScratch.Add(container);
                }
            }

            return NearbyScratch;
        }

        internal static int RegisteredCount => All.Count;

        internal static void ClearNearby() => NearbyScratch.Clear();
    }
}
