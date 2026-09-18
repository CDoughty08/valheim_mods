// Minimal API doubles. Game relationship policy is intentionally supplied by each test;
// these tests verify the mod consults it, not a duplicate of vanilla faction rules.
namespace UnityEngine
{
    public struct Color(float r, float g, float b, float a)
    {
        public float r = r, g = g, b = b, a = a;
    }
    public class Transform { public Vector3 position; }
    public class GameObject
    {
        public string name;
        public readonly Dictionary<Type, object> Components = new();
        public T GetComponent<T>() where T : class => Components.GetValueOrDefault(typeof(T)) as T;
    }
    public struct Vector2(float x, float y) { public float x = x, y = y; }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public float sqrMagnitude => x*x + y*y + z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
    }
    public static class Mathf
    {
        public static float Sqrt(float x) => MathF.Sqrt(x);
        public static float Max(float a, float b) => MathF.Max(a,b);
    }
    public static class Time { public static float deltaTime; }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
}
public class Character
{
    private static int NextId;
    private readonly int Id = ++NextId;
    public int GetInstanceID() => Id;
    public UnityEngine.Transform transform = new();
    public enum Faction { AnimalsVeg, Players, TrainingDummy, Boss, Dverger }
    public UnityEngine.GameObject gameObject = new();
    public string name => gameObject.name;
    public bool Dead, Tamed, m_boss;
    public int Level = 1;
    public Faction faction;
    public BaseAI AI;
    public bool IsDead() => Dead;
    public bool IsTamed() => Tamed;
    public bool IsPlayer() => this is Player;
    public int GetLevel() => Level;
    public Faction GetFaction() => faction;
    public BaseAI GetBaseAI() => AI;
    public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
}
public class BaseAI
{
    public static Func<Character, Character, bool> Enemy = (_,_) => false;
    public bool Alerted;
    public static bool IsEnemy(Character a, Character b) => Enemy(a,b);
}
public class AnimalAI : BaseAI { }
public class Minimap
{
    public Func<UnityEngine.Vector3, bool> Explored = _ => true;
    public bool IsExplored(UnityEngine.Vector3 position) => Explored(position);
}
public class MonsterAI : BaseAI { }
public class Player : Character
{
    public static Player m_localPlayer;
    public readonly HashSet<string> m_trophies = new();
    public readonly HashSet<string> Materials = new();
    public Skills Skills = new();
    public float Raised;
    public bool Moving = true;
    public bool IsMaterialKnown(string key) => Materials.Contains(key);
    public void AddKnownItem() { }
    public bool InCutscene() => false;
    public bool IsTeleporting() => false;
    public bool IsSitting() => false;
    public bool IsAttached() => false;
    public bool IsAttachedToShip() => false;
    public bool CanMove() => true;
    public UnityEngine.Vector3 GetMoveDir() => new(Moving ? 1 : 0, 0, 0);
    public UnityEngine.Vector3 GetVelocity() => GetMoveDir();
    public Skills GetSkills() => Skills;
    public void RaiseSkill(Skills.SkillType type, float amount) => Raised += amount;
}
public class Skills
{
    public enum SkillType { Tracking = 12345 }
    public const float c_MaxSkillLevel = 100;
    public class Skill { public float m_level; }
    public Skill Value = new();
    public float EffectiveLevel;
    public Skill GetSkill(SkillType type) => Value;
    public float GetSkillLevel(SkillType type) => EffectiveLevel;
}
public class CharacterDrop
{
    public class Drop { public UnityEngine.GameObject m_prefab; }
    public List<Drop> m_drops = new();
}
public class ItemDrop
{
    public class ItemData
    {
        public enum ItemType { Material, Trophy }
        public class SharedData { public ItemType m_itemType; public string m_name; }
        public SharedData m_shared = new();
    }
    public ItemData m_itemData = new();
}
public static class Utils { public static string GetPrefabName(UnityEngine.GameObject go) => go.name; }
namespace VariaTracking
{
    internal struct TrackingConfigSnapshot
    {
        public bool ShowBoss, ShowHostile, ShowPassive;
        public bool ShowStarred, ShowTooltips, TrophyEarlyNames, NameTrophylessCreatures;
        public UnityEngine.Color BossColor, HostileColor, PassiveColor;
        public float ExpRate, MinMoveDirSqr, MinMoveSpeed, XpIntervalSeconds, StarredExpBonus;
    }
    internal struct TrackingUnlockState { public bool ShowHostility, ShowBossColor, ShowStars, ShowAllNames, PierceFog; }
    internal static class TrackingSkill { public const Skills.SkillType SkillType = Skills.SkillType.Tracking; }
    internal static class TrackingRadar
    {
        public static int LastTrackableCount, LastStarredInRange;
        public static string BuildDisplayName(Character character, bool showStars) => character.name;
    }
}
