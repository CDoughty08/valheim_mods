using UnityEngine;

namespace VariaTracking
{
    /// <summary>
    /// Hunting posture (crouch or settled still) → smoothed range multiplier.
    /// Applied outside <see cref="TrackingRange"/> cache.
    /// </summary>
    internal static class TrackingPosture
    {
        private const float SmoothSeconds = 0.35f;

        private static float _stillTimer;
        private static float _smoothedMul = 1f;

        public static void Reset()
        {
            _stillTimer = 0f;
            _smoothedMul = 1f;
        }

        public static bool IsHuntingPosture(Player player, TrackingConfigSnapshot cfg)
        {
            if (player == null)
            {
                return false;
            }

            if (player.IsCrouching())
            {
                return true;
            }

            Vector3 velocity = player.GetVelocity();
            velocity.y = 0f;
            float stillSpeed = Mathf.Max(0.01f, cfg.PostureStillSpeed);
            if (velocity.sqrMagnitude <= stillSpeed * stillSpeed)
            {
                _stillTimer += Time.deltaTime;
                return _stillTimer >= cfg.PostureStillSeconds;
            }

            _stillTimer = 0f;
            return false;
        }

        public static float GetSmoothedMultiplier(Player player, TrackingConfigSnapshot cfg, float targetMulWhenPostured)
        {
            float target = IsHuntingPosture(player, cfg) ? Mathf.Max(1f, targetMulWhenPostured) : 1f;
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.05f, SmoothSeconds));
            _smoothedMul = Mathf.Lerp(_smoothedMul, target, t);
            if (Mathf.Abs(_smoothedMul - target) < 0.001f)
            {
                _smoothedMul = target;
            }

            return _smoothedMul;
        }

        public static float GetEffectiveRadius(
            Player player,
            TrackingConfigSnapshot cfg,
            TrackingUnlockState unlocks,
            out float skillLevel)
        {
            float baseRadius = TrackingRange.GetRadius(player, cfg, out skillLevel);
            if (baseRadius <= 0f)
            {
                return 0f;
            }

            float mul = GetSmoothedMultiplier(player, cfg, unlocks.PostureRangeMul);
            return baseRadius * mul;
        }
    }
}
