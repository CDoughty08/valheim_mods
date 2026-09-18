using UnityEngine;

namespace VariaTracking
{
    /// <summary>Single place that resolves marker color / star / name visibility from kind + unlocks.</summary>
    internal static class TrackingDisplay
    {
        private static readonly Color GreyBlip = new(0.72f, 0.72f, 0.72f, 0.92f);

        public static Color ResolveColor(
            MarkerKind kind,
            TrackingUnlockState unlocks,
            TrackingConfigSnapshot cfg)
        {
            if (kind == MarkerKind.Boss && unlocks.ShowBossColor && cfg.ShowBoss)
            {
                return cfg.BossColor;
            }

            if (!unlocks.ShowHostility)
            {
                return GreyBlip;
            }

            if (kind == MarkerKind.Boss || kind == MarkerKind.Hostile)
            {
                return cfg.HostileColor;
            }

            return cfg.PassiveColor;
        }

        public static bool ShowStarRing(bool starred, TrackingUnlockState unlocks, TrackingConfigSnapshot cfg)
        {
            return starred && unlocks.ShowStars && cfg.ShowStarred;
        }

        public static bool CanShowRealName(
            Character character,
            Player player,
            TrackingUnlockState unlocks,
            TrackingConfigSnapshot cfg)
        {
            if (!cfg.ShowTooltips)
            {
                return false;
            }

            if (unlocks.ShowAllNames)
            {
                return true;
            }

            if (cfg.TrophyEarlyNames || cfg.NameTrophylessCreatures)
            {
                bool studied = TrackingTrophyKnowledge.GetKnowledge(player, character, out bool hasTrophy);
                return (cfg.TrophyEarlyNames && studied) || (cfg.NameTrophylessCreatures && !hasTrophy);
            }

            return false;
        }

        public static string ResolveDisplayName(
            Character character,
            Player player,
            bool starred,
            TrackingUnlockState unlocks,
            TrackingConfigSnapshot cfg)
        {
            if (!CanShowRealName(character, player, unlocks, cfg))
            {
                return "???";
            }

            bool starInName = ShowStarRing(starred, unlocks, cfg);
            return TrackingRadar.BuildDisplayName(character, starInName);
        }
    }
}
