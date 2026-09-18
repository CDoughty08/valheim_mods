// Authored test doubles: no game code or assemblies are included in this runner.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float a) => Math.Abs(a);
        public static float Floor(float a) => (float)Math.Floor(a);
        public static int CeilToInt(float a) => (int)Math.Ceiling(a);
        public static int FloorToInt(float a) => (int)Math.Floor(a);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Clamp01(float a) => Math.Max(0f, Math.Min(1f, a));
    }
}

public class ItemDrop : UnityEngine.Component
{
    public ItemData m_itemData;
    public class ItemData
    {
        public class SharedData
        {
            public string m_name;
            public float m_food, m_foodStamina, m_foodEitr, m_foodBurnTime, m_foodRegen;
            public StatusEffect m_consumeStatusEffect;
        }
        public SharedData m_shared = new SharedData();
        public UnityEngine.GameObject m_dropPrefab;
        public bool m_cheated;
        private readonly UnityEngine.Sprite _icon = new UnityEngine.Sprite();
        public UnityEngine.Sprite GetIcon() => _icon;
        public static string OriginalTooltip;
        public static string GetDurationString(float time) => time.ToString("0.##");
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetTooltip(ItemData item, int qualityLevel, bool crafting,
            float worldLevel, int stackOverride = -1, bool appending = false) => OriginalTooltip;
    }
}

public sealed class StatusEffect
{
    public float m_ttl, Remaining;
    public int Hash = 1;
    public int NameHash() => Hash;
    public float GetRemaningTime() => Remaining;
}

public sealed class SEMan
{
    public readonly Dictionary<int, StatusEffect> Effects = new Dictionary<int, StatusEffect>();
    public float HealthModifier = 1f;
    public void ModifyHealthRegen(ref float amount) => amount *= HealthModifier;
    public StatusEffect GetStatusEffect(int hash) => Effects.TryGetValue(hash, out var effect) ? effect : null;
}

public sealed class Player
{
    public sealed class Food
    {
        public string m_name;
        public ItemDrop.ItemData m_item;
        public float m_time, m_health, m_stamina, m_eitr;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool CanEatAgain() => m_time < m_item.m_shared.m_foodBurnTime / 2f;
    }

    public readonly List<Food> m_foods = new List<Food>();
    public List<Food> GetFoods() => m_foods;
    public readonly SEMan m_seman = new SEMan();
    public float m_foodUpdateTimer, m_foodRegenTimer;
    public float Health = 1f, MaxHealth = 25f, Stamina, MaxStamina = 50f, Eitr, MaxEitr;
    public int VanillaUpdates, Heals;
    public bool ThrowOnEat;
    public Action DuringEat;
    public float GetHealth() => Health;
    public float GetMaxHealth() => MaxHealth;
    public float GetStamina() => Stamina;
    public float GetMaxStamina() => MaxStamina;
    public float GetEitr() => Eitr;
    public float GetMaxEitr() => MaxEitr;
    public void SetMaxHealth(float hp, bool flashBar) => MaxHealth = hp;
    public void SetMaxStamina(float stamina, bool flashBar) => MaxStamina = stamina;
    public void SetMaxEitr(float eitr, bool flashBar) => MaxEitr = eitr;
    public void Heal(float amount, bool showText) { Health = Math.Min(MaxHealth, Health + amount); Heals++; }
    public void AddStamina(float amount) => Stamina = Math.Min(MaxStamina, Stamina + amount);
    public void AddEitr(float amount) => Eitr = Math.Min(MaxEitr, Eitr + amount);
    public void Message(MessageHud.MessageType type, string message) { }
    public void ShowTutorial(string name) { }
    public void GetTotalFoodValue(out float hp, out float stamina, out float eitr)
    {
        hp = 25f; stamina = 50f; eitr = 0f;
        foreach (var food in m_foods) { hp += food.m_health; stamina += food.m_stamina; eitr += food.m_eitr; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UpdateFood(float dt, bool forceUpdate) => VanillaUpdates++;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool CanEat(ItemDrop.ItemData item, bool showMessages) => true;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool EatFood(ItemDrop.ItemData item)
    {
        DuringEat?.Invoke();
        if (ThrowOnEat) throw new InvalidOperationException("injected failure");
        // The minimal vanilla-side contract needed to exercise the duration hook.
        m_foods.Add(new Food { m_item = item, m_name = item.m_dropPrefab.name, m_time = item.m_shared.m_foodBurnTime });
        UpdateFood(0f, true);
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ConsumeItem(Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false) => true;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Load(ZPackage pkg) { }
}

public sealed class Inventory { }
public sealed class ZPackage { }
public static class MessageHud { public enum MessageType { Center } }
public sealed class Localization
{
    public static readonly Localization instance = new Localization();
    public string Localize(string key, string argument) => key + argument;
}
public enum PlayerStatType { FoodEaten }
public sealed class Game
{
    public static float m_foodRate = 1f;
    public static Game instance = new Game();
    public int FoodEaten;
    public void IncrementPlayerStat(PlayerStatType type) => FoodEaten++;
    public Game GetPlayerProfile() => this;
    public void IncrementStatFoodEaten(string item, float count, bool cheated) { }
}
namespace VariaFood
{
    internal static class VariaFoodPlugin
    {
        internal static FoodConfigSnapshot ConfigSnapshot;
        internal const float RegenTickSeconds = 10f;
        internal const float StatBoostBaseFractionPerTick = 0.25f;
        internal static readonly TestLogger Log = new TestLogger();
    }
    internal sealed class TestLogger
    {
        internal void LogError(string message) => throw new Exception(message);
        internal void LogWarning(string message) => Console.Error.WriteLine(message);
        internal void LogInfo(string message) { }
    }
}
