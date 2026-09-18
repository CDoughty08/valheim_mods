// Behavioral test doubles, not game source. No Unity runtime or game DLLs are shipped.
using VariaChestFocus;

namespace UnityEngine
{
    public class Sprite { }
    public class GameObject : Object
    {
        public string name;
        public ItemDrop Drop;
        public List<object> Components = new();
        public T GetComponent<T>() where T : class => Drop as T ?? Components.OfType<T>().FirstOrDefault();
        public T AddComponent<T>() where T : Component
        {
            var component = (T)Activator.CreateInstance(typeof(T), true);
            component.gameObject = this;
            Components.Add(component);
            return component;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class => Components.OfType<T>().ToArray();
    }
    public struct Vector3
    {
        public float x;
        public float sqrMagnitude => x * x;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new() { x = a.x - b.x };
    }
    public class Transform { public Vector3 position; }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
}
public struct Vector2i { public int x, y; public Vector2i(int x, int y) { this.x = x; this.y = y; } }
public class ItemDrop
{
    public ItemData m_itemData;
    public UnityEngine.GameObject gameObject;
    public class ItemData
    {
        public enum ItemType { Consumable, Trophy, Ammo, AmmoNonEquipable, Tool, OneHandedWeapon,
            TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Attach_Atgeir, Torch, Shield, Helmet,
            Chest, Legs, Hands, Shoulder, Utility, Trinket, Material }
        public class SharedData
        {
            public string m_name;
            public ItemType m_itemType;
            public float m_food, m_foodStamina, m_foodEitr;
            public int m_value, m_maxQuality = 1, m_maxStackSize = 50;
            public UnityEngine.Sprite[] m_icons;
            public PieceTable m_buildPieces;
        }
        public SharedData m_shared = new();
        public UnityEngine.GameObject m_dropPrefab;
        public int m_stack = 1, m_quality = 1;
        public bool m_equipped;
        public Vector2i m_gridPos;
        public ItemData Clone() => (ItemData)MemberwiseClone();
    }
}
public class ObjectDB
{
    public static ObjectDB instance;
    public List<UnityEngine.GameObject> m_items = new();
    public List<Recipe> m_recipes = new();
}
public class Recipe { public ItemDrop m_item; public Piece.Requirement[] m_resources; }
public class Piece
{
    public Requirement[] m_resources;
    public class Requirement { public ItemDrop m_resItem; }
}
public class Plant { }
public class PieceTable { public List<UnityEngine.GameObject> m_pieces = new(); }
public class Smelter
{
    public class ItemConversion { public ItemDrop m_from, m_to; }
    public List<ItemConversion> m_conversion = new();
}
public class CookingStation
{
    public class ItemConversion { public ItemDrop m_from, m_to; }
    public List<ItemConversion> m_conversion = new();
}
public class Fermenter
{
    public class ItemConversion { public ItemDrop m_from, m_to; }
    public List<ItemConversion> m_conversion = new();
}
public class Localization
{
    public static Localization instance = new();
    public string Language = "English";
    public string GetSelectedLanguage() => Language;
    public string Localize(string name) => Language + ":" + name;
}
public class ZDO
{
    private readonly Dictionary<string, string> _data = new();
    public int Writes;
    public string GetString(string key, string fallback) => _data.TryGetValue(key, out var v) ? v : fallback;
    public void Set(string key, string value) { _data[key] = value; Writes++; }
}
public class ZNetView
{
    public bool Owner = true, Valid = true;
    public ZDO Zdo = new();
    public bool IsValid() => Valid;
    public bool IsOwner() => Owner;
    public ZDO GetZDO() => Zdo;
}
public class Inventory
{
    public readonly List<ItemDrop.ItemData> Items = new();
    public Container Owner;
    public int Capacity = 100;
    public bool ThrowOnMove;
    public List<ItemDrop.ItemData> GetAllItems() => Items;
    public bool ContainsItem(ItemDrop.ItemData item) => Items.Contains(item);
    public ItemDrop.ItemData GetItemAt(int x, int y) => Items.FirstOrDefault(i => i.m_gridPos.x == x && i.m_gridPos.y == y);
    public bool CanAddItem(ItemDrop.ItemData item, int amount) => !FilterLogic.ShouldBlock(this, item)
        && Capacity - Items.Sum(i => i.m_stack) >= amount;
    public bool AddItem(ItemDrop.ItemData item)
    {
        int amount = Math.Min(item.m_stack, Math.Max(0, Capacity - Items.Sum(i => i.m_stack)));
        if (amount == 0) return false;
        var stored = item.Clone(); stored.m_stack = amount; Items.Add(stored);
        item.m_stack -= amount;
        Changed();
        return item.m_stack == 0;
    }
    public bool AddItem(ItemDrop.ItemData item, int amount, int x, int y, bool skipValidPositionCheck = false) => AddItem(item);
    public void MoveItemToThis(Inventory fromInventory, ItemDrop.ItemData item)
    {
        if (ThrowOnMove) throw new InvalidOperationException("Simulated transfer failure");
        if (FilterLogic.ShouldBlock(this, item)) return;
        if (AddItem(item)) fromInventory.Items.Remove(item);
        Changed();
    }
    public bool MoveItemToThis(Inventory fromInventory, ItemDrop.ItemData item, int amount, int x, int y) => throw new NotSupportedException();
    public int StackAll(Inventory fromInventory, bool message = false) => throw new NotSupportedException();
    public void MoveAll(Inventory fromInventory) => throw new NotSupportedException();
    private void Changed()
    {
        if (Owner != null && !QuickSort.DeferSave(Owner) && !Owner.m_loading && Owner.IsOwner()) Owner.Save();
    }
}
public class TombStone { }
public class Container
{
    public TombStone Grave;
    public T GetComponent<T>() where T : class => Grave as T;
    public object m_rootObjectOverride;
    public Inventory m_inventory;
    public ZNetView m_nview = new();
    public bool m_checkGuardStone, m_loading, InUse, Accessible = true;
    public int Saves, Loads;
    public bool ThrowOnSave;
    public int SavedTotal;
    public UnityEngine.Transform transform = new();
    public Container() { m_inventory = new() { Owner = this }; }
    public Inventory GetInventory() => m_inventory;
    public bool IsInUse() => InUse;
    public bool IsOwner() => m_nview.IsOwner();
    public bool CheckAccess(long id) => Accessible;
    public string GetHoverText() => "Chest\nOpen";
    public void Load() => Loads++;
    public void Save()
    {
        Saves++;
        if (ThrowOnSave) throw new InvalidOperationException("Simulated serialization failure");
        SavedTotal = m_inventory.Items.Sum(item => item.m_stack);
    }
}
public class InventoryGrid { public Inventory m_inventory; public bool DropItem() => true; }
public static class PrivateArea
{
    public static bool Allowed = true;
    public static bool CheckAccess(UnityEngine.Vector3 position, float radius, bool flash) => Allowed;
}
public class Player { public long Id = 1; public long GetPlayerID() => Id; public Inventory Inventory = new(); public UnityEngine.Transform transform = new(); public Inventory GetInventory() => Inventory; }
public class Game { public static Game instance = new(); public Game GetPlayerProfile() => this; public long GetPlayerID() => 1; }
namespace BepInEx.Bootstrap
{
    public class PluginInfo { public object Instance = new AzuEPI.TestPlugin(); }
    public static class Chainloader { public static Dictionary<string, PluginInfo> PluginInfos = new(); }
}
namespace VariaChestFocus
{
    internal struct ChestFocusConfigSnapshot { public bool Enabled, ProtectHotbar; public float SortRange; public int MaxChests, MaxMoves; }
    internal static class VariaChestFocusPlugin
    {
        public static bool IsModEnabled = true;
        public static ChestFocusConfigSnapshot ConfigSnapshot = new();
        public static TestLog Log = new();
    }
    internal class TestLog { public void LogWarning(string s) { } }
}
// Match the installed release: the compatibility API exists but lacks the slot lookup.
namespace AzuExtendedPlayerInventory { public class API { } }
namespace AzuEPI
{
    public class TestPlugin { }
    public class API
    {
        public static bool Throw;
        public static bool SuppressGridLookup, ThrowQuickSlots;
        public static readonly List<ItemDrop.ItemData> QuickSlotItems = new();
        public static List<ItemDrop.ItemData> GetQuickSlotsItems()
        {
            if (ThrowQuickSlots) throw new InvalidOperationException("Simulated quick-slot API failure");
            return new List<ItemDrop.ItemData>(QuickSlotItems);
        }
        public static bool TryGetSlotIndexAtGridPos(Inventory inventory, Vector2i position, out int index)
        {
            if (Throw) throw new InvalidOperationException("Simulated API failure");
            index = !SuppressGridLookup && position.y >= 4 ? position.x : -1;
            return index >= 0;
        }
    }
}
namespace AzuEPI.Game.Favoriting
{
    public class UserConfig
    {
        private static readonly Dictionary<long, UserConfig> Players = new();
        public readonly HashSet<string> Items = new();
        public readonly HashSet<Vector2i> Slots = new();
        public static bool Throw;
        public static UserConfig GetPlayerConfig(long id)
        {
            if (Throw) throw new InvalidOperationException("Simulated favorite API failure");
            if (!Players.TryGetValue(id, out var config)) Players[id] = config = new();
            return config;
        }
        public bool IsItemNameOrSlotFavorited(ItemDrop.ItemData item) => Items.Contains(item.m_shared.m_name) || Slots.Contains(item.m_gridPos);
    }
}
