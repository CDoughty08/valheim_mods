using UnityEngine;

namespace VariaFood
{
    public enum RegenMode
    {
        PerTick,
        PerSecond
    }

    /// <summary>
    /// What stamina/eitr regen is derived from.
    /// Health always uses vanilla <see cref="ItemDrop.ItemData.SharedData.m_foodRegen"/>.
    /// </summary>
    public enum RegenScaleMode
    {
        /// <summary>Old behaviour: regen × food's vanilla health-regen stat.</summary>
        FoodRegen,

        /// <summary>
        /// Regen × that food's stamina/eitr boost (after Stamina/Eitr multipliers).
        /// Multiplier is fraction of boost per 10s tick (0.25 ⇒ 2.5%/sec).
        /// </summary>
        StatBoost
    }

    /// <summary>
    /// Snapshot of config for the UpdateFood hot path (no ConfigEntry reads per frame).
    /// Refreshed on bind and whenever any setting changes.
    /// </summary>
    internal struct FoodConfigSnapshot
    {
        public bool Enabled;
        public bool NoDecay;
        public float DurationMult;
        public float HealthMult;
        public float StaminaMult;
        public float EitrMult;
        public float HealthRegenMult;
        public float StaminaRegenMult;
        public float EitrRegenMult;
        public RegenMode HealthRegenMode;
        public RegenMode StaminaRegenMode;
        public RegenMode EitrRegenMode;
        public RegenScaleMode StaminaRegenScale;
        public RegenScaleMode EitrRegenScale;
        public bool ExtraSlots;
        public int FoodSlots;
        public int DrinkSlots;
        public float DrinkBurn;

        /// <summary>
        /// True when our patches would match vanilla UpdateFood / regen behaviour.
        /// Extra slots force our path so drink burn / CanEatAgain stay correct.
        /// </summary>
        public bool MatchesVanillaFoodBehaviour()
        {
            return !ExtraSlots
                && !NoDecay
                && Approximately(DurationMult, 1f)
                && Approximately(HealthMult, 1f)
                && Approximately(StaminaMult, 1f)
                && Approximately(EitrMult, 1f)
                && Approximately(HealthRegenMult, 1f)
                && StaminaRegenMult <= 0f
                && EitrRegenMult <= 0f
                && HealthRegenMode == RegenMode.PerTick;
        }

        public bool NeedsContinuousRegen()
        {
            return (HealthRegenMode == RegenMode.PerSecond && HealthRegenMult > 0f)
                || (StaminaRegenMode == RegenMode.PerSecond && StaminaRegenMult > 0f)
                || (EitrRegenMode == RegenMode.PerSecond && EitrRegenMult > 0f);
        }

        public bool NeedsTickRegen()
        {
            return (HealthRegenMode == RegenMode.PerTick && HealthRegenMult > 0f)
                || (StaminaRegenMode == RegenMode.PerTick && StaminaRegenMult > 0f)
                || (EitrRegenMode == RegenMode.PerTick && EitrRegenMult > 0f);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }

}
