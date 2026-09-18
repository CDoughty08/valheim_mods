using System;
using System.Collections.Generic;
using UnityEngine;

namespace VariaChestFocus
{
    // Selective color for inspected vanilla storage surfaces. Unknown materials retain dye fallback.
    internal static class ChestPaintTextures
    {
        internal sealed class Lease
        {
            internal Material Original;
            internal int Rgb;
            internal Texture2D Texture;
            internal int Users;
            internal ChestFinish Finish;
            internal long Bytes;
        }

        private sealed class Source
        {
            internal int Width, Height, Users;
            // Readback is RGBA32: retaining float colors would use four times the memory
            // for the same eight-bit channel data throughout the lifetime of the paint.
            internal Color32[] Pixels;
            internal float[] Coverage, Grain;
        }

        private static readonly Dictionary<(Material, int), Lease> Painted = new Dictionary<(Material, int), Lease>();
        private static readonly Dictionary<Material, Source> Sources = new Dictionary<Material, Source>();
        private static readonly HashSet<Material> Failed = new HashSet<Material>();
        private static readonly PaintMemoryBudget Budget = new PaintMemoryBudget();

        internal static Lease Acquire(Material original, int rgb)
        {
            if (original == null || original.shader == null || original.shader.name != "Custom/Piece"
                || !original.HasProperty("_MainTex") || Failed.Contains(original)) return null;
            Texture texture = original.GetTexture("_MainTex");
            if (!(texture is Texture2D)) return null;
            ChestFinish finish = ChestPaintProfile.Get(texture.name);
            if (finish == ChestFinish.None) return null;
            if (Painted.TryGetValue((original, rgb), out Lease cached)) { cached.Users++; return cached; }

            float scale = Mathf.Min(1f, 1024f / Mathf.Max(texture.width, texture.height));
            long bytes = PaintMemoryBudget.TextureBytes(Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(texture.height * scale)));
            // Existing leases stay valid; at capacity use the shared, color-independent dye path.
            if (!Budget.TryReserve(bytes, VariaChestFocusPlugin.ConfigSnapshot.MaxPaintTextureBytes)) return null;

            Texture2D painted = null;
            try
            {
                if (!Sources.TryGetValue(original, out Source source))
                {
                    source = ReadSource(original, texture, finish);
                    Sources.Add(original, source);
                }
                Color color = ChestTint.ToColor(rgb);
                Color multiplier = original.GetColor("_Color");
                Color32[] pixels = new Color32[source.Pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color p = source.Pixels[i];
                    float coverage = source.Coverage[i], grain = source.Grain[i];
                    pixels[i] = new Color(
                        ChestDyeMath.PaintChannel(p.r * multiplier.r, color.r, grain, coverage),
                        ChestDyeMath.PaintChannel(p.g * multiplier.g, color.g, grain, coverage),
                        ChestDyeMath.PaintChannel(p.b * multiplier.b, color.b, grain, coverage), p.a);
                }
                painted = new Texture2D(source.Width, source.Height, TextureFormat.RGBA32, true, false)
                {
                    name = "ChestFocus_Paint_" + texture.name + "_" + rgb.ToString("X6"),
                    wrapMode = texture.wrapMode, filterMode = texture.filterMode, anisoLevel = texture.anisoLevel
                };
                painted.SetPixels32(pixels);
                painted.Apply(true, true);
                Lease lease = new Lease { Original = original, Rgb = rgb, Texture = painted, Users = 1, Finish = finish, Bytes = bytes };
                Painted.Add((original, rgb), lease);
                source.Users++;
                return lease;
            }
            catch (Exception error)
            {
                Budget.Release(bytes);
                if (painted != null) UnityEngine.Object.Destroy(painted);
                Failed.Add(original);
                if (Sources.TryGetValue(original, out Source unused) && unused.Users == 0) Sources.Remove(original);
                VariaChestFocusPlugin.Log?.LogWarning("Chest paint: using dye fallback for " + original.name + ": " + error.Message);
                return null;
            }
        }

        private static Source ReadSource(Material material, Texture texture, ChestFinish finish)
        {
            float scale = Mathf.Min(1f, 1024f / Mathf.Max(texture.width, texture.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
            Color32[] pixels = ReadPixels(texture, width, height, true);
            Texture metal = material.HasProperty("_MetallicTex") ? material.GetTexture("_MetallicTex") : null;
            // These inspected atlases use matching UVs; don't silently apply an offset modded mask.
            if (metal != null && (material.GetTextureScale("_MainTex") != material.GetTextureScale("_MetallicTex")
                || material.GetTextureOffset("_MainTex") != material.GetTextureOffset("_MetallicTex")))
                throw new InvalidOperationException("Metal and color UVs do not match");
            Color32[] metals = metal is Texture2D ? ReadPixels(metal, width, height, false) : null;
            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            Source source = new Source { Width = width, Height = height, Pixels = pixels,
                Coverage = new float[pixels.Length], Grain = new float[pixels.Length] };
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                source.Coverage[i] = ChestPaintProfile.Coverage(finish, texture.name, p.r, p.g, p.b,
                    metals == null ? metallic : metals[i].r / 255f * metallic,
                    (i % width + 0.5f) / width, (i / width + 0.5f) / height);
                source.Grain[i] = ChestPaintProfile.Grain(finish, p.r, p.g, p.b);
            }
            return source;
        }

        private static Color32[] ReadPixels(Texture source, int width, int height, bool srgb)
        {
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            RenderTexture temporary = null;
            Texture2D readable = null;
            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                    srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                GL.sRGBWrite = srgb && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                if (readable != null) UnityEngine.Object.Destroy(readable);
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        internal static Lease Retain(Lease lease)
        {
            if (lease != null) lease.Users++;
            return lease;
        }

        internal static void Release(Lease lease)
        {
            if (lease == null || --lease.Users > 0) return;
            UnityEngine.Object.Destroy(lease.Texture);
            Painted.Remove((lease.Original, lease.Rgb));
            Budget.Release(lease.Bytes);
            if (Sources.TryGetValue(lease.Original, out Source source) && --source.Users == 0) Sources.Remove(lease.Original);
        }

        internal static void Clear()
        {
            foreach (Lease lease in Painted.Values) UnityEngine.Object.Destroy(lease.Texture);
            Painted.Clear(); Sources.Clear(); Failed.Clear();
            Budget.Clear();
        }
    }
}
