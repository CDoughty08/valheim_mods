using System;
using System.Collections.Generic;
using UnityEngine;

namespace VariaChestFocus
{
    // Cache by source texture, not by chest or chosen color. Never modify game assets.
    internal static class ChestDyeTextures
    {
        private sealed class Entry
        {
            internal Texture2D Texture;
            internal int Users;
        }

        private static readonly Dictionary<Texture, Entry> Cache = new Dictionary<Texture, Entry>();
        private static readonly HashSet<Texture> Failed = new HashSet<Texture>();

        internal static Texture2D Acquire(Texture source)
        {
            if (!(source is Texture2D) || Failed.Contains(source)) return null;
            if (Cache.TryGetValue(source, out Entry cached))
            {
                cached.Users++;
                return cached.Texture;
            }

            Texture2D texture = null;
            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                // Bound the added GPU memory for large modded textures, preserving aspect ratio.
                float scale = Mathf.Min(1f, 1024f / Mathf.Max(source.width, source.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    name = "ChestFocus_Dye_" + source.name,
                    wrapMode = source.wrapMode,
                    filterMode = source.filterMode,
                    anisoLevel = source.anisoLevel
                };
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    float gray = ChestDyeMath.NeutralAlbedo(pixel.r, pixel.g, pixel.b);
                    pixels[i] = new Color(gray, gray, gray, pixel.a);
                }
                texture.SetPixels(pixels);
                texture.Apply(true, true); // Generate mipmaps, then release the readable CPU copy.
                Cache.Add(source, new Entry { Texture = texture, Users = 1 });
                return texture;
            }
            catch (Exception error)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Failed.Add(source);
                VariaChestFocusPlugin.Log?.LogWarning("Chest color: cannot prepare texture " + source.name
                    + "; using material tint. " + error.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        internal static Texture Retain(Texture source)
        {
            if (ReferenceEquals(source, null) || !Cache.TryGetValue(source, out Entry entry)) return null;
            entry.Users++;
            return source;
        }

        internal static void Release(Texture source)
        {
            if (ReferenceEquals(source, null) || !Cache.TryGetValue(source, out Entry entry)) return;
            if (--entry.Users > 0) return;
            UnityEngine.Object.Destroy(entry.Texture);
            Cache.Remove(source);
        }

        internal static void Clear()
        {
            foreach (Entry entry in Cache.Values) UnityEngine.Object.Destroy(entry.Texture);
            Cache.Clear();
            Failed.Clear();
        }
    }
}
