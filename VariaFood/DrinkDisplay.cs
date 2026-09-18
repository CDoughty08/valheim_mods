using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VariaFood
{
    /// <summary>Transient potion indicators. Never part of the player's gameplay/save food list.</summary>
    internal static class DrinkDisplay
    {
        private static ConditionalWeakTable<Player, List<Player.Food>> Entries = new ConditionalWeakTable<Player, List<Player.Food>>();

        internal static void Clear(Player player) => Entries.Remove(player);
        internal static void ClearAll() => Entries = new ConditionalWeakTable<Player, List<Player.Food>>();

        internal static void Add(Player player, ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            if (player == null || !cfg.Enabled || !cfg.ExtraSlots || cfg.DrinkSlots <= 0
                || !FoodMath.IsDisplayOnlyDrink(item?.m_shared) || !DrinkClassifier.IsDrink(item)
                || item.m_dropPrefab == null) return;
            Update(player, 0f, cfg);
            List<Player.Food> drinks = Entries.GetValue(player, _ => new List<Player.Food>(2));
            Player.Food target = null;
            foreach (Player.Food drink in drinks)
            {
                if (drink.m_item.m_shared.m_name == item.m_shared.m_name) { target = drink; break; }
            }
            int used = 0;
            foreach (Player.Food food in player.m_foods)
                if (FoodSlots.WantsDrinkSlot(food.m_item, food.m_name, cfg)) used++;
            if (target == null)
            {
                if (used + drinks.Count >= cfg.DrinkSlots) return;
                target = new Player.Food();
                drinks.Add(target);
            }
            FoodSlots.Fill(target, item, cfg);
            Update(player, 0f, cfg);
        }

        internal static void Update(Player player, float dt, in FoodConfigSnapshot cfg)
        {
            if (!cfg.Enabled || !cfg.ExtraSlots || cfg.DrinkSlots <= 0) { Clear(player); return; }
            if (!Entries.TryGetValue(player, out List<Player.Food> drinks)) return;
            for (int i = drinks.Count - 1; i >= 0; i--)
            {
                Player.Food drink = drinks[i];
                StatusEffect effect = drink.m_item.m_shared.m_consumeStatusEffect;
                if (effect != null && effect.m_ttl > 0f)
                {
                    StatusEffect active = player.m_seman.GetStatusEffect(effect.NameHash());
                    drink.m_time = active != null ? active.GetRemaningTime() : 0f;
                }
                else drink.m_time -= dt;
                if (drink.m_time <= 0f || i >= cfg.DrinkSlots) drinks.RemoveAt(i);
            }
        }

        internal static void AppendTo(Player player, List<Player.Food> display, in FoodConfigSnapshot cfg)
        {
            if (!cfg.Enabled || !cfg.ExtraSlots || cfg.DrinkSlots <= 0) return;
            if (Entries.TryGetValue(player, out List<Player.Food> drinks)) display.AddRange(drinks);
        }
    }
}
