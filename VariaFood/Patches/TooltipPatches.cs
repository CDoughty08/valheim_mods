using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace VariaFood.Patches
{
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new Type[]
    {
        typeof(ItemDrop.ItemData),
        typeof(int),
        typeof(bool),
        typeof(float),
        typeof(int),
        typeof(bool)
    })]
    internal static class ItemDataGetTooltipPatch
    {
        // Fallback only — prefer Ordinal Replace of exact vanilla substrings (no per-hover compile).
        private static readonly Regex RegenLineFallback = new Regex(
            @"\n\$item_food_regen:\s*<color=orange>[^<]+</color>",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || item?.m_shared == null || string.IsNullOrEmpty(__result))
            {
                return;
            }

            ItemDrop.ItemData.SharedData shared = item.m_shared;
            if (shared.m_food <= 0f && shared.m_foodStamina <= 0f && shared.m_foodEitr <= 0f)
            {
                return;
            }

            if (shared.m_food > 0f && !Approximately(cfg.HealthMult, 1f))
            {
                TryReplaceExact(
                    ref __result,
                    BuildColoredStat("\n$item_food_health: ", "#ff8080ff", FmtVanilla(shared.m_food)),
                    BuildColoredStat("\n$item_food_health: ", "#ff8080ff", FmtDisplay(shared.m_food * cfg.HealthMult)));
            }

            if (shared.m_foodStamina > 0f && !Approximately(cfg.StaminaMult, 1f))
            {
                TryReplaceExact(
                    ref __result,
                    BuildColoredStat("\n$item_food_stamina: ", "#ffff80ff", FmtVanilla(shared.m_foodStamina)),
                    BuildColoredStat("\n$item_food_stamina: ", "#ffff80ff", FmtDisplay(shared.m_foodStamina * cfg.StaminaMult)));
            }

            if (shared.m_foodEitr > 0f && !Approximately(cfg.EitrMult, 1f))
            {
                TryReplaceExact(
                    ref __result,
                    BuildColoredStat("\n$item_food_eitr: ", "#9090ffff", FmtVanilla(shared.m_foodEitr)),
                    BuildColoredStat("\n$item_food_eitr: ", "#9090ffff", FmtDisplay(shared.m_foodEitr * cfg.EitrMult)));
            }

            if (!Approximately(cfg.DurationMult, 1f))
            {
                string oldDuration = "\n$item_food_duration: <color=orange>"
                    + ItemDrop.ItemData.GetDurationString(shared.m_foodBurnTime)
                    + "</color>";
                string newDuration = "\n$item_food_duration: <color=orange>"
                    + ItemDrop.ItemData.GetDurationString(FoodMath.EffectiveBurnTime(item, cfg))
                    + "</color>";
                TryReplaceExact(ref __result, oldDuration, newDuration);
            }

            float baseRegen = shared.m_foodRegen;
            PlayerUpdateFoodPatch.ComputeItemRegenTickAmounts(
                shared,
                cfg,
                out float healthTick,
                out float staminaTick,
                out float eitrTick);

            bool regenNeedsRewrite =
                !Approximately(cfg.HealthRegenMult, 1f)
                || cfg.HealthRegenMode != RegenMode.PerTick
                || staminaTick > 0f
                || eitrTick > 0f
                || (healthTick > 0f && cfg.HealthRegenMode == RegenMode.PerSecond);

            if (!regenNeedsRewrite)
            {
                return;
            }

            // If vanilla showed no healing line (0 food regen) but we add stam/eitr regen, append after duration.
            string regenBlock = BuildRegenLines(healthTick, staminaTick, eitrTick, cfg);
            if (baseRegen > 0f)
            {
                string vanillaRegen = "\n$item_food_regen: <color=orange>" + FmtVanilla(baseRegen) + " hp/tick</color>";
                if (!TryReplaceExact(ref __result, vanillaRegen, regenBlock))
                {
                    __result = RegenLineFallback.Replace(__result, regenBlock);
                }
            }
            else
            {
                __result += regenBlock;
            }
        }

        private static string BuildRegenLines(
            float healthTick,
            float staminaTick,
            float eitrTick,
            FoodConfigSnapshot cfg)
        {
            var sb = new StringBuilder(160);

            if (healthTick > 0f)
            {
                float shown = PlayerUpdateFoodPatch.DisplayRegenAmount(healthTick, cfg.HealthRegenMode);
                sb.Append("\n$item_food_regen: <color=#ff8080ff>")
                    .Append(FmtDisplay(shown))
                    .Append(" hp")
                    .Append(PlayerUpdateFoodPatch.RegenUnitSuffix(cfg.HealthRegenMode))
                    .Append("</color>");
            }

            if (staminaTick > 0f)
            {
                float shown = PlayerUpdateFoodPatch.DisplayRegenAmount(staminaTick, cfg.StaminaRegenMode);
                sb.Append("\nStamina regen: <color=#ffff80ff>")
                    .Append(FmtDisplay(shown))
                    .Append(PlayerUpdateFoodPatch.RegenUnitSuffix(cfg.StaminaRegenMode))
                    .Append("</color>");
            }

            if (eitrTick > 0f)
            {
                float shown = PlayerUpdateFoodPatch.DisplayRegenAmount(eitrTick, cfg.EitrRegenMode);
                sb.Append("\nEitr regen: <color=#9090ffff>")
                    .Append(FmtDisplay(shown))
                    .Append(PlayerUpdateFoodPatch.RegenUnitSuffix(cfg.EitrRegenMode))
                    .Append("</color>");
            }

            return sb.ToString();
        }

        private static string BuildColoredStat(string label, string color, string value)
        {
            return label + "<color=" + color + ">" + value + "</color>";
        }

        private static bool TryReplaceExact(ref string text, string oldValue, string newValue)
        {
            int index = text.IndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            text = text.Remove(index, oldValue.Length).Insert(index, newValue);
            return true;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        // Must match ItemDrop.ItemData.GetTooltip AppendFormat("{0}", float) output.
        private static string FmtVanilla(float value)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}", value);
        }

        private static string FmtDisplay(float value)
        {
            return value.ToString("0.##", CultureInfo.CurrentCulture);
        }
    }
}
