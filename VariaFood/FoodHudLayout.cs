using System.Collections.Generic;

namespace VariaFood
{
    internal static class FoodHudLayout
    {
        // Maps visual slots to the original food-list index, without reordering
        // the player's saved food list or inserting placeholder foods into it.
        internal static void MapSlots(List<Player.Food> foods, in FoodConfigSnapshot cfg, int[] sources)
        {
            for (int i = 0; i < sources.Length; i++) sources[i] = -1;

            int solids = 0;
            int drinks = 0;
            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food?.m_item?.m_shared == null) continue;
                bool drink = FoodSlots.WantsDrinkSlot(food.m_item, food.m_name, cfg);
                int ordinal = drink ? drinks++ : solids++;
                if (ordinal >= FoodSlots.SlotLimit(drink, cfg)) continue;
                int slot = (drink ? cfg.FoodSlots : 0) + ordinal;
                if (slot < sources.Length) sources[slot] = i;
            }
        }
    }
}
