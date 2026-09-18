using System;
using UnityEngine;

namespace VariaChestFocus
{
    internal static class ItemUtil
    {
        internal static string GetPrefabName(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.m_dropPrefab != null)
            {
                return StripClone(item.m_dropPrefab.name);
            }

            // Fallback: shared name is a localization token; ObjectDB lookup by token is unreliable.
            // Prefer drop prefab whenever present.
            return null;
        }

        internal static string GetPrefabName(GameObject go)
        {
            return go == null ? null : StripClone(go.name);
        }

        internal static string StripClone(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            const string clone = "(Clone)";
            if (name.EndsWith(clone, StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - clone.Length).TrimEnd();
            }

            return name;
        }

        internal static string GetDisplayName(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return "?";
            }

            string localized = Localization.instance != null
                ? Localization.instance.Localize(item.m_shared.m_name)
                : item.m_shared.m_name;
            return string.IsNullOrEmpty(localized) ? item.m_shared.m_name : localized;
        }

        internal static Sprite GetIcon(ItemDrop.ItemData item)
        {
            if (item?.m_shared?.m_icons == null || item.m_shared.m_icons.Length == 0)
            {
                return null;
            }

            // Some modded items leave the first variant empty but provide another icon.
            foreach (Sprite icon in item.m_shared.m_icons)
            {
                if (icon != null) return icon;
            }
            return null;
        }
    }
}
