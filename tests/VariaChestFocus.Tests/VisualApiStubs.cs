// Authored lifetime doubles: test material ownership, not Unity shaders or GPU texture creation.
using System.Reflection;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public int DestroyCalls;
        public static void Destroy(Object value)
        {
            if (value == null || value.Destroyed) return;
            value.Destroyed = true;
            value.DestroyCalls++;
            if (value is GameObject go)
                foreach (var component in go.Components.OfType<Object>().ToArray()) Destroy(component);
            value.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(value, null);
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class Texture : Object { }
    public class Material : Object
    {
        public int ColorMarker;
        public Texture Texture;
        public Material() { }
        public Material(Material source) { ColorMarker = source.ColorMarker; Texture = source.Texture; }
    }
    public class Renderer : Component { public Material[] sharedMaterials { get; set; } = Array.Empty<Material>(); }
}
public static class Destructible { public static void CreateFragments(UnityEngine.GameObject rootObject, bool visibleOnly = true) { } }
namespace VariaChestFocus
{
    internal static class ChestPaintTextures
    {
        internal class Lease { internal int Users = 1; internal UnityEngine.Texture Texture = new(); }
        internal static int Clears;
        internal static Lease Retain(Lease lease) { if (lease != null) lease.Users++; return lease; }
        internal static void Release(Lease lease) { if (lease != null && --lease.Users == 0) UnityEngine.Object.Destroy(lease.Texture); }
        internal static void Clear() { Clears++; }
    }
    internal static class ChestDyeTextures
    {
        internal static readonly Dictionary<UnityEngine.Texture, int> Users = new();
        internal static int Clears;
        internal static UnityEngine.Texture Retain(UnityEngine.Texture source)
        {
            if (source == null) return null;
            Users[source]++;
            return source;
        }
        internal static void Release(UnityEngine.Texture source)
        {
            if (source == null) return;
            if (--Users[source] == 0) { Users.Remove(source); UnityEngine.Object.Destroy(source); }
        }
        internal static void Clear() { Clears++; }
    }
}
