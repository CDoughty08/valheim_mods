using System.Collections.Generic;
using HarmonyLib;

namespace VariaFood.Patches
{
    /// <summary>
    /// Status effects do not persist across load, but m_foods does — drop zero-stat drink
    /// icons so they cannot permanently occupy the drink slot after relog.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Load), new System.Type[] { typeof(ZPackage) })]
    internal static class PlayerLoadFoodPatch
    {
        private static void Postfix(Player __instance)
        {
            DrinkDisplay.Clear(__instance);
            List<Player.Food> foods = __instance.m_foods;
            for (int i = foods.Count - 1; i >= 0; i--)
            {
                Player.Food food = foods[i];
                if (food?.m_item?.m_shared == null)
                {
                    foods.RemoveAt(i);
                    continue;
                }

                ItemDrop.ItemData.SharedData shared = food.m_item.m_shared;
                if (FoodMath.IsDisplayOnlyDrink(shared))
                {
                    foods.RemoveAt(i);
                }
            }
        }
    }
}
