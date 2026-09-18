// Minimal authored hierarchy/UI doubles for slot layout and teardown checks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object obj)
        {
            if (obj is GameObject go) { go.SetActive(false); go.transform.SetParent(null, false); }
        }
        public static GameObject Instantiate(GameObject source, Transform parent)
        {
            var copy = new GameObject(source.name);
            copy.transform.SetParent(parent, false);
            var a=(RectTransform)source.transform; var b=(RectTransform)copy.transform;
            b.anchoredPosition=a.anchoredPosition; b.sizeDelta=a.sizeDelta;
            b.anchorMin=a.anchorMin; b.anchorMax=a.anchorMax; b.pivot=a.pivot;
            foreach (Component component in source.Components)
            {
                if (component is Transform) continue;
                var clone=copy.Add(component.GetType());
                if (component is Image image && clone is Image next) { next.sprite=image.sprite; next.color=image.color; }
                if (component is TMP_Text text && clone is TMP_Text nextText) { nextText.text=text.text; nextText.color=text.color; }
            }
            foreach (Transform child in source.transform.Children) Instantiate(child.gameObject,copy.transform);
            return copy;
        }
    }
    public class GameObject : Object
    {
        public string name;
        public int layer;
        public bool activeSelf=true;
        public Transform transform;
        internal readonly List<Component> Components=new List<Component>();
        public GameObject(string name="",params Type[] types)
        {
            this.name=name; transform=(Transform)Add(typeof(RectTransform));
            foreach (Type type in types) if (type!=typeof(RectTransform)) Add(type);
        }
        internal Component Add(Type type) { var component=(Component)Activator.CreateInstance(type); component.gameObject=this; Components.Add(component); return component; }
        public T GetComponent<T>() where T:class => Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class => Components.OfType<T>().Concat(transform.Children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
        public void SetActive(bool value) => activeSelf=value;
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T:class => gameObject.GetComponent<T>();
    }
    public class Transform : Component
    {
        public Transform parent;
        internal readonly List<Transform> Children=new List<Transform>();
        public int childCount => Children.Count;
        public Vector3 localScale;
        public object localRotation;
        public Transform GetChild(int i) => Children[i];
        public void SetParent(Transform next,bool worldPositionStays) { parent?.Children.Remove(this); parent=next; parent?.Children.Add(this); }
        public Transform Find(string name) => Children.FirstOrDefault(c=>c.name==name);
        public bool IsChildOf(Transform other) => this==other || parent!=null && parent.IsChildOf(other);
        public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
        public void SetSiblingIndex(int index) { if(parent==null)return; parent.Children.Remove(this); parent.Children.Insert(Math.Min(index,parent.Children.Count),this); }
    }
    public class RectTransform : Transform { public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta; }
    public class CanvasRenderer : Component { }
    public class Sprite : Object { public string Name; }
    public struct Vector3 { }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x;this.y=y; }
        public static Vector2 zero => new Vector2();
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a,int b)=>new Vector2(a.x*b,a.y*b);
        public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;
        public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b);
        public override bool Equals(object obj)=>obj is Vector2 v && this==v;
        public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode();
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color white => new Color(1,1,1,1);
    }
}
namespace UnityEngine.UI
{
    public class Image : Component
    {
        public Sprite sprite;
        public Color color;
        public bool preserveAspect,raycastTarget;
        public RectTransform rectTransform => (RectTransform)transform;
    }
    public class LayoutGroup : Component { }
}
namespace TMPro
{
    public class TMP_Text : Component { public string text;public Color color; }
}
public class ObjectDB
{
    public static ObjectDB instance;
    public List<GameObject> m_items=new List<GameObject>();
    public GameObject GetItemPrefab(string name)=>m_items.FirstOrDefault(i=>i.name==name);
}
public class Hud
{
    public Image[] m_foodIcons,m_foodBars;
    public TMP_Text[] m_foodTime;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Awake() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UpdateFood(Player player)
    {
        for(int i=0;i<m_foodIcons.Length;i++)
        {
            bool active=i<player.m_foods.Count;
            m_foodIcons[i].gameObject.SetActive(active);m_foodBars[i].gameObject.SetActive(active);m_foodTime[i].gameObject.SetActive(active);
            if(!active)continue;
            m_foodIcons[i].sprite=player.m_foods[i].m_item.GetIcon();m_foodIcons[i].color=Color.white;
            m_foodTime[i].text="vanilla:"+player.m_foods[i].m_name;m_foodTime[i].color=Color.white;
        }
    }
}
