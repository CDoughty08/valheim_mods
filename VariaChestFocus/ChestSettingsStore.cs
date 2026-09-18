using System;
using System.Runtime.CompilerServices;

namespace VariaChestFocus
{
    internal static class ChestSettingsStore
    {
        internal const string ZdoKey = "com.varia.chestfocus.settings.v1";

        private static readonly ConditionalWeakTable<Container, CacheEntry> Cache = new ConditionalWeakTable<Container, CacheEntry>();
        private static ChestSettings _clipboard;

        private sealed class CacheEntry
        {
            public string Raw;
            public ChestSettings Settings;
        }

        internal static ChestSettings Get(Container container)
        {
            ZDO zdo = GetZdo(container);
            if (zdo == null)
            {
                return new ChestSettings();
            }

            string raw = zdo.GetString(ZdoKey, string.Empty);
            CacheEntry entry = Cache.GetValue(container, _ => new CacheEntry());
            if (entry.Raw == raw && entry.Settings != null)
            {
                return entry.Settings;
            }

            if (!ChestSettings.TryParse(raw, out ChestSettings settings) || settings == null)
            {
                settings = new ChestSettings();
            }

            entry.Raw = raw;
            entry.Settings = settings;
            return settings;
        }

        internal static bool TrySet(Container container, ChestSettings settings)
        {
            if (container == null || settings == null)
            {
                return false;
            }

            ZNetView nview = container.m_nview;
            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            if (!nview.IsOwner())
            {
                // Opening a chest usually grants ownership; refuse silent remote writes.
                return false;
            }

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
            {
                return false;
            }

            string raw = settings.IsEmpty && settings.Priority == ChestPriority.Medium && !settings.HasTint
                ? string.Empty
                : settings.Serialize();

            if (zdo.GetString(ZdoKey, string.Empty) != raw)
            {
                zdo.Set(ZdoKey, raw);
            }
            CacheEntry entry = Cache.GetValue(container, _ => new CacheEntry());
            entry.Raw = raw;
            entry.Settings = settings.Clone();
            return true;
        }

        internal static void Invalidate(Container container)
        {
            if (container != null)
            {
                Cache.Remove(container);
            }
        }

        internal static void Copy(ChestSettings settings)
        {
            _clipboard = settings?.Clone();
        }

        internal static bool HasClipboard => _clipboard != null;

        internal static ChestSettings PasteClone()
        {
            return _clipboard?.Clone();
        }

        private static ZDO GetZdo(Container container)
        {
            if (container?.m_nview == null || !container.m_nview.IsValid())
            {
                return null;
            }

            return container.m_nview.GetZDO();
        }
    }
}
