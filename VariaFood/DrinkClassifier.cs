using System;
using System.Collections.Generic;
using System.Text;

namespace VariaFood
{
    /// <summary>
    /// Whole-word drink detection for the dedicated drink slot. Cached per SharedData.
    /// </summary>
    internal static class DrinkClassifier
    {
        private static readonly string[] DefaultTokens =
        {
            "mead", "ale", "beer", "wine", "cider", "brew", "tea", "juice",
            "nectar", "grog", "elixir", "tonic", "potion"
        };

        private static readonly HashSet<string> DrinkTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private struct Classification
        {
            internal string PrefabName;
            internal string ItemName;
            internal bool IsDrink;
        }

        private static readonly Dictionary<ItemDrop.ItemData.SharedData, Classification> Cache =
            new Dictionary<ItemDrop.ItemData.SharedData, Classification>();

        private static readonly StringBuilder TokenBuffer = new StringBuilder(32);
        private static readonly List<string> TokenScratch = new List<string>(8);

        internal static void Rebuild(string extraKeywords)
        {
            DrinkTokens.Clear();
            Cache.Clear();

            for (int i = 0; i < DefaultTokens.Length; i++)
            {
                DrinkTokens.Add(DefaultTokens[i]);
            }

            if (string.IsNullOrWhiteSpace(extraKeywords))
            {
                return;
            }

            string[] parts = extraKeywords.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length > 0)
                {
                    DrinkTokens.Add(token);
                }
            }
        }

        internal static bool IsDrink(ItemDrop.ItemData item, string prefabNameHint = null)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            ItemDrop.ItemData.SharedData shared = item.m_shared;
            string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : prefabNameHint;
            if (Cache.TryGetValue(shared, out Classification cached)
                && cached.PrefabName == prefabName && cached.ItemName == shared.m_name)
            {
                return cached.IsDrink;
            }

            bool result = HasDrinkToken(prefabName) || HasDrinkToken(shared.m_name);
            Cache[shared] = new Classification
            {
                PrefabName = prefabName,
                ItemName = shared.m_name,
                IsDrink = result
            };
            return result;
        }

        private static bool HasDrinkToken(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            TokenScratch.Clear();
            Tokenize(source, TokenScratch);
            for (int i = 0; i < TokenScratch.Count; i++)
            {
                if (DrinkTokens.Contains(TokenScratch[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Split on non-letters and lower→upper transitions ($item_mead_tasty, BarleyWine, Apple Juice).
        /// </summary>
        private static void Tokenize(string s, List<string> into)
        {
            TokenBuffer.Clear();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetter(c))
                {
                    FlushToken(into);
                    continue;
                }

                if (TokenBuffer.Length > 0 && char.IsUpper(c)
                    && (char.IsLower(s[i - 1])
                        || (char.IsUpper(s[i - 1]) && i + 1 < s.Length && char.IsLower(s[i + 1]))))
                {
                    FlushToken(into);
                }

                TokenBuffer.Append(char.ToLowerInvariant(c));
            }

            FlushToken(into);
        }

        private static void FlushToken(List<string> into)
        {
            if (TokenBuffer.Length == 0)
            {
                return;
            }

            into.Add(TokenBuffer.ToString());
            TokenBuffer.Clear();
        }
    }
}
