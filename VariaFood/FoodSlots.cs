using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VariaFood
{
    /// <summary>
    /// 4 solid-food + N drink-slot eat rules (no Harmony attributes).
    /// </summary>
    internal static class FoodSlots
    {
        private static readonly StringBuilder MessageScratch = new StringBuilder(64);

        internal static int SlotLimit(bool isDrink, in FoodConfigSnapshot cfg)
        {
            return isDrink ? cfg.DrinkSlots : cfg.FoodSlots;
        }

        internal static int TotalSlots(in FoodConfigSnapshot cfg)
        {
            return cfg.FoodSlots + cfg.DrinkSlots;
        }

        internal static bool WantsDrinkSlot(ItemDrop.ItemData item, string prefabHint, in FoodConfigSnapshot cfg)
        {
            return cfg.DrinkSlots > 0 && DrinkClassifier.IsDrink(item, prefabHint);
        }

        internal static bool CanEat(Player player, ItemDrop.ItemData item, bool showMessages, in FoodConfigSnapshot cfg)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            List<Player.Food> foods = player.m_foods;
            string itemName = item.m_shared.m_name;

            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item?.m_shared?.m_name != itemName)
                {
                    continue;
                }

                if (food.CanEatAgain())
                {
                    return true;
                }

                if (showMessages)
                {
                    player.Message(
                        MessageHud.MessageType.Center,
                        Localization.instance.Localize("$msg_nomore", itemName));
                }

                return false;
            }

            bool wantDrink = WantsDrinkSlot(item, null, cfg);
            int limit = SlotLimit(wantDrink, cfg);
            int used = 0;
            bool sameCatDepleted = false;

            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item == null)
                {
                    continue;
                }

                bool isDrink = WantsDrinkSlot(food.m_item, food.m_name, cfg);
                if (isDrink != wantDrink)
                {
                    continue;
                }

                used++;
                if (food.CanEatAgain())
                {
                    sameCatDepleted = true;
                }
            }

            if (used < limit || sameCatDepleted)
            {
                return true;
            }

            if (showMessages)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_isfull");
            }

            return false;
        }

        internal static bool EatFood(Player player, ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            if (!CanEat(player, item, showMessages: false, cfg))
            {
                return false;
            }

            string msg = BuildEatMessage(item, cfg);
            if (msg.Length > 0)
            {
                player.Message(MessageHud.MessageType.Center, msg);
            }

            List<Player.Food> foods = player.m_foods;
            string itemName = item.m_shared.m_name;
            bool wantDrink = WantsDrinkSlot(item, null, cfg);

            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item?.m_shared?.m_name != itemName)
                {
                    continue;
                }

                if (!food.CanEatAgain())
                {
                    return false;
                }

                Fill(food, item, cfg);
                IncrementFoodStats(item);
                player.UpdateFood(0f, forceUpdate: true);
                return true;
            }

            if (CountCategory(foods, wantDrink, cfg) < SlotLimit(wantDrink, cfg))
            {
                if (item.m_dropPrefab == null)
                {
                    return false;
                }

                Player.Food added = new Player.Food
                {
                    m_name = item.m_dropPrefab.name,
                    m_item = item
                };
                Fill(added, item, cfg);
                foods.Add(added);
                IncrementFoodStats(item);
                player.UpdateFood(0f, forceUpdate: true);
                return true;
            }

            Player.Food target = MostDepletedInCategory(foods, wantDrink, cfg);
            if (target == null)
            {
                return false;
            }

            if (item.m_dropPrefab == null)
            {
                return false;
            }

            target.m_name = item.m_dropPrefab.name;
            target.m_item = item;
            Fill(target, item, cfg);
            IncrementFoodStats(item);
            player.UpdateFood(0f, forceUpdate: true);
            return true;
        }

        /// <summary>
        /// Additive: zero-stat drinks occupy the drink slot when free; never blocks the potion.
        /// </summary>
        internal static void TryRouteDrink(Player player, ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            DrinkDisplay.Add(player, item, cfg);
        }

        internal static void Fill(Player.Food food, ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            food.m_item = item;
            if (item.m_dropPrefab != null) food.m_name = item.m_dropPrefab.name;
            food.m_time = FoodMath.EffectiveBurnTime(item, cfg);
            food.m_health = shared.m_food * cfg.HealthMult;
            food.m_stamina = shared.m_foodStamina * cfg.StaminaMult;
            food.m_eitr = shared.m_foodEitr * cfg.EitrMult;
        }

        private static int CountCategory(List<Player.Food> foods, bool wantDrink, in FoodConfigSnapshot cfg)
        {
            int used = 0;
            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item == null)
                {
                    continue;
                }

                if (WantsDrinkSlot(food.m_item, food.m_name, cfg) == wantDrink)
                {
                    used++;
                }
            }

            return used;
        }

        private static Player.Food MostDepletedInCategory(
            List<Player.Food> foods,
            bool wantDrink,
            in FoodConfigSnapshot cfg)
        {
            Player.Food best = null;
            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item == null)
                {
                    continue;
                }

                if (WantsDrinkSlot(food.m_item, food.m_name, cfg) != wantDrink)
                {
                    continue;
                }

                if (!food.CanEatAgain())
                {
                    continue;
                }

                if (best == null || food.m_time < best.m_time)
                {
                    best = food;
                }
            }

            return best;
        }

        private static string BuildEatMessage(ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            MessageScratch.Clear();
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            float hp = shared.m_food * cfg.HealthMult;
            float stamina = shared.m_foodStamina * cfg.StaminaMult;
            float eitr = shared.m_foodEitr * cfg.EitrMult;

            if (hp > 0f)
            {
                MessageScratch.Append(" +").Append(hp).Append(" $item_food_health ");
            }

            if (stamina > 0f)
            {
                MessageScratch.Append(" +").Append(stamina).Append(" $item_food_stamina ");
            }

            if (eitr > 0f)
            {
                MessageScratch.Append(" +").Append(eitr).Append(" $item_food_eitr ");
            }

            return MessageScratch.ToString();
        }

        private static void IncrementFoodStats(ItemDrop.ItemData item)
        {
            if (Game.instance == null || FoodMath.IsDisplayOnlyDrink(item.m_shared))
            {
                return;
            }

            Game.instance.IncrementPlayerStat(PlayerStatType.FoodEaten);
            Game.instance.GetPlayerProfile().IncrementStatFoodEaten(item.m_shared.m_name, 1f, item.m_cheated);
        }
    }
}
