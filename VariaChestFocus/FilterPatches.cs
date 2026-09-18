using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VariaChestFocus
{
    internal static class FilterLogic
    {
        internal static bool ShouldBlock(Inventory inventory, ItemDrop.ItemData item)
        {
            if (!VariaChestFocusPlugin.IsModEnabled)
            {
                return false;
            }

            if (inventory == null || item == null)
            {
                return false;
            }

            if (!ContainerRegistry.TryGetContainer(inventory, out Container container) || container == null)
            {
                return false;
            }

            ChestSettings settings = ChestSettingsStore.Get(container);
            if (settings.IsEmpty)
            {
                return false;
            }

            return !settings.Allows(item);
        }
    }

    // Restrict only the AddItem calls inside bulk transfers. A global AddItem rejection
    // could destroy producer output or discard saved inventory items during Load.
    [HarmonyPatch]
    internal static class BulkTransferFilterPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.StackAll));
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.MoveAll));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo add = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
                new[] { typeof(ItemDrop.ItemData) });
            MethodInfo addAt = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
                new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(add) || instruction.Calls(addAt))
                {
                    string replacement = instruction.Calls(add) ? nameof(AddAllowed) : nameof(AddAllowedAt);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(BulkTransferFilterPatches), replacement);
                }
                yield return instruction;
            }
        }

        private static bool AddAllowed(Inventory inventory, ItemDrop.ItemData item)
        {
            return !FilterLogic.ShouldBlock(inventory, item) && inventory.AddItem(item);
        }

        private static bool AddAllowedAt(Inventory inventory, ItemDrop.ItemData item, int amount,
            int x, int y, bool skipValidPositionCheck)
        {
            return !FilterLogic.ShouldBlock(inventory, item)
                && inventory.AddItem(item, amount, x, y, skipValidPositionCheck);
        }
    }

    [HarmonyPatch(typeof(Container))]
    internal static class ContainerLifecyclePatches
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch("OnContainerChanged")]
        private static bool OnContainerChangedPrefix(Container __instance)
        {
            return !QuickSort.DeferSave(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix(Container __instance)
        {
            ContainerRegistry.Register(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroyed")]
        private static void OnDestroyedPostfix(Container __instance)
        {
            ContainerRegistry.Unregister(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory))]
    internal static class InventoryFilterPatches
    {
        // Prefer CanAddItem + MoveItemToThis over AddItem prefixes: some producers destroy
        // items when AddItem returns false. UI and quick-sort use MoveItemToThis.

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        private static bool CanAddItemPrefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!FilterLogic.ShouldBlock(__instance, item))
            {
                return true;
            }

            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
        private static bool MoveItemPrefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            return __instance == fromInventory || !FilterLogic.ShouldBlock(__instance, item);
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
        private static bool MoveItemAmountPrefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (__instance == fromInventory || !FilterLogic.ShouldBlock(__instance, item))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    /// <summary>
    /// UI grid drops go through DropItem; safer than globally rejecting Inventory.AddItem.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid))]
    internal static class InventoryGridFilterPatches
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(nameof(InventoryGrid.DropItem))]
        private static bool DropItemPrefix(InventoryGrid __instance, Inventory fromInventory,
            ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
        {
            Inventory inventory = __instance != null ? __instance.m_inventory : null;
            if (inventory == fromInventory || item == null || inventory == null)
            {
                return true;
            }

            bool blocked = FilterLogic.ShouldBlock(inventory, item);
            ItemDrop.ItemData displaced = inventory.GetItemAt(pos.x, pos.y);
            // DropItem removes the dragged item before attempting either leg of a swap.
            // Validate the reverse deposit as well, before vanilla can remove anything.
            bool swaps = displaced != null && displaced != item && item.m_stack == amount
                && (displaced.m_shared.m_name != item.m_shared.m_name
                    || item.m_shared.m_maxQuality > 1 && displaced.m_quality != item.m_quality
                    || displaced.m_shared.m_maxStackSize == 1);
            if (!blocked && (!swaps || !FilterLogic.ShouldBlock(fromInventory, displaced)))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
