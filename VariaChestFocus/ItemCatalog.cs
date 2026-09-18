using System;
using System.Collections.Generic;
using UnityEngine;

namespace VariaChestFocus
{
    internal enum CatalogView { Items, Pinned, NoIcon, Missing }

    internal sealed class CatalogEntry
    {
        internal string PrefabName;
        internal string DisplayName;
        internal string DisplayNameLower;
        internal string PrefabLower;
        internal Sprite Icon;
        internal CategoryId Category;
    }

    internal static class ItemCatalog
    {
        private static readonly List<CatalogEntry> Entries = new List<CatalogEntry>(512);
        private static readonly Dictionary<string, CatalogEntry> ByPrefab =
            new Dictionary<string, CatalogEntry>(512, StringComparer.OrdinalIgnoreCase);

        private static bool _built;
        private static int _objectDbCount = -1;
        private static ObjectDB _objectDb;
        private static string _language;
        private static int _recipeCount = -1;

        internal static IReadOnlyList<CatalogEntry> All => Entries;

        // Called when opening the browser and after ObjectDB rebuilds its registrations.
        // This also catches in-place changes made by mods without polling all items per frame.
        internal static void Invalidate()
        {
            _built = false;
            CategoryDefs.Invalidate();
        }

        internal static void EnsureBuilt()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                return;
            }

            int count = db.m_items.Count;
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : null;
            if (_built && ReferenceEquals(db, _objectDb) && count == _objectDbCount
                && _recipeCount == (db.m_recipes?.Count ?? -1) && _language == language)
            {
                return;
            }

            Rebuild(db);
        }

        internal static void Rebuild(ObjectDB db)
        {
            Entries.Clear();
            ByPrefab.Clear();
            _built = false;
            _objectDb = db;
            _language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : null;
            _recipeCount = db?.m_recipes?.Count ?? -1;
            _objectDbCount = db?.m_items?.Count ?? -1;
            if (db?.m_items == null)
            {
                return;
            }

            for (int i = 0; i < db.m_items.Count; i++)
            {
                GameObject go = db.m_items[i];
                if (go == null)
                {
                    continue;
                }

                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared == null)
                {
                    continue;
                }

                // Ensure dropPrefab is set for classification helpers.
                ItemDrop.ItemData data = drop.m_itemData.Clone();
                data.m_dropPrefab = go;

                string prefab = ItemUtil.GetPrefabName(go);
                if (string.IsNullOrEmpty(prefab) || ByPrefab.ContainsKey(prefab))
                {
                    continue;
                }

                CatalogEntry entry = new CatalogEntry
                {
                    PrefabName = prefab,
                    DisplayName = ItemUtil.GetDisplayName(data),
                    Icon = ItemUtil.GetIcon(data),
                    Category = CategoryDefs.Classify(data)
                };
                entry.DisplayNameLower = entry.DisplayName?.ToLowerInvariant() ?? string.Empty;
                entry.PrefabLower = prefab.ToLowerInvariant();

                ByPrefab[prefab] = entry;
                Entries.Add(entry);
            }

            Entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _built = true;
        }

        internal static bool TryGet(string prefab, out CatalogEntry entry)
        {
            EnsureBuilt();
            return ByPrefab.TryGetValue(prefab, out entry);
        }

        internal static void Search(string query, CategoryId categoryFilter, List<CatalogEntry> results, int maxResults)
        {
            EnsureBuilt();
            results.Clear();
            if (maxResults <= 0) return;
            string q = string.IsNullOrEmpty(query) ? null : query.Trim().ToLowerInvariant();

            for (int i = 0; i < Entries.Count; i++)
            {
                CatalogEntry e = Entries[i];
                if (categoryFilter != CategoryId.None && (e.Category & categoryFilter) == 0)
                {
                    continue;
                }

                if (q != null
                    && e.DisplayNameLower.IndexOf(q, StringComparison.Ordinal) < 0
                    && e.PrefabLower.IndexOf(q, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                results.Add(e);
                if (results.Count >= maxResults)
                {
                    break;
                }
            }
        }

        internal static List<string> GetMissingPrefabs(IEnumerable<string> stored)
        {
            EnsureBuilt();
            List<string> missing = new List<string>();
            if (stored == null)
            {
                return missing;
            }

            foreach (string prefab in stored)
            {
                if (string.IsNullOrEmpty(prefab))
                {
                    continue;
                }

                if (!ByPrefab.ContainsKey(prefab))
                {
                    missing.Add(prefab);
                }
            }

            missing.Sort(StringComparer.OrdinalIgnoreCase);
            return missing;
        }

        internal static void Browse(string query, CatalogView view, ChestSettings settings, List<CatalogEntry> results)
        {
            Search(query, CategoryId.None, results, int.MaxValue);
            results.RemoveAll(entry => view == CatalogView.Missing
                || view == CatalogView.Items && entry.Icon == null
                || view == CatalogView.NoIcon && entry.Icon != null
                || view == CatalogView.Pinned && !settings.AllowedPrefabs.Contains(entry.PrefabName));
            if (view != CatalogView.Missing) return;

            string q = query?.Trim() ?? string.Empty;
            foreach (string prefab in GetMissingPrefabs(settings.AllowedPrefabs))
            {
                if (prefab.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                results.Add(new CatalogEntry { PrefabName = prefab, DisplayName = prefab });
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(ObjectDB), "UpdateRegisters")]
    internal static class CatalogRegistrationPatch
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix() => ItemCatalog.Invalidate();
    }
}
