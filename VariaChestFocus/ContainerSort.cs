using System;
using System.Collections.Generic;

namespace VariaChestFocus
{
    internal static class ContainerSort
    {
        internal static bool Run(Player player, Container container)
        {
            if (!VariaChestFocusPlugin.IsModEnabled
                || !ContainerAccess.CanModify(player, container, allowInUse: true)) return false;

            // The open chest already has its live inventory. Keep the original objects,
            // stacks and metadata; changing grid positions also permits excluded old items.
            Inventory inventory = container.GetInventory();
            if (inventory == null) return false;
            int width = inventory.GetWidth();
            int height = inventory.GetHeight();
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            if (width <= 0 || height <= 0 || items.Count > (long)width * height
                || items.Exists(item => item?.m_shared == null)) return false;

            items.Sort(CompareItems);
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                var position = new Vector2i(i % width, i / width);
                if (items[i].m_gridPos.x != position.x || items[i].m_gridPos.y != position.y)
                {
                    items[i].m_gridPos = position;
                    changed = true;
                }
            }
            if (changed) inventory.Changed();
            return true;
        }

        private static int CompareItems(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            int order = a.m_shared.m_itemType.CompareTo(b.m_shared.m_itemType);
            if (order == 0) order = StringComparer.CurrentCultureIgnoreCase.Compare(
                ItemUtil.GetDisplayName(a), ItemUtil.GetDisplayName(b));
            if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(ItemUtil.GetPrefabName(a), ItemUtil.GetPrefabName(b));
            if (order == 0) order = b.m_quality.CompareTo(a.m_quality);
            if (order == 0) order = b.m_stack.CompareTo(a.m_stack);
            // Stable ties keep duplicate items from swapping on subsequent presses.
            if (order == 0) order = a.m_gridPos.y.CompareTo(b.m_gridPos.y);
            if (order == 0) order = a.m_gridPos.x.CompareTo(b.m_gridPos.x);
            return order;
        }
    }
}
