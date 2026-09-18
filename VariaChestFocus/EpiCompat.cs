using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace VariaChestFocus
{
    /// <summary>
    /// Soft integration with Azumatt AzuEPI — protect slots and live favorites when sorting.
    /// </summary>
    internal static class EpiCompat
    {
        private const string EpiGuid = "Azumatt.AzuExtendedPlayerInventory";

        private static bool _resolved;
        private static bool _available;
        private delegate bool SlotLookup(Inventory inventory, Vector2i position, out int index);
        private static SlotLookup _tryGetSlotIndexAtGridPos;
        private static Func<List<ItemDrop.ItemData>> _getQuickSlotItems;
        private static MethodInfo _getPlayerFavorites;
        private static MethodInfo _isFavorite;
        private static bool _failed;

        internal static bool IsProtectedPlayerItem(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null)
            {
                return true;
            }

            if (item.m_equipped)
            {
                return true;
            }

            EnsureResolved();
            if (!_available || _failed || _tryGetSlotIndexAtGridPos == null
                || _getQuickSlotItems == null || _getPlayerFavorites == null || _isFavorite == null)
            {
                return _available;
            }

            try
            {
                // Use actual item references as well as grid classification. AzuEPI's
                // quick-slot enumeration is independent of its grid protection gate.
                List<ItemDrop.ItemData> quickItems = _getQuickSlotItems();
                if (quickItems == null || quickItems.Contains(item)) return true;

                Inventory inventory = player.GetInventory();
                if (_tryGetSlotIndexAtGridPos(inventory, item.m_gridPos, out _)) return true;

                // Ask AzuEPI's live per-character state so Alt-click changes take effect
                // immediately, including both item-name favorites and favorite grid slots.
                object favorites = _getPlayerFavorites.Invoke(null, new object[] { player.GetPlayerID() });
                return favorites == null || (bool)_isFavorite.Invoke(favorites, new object[] { item });
            }
            catch (Exception ex)
            {
                if (!_failed)
                {
                    VariaChestFocusPlugin.Log?.LogWarning($"AzuEPI protection check failed; quick-sort paused to protect slots and favorites: {ex.Message}");
                    _failed = true;
                }
                return true;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            if (!Chainloader.PluginInfos.TryGetValue(EpiGuid, out var plugin))
            {
                return;
            }
            _available = true;

            // Other mods may ship API shims with the same type names. Resolve only
            // against the loaded plugin so a shim cannot replace the real methods.
            Assembly assembly = plugin.Instance?.GetType().Assembly;
            if (assembly != null)
            {
                Type favorites = assembly.GetType("AzuEPI.Game.Favoriting.UserConfig", false);
                if (favorites != null)
                {
                    MethodInfo get = favorites.GetMethod("GetPlayerConfig", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(long) }, null);
                    MethodInfo check = favorites.GetMethod("IsItemNameOrSlotFavorited", BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(ItemDrop.ItemData) }, null);
                    if (get?.ReturnType == favorites && check?.ReturnType == typeof(bool))
                    {
                        _getPlayerFavorites = get;
                        _isFavorite = check;
                    }
                }

                // Some releases expose both APIs, with the slot method only on AzuEPI.API.
                foreach (string typeName in new[] { "AzuEPI.API", "AzuExtendedPlayerInventory.API" })
                {
                    Type api = assembly.GetType(typeName, false);
                    if (api == null)
                    {
                        continue;
                    }

                    MethodInfo quickItems = api.GetMethod("GetQuickSlotsItems", BindingFlags.Public | BindingFlags.Static,
                        null, Type.EmptyTypes, null);
                    if (_getQuickSlotItems == null && quickItems?.ReturnType == typeof(List<ItemDrop.ItemData>))
                        _getQuickSlotItems = (Func<List<ItemDrop.ItemData>>)Delegate.CreateDelegate(typeof(Func<List<ItemDrop.ItemData>>), quickItems);

                    foreach (MethodInfo method in api.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (method.Name != "TryGetSlotIndexAtGridPos")
                        {
                            continue;
                        }

                        ParameterInfo[] ps = method.GetParameters();
                        if (_tryGetSlotIndexAtGridPos == null && method.ReturnType == typeof(bool) && ps.Length == 3
                            && ps[0].ParameterType == typeof(Inventory)
                            && ps[1].ParameterType == typeof(Vector2i)
                            && ps[2].ParameterType == typeof(int).MakeByRefType())
                        {
                            _tryGetSlotIndexAtGridPos = (SlotLookup)Delegate.CreateDelegate(typeof(SlotLookup), method);
                        }
                    }
                }
            }
            if (_tryGetSlotIndexAtGridPos == null || _getQuickSlotItems == null || _getPlayerFavorites == null || _isFavorite == null)
                VariaChestFocusPlugin.Log?.LogWarning("AzuEPI protection API unavailable; quick-sort paused to protect equipment/quick slots and favorites.");
        }
    }
}
