using HarmonyLib;
using UnityEngine;

namespace VariaTracking
{
    /// <summary>Read the hovered creature's current drops; no species cache or invalidation assumptions.</summary>
    internal static class TrackingTrophyKnowledge
    {
        public static bool HasTrophyDrop(Character character)
        {
            GetKnowledge(null, character, out bool hasTrophy);
            return hasTrophy;
        }

        public static bool IsStudied(Player player, Character character)
        {
            return GetKnowledge(player, character, out _);
        }

        public static bool GetKnowledge(Player player, Character character, out bool hasTrophy)
        {
            hasTrophy = false;
            if (character == null)
            {
                return false;
            }

            CharacterDrop drop = character.GetComponent<CharacterDrop>();
            if (drop?.m_drops == null)
            {
                return false;
            }

            bool studied = false;
            foreach (CharacterDrop.Drop entry in drop.m_drops)
            {
                GameObject prefab = entry?.m_prefab;
                if (prefab == null)
                {
                    continue;
                }

                ItemDrop item = prefab.GetComponent<ItemDrop>();
                if (item?.m_itemData?.m_shared?.m_itemType != ItemDrop.ItemData.ItemType.Trophy)
                {
                    continue;
                }

                hasTrophy = true;
                if (player != null && (IsKnown(player, prefab.name) || IsKnown(player, item.m_itemData.m_shared.m_name)))
                {
                    studied = true;
                }
            }
            return studied;
        }

        private static bool IsKnown(Player player, string key)
        {
            return !string.IsNullOrEmpty(key)
                && ((player.m_trophies != null && player.m_trophies.Contains(key)) || player.IsMaterialKnown(key));
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
    internal static class PlayerAddKnownItemPatch
    {
        private static void Prefix(Player __instance, out int __state)
        {
            __state = __instance == Player.m_localPlayer ? __instance.m_trophies.Count : -1;
        }

        private static void Postfix(Player __instance, int __state)
        {
            // Vanilla calls AddKnownItem for every inventory item on every inventory change.
            if (__state >= 0 && __instance.m_trophies.Count != __state)
            {
                TrackingKnowledge.Bump();
            }
        }
    }
}
