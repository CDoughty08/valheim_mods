using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace VariaWeight
{
    /// <summary>
    /// Cached config for hot paths (carry weight + XP tick).
    /// </summary>
    internal struct WeightConfigSnapshot
    {
        public bool Enabled;
        public float BaselineBonus;
        public float MaxBonus;
        /// <summary>0–1, where 0.5 is linear.</summary>
        public float Balance;
        public float ExpRate;
        /// <summary>Carried weight that yields <see cref="ExpRate"/> XP per second while moving.</summary>
        public float ExpReferenceWeight;
        public float MinMoveSpeed;
        public float MinMoveDirSqr;
        public float XpIntervalSeconds;

        public bool ApproximatelyVanillaCarry()
        {
            return Approximately(BaselineBonus, 0f)
                && Approximately(MaxBonus, 0f);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    public class VariaWeightPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.varia.weight";
        public const string PluginName = "VariaWeight";
        public const string PluginVersion = "0.0.4";

        /// <summary>Stable skill id — do not change after release (save data keys off SkillType hash).</summary>
        public const string SkillIdentifier = "com.varia.weight.weightlifting";

        public const float VanillaBaseCarryWeight = 300f;

        private static readonly ConfigSync ConfigSync = new(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            ModRequired = false
        };

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> BaselineCarryBonus;
        internal static ConfigEntry<float> MaxCarryBonus;
        internal static ConfigEntry<float> BalancePercent;
        internal static ConfigEntry<float> ExpRate;
        internal static ConfigEntry<float> ExpReferenceWeight;
        internal static ConfigEntry<float> MinMoveSpeed;

        internal static WeightConfigSnapshot ConfigSnapshot;

        private Harmony _harmony;
        private readonly Varia.Shared.MovementExperience _experience = new Varia.Shared.MovementExperience();

        private void Awake()
        {
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
                "Master toggle for VariaWeight.");

            BaselineCarryBonus = ConfigBind(
                "CarryBonus",
                "BaselineCarryBonus",
                0f,
                new ConfigDescription(
                    "Extra max carry weight at skill level 0. Default 0 = no benefit.",
                    new AcceptableValueRange<float>(0f, 2000f)));

            MaxCarryBonus = ConfigBind(
                "CarryBonus",
                "MaxCarryBonus",
                300f,
                new ConfigDescription(
                    "Extra max carry weight at skill level 100. Default 300 ≈ double vanilla (300→600).",
                    new AcceptableValueRange<float>(0f, 2000f)));

            BalancePercent = ConfigBind(
                "CarryBonus",
                "BalancePercent",
                50f,
                new ConfigDescription(
                    "How bonus scales with level. 50 = linear. Higher (e.g. 90) shifts gains to late levels. Lower (e.g. 10) front-loads with diminishing late returns.",
                    new AcceptableValueRange<float>(1f, 99f)));

            ExpRate = ConfigBind(
                "Experience",
                "ExpRate",
                0.2f,
                new ConfigDescription(
                    "Skill XP per second while moving when carried weight equals ExpReferenceWeight. Scales linearly with current weight. Default 0.2 keeps early levels from racing ahead on light loads.",
                    new AcceptableValueRange<float>(0f, 20f)));

            ExpReferenceWeight = ConfigBind(
                "Experience",
                "ExpReferenceWeight",
                VanillaBaseCarryWeight,
                new ConfigDescription(
                    "Carried weight that yields ExpRate XP/sec while moving. Default 300 (vanilla base capacity).",
                    new AcceptableValueRange<float>(1f, 2000f)));

            MinMoveSpeed = ConfigBind(
                "Experience",
                "MinMoveSpeed",
                0.15f,
                new ConfigDescription(
                    "Minimum horizontal speed to count as moving for XP.",
                    new AcceptableValueRange<float>(0.01f, 5f)));

            RefreshConfigSnapshot();
            Config.SettingChanged += OnSettingChanged;

            WeightSkill.Initialize();

            _harmony = new Harmony(PluginGuid);
            PatchOwnTypes(_harmony);

            // Language may already be set up before this plugin loads.
            WeightSkill.RegisterLocalization();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (skill type {(int)WeightSkill.SkillType})");
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            WeightConfigSnapshot cfg = ConfigSnapshot;
            if (!cfg.Enabled || cfg.ExpRate <= 0f)
            {
                _experience.Reset();
                return;
            }
            Player player = Player.m_localPlayer;
            float weight = player != null ? player.GetInventory().GetTotalWeight() : 0f;
            _experience.Tick(player, WeightSkill.SkillType, weight / cfg.ExpReferenceWeight * cfg.ExpRate,
                cfg.XpIntervalSeconds, cfg.MinMoveDirSqr, cfg.MinMoveSpeed);
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            RefreshConfigSnapshot();
        }

        internal static void RefreshConfigSnapshot()
        {
            float minSpeed = Mathf.Max(0.01f, MinMoveSpeed.Value);
            ConfigSnapshot = new WeightConfigSnapshot
            {
                Enabled = Enabled.Value,
                BaselineBonus = Mathf.Max(0f, BaselineCarryBonus.Value),
                MaxBonus = Mathf.Max(0f, MaxCarryBonus.Value),
                Balance = Mathf.Clamp(BalancePercent.Value, 1f, 99f) / 100f,
                ExpRate = Mathf.Max(0f, ExpRate.Value),
                ExpReferenceWeight = Mathf.Max(1f, ExpReferenceWeight.Value),
                MinMoveSpeed = minSpeed,
                MinMoveDirSqr = 0.01f * 0.01f,
                XpIntervalSeconds = 1f
            };
            WeightCurve.InvalidateCarryBonusCache();
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
