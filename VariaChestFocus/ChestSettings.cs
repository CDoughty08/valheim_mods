using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VariaChestFocus
{
    internal sealed class ChestSettings
    {
        internal ChestPriority Priority = ChestPriority.Medium;
        internal CategoryId CategoryFlags = CategoryId.None;
        // -1 keeps the original appearance. RGB is independent of deposit/sort filters.
        internal int TintRgb = -1;
        internal bool HasTint => TintRgb >= 0 && TintRgb <= 0xFFFFFF;
        internal readonly HashSet<string> AllowedPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal bool IsEmpty =>
            CategoryFlags == CategoryId.None && AllowedPrefabs.Count == 0;

        internal bool HasConfiguration => !IsEmpty || Priority == ChestPriority.Never;

        internal bool Allows(ItemDrop.ItemData item)
        {
            if (IsEmpty)
            {
                return true;
            }

            if (item == null)
            {
                return false;
            }

            string prefab = ItemUtil.GetPrefabName(item);
            if (!string.IsNullOrEmpty(prefab) && AllowedPrefabs.Contains(prefab))
            {
                return true;
            }

            return CategoryDefs.Matches(item, CategoryFlags);
        }

        internal ChestSettings Clone()
        {
            ChestSettings copy = new ChestSettings
            {
                Priority = Priority,
                CategoryFlags = CategoryFlags,
                TintRgb = TintRgb
            };
            foreach (string prefab in AllowedPrefabs)
            {
                copy.AllowedPrefabs.Add(prefab);
            }

            return copy;
        }

        internal void PinFromInventory(Inventory inventory)
        {
            if (inventory == null)
            {
                return;
            }

            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                string prefab = ItemUtil.GetPrefabName(items[i]);
                if (!string.IsNullOrEmpty(prefab))
                {
                    AllowedPrefabs.Add(prefab);
                }
            }
        }

        internal void ClearFilters()
        {
            CategoryFlags = CategoryId.None;
            AllowedPrefabs.Clear();
        }

        internal string Serialize()
        {
            StringBuilder sb = new StringBuilder(64 + AllowedPrefabs.Count * 12);
            sb.Append("v1|P:")
                .Append(((int)Priority).ToString(CultureInfo.InvariantCulture))
                .Append("|C:")
                .Append(((ulong)CategoryFlags).ToString(CultureInfo.InvariantCulture))
                .Append("|I:");

            bool first = true;
            foreach (string prefab in SortedPrefabs())
            {
                if (string.IsNullOrEmpty(prefab))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(Escape(prefab));
            }

            if (HasTint) sb.Append("|T:").Append(TintRgb.ToString("X6", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static bool TryParse(string raw, out ChestSettings settings)
        {
            settings = new ChestSettings();
            if (string.IsNullOrEmpty(raw))
            {
                return true;
            }

            if (!raw.StartsWith("v1|", StringComparison.Ordinal))
            {
                return false;
            }

            List<string> parts = SplitEscaped(raw, '|');
            for (int i = 1; i < parts.Count; i++)
            {
                string part = parts[i];
                if (part.StartsWith("P:", StringComparison.Ordinal))
                {
                    if (int.TryParse(part.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)
                        && p >= 0 && p <= (int)ChestPriority.Critical)
                    {
                        settings.Priority = (ChestPriority)p;
                    }
                }
                else if (part.StartsWith("C:", StringComparison.Ordinal))
                {
                    if (ulong.TryParse(part.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong flags))
                    {
                        settings.CategoryFlags = (CategoryId)flags;
                    }
                }
                else if (part.StartsWith("T:", StringComparison.Ordinal))
                {
                    string hex = part.Substring(2);
                    if (hex.Length == 6 && int.TryParse(hex, NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture, out int rgb)) settings.TintRgb = rgb;
                }
                else if (part.StartsWith("I:", StringComparison.Ordinal))
                {
                    string list = part.Substring(2);
                    if (string.IsNullOrEmpty(list))
                    {
                        continue;
                    }

                    List<string> names = SplitEscaped(list, ',');
                    for (int n = 0; n < names.Count; n++)
                    {
                        string name = Unescape(names[n]);
                        if (!string.IsNullOrEmpty(name))
                        {
                            settings.AllowedPrefabs.Add(name);
                        }
                    }
                }
            }

            return true;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace(",", "\\,").Replace("|", "\\|");
        }

        private List<string> SortedPrefabs()
        {
            List<string> names = new List<string>(AllowedPrefabs);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // Keep escapes until the final name is decoded, so delimiters inside names survive.
        private static List<string> SplitEscaped(string value, char separator)
        {
            List<string> parts = new List<string>();
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    i++;
                }
                else if (value[i] == separator)
                {
                    parts.Add(value.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(value.Substring(start));
            return parts;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    sb.Append(value[i + 1]);
                    i++;
                    continue;
                }

                sb.Append(value[i]);
            }

            return sb.ToString();
        }
    }
}
