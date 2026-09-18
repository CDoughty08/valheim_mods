using UnityEngine;

namespace VariaFood
{
    /// <summary>
    /// Burn-time helpers for solids (vanilla burn × duration mult) and zero-burn drinks
    /// (status-effect TTL or config fallback). Never stores per-entry burn — always recomputed.
    /// </summary>
    internal static class FoodMath
    {
        internal static bool IsDisplayOnlyDrink(ItemDrop.ItemData.SharedData shared)
        {
            return shared != null && shared.m_food <= 0f
                && shared.m_foodStamina <= 0f && shared.m_foodEitr <= 0f;
        }

        internal static float EffectiveBurnTime(ItemDrop.ItemData item, in FoodConfigSnapshot cfg)
        {
            if (item?.m_shared == null)
            {
                return 0.01f;
            }

            float burn = item.m_shared.m_foodBurnTime;
            if (burn > 0f)
            {
                return Mathf.Max(0.01f, burn * cfg.DurationMult);
            }

            if (!cfg.ExtraSlots)
            {
                return 0.01f;
            }

            return DrinkBurnTime(item.m_shared, cfg);
        }

        private static float DrinkBurnTime(ItemDrop.ItemData.SharedData shared, in FoodConfigSnapshot cfg)
        {
            float ttl = 0f;
            if (shared.m_consumeStatusEffect != null)
            {
                ttl = shared.m_consumeStatusEffect.m_ttl;
            }

            return Mathf.Max(1f, ttl > 0f ? ttl : cfg.DrinkBurn);
        }
    }
}
