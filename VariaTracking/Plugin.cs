using System;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace VariaTracking
{
    /// <summary>
    /// Cached config for hot paths (radar + XP tick).
    /// </summary>
    internal struct TrackingConfigSnapshot
    {
        public bool Enabled;

        public float MinRange;
        public float MaxRange;
        /// <summary>0–1, where 0.5 is linear.</summary>
        public float Balance;
        public bool ShowRangeCircle;
        public Color CircleColor;
        public bool ShowOnMinimap;
        public bool ShowOnLargeMap;

        public bool ShowPassive;
        public bool ShowHostile;
        public bool ShowBoss;
        public bool ShowStarred;
        public Color PassiveColor;
        public Color HostileColor;
        public Color BossColor;
        public float DotSize;
        public float StarRingSize;
        public float UpdateIntervalSeconds;
        public int MaxDots;
        public bool ShowTooltips;

        public float FogPierceLevel;
        public bool PierceEnabled;

        public float ExpRate;
        public float MinMoveSpeed;
        public float MinMoveDirSqr;
        public float StarredExpBonus;
        public float XpIntervalSeconds;

        // Progression
        public float ConeStartDegrees;
        public float ConeFullLevel;
        public float HostilityUnlockLevel;
        public float StarsUnlockLevel;
        public float BossUnlockLevel;
        public float NamesUnlockLevel;
        public float ConeHysteresisSeconds;
        public float JitterMetersAt0;
        public float JitterZeroLevel;
        public float PostureFloor;
        public float PostureBonusAt100;
        public float PostureStillSeconds;
        public float PostureStillSpeed;
        public bool TrophyEarlyNames;
        public bool NameTrophylessCreatures;
        public int MaxDotsCap0;
        public int MaxDotsCap1;
        public int MaxDotsCap2;
        public int MaxDotsCap3;
        public float MaxDotsLevel1;
        public float MaxDotsLevel2;
        public float MaxDotsLevel3;
        public float MaxDotsLevel4;
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    public class VariaTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.varia.tracking";
        public const string PluginName = "VariaTracking";
        public const string PluginVersion = "0.1.1";

        /// <summary>Stable skill id — do not change after release (save data keys off SkillType hash).</summary>
        public const string SkillIdentifier = "com.varia.tracking.tracking";

        private static readonly ConfigSync ConfigSync = new(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            ModRequired = false
        };

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<float> MinRange;
        internal static ConfigEntry<float> MaxRange;
        internal static ConfigEntry<float> BalancePercent;
        internal static ConfigEntry<bool> ShowRangeCircle;
        internal static ConfigEntry<string> CircleColor;
        internal static ConfigEntry<bool> ShowOnMinimap;
        internal static ConfigEntry<bool> ShowOnLargeMap;

        internal static ConfigEntry<bool> ShowPassive;
        internal static ConfigEntry<bool> ShowHostile;
        internal static ConfigEntry<bool> ShowBoss;
        internal static ConfigEntry<bool> ShowStarred;
        internal static ConfigEntry<string> PassiveColor;
        internal static ConfigEntry<string> HostileColor;
        internal static ConfigEntry<string> BossColor;
        internal static ConfigEntry<float> DotSize;
        internal static ConfigEntry<float> StarRingSize;
        internal static ConfigEntry<float> UpdateIntervalSeconds;
        internal static ConfigEntry<int> MaxDots;
        internal static ConfigEntry<bool> ShowTooltips;

        internal static ConfigEntry<float> FogPierceLevel;
        internal static ConfigEntry<bool> PierceEnabled;

        internal static ConfigEntry<float> ExpRate;
        internal static ConfigEntry<float> MinMoveSpeed;
        internal static ConfigEntry<float> StarredExpBonus;

        internal static ConfigEntry<float> ConeStartDegrees;
        internal static ConfigEntry<float> ConeFullLevel;
        internal static ConfigEntry<float> HostilityUnlockLevel;
        internal static ConfigEntry<float> StarsUnlockLevel;
        internal static ConfigEntry<float> BossUnlockLevel;
        internal static ConfigEntry<float> NamesUnlockLevel;
        internal static ConfigEntry<float> ConeHysteresisSeconds;
        internal static ConfigEntry<float> JitterMetersAt0;
        internal static ConfigEntry<float> JitterZeroLevel;
        internal static ConfigEntry<float> PostureFloor;
        internal static ConfigEntry<float> PostureBonusAt100;
        internal static ConfigEntry<float> PostureStillSeconds;
        internal static ConfigEntry<float> PostureStillSpeed;
        internal static ConfigEntry<bool> TrophyEarlyNames;
        internal static ConfigEntry<bool> NameTrophylessCreatures;
        internal static ConfigEntry<int> MaxDotsCap0;
        internal static ConfigEntry<int> MaxDotsCap1;
        internal static ConfigEntry<int> MaxDotsCap2;
        internal static ConfigEntry<int> MaxDotsCap3;
        internal static ConfigEntry<float> MaxDotsLevel1;
        internal static ConfigEntry<float> MaxDotsLevel2;
        internal static ConfigEntry<float> MaxDotsLevel3;
        internal static ConfigEntry<float> MaxDotsLevel4;

        internal static TrackingConfigSnapshot ConfigSnapshot;

        private Harmony _harmony;

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
                "Master toggle for VariaTracking.");

            MinRange = ConfigBind(
                "Range",
                "MinRange",
                40f,
                new ConfigDescription(
                    "Tracking radius in meters at skill level 0.",
                    new AcceptableValueRange<float>(0f, 500f)));

            MaxRange = ConfigBind(
                "Range",
                "MaxRange",
                200f,
                new ConfigDescription(
                    "Tracking radius in meters at skill level 100.",
                    new AcceptableValueRange<float>(0f, 500f)));

            BalancePercent = ConfigBind(
                "Range",
                "BalancePercent",
                50f,
                new ConfigDescription(
                    "How range scales with level. 50 = linear. Higher (e.g. 90) shifts gains to late levels. Lower (e.g. 10) front-loads.",
                    new AcceptableValueRange<float>(1f, 99f)));

            ShowRangeCircle = ConfigBind(
                "Range",
                "ShowRangeCircle",
                true,
                "Draw tracking range wedge/circle on the map.");

            CircleColor = ConfigBind(
                "Range",
                "CircleColor",
                "1, 0.92, 0.2, 0.16",
                "Range wedge/circle color as R, G, B, A (0–1). Wedge fill uses a lower effective alpha.");

            ShowOnMinimap = ConfigBind(
                "Range",
                "ShowOnMinimap",
                true,
                "Show tracking dots and range circle on the small minimap.");

            ShowOnLargeMap = ConfigBind(
                "Range",
                "ShowOnLargeMap",
                true,
                "Show tracking dots and range circle on the large map (M).");

            ShowPassive = ConfigBind(
                "Markers",
                "ShowPassive",
                true,
                "Show passive creatures (after hostility unlock). Pre-unlock all trackables appear as grey.");

            ShowHostile = ConfigBind(
                "Markers",
                "ShowHostile",
                true,
                "Show hostile creatures (after hostility unlock).");

            ShowBoss = ConfigBind(
                "Markers",
                "ShowBoss",
                true,
                "Show boss markers at all skill levels, independent of ShowHostile. Boss color unlocks at BossUnlockLevel; before that, bosses use the currently unlocked generic color.");

            ShowStarred = ConfigBind(
                "Markers",
                "ShowStarred",
                true,
                "Draw a special ring on starred creatures once stars unlock.");

            PassiveColor = ConfigBind(
                "Markers",
                "PassiveColor",
                "1, 1, 1, 0.95",
                "Passive creature dot color as R, G, B, A (0–1).");

            HostileColor = ConfigBind(
                "Markers",
                "HostileColor",
                "1, 0.85, 0.15, 0.95",
                "Hostile creature dot color as R, G, B, A (0–1).");

            BossColor = ConfigBind(
                "Markers",
                "BossColor",
                "1, 0.35, 0.15, 0.95",
                "Boss creature dot color as R, G, B, A (0–1).");

            DotSize = ConfigBind(
                "Markers",
                "DotSize",
                2.5f,
                new ConfigDescription(
                    "Dot size in UI pixels (approximate).",
                    new AcceptableValueRange<float>(1f, 48f)));

            StarRingSize = ConfigBind(
                "Markers",
                "StarRingSize",
                4f,
                new ConfigDescription(
                    "Star ring size in UI pixels (approximate).",
                    new AcceptableValueRange<float>(2f, 64f)));

            UpdateIntervalSeconds = ConfigBind(
                "Markers",
                "UpdateIntervalSeconds",
                0.25f,
                new ConfigDescription(
                    "Seconds between scans for new nearby creatures. Known candidates update position, classification, range, fog, and XP eligibility every frame.",
                    new AcceptableValueRange<float>(0.05f, 2f)));

            MaxDots = ConfigBind(
                "Markers",
                "MaxDots",
                64,
                new ConfigDescription(
                    "Hard cap on drawn dots (also caps progression MaxDots ladder).",
                    new AcceptableValueRange<int>(1, 256)));

            ShowTooltips = ConfigBind(
                "Markers",
                "ShowTooltips",
                true,
                "Show name / ??? tooltips when hovering dots on the large map (M).");

            FogPierceLevel = ConfigBind(
                "Fog",
                "FogPierceLevel",
                100f,
                new ConfigDescription(
                    "Tracking skill level required to show creatures in unexplored fog. Default 100 = cap unlock.",
                    new AcceptableValueRange<float>(0f, 100f)));

            PierceEnabled = ConfigBind(
                "Fog",
                "PierceEnabled",
                true,
                "If true, reaching FogPierceLevel lets tracking ignore exploration fog.");

            ExpRate = ConfigBind(
                "Experience",
                "ExpRate",
                0.25f,
                new ConfigDescription(
                    "Skill XP per second while moving with at least one trackable creature in range.",
                    new AcceptableValueRange<float>(0f, 20f)));

            MinMoveSpeed = ConfigBind(
                "Experience",
                "MinMoveSpeed",
                0.15f,
                new ConfigDescription(
                    "Minimum horizontal speed to count as moving for XP.",
                    new AcceptableValueRange<float>(0.01f, 5f)));

            StarredExpBonus = ConfigBind(
                "Experience",
                "StarredExpBonus",
                0.25f,
                new ConfigDescription(
                    "Extra XP multiplier while any starred creature is in range (0.25 = +25%).",
                    new AcceptableValueRange<float>(0f, 5f)));

            ConeStartDegrees = ConfigBind(
                "Progression",
                "ConeStartDegrees",
                70f,
                new ConfigDescription(
                    "Forward tracking FOV at skill level 0.",
                    new AcceptableValueRange<float>(10f, 360f)));

            ConeFullLevel = ConfigBind(
                "Progression",
                "ConeFullLevel",
                50f,
                new ConfigDescription(
                    "Skill level at which tracking FOV becomes a full 360° circle.",
                    new AcceptableValueRange<float>(1f, 100f)));

            HostilityUnlockLevel = ConfigBind(
                "Progression",
                "HostilityUnlockLevel",
                20f,
                new ConfigDescription(
                    "Level to distinguish hostile vs passive colors.",
                    new AcceptableValueRange<float>(0f, 100f)));

            StarsUnlockLevel = ConfigBind(
                "Progression",
                "StarsUnlockLevel",
                30f,
                new ConfigDescription(
                    "Level to show starred creature rings.",
                    new AcceptableValueRange<float>(0f, 100f)));

            BossUnlockLevel = ConfigBind(
                "Progression",
                "BossUnlockLevel",
                60f,
                new ConfigDescription(
                    "Level to show distinct boss marker color, independent of HostilityUnlockLevel.",
                    new AcceptableValueRange<float>(0f, 100f)));

            NamesUnlockLevel = ConfigBind(
                "Progression",
                "NamesUnlockLevel",
                80f,
                new ConfigDescription(
                    "Level to show names for all detected creatures (trophy study can unlock earlier).",
                    new AcceptableValueRange<float>(0f, 100f)));

            ConeHysteresisSeconds = ConfigBind(
                "Progression",
                "ConeHysteresisSeconds",
                0.5f,
                new ConfigDescription(
                    "Keep a creature visible briefly after it leaves the tracking cone.",
                    new AcceptableValueRange<float>(0f, 3f)));

            JitterMetersAt0 = ConfigBind(
                "Progression",
                "JitterMetersAt0",
                8f,
                new ConfigDescription(
                    "Max position blur in meters at level 0 (decays to 0 at JitterZeroLevel).",
                    new AcceptableValueRange<float>(0f, 40f)));

            JitterZeroLevel = ConfigBind(
                "Progression",
                "JitterZeroLevel",
                60f,
                new ConfigDescription(
                    "Skill level where position jitter reaches zero.",
                    new AcceptableValueRange<float>(1f, 100f)));

            PostureFloor = ConfigBind(
                "Progression",
                "PostureFloor",
                0.10f,
                new ConfigDescription(
                    "Minimum crouch/still range bonus fraction (0.10 = +10% even at low skill).",
                    new AcceptableValueRange<float>(0f, 1f)));

            PostureBonusAt100 = ConfigBind(
                "Progression",
                "PostureBonusAt100",
                0.25f,
                new ConfigDescription(
                    "Extra crouch/still range bonus fraction at level 100 (added on top of PostureFloor).",
                    new AcceptableValueRange<float>(0f, 2f)));

            PostureStillSeconds = ConfigBind(
                "Progression",
                "PostureStillSeconds",
                0.75f,
                new ConfigDescription(
                    "Seconds standing still before hunting-posture range bonus applies (crouch is instant).",
                    new AcceptableValueRange<float>(0f, 5f)));

            PostureStillSpeed = ConfigBind(
                "Progression",
                "PostureStillSpeed",
                0.08f,
                new ConfigDescription(
                    "Max horizontal speed to count as settled still.",
                    new AcceptableValueRange<float>(0.01f, 1f)));

            TrophyEarlyNames = ConfigBind(
                "Progression",
                "TrophyEarlyNames",
                true,
                "If true, claimed trophies reveal that species' name before NamesUnlockLevel.");

            NameTrophylessCreatures = ConfigBind(
                "Progression",
                "NameTrophylessCreatures",
                true,
                "If true, creatures with no trophy drop show real names before NamesUnlockLevel.");

            MaxDotsCap0 = ConfigBind(
                "Progression",
                "MaxDotsCap0",
                8,
                new ConfigDescription(
                    "Drawn-dot cap below MaxDotsLevel1.",
                    new AcceptableValueRange<int>(1, 256)));

            MaxDotsCap1 = ConfigBind(
                "Progression",
                "MaxDotsCap1",
                16,
                new ConfigDescription(
                    "Drawn-dot cap from MaxDotsLevel1 until MaxDotsLevel2.",
                    new AcceptableValueRange<int>(1, 256)));

            MaxDotsCap2 = ConfigBind(
                "Progression",
                "MaxDotsCap2",
                32,
                new ConfigDescription(
                    "Drawn-dot cap from MaxDotsLevel2 until MaxDotsLevel3.",
                    new AcceptableValueRange<int>(1, 256)));

            MaxDotsCap3 = ConfigBind(
                "Progression",
                "MaxDotsCap3",
                48,
                new ConfigDescription(
                    "Drawn-dot cap from MaxDotsLevel3 until MaxDotsLevel4 (then MaxDots).",
                    new AcceptableValueRange<int>(1, 256)));

            MaxDotsLevel1 = ConfigBind(
                "Progression",
                "MaxDotsLevel1",
                10f,
                new ConfigDescription("Skill level for MaxDotsCap1.", new AcceptableValueRange<float>(0f, 100f)));

            MaxDotsLevel2 = ConfigBind(
                "Progression",
                "MaxDotsLevel2",
                20f,
                new ConfigDescription("Skill level for MaxDotsCap2.", new AcceptableValueRange<float>(0f, 100f)));

            MaxDotsLevel3 = ConfigBind(
                "Progression",
                "MaxDotsLevel3",
                45f,
                new ConfigDescription("Skill level for MaxDotsCap3.", new AcceptableValueRange<float>(0f, 100f)));

            MaxDotsLevel4 = ConfigBind(
                "Progression",
                "MaxDotsLevel4",
                75f,
                new ConfigDescription("Skill level for full MaxDots cap.", new AcceptableValueRange<float>(0f, 100f)));

            RefreshConfigSnapshot();
            Config.SettingChanged += OnSettingChanged;

            TrackingSkill.Initialize();
            TrackingRadar.Initialize();

            _harmony = new Harmony(PluginGuid);
            PatchOwnTypes(_harmony);

            TrackingSkill.RegisterLocalization();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (skill type {(int)TrackingSkill.SkillType})");
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            TrackingRadar.Shutdown();
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            TrackingConfigSnapshot cfg = ConfigSnapshot;
            if (!cfg.Enabled)
            {
                TrackingRadar.HideAll();
                return;
            }

            Player player = Player.m_localPlayer;
            TrackingRadar.Tick(player, cfg);
            TrackingExperience.Tick(player, cfg);
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            RefreshConfigSnapshot();
        }

        internal static void RefreshConfigSnapshot()
        {
            float minSpeed = Mathf.Max(0.01f, MinMoveSpeed.Value);
            ConfigSnapshot = new TrackingConfigSnapshot
            {
                Enabled = Enabled.Value,
                MinRange = Mathf.Max(0f, MinRange.Value),
                MaxRange = Mathf.Max(0f, MaxRange.Value),
                Balance = Mathf.Clamp(BalancePercent.Value, 1f, 99f) / 100f,
                ShowRangeCircle = ShowRangeCircle.Value,
                CircleColor = ParseColor(CircleColor.Value, new Color(1f, 0.92f, 0.2f, 0.16f)),
                ShowOnMinimap = ShowOnMinimap.Value,
                ShowOnLargeMap = ShowOnLargeMap.Value,
                ShowPassive = ShowPassive.Value,
                ShowHostile = ShowHostile.Value,
                ShowBoss = ShowBoss.Value,
                ShowStarred = ShowStarred.Value,
                PassiveColor = ParseColor(PassiveColor.Value, Color.white),
                HostileColor = ParseColor(HostileColor.Value, new Color(1f, 0.85f, 0.15f, 0.95f)),
                BossColor = ParseColor(BossColor.Value, new Color(1f, 0.35f, 0.15f, 0.95f)),
                DotSize = Mathf.Clamp(DotSize.Value, 1f, 48f),
                StarRingSize = Mathf.Clamp(StarRingSize.Value, 2f, 64f),
                UpdateIntervalSeconds = Mathf.Clamp(UpdateIntervalSeconds.Value, 0.05f, 2f),
                MaxDots = Mathf.Clamp(MaxDots.Value, 1, 256),
                ShowTooltips = ShowTooltips.Value,
                FogPierceLevel = Mathf.Clamp(FogPierceLevel.Value, 0f, Skills.c_MaxSkillLevel),
                PierceEnabled = PierceEnabled.Value,
                ExpRate = Mathf.Max(0f, ExpRate.Value),
                MinMoveSpeed = minSpeed,
                MinMoveDirSqr = 0.01f * 0.01f,
                StarredExpBonus = Mathf.Max(0f, StarredExpBonus.Value),
                XpIntervalSeconds = 1f,
                ConeStartDegrees = Mathf.Clamp(ConeStartDegrees.Value, 10f, 360f),
                ConeFullLevel = Mathf.Clamp(ConeFullLevel.Value, 1f, Skills.c_MaxSkillLevel),
                HostilityUnlockLevel = Mathf.Clamp(HostilityUnlockLevel.Value, 0f, Skills.c_MaxSkillLevel),
                StarsUnlockLevel = Mathf.Clamp(StarsUnlockLevel.Value, 0f, Skills.c_MaxSkillLevel),
                BossUnlockLevel = Mathf.Clamp(BossUnlockLevel.Value, 0f, Skills.c_MaxSkillLevel),
                NamesUnlockLevel = Mathf.Clamp(NamesUnlockLevel.Value, 0f, Skills.c_MaxSkillLevel),
                ConeHysteresisSeconds = Mathf.Clamp(ConeHysteresisSeconds.Value, 0f, 3f),
                JitterMetersAt0 = Mathf.Max(0f, JitterMetersAt0.Value),
                JitterZeroLevel = Mathf.Clamp(JitterZeroLevel.Value, 1f, Skills.c_MaxSkillLevel),
                PostureFloor = Mathf.Clamp(PostureFloor.Value, 0f, 1f),
                PostureBonusAt100 = Mathf.Max(0f, PostureBonusAt100.Value),
                PostureStillSeconds = Mathf.Max(0f, PostureStillSeconds.Value),
                PostureStillSpeed = Mathf.Max(0.01f, PostureStillSpeed.Value),
                TrophyEarlyNames = TrophyEarlyNames.Value,
                NameTrophylessCreatures = NameTrophylessCreatures.Value,
                MaxDotsCap0 = Mathf.Clamp(MaxDotsCap0.Value, 1, 256),
                MaxDotsCap1 = Mathf.Clamp(MaxDotsCap1.Value, 1, 256),
                MaxDotsCap2 = Mathf.Clamp(MaxDotsCap2.Value, 1, 256),
                MaxDotsCap3 = Mathf.Clamp(MaxDotsCap3.Value, 1, 256),
                MaxDotsLevel1 = Mathf.Clamp(MaxDotsLevel1.Value, 0f, Skills.c_MaxSkillLevel),
                MaxDotsLevel2 = Mathf.Clamp(MaxDotsLevel2.Value, 0f, Skills.c_MaxSkillLevel),
                MaxDotsLevel3 = Mathf.Clamp(MaxDotsLevel3.Value, 0f, Skills.c_MaxSkillLevel),
                MaxDotsLevel4 = Mathf.Clamp(MaxDotsLevel4.Value, 0f, Skills.c_MaxSkillLevel)
            };
            TrackingRange.InvalidateCache();
            TrackingKnowledge.Bump();
        }

        internal static Color ParseColor(string raw, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            string[] parts = raw.Split(',');
            if (parts.Length < 3)
            {
                return fallback;
            }

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                || !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                return fallback;
            }

            float a = 1f;
            if (parts.Length >= 4
                && float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedA))
            {
                a = parsedA;
            }

            return new Color(
                Mathf.Clamp01(r),
                Mathf.Clamp01(g),
                Mathf.Clamp01(b),
                Mathf.Clamp01(a));
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
