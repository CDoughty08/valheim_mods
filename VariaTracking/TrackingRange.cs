using UnityEngine;

namespace VariaTracking
{
    /// <summary>
    /// Maps skill level → tracking radius using a tunable ease curve.
    /// Balance 0.5 = linear; &gt;0.5 back-loads; &lt;0.5 front-loads.
    /// </summary>
    internal static class TrackingRange
    {
        private const float CacheTtlSeconds = 0.25f;

        private static Player _cachePlayer;
        private static float _cacheTime = float.NegativeInfinity;
        private static float _cacheMin = float.NaN;
        private static float _cacheMax = float.NaN;
        private static float _cacheBalance = float.NaN;
        private static bool _cacheEnabled;
        private static float _cacheRadius;
        private static float _cacheLevel;

        public static void InvalidateCache()
        {
            _cachePlayer = null;
            _cacheTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Progress t in [0,1] curved by balance in (0,1).
        /// exponent = 4^(2*(balance-0.5)) → 0.1≈0.33, 0.5=1, 0.9≈3.03
        /// </summary>
        public static float ApplyBalance(float t, float balance01)
        {
            t = Mathf.Clamp01(t);
            float b = Mathf.Clamp(balance01, 0.01f, 0.99f);
            if (Mathf.Abs(b - 0.5f) <= 0.0001f)
            {
                return t;
            }

            float exponent = Mathf.Pow(4f, 2f * (b - 0.5f));
            return Mathf.Pow(t, exponent);
        }

        public static float GetRadius(float skillLevel0To100, TrackingConfigSnapshot cfg)
        {
            if (!cfg.Enabled)
            {
                return 0f;
            }

            float lo = Mathf.Min(cfg.MinRange, cfg.MaxRange);
            float hi = Mathf.Max(cfg.MinRange, cfg.MaxRange);
            float t = Mathf.Clamp(skillLevel0To100, 0f, Skills.c_MaxSkillLevel) / Skills.c_MaxSkillLevel;
            float curved = ApplyBalance(t, cfg.Balance);
            return Mathf.Lerp(lo, hi, curved);
        }

        public static float GetRadius(Player player, TrackingConfigSnapshot cfg, out float skillLevel)
        {
            skillLevel = 0f;
            if (player == null || !cfg.Enabled)
            {
                return 0f;
            }

            float now = Time.unscaledTime;
            if (ReferenceEquals(player, _cachePlayer)
                && _cacheEnabled == cfg.Enabled
                && cfg.MinRange == _cacheMin
                && cfg.MaxRange == _cacheMax
                && cfg.Balance == _cacheBalance
                && now - _cacheTime < CacheTtlSeconds)
            {
                skillLevel = _cacheLevel;
                return _cacheRadius;
            }

            Skills skills = player.GetSkills();
            skillLevel = skills != null ? skills.GetSkillLevel(TrackingSkill.SkillType) : 0f;
            float radius = GetRadius(skillLevel, cfg);

            _cachePlayer = player;
            _cacheTime = now;
            _cacheEnabled = cfg.Enabled;
            _cacheMin = cfg.MinRange;
            _cacheMax = cfg.MaxRange;
            _cacheBalance = cfg.Balance;
            _cacheRadius = radius;
            _cacheLevel = skillLevel;
            return radius;
        }
    }
}
