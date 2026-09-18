using UnityEngine;

namespace VariaTracking
{
    internal enum MarkerKind : byte
    {
        Passive = 0,
        Hostile = 1,
        Boss = 2
    }

    /// <summary>
    /// Creature → marker kind from game AI / faction metadata (no prefab-name lists).
    /// Pre-hostility-unlock, config ShowPassive/ShowHostile are not used as filters (avoids leaking class).
    /// </summary>
    internal static class CreatureClassifier
    {
        public static bool TryClassify(
            Character character,
            Player player,
            TrackingConfigSnapshot cfg,
            TrackingUnlockState unlocks,
            out MarkerKind kind,
            out bool starred)
        {
            kind = MarkerKind.Passive;
            starred = false;

            if (character == null || character == player || character.IsPlayer() || character.IsDead())
            {
                return false;
            }

            Character.Faction faction = character.GetFaction();
            if (faction == Character.Faction.Players || faction == Character.Faction.TrainingDummy)
            {
                return false;
            }

            if (character.IsTamed())
            {
                return false;
            }

            starred = character.GetLevel() > 1;
            bool boss = character.m_boss || faction == Character.Faction.Boss;
            if (boss)
            {
                kind = MarkerKind.Boss;
                return cfg.ShowBoss;
            }
            bool hostile = IsHostileNonBoss(character, player);

            if (!unlocks.ShowHostility)
            {
                // Pre-unlock: include all trackables as dots (display greys them).
                kind = hostile ? MarkerKind.Hostile : MarkerKind.Passive;
                return true;
            }

            if (hostile)
            {
                if (!cfg.ShowHostile)
                {
                    return false;
                }

                kind = MarkerKind.Hostile;
                return true;
            }

            if (!cfg.ShowPassive)
            {
                return false;
            }

            kind = MarkerKind.Passive;
            return true;
        }

        /// <summary>
        /// Use the game's relationship to the local player, not alertness toward any target.
        /// AnimalAI (flee-only) is never hostile. Bosses handled separately.
        /// </summary>
        private static bool IsHostileNonBoss(Character character, Player player)
        {
            BaseAI ai = character.GetBaseAI();
            if (ai is AnimalAI)
            {
                return false;
            }

            return BaseAI.IsEnemy(character, player);
        }
    }
}
