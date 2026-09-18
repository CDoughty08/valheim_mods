using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace VariaFood
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    public class VariaFoodPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.varia.food";
        public const string PluginName = "VariaFood";
        public const string PluginVersion = "0.1.4";

        /// <summary>Vanilla food regen pulse interval (seconds).</summary>
        public const float RegenTickSeconds = 10f;

        /// <summary>
        /// Built into StatBoost: fraction of stamina/eitr boost per 10s tick at multiplier 1.0.
        /// Honey 35 → 0.875/sec, Grilled Neck 8 → 0.2/sec, Ashlands 100 → 2.5/sec.
        /// </summary>
        public const float StatBoostBaseFractionPerTick = 0.25f;

        private static readonly ConfigSync ConfigSync = new(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            ModRequired = false
        };

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> DisableFoodDecay;
        internal static ConfigEntry<float> DurationMultiplier;
        internal static ConfigEntry<float> HealthMultiplier;
        internal static ConfigEntry<float> StaminaMultiplier;
        internal static ConfigEntry<float> EitrMultiplier;
        internal static ConfigEntry<float> HealthRegenMultiplier;
        internal static ConfigEntry<float> StaminaRegenMultiplier;
        internal static ConfigEntry<float> EitrRegenMultiplier;
        internal static ConfigEntry<RegenMode> HealthRegenMode;
        internal static ConfigEntry<RegenMode> StaminaRegenMode;
        internal static ConfigEntry<RegenMode> EitrRegenMode;
        internal static ConfigEntry<RegenScaleMode> StaminaRegenScale;
        internal static ConfigEntry<RegenScaleMode> EitrRegenScale;
        internal static ConfigEntry<bool> ExtraFoodSlots;
        internal static ConfigEntry<int> FoodSlotCount;
        internal static ConfigEntry<int> DrinkSlotCount;
        internal static ConfigEntry<float> DrinkSlotDuration;
        internal static ConfigEntry<string> ExtraDrinkKeywords;

        internal static FoodConfigSnapshot ConfigSnapshot;
        internal static BepInEx.Logging.ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            LockConfiguration = ConfigBind(
                "General",
                "LockConfiguration",
                false,
                "If enabled on a server that also runs this mod, force all clients to use the server's config. Leave off for client-side / optional settings.");
            _ = ConfigSync.AddLockingConfigEntry(LockConfiguration);

            Enabled = ConfigBind(
                "General",
                "Enabled",
                true,
                "Master toggle for VariaFood. Disable to restore vanilla food behaviour.");

            DisableFoodDecay = ConfigBind(
                "Food",
                "DisableFoodDecay",
                true,
                "If enabled, active food stays at full health/stamina/eitr until it expires (no gradual fade).");

            DurationMultiplier = ConfigBind(
                "Food",
                "DurationMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplies how long food lasts. 2 = twice as long, 0.5 = half as long.",
                    new AcceptableValueRange<float>(0.1f, 20f)));

            HealthMultiplier = ConfigBind(
                "Food",
                "HealthMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplies food health (max HP from food). 2 = double.",
                    new AcceptableValueRange<float>(0f, 20f)));

            StaminaMultiplier = ConfigBind(
                "Food",
                "StaminaMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplies food stamina. 2 = double.",
                    new AcceptableValueRange<float>(0f, 20f)));

            EitrMultiplier = ConfigBind(
                "Food",
                "EitrMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplies food eitr. 2 = double.",
                    new AcceptableValueRange<float>(0f, 20f)));

            HealthRegenMultiplier = ConfigBind(
                "Food",
                "HealthRegenMultiplier",
                1f,
                new ConfigDescription(
                    "Vanilla-style: food health-regen × this. PerSecond spreads that amount over 10s.",
                    new AcceptableValueRange<float>(0f, 20f)));

            StaminaRegenScale = ConfigBind(
                "Food",
                "StaminaRegenScale",
                RegenScaleMode.StatBoost,
                "StatBoost = proportional to food stamina boost (recommended). FoodRegen = old (× health-regen stat).");

            StaminaRegenMultiplier = ConfigBind(
                "Food",
                "StaminaRegenMultiplier",
                0f,
                new ConfigDescription(
                    "0 = off. StatBoost: 1.0 = Honey 0.875/sec (Neck 0.2, Ashlands 100→2.5). FoodRegen: × health-regen.",
                    new AcceptableValueRange<float>(0f, 20f)));

            EitrRegenScale = ConfigBind(
                "Food",
                "EitrRegenScale",
                RegenScaleMode.StatBoost,
                "StatBoost = proportional to food eitr boost (recommended). FoodRegen = old (× health-regen stat).");

            EitrRegenMultiplier = ConfigBind(
                "Food",
                "EitrRegenMultiplier",
                0f,
                new ConfigDescription(
                    "0 = off. StatBoost: 1.0 matches stamina scaling (eitr boost × same base rate). FoodRegen: × health-regen.",
                    new AcceptableValueRange<float>(0f, 20f)));

            HealthRegenMode = ConfigBind(
                "Food",
                "HealthRegenMode",
                RegenMode.PerTick,
                "PerTick = pulse every 10s (vanilla). PerSecond = smooth regen (tick amount ÷ 10 each second).");

            StaminaRegenMode = ConfigBind(
                "Food",
                "StaminaRegenMode",
                RegenMode.PerSecond,
                "PerTick = pulse every 10s. PerSecond = smooth regen (tick amount ÷ 10 each second).");

            EitrRegenMode = ConfigBind(
                "Food",
                "EitrRegenMode",
                RegenMode.PerSecond,
                "PerTick = pulse every 10s. PerSecond = smooth regen (tick amount ÷ 10 each second).");

            ExtraFoodSlots = ConfigBind(
                "Slots",
                "ExtraFoodSlots",
                true,
                "Enable extra food slots (default 4 solid + 1 drink). Requires Enabled.");

            FoodSlotCount = ConfigBind(
                "Slots",
                "FoodSlotCount",
                4,
                new ConfigDescription(
                    "Solid-food slots when ExtraFoodSlots is on.",
                    new AcceptableValueRange<int>(1, 6)));

            DrinkSlotCount = ConfigBind(
                "Slots",
                "DrinkSlotCount",
                1,
                new ConfigDescription(
                    "Dedicated drink slots. 0 = no reservation (everything counts as solid food).",
                    new AcceptableValueRange<int>(0, 2)));

            DrinkSlotDuration = ConfigBind(
                "Slots",
                "DrinkSlotDuration",
                1200f,
                new ConfigDescription(
                    "Fallback burn time (seconds) for drinks with no foodBurnTime and no status-effect TTL. DurationMultiplier does not apply.",
                    new AcceptableValueRange<float>(60f, 7200f)));

            ExtraDrinkKeywords = ConfigBind(
                "Slots",
                "ExtraDrinkKeywords",
                "",
                "Comma-separated extra whole-word tokens that mark an item as a drink (modded juices, etc.).");

            RefreshConfigSnapshot();
            Config.SettingChanged += OnSettingChanged;

            _harmony = new Harmony(PluginGuid);
            PatchOwnTypes(_harmony);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            Patches.HudAwakeFoodSlotsPatch.Shutdown();
            DrinkDisplay.ClearAll();
            _harmony?.UnpatchSelf();
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            RefreshConfigSnapshot();
        }

        internal static void RefreshConfigSnapshot()
        {
            ConfigSnapshot = new FoodConfigSnapshot
            {
                Enabled = Enabled.Value,
                NoDecay = DisableFoodDecay.Value,
                DurationMult = Mathf.Max(0.1f, DurationMultiplier.Value),
                HealthMult = Mathf.Max(0f, HealthMultiplier.Value),
                StaminaMult = Mathf.Max(0f, StaminaMultiplier.Value),
                EitrMult = Mathf.Max(0f, EitrMultiplier.Value),
                HealthRegenMult = Mathf.Max(0f, HealthRegenMultiplier.Value),
                StaminaRegenMult = Mathf.Max(0f, StaminaRegenMultiplier.Value),
                EitrRegenMult = Mathf.Max(0f, EitrRegenMultiplier.Value),
                HealthRegenMode = HealthRegenMode.Value,
                StaminaRegenMode = StaminaRegenMode.Value,
                EitrRegenMode = EitrRegenMode.Value,
                StaminaRegenScale = StaminaRegenScale.Value,
                EitrRegenScale = EitrRegenScale.Value,
                ExtraSlots = ExtraFoodSlots.Value,
                FoodSlots = Mathf.Clamp(FoodSlotCount.Value, 1, 6),
                DrinkSlots = Mathf.Clamp(DrinkSlotCount.Value, 0, 2),
                DrinkBurn = Mathf.Clamp(DrinkSlotDuration.Value, 60f, 7200f)
            };

            DrinkClassifier.Rebuild(ExtraDrinkKeywords.Value);
        }

        private ConfigEntry<T> ConfigBind<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return ConfigBind(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private ConfigEntry<T> ConfigBind<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigDescription extended = new(
                description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues,
                description.Tags);

            ConfigEntry<T> entry = Config.Bind(group, name, value, extended);
            SyncedConfigEntry<T> synced = ConfigSync.AddConfigEntry(entry);
            synced.SynchronizedConfig = synchronizedSetting;
            return entry;
        }

        /// <summary>
        /// Patch only this mod's types — ServerSync applies its own Harmony patches.
        /// </summary>
        private static void PatchOwnTypes(Harmony harmony)
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Namespace != null && type.Namespace.StartsWith("ServerSync", StringComparison.Ordinal))
                {
                    continue;
                }

                harmony.CreateClassProcessor(type).Patch();
            }
        }
    }
}
