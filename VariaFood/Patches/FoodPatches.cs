using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VariaFood.Patches
{
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class PlayerUpdateFoodPatch
    {
        // Set by the vanilla-slot EatFood prefix so forceUpdate sees multiplied duration/stats
        // before SetMax* runs (avoids one incorrect strength sample on eat).
        internal struct PendingFood
        {
            internal Player Player;
            internal ItemDrop.ItemData Item;
        }

        internal static PendingFood Pending;

        private static bool Prefix(Player __instance, float dt, bool forceUpdate)
        {
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            DrinkDisplay.Update(__instance, dt, cfg);
            if (!cfg.Enabled || cfg.MatchesVanillaFoodBehaviour())
            {
                return true;
            }

            if (Pending.Player == __instance && Pending.Item != null)
            {
                ItemDrop.ItemData item = Pending.Item;
                Pending = default;
                ApplyEatenFoodMultipliers(__instance, item, cfg);
            }

            // A forced stat refresh must not consume time or put the timer into debt.
            __instance.m_foodUpdateTimer += dt * Game.m_foodRate;
            float elapsedFood = Mathf.Floor(Mathf.Max(0f, __instance.m_foodUpdateTimer));
            if (elapsedFood > 0f || forceUpdate)
            {
                __instance.m_foodUpdateTimer -= elapsedFood;
                UpdateActiveFoods(__instance, elapsedFood, cfg);
            }

            if (forceUpdate)
            {
                return false;
            }

            bool needsContinuous = cfg.NeedsContinuousRegen();
            bool needsTick = cfg.NeedsTickRegen();
            // Keep vanilla's pulse clock running even on an empty stomach. Compute
            // tick-only totals only when a pulse is due, not on every frame.
            __instance.m_foodRegenTimer += dt;
            bool pulseDue = __instance.m_foodRegenTimer >= VariaFoodPlugin.RegenTickSeconds;
            if (pulseDue)
            {
                __instance.m_foodRegenTimer %= VariaFoodPlugin.RegenTickSeconds;
            }

            if (!needsContinuous && !(needsTick && pulseDue))
            {
                return false;
            }

            ComputeRegenTickAmounts(
                __instance,
                cfg,
                out float healthTickAmount,
                out float staminaTickAmount,
                out float eitrTickAmount);

            if (healthTickAmount <= 0f && staminaTickAmount <= 0f && eitrTickAmount <= 0f)
            {
                return false;
            }

            if (needsContinuous)
            {
                ApplyContinuousRegen(__instance, dt, cfg, healthTickAmount, staminaTickAmount, eitrTickAmount);
            }

            if (needsTick && pulseDue)
            {
                ApplyTickRegen(__instance, cfg, healthTickAmount, staminaTickAmount, eitrTickAmount);
            }

            return false;
        }

        private static void UpdateActiveFoods(Player player, float foodTick, FoodConfigSnapshot cfg)
        {
            List<Player.Food> foods = player.m_foods;
            bool noDecay = cfg.NoDecay;
            float healthMult = cfg.HealthMult;
            float staminaMult = cfg.StaminaMult;
            float eitrMult = cfg.EitrMult;

            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food?.m_item?.m_shared == null)
                {
                    foods.RemoveAt(i--);
                    continue;
                }

                if (!FoodMath.IsDisplayOnlyDrink(food.m_item.m_shared))
                {
                    food.m_time -= foodTick;
                }

                if (food.m_time <= 0f)
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_food_done");
                    foods.RemoveAt(i--);
                    continue;
                }

                float burnTime = FoodMath.EffectiveBurnTime(food.m_item, cfg);
                float strength = noDecay ? 1f : Mathf.Pow(Mathf.Clamp01(food.m_time / burnTime), 0.3f);

                ItemDrop.ItemData.SharedData shared = food.m_item.m_shared;
                food.m_health = shared.m_food * strength * healthMult;
                food.m_stamina = shared.m_foodStamina * strength * staminaMult;
                food.m_eitr = shared.m_foodEitr * strength * eitrMult;

            }

            player.GetTotalFoodValue(out float hp, out float stamina, out float eitr);
            player.SetMaxHealth(hp, flashBar: true);
            player.SetMaxStamina(stamina, flashBar: true);
            player.SetMaxEitr(eitr, flashBar: true);
            if (eitr > 0f)
            {
                player.ShowTutorial("eitr");
            }
        }

        private static void ApplyContinuousRegen(
            Player player,
            float dt,
            FoodConfigSnapshot cfg,
            float healthTickAmount,
            float staminaTickAmount,
            float eitrTickAmount)
        {
            float scale = dt / VariaFoodPlugin.RegenTickSeconds;

            if (cfg.HealthRegenMode == RegenMode.PerSecond && healthTickAmount > 0f)
            {
                // showText: false — PerSecond would otherwise spam floating heal text every frame.
                TryHeal(player, healthTickAmount * scale, showText: false);
            }

            if (cfg.StaminaRegenMode == RegenMode.PerSecond && staminaTickAmount > 0f)
            {
                TryAddStamina(player, staminaTickAmount * scale);
            }

            if (cfg.EitrRegenMode == RegenMode.PerSecond && eitrTickAmount > 0f)
            {
                TryAddEitr(player, eitrTickAmount * scale);
            }
        }

        private static void ApplyTickRegen(
            Player player,
            FoodConfigSnapshot cfg,
            float healthTickAmount,
            float staminaTickAmount,
            float eitrTickAmount)
        {
            if (cfg.HealthRegenMode == RegenMode.PerTick && healthTickAmount > 0f)
            {
                // Match vanilla: one heal pulse with floating text.
                TryHeal(player, healthTickAmount, showText: true);
            }

            if (cfg.StaminaRegenMode == RegenMode.PerTick && staminaTickAmount > 0f)
            {
                TryAddStamina(player, staminaTickAmount);
            }

            if (cfg.EitrRegenMode == RegenMode.PerTick && eitrTickAmount > 0f)
            {
                TryAddEitr(player, eitrTickAmount);
            }
        }

        private static void TryHeal(Player player, float amount, bool showText)
        {
            if (amount <= 0f || player.GetHealth() >= player.GetMaxHealth())
            {
                return;
            }

            float statusRegenMult = 1f;
            player.m_seman.ModifyHealthRegen(ref statusRegenMult);
            float heal = amount * statusRegenMult;
            if (heal > 0f)
            {
                player.Heal(heal, showText);
            }
        }

        private static void TryAddStamina(Player player, float amount)
        {
            if (amount <= 0f || player.GetStamina() >= player.GetMaxStamina())
            {
                return;
            }

            player.AddStamina(amount);
        }

        private static void TryAddEitr(Player player, float amount)
        {
            if (amount <= 0f || player.GetMaxEitr() <= 0f || player.GetEitr() >= player.GetMaxEitr())
            {
                return;
            }

            player.AddEitr(amount);
        }

        internal static void ApplyEatenFoodMultipliers(Player player, ItemDrop.ItemData item, FoodConfigSnapshot cfg)
        {
            if (item?.m_shared == null)
            {
                return;
            }

            string name = item.m_shared.m_name;
            List<Player.Food> foods = player.m_foods;
            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food.m_item?.m_shared?.m_name != name)
                {
                    continue;
                }

                FoodSlots.Fill(food, item, cfg);
                return;
            }
        }

        /// <summary>
        /// Tick-equivalent regen from active foods. Health always uses vanilla food regen;
        /// stamina/eitr use StatBoost (× pool) or FoodRegen (× heal stat) per config.
        /// </summary>
        internal static void ComputeRegenTickAmounts(
            Player player,
            FoodConfigSnapshot cfg,
            out float healthTick,
            out float staminaTick,
            out float eitrTick)
        {
            healthTick = 0f;
            staminaTick = 0f;
            eitrTick = 0f;

            List<Player.Food> foods = player.m_foods;
            for (int i = 0, count = foods.Count; i < count; i++)
            {
                ItemDrop.ItemData.SharedData shared = foods[i]?.m_item?.m_shared;
                if (shared == null || FoodMath.IsDisplayOnlyDrink(shared))
                {
                    continue;
                }
                AccumulateItemRegen(shared, cfg, ref healthTick, ref staminaTick, ref eitrTick);
            }
        }

        internal static void ComputeItemRegenTickAmounts(
            ItemDrop.ItemData.SharedData shared,
            FoodConfigSnapshot cfg,
            out float healthTick,
            out float staminaTick,
            out float eitrTick)
        {
            healthTick = 0f;
            staminaTick = 0f;
            eitrTick = 0f;
            if (shared == null)
            {
                return;
            }

            AccumulateItemRegen(shared, cfg, ref healthTick, ref staminaTick, ref eitrTick);
        }

        private static void AccumulateItemRegen(
            ItemDrop.ItemData.SharedData shared,
            FoodConfigSnapshot cfg,
            ref float healthTick,
            ref float staminaTick,
            ref float eitrTick)
        {
            if (cfg.HealthRegenMult > 0f && shared.m_foodRegen > 0f)
            {
                healthTick += shared.m_foodRegen * cfg.HealthRegenMult;
            }

            if (cfg.StaminaRegenMult > 0f)
            {
                staminaTick += cfg.StaminaRegenScale == RegenScaleMode.StatBoost
                    ? shared.m_foodStamina * cfg.StaminaMult * cfg.StaminaRegenMult
                        * VariaFoodPlugin.StatBoostBaseFractionPerTick
                    : shared.m_foodRegen * cfg.StaminaRegenMult;
            }

            if (cfg.EitrRegenMult > 0f)
            {
                eitrTick += cfg.EitrRegenScale == RegenScaleMode.StatBoost
                    ? shared.m_foodEitr * cfg.EitrMult * cfg.EitrRegenMult
                        * VariaFoodPlugin.StatBoostBaseFractionPerTick
                    : shared.m_foodRegen * cfg.EitrRegenMult;
            }
        }

        internal static float DisplayRegenAmount(float tickAmount, RegenMode mode)
        {
            return mode == RegenMode.PerSecond
                ? tickAmount / VariaFoodPlugin.RegenTickSeconds
                : tickAmount;
        }

        internal static string RegenUnitSuffix(RegenMode mode)
        {
            return mode == RegenMode.PerSecond ? "/sec" : "/tick";
        }
    }

    [HarmonyPatch(typeof(Player), "EatFood")]
    internal static class PlayerEatFoodPatch
    {
        private static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result,
            out PlayerUpdateFoodPatch.PendingFood __state)
        {
            __state = PlayerUpdateFoodPatch.Pending;
            PlayerUpdateFoodPatch.Pending = default;
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || item?.m_shared == null)
            {
                return true;
            }

            if (!cfg.ExtraSlots)
            {
                PlayerUpdateFoodPatch.Pending = new PlayerUpdateFoodPatch.PendingFood
                {
                    Player = __instance,
                    Item = item
                };
                return true;
            }

            try
            {
                __result = FoodSlots.EatFood(__instance, item, cfg);
            }
            catch (System.Exception ex)
            {
                VariaFoodPlugin.Log?.LogError($"VariaFood EatFood failed: {ex}");
                __result = false;
            }

            return false;
        }

        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || item == null
                || PlayerUpdateFoodPatch.Pending.Player != __instance
                || PlayerUpdateFoodPatch.Pending.Item != item)
            {
                return;
            }

            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || cfg.MatchesVanillaFoodBehaviour())
            {
                return;
            }

            // Safety net if vanilla's UpdateFood was skipped by another patch.
            __instance.UpdateFood(0f, forceUpdate: true);
        }

        private static void Finalizer(PlayerUpdateFoodPatch.PendingFood __state)
        {
            // Also runs on exceptions; nested EatFood calls restore their caller.
            PlayerUpdateFoodPatch.Pending = __state;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.CanEat))]
    internal static class PlayerCanEatPatch
    {
        private static bool Prefix(Player __instance, ItemDrop.ItemData item, bool showMessages, ref bool __result)
        {
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || !cfg.ExtraSlots || item?.m_shared == null)
            {
                return true;
            }

            __result = FoodSlots.CanEat(__instance, item, showMessages, cfg);
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem), new System.Type[]
    {
        typeof(Inventory),
        typeof(ItemDrop.ItemData),
        typeof(bool)
    })]
    internal static class PlayerConsumeItemDrinkPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || item == null)
            {
                return;
            }

            try
            {
                FoodSlots.TryRouteDrink(__instance, item, VariaFoodPlugin.ConfigSnapshot);
            }
            catch (System.Exception ex)
            {
                VariaFoodPlugin.Log?.LogError($"VariaFood drink routing failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Player.Food), nameof(Player.Food.CanEatAgain))]
    internal static class FoodCanEatAgainPatch
    {
        private static bool Prefix(Player.Food __instance, ref bool __result)
        {
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || cfg.MatchesVanillaFoodBehaviour())
            {
                return true;
            }

            float halfLife = FoodMath.EffectiveBurnTime(__instance.m_item, cfg) * 0.5f;
            __result = __instance.m_time < halfLife;
            return false;
        }
    }
}
