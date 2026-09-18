using UnityEngine;

namespace VariaTracking
{
    /// <summary>Skill-gated tracking clarity / FOV / caps for one frame.</summary>
    internal struct TrackingUnlockState
    {
        public float SkillLevel;
        public float ConeDegrees;
        public int MaxDotsCap;
        public float JitterMeters;
        public float PostureRangeMul;
        public bool ShowHostility;
        public bool ShowStars;
        public bool ShowBossColor;
        public bool ShowAllNames;
        public bool PierceFog;
    }

    /// <summary>Maps Tracking skill level → unlock flags and continuous soft stats.</summary>
    internal static class TrackingUnlocks
    {
        public static TrackingUnlockState GetState(float skillLevel, TrackingConfigSnapshot cfg)
        {
            float level = Mathf.Clamp(skillLevel, 0f, Skills.c_MaxSkillLevel);

            float coneFull = Mathf.Max(1f, cfg.ConeFullLevel);
            float coneT = Mathf.Clamp01(level / coneFull);
            float cone = Mathf.Lerp(cfg.ConeStartDegrees, 360f, coneT);
            if (level >= coneFull)
            {
                cone = 360f;
            }

            float jitterT = cfg.JitterZeroLevel > 0.01f
                ? Mathf.Clamp01(1f - level / cfg.JitterZeroLevel)
                : 0f;
            float jitter = cfg.JitterMetersAt0 * jitterT;

            float postureMul = 1f + cfg.PostureFloor + (level / Skills.c_MaxSkillLevel) * cfg.PostureBonusAt100;

            return new TrackingUnlockState
            {
                SkillLevel = level,
                ConeDegrees = cone,
                MaxDotsCap = GetMaxDotsCap(level, cfg),
                JitterMeters = Mathf.Max(0f, jitter),
                PostureRangeMul = Mathf.Max(1f, postureMul),
                ShowHostility = level >= cfg.HostilityUnlockLevel,
                ShowStars = level >= cfg.StarsUnlockLevel,
                ShowBossColor = level >= cfg.BossUnlockLevel,
                ShowAllNames = level >= cfg.NamesUnlockLevel,
                PierceFog = cfg.PierceEnabled && level >= cfg.FogPierceLevel
            };
        }

        public static int GetMaxDotsCap(float skillLevel, TrackingConfigSnapshot cfg)
        {
            int cap;
            if (skillLevel < cfg.MaxDotsLevel1)
            {
                cap = cfg.MaxDotsCap0;
            }
            else if (skillLevel < cfg.MaxDotsLevel2)
            {
                cap = cfg.MaxDotsCap1;
            }
            else if (skillLevel < cfg.MaxDotsLevel3)
            {
                cap = cfg.MaxDotsCap2;
            }
            else if (skillLevel < cfg.MaxDotsLevel4)
            {
                cap = cfg.MaxDotsCap3;
            }
            else
            {
                cap = cfg.MaxDots;
            }

            return Mathf.Clamp(Mathf.Min(cap, cfg.MaxDots), 1, cfg.MaxDots);
        }
    }
}
