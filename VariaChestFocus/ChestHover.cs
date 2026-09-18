using System.Text;
using HarmonyLib;

namespace VariaChestFocus
{
    internal static class ChestHoverSummary
    {
        // Hover runs every frame. Cache the displayed values, without retaining a chest
        // or building the item catalog. ZDO changes still flow through SettingsStore.Get.
        private static CategoryId _categories;
        private static ChestPriority _priority;
        private static int _pins;
        private static string _text;

        internal static string Get(ChestSettings settings)
        {
            if (_text != null && _categories == settings.CategoryFlags
                && _priority == settings.Priority && _pins == settings.AllowedPrefabs.Count)
            {
                return _text;
            }

            _categories = settings.CategoryFlags;
            _priority = settings.Priority;
            _pins = settings.AllowedPrefabs.Count;

            var text = new StringBuilder("\n<color=#E8C879>Chest Focus · Priority: ");
            text.Append(_priority).Append("</color>\nAllows: ");
            if (settings.IsEmpty)
            {
                text.Append("all items");
            }
            else
            {
                int count = 0;
                foreach (var category in CategoryDefs.AllLabels)
                {
                    if ((_categories & category.Id) == 0) continue;
                    if (count < 2)
                    {
                        if (count > 0) text.Append(", ");
                        text.Append(category.Label);
                    }
                    count++;
                }
                if (count > 2) text.Append(" +").Append(count - 2).Append(" more categories");
                if (_pins > 0)
                {
                    if (count > 0) text.Append(" + ");
                    text.Append(_pins).Append(_pins == 1 ? " pinned item" : " pinned items");
                }
                // Unknown saved category bits must not be presented as unrestricted.
                if (count == 0 && _pins == 0) text.Append("unrecognized categories");
            }

            if (_priority == ChestPriority.Never)
                text.Append("\nQuick-sort: excluded (Never priority)");
            else if (settings.IsEmpty)
                text.Append("\nQuick-sort: after filtered chests");

            return _text = text.ToString();
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class ChestHoverPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Container __instance, ref string __result)
        {
            if (!VariaChestFocusPlugin.IsModEnabled || __instance == null
                || string.IsNullOrEmpty(__result) || Game.instance == null
                || __instance.m_rootObjectOverride != null
                || __instance.m_nview == null || !__instance.m_nview.IsValid()) return;

            if (!__instance.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID())
                || __instance.m_checkGuardStone
                    && !PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false)) return;

            __result += ChestHoverSummary.Get(ChestSettingsStore.Get(__instance));
        }
    }
}
