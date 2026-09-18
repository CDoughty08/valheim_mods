using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VariaChestFocus
{
    // Local visual only: the owner writes settings, every client reads the same ZDO.
    internal sealed class ChestTint : MonoBehaviour
    {
        private static readonly HashSet<ChestTint> Live = new HashSet<ChestTint>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private readonly List<Slot> _slots = new List<Slot>();
        private Container _container;
        private int _applied = -1;

        private sealed class Slot : ChestTintMaterial
        {
            internal Renderer Renderer;
            internal int Index;
            internal Slot(Material original) : base(original) { }
        }

        internal static void Attach(Container container)
        {
            if (container == null || container.m_rootObjectOverride != null || container.GetInventory() == null
                || container.m_nview == null || !container.m_nview.IsValid()) return;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (container.GetComponent<ChestTint>() == null) container.gameObject.AddComponent<ChestTint>();
        }

        private void Awake()
        {
            _container = GetComponent<Container>();
            Live.Add(this);
            // Delay until all prefab components have initialized; include remote ownership updates.
            InvokeRepeating(nameof(RefreshTint), 0.1f, 0.5f);
        }

        internal static void Refresh(Container container)
        {
            Attach(container);
            container?.GetComponent<ChestTint>()?.RefreshTint();
        }

        private void RefreshTint()
        {
            if (_container == null) return;
            ChestSettings settings = ChestSettingsStore.Get(_container);
            int rgb = VariaChestFocusPlugin.IsModEnabled && settings.HasTint ? settings.TintRgb : -1;
            if (rgb == _applied) return;
            if (rgb >= 0 && _applied >= 0)
            {
                // Reuse the prepared textures/materials when switching palette/custom colors.
                foreach (Slot slot in _slots)
                {
                    if (slot.Tint == null || slot.Original == null) continue;
                    if (slot.Paint != null)
                    {
                        ChestPaintTextures.Lease next = ChestPaintTextures.Acquire(slot.Original, rgb);
                        if (next != null)
                        {
                            slot.Tint.SetTexture(MainTexId, next.Texture);
                            ChestPaintTextures.Release(slot.Paint);
                            slot.Paint = next;
                            continue;
                        }
                        // A failed recolor must not leave the previous saved color on screen.
                        ChestPaintTextures.Release(slot.Paint);
                        slot.Paint = null;
                        slot.Tint.SetTexture(MainTexId, slot.Original.GetTexture(MainTexId));
                        PrepareDye(slot);
                    }
                    ApplyColor(slot, ToColor(rgb));
                }
                _applied = rgb;
                return;
            }
            Restore();
            _applied = rgb;
            if (rgb < 0) return;

            Color tint = ToColor(rgb);
            foreach (Renderer renderer in _container.GetComponentsInChildren<Renderer>(true))
            {
                // Exclude particles, text, and nested containers; include inactive lids/wear models.
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (renderer.GetComponentInParent<Container>() != _container) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || (!original.HasProperty(ColorId) && !original.HasProperty(BaseColorId))) continue;
                    if (original.shader != null && ChestPaintProfile.PreserveMaterial(original.shader.name)) continue;
                    Slot slot = new Slot(original) { Renderer = renderer, Index = i };
                    Material copy = slot.Tint;
                    slot.Paint = ChestPaintTextures.Acquire(original, rgb);
                    if (slot.Paint != null)
                    {
                        copy.SetTexture(MainTexId, slot.Paint.Texture);
                        copy.SetColor(ColorId, new Color(1f, 1f, 1f, original.GetColor(ColorId).a));
                        // Custom/Piece separates non-metal gloss from the metal map's gloss.
                        // Keep original metal color/maps, relief and weather. Only timber becomes matte;
                        // wrapping, ceramic glaze and the black-metal frame retain their own finish.
                        if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness",
                            ChestPaintProfile.Gloss(slot.Paint.Finish, original.GetFloat("_Glossiness")));
                        if (copy.HasProperty("_ValueNoise")) copy.SetFloat("_ValueNoise",
                            ChestPaintProfile.ValueNoise(slot.Paint.Finish, original.GetFloat("_ValueNoise")));
                        _slots.Add(slot);
                        materials[i] = copy;
                        changed = true;
                        continue;
                    }
                    // Keep the game's shader, normal/metallic maps, alpha and weather properties.
                    PrepareDye(slot);
                    ApplyColor(slot, tint);
                    _slots.Add(slot);
                    materials[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        internal static Color ToColor(int rgb) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        private static void PrepareDye(Slot slot)
        {
            if (slot.AlbedoSource != null) return;
            int albedoId = slot.Original.HasProperty(BaseMapId) ? BaseMapId : MainTexId;
            if (!slot.Original.HasProperty(albedoId)) return;
            Texture source = slot.Original.GetTexture(albedoId);
            Texture2D neutral = ChestDyeTextures.Acquire(source);
            if (neutral == null) return;
            slot.Tint.SetTexture(albedoId, neutral);
            slot.AlbedoSource = source;
        }

        private static void ApplyColor(Slot slot, Color tint)
        {
            if (slot.Tint == null || slot.Original == null) return;
            ApplyColorProperty(slot, ColorId, tint);
            ApplyColorProperty(slot, BaseColorId, tint);
        }

        private static void ApplyColorProperty(Slot slot, int id, Color tint)
        {
            if (!slot.Tint.HasProperty(id)) return;
            Color original = slot.Original.GetColor(id);
            if (slot.AlbedoSource != null)
            {
                // Replace the brown material multiplier too; preserve transparency.
                tint.a = original.a;
                slot.Tint.SetColor(id, tint);
            }
            else slot.Tint.SetColor(id, original * tint);
        }

        private void Restore()
        {
            foreach (Slot slot in _slots)
            {
                if (slot.Renderer != null)
                {
                    Material[] materials = slot.Renderer.sharedMaterials;
                    // Preserve materials replaced by another mod while this tint was active.
                    if (slot.Index < materials.Length && materials[slot.Index] == slot.Tint)
                    {
                        materials[slot.Index] = slot.Original;
                        slot.Renderer.sharedMaterials = materials;
                    }
                }
                slot.Release();
            }
            _slots.Clear();
            _applied = -1;
        }

        private void OnDestroy()
        {
            CancelInvoke();
            Restore();
            Live.Remove(this);
        }

        internal static void RemoveAll()
        {
            foreach (ChestTint tint in new List<ChestTint>(Live))
            {
                if (tint == null) continue;
                tint.CancelInvoke();
                tint.Restore();
                Destroy(tint);
            }
            Live.Clear();
            ChestDebrisTint.RemoveAll();
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ChestTintPatch
    {
        private static void Postfix(Container __instance) => ChestTint.Attach(__instance);
    }
}
