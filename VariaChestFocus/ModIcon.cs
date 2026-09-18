using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace VariaChestFocus
{
    internal static class ModIcon
    {
        private const string ResourceName = "VariaChestFocus.Assets.chestfocus_icon.png";
        private static Sprite _sprite;

        internal static void Destroy()
        {
            if (_sprite == null) return;
            UnityEngine.Object.Destroy(_sprite.texture);
            UnityEngine.Object.Destroy(_sprite);
            _sprite = null;
        }

        internal static Sprite GetSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            Assembly asm = typeof(ModIcon).Assembly;
            using (Stream stream = asm.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    VariaChestFocusPlugin.Log?.LogWarning("Missing embedded icon: " + ResourceName);
                    return null;
                }

                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!TryLoadImage(tex, bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    VariaChestFocusPlugin.Log?.LogWarning("Failed to decode chest focus icon PNG");
                    return null;
                }

                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = "VariaChestFocus_Icon";
                _sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                return _sprite;
            }
        }

        private static bool TryLoadImage(Texture2D texture, byte[] data)
        {
            Type conversion = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                throwOnError: false);
            if (conversion == null)
            {
                return false;
            }

            MethodInfo loadImage = conversion.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                modifiers: null);
            if (loadImage == null)
            {
                loadImage = conversion.GetMethod(
                    "LoadImage",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Texture2D), typeof(byte[]) },
                    modifiers: null);
            }

            if (loadImage == null)
            {
                return false;
            }

            object result = loadImage.GetParameters().Length == 3
                ? loadImage.Invoke(null, new object[] { texture, data, false })
                : loadImage.Invoke(null, new object[] { texture, data });
            return result is bool ok && ok;
        }
    }
}
