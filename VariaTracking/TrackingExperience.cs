namespace VariaTracking
{
    internal static class TrackingExperience
    {
        private static readonly Varia.Shared.MovementExperience Experience = new Varia.Shared.MovementExperience();
        public static void Reset() => Experience.Reset();

        public static void Tick(Player player, TrackingConfigSnapshot cfg)
        {
            if (cfg.ExpRate <= 0f) { Reset(); return; }
            float rate = TrackingRadar.LastTrackableCount > 0 ? cfg.ExpRate : 0f;
            if (TrackingRadar.LastStarredInRange > 0 && cfg.StarredExpBonus > 0f)
                rate *= 1f + cfg.StarredExpBonus;
            Experience.Tick(player, TrackingSkill.SkillType, rate, cfg.XpIntervalSeconds,
                cfg.MinMoveDirSqr, cfg.MinMoveSpeed);
        }
    }
}
