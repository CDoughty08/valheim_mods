using UnityEngine;

namespace VariaTracking
{
    /// <summary>Procedural circle sprites for dots / star rings / range rings / FOV wedges.</summary>
    internal static class CircleSprites
    {
        /// <summary>Inner hole of range-ring sprites (must match Create args).</summary>
        public const float RangeRingInnerLarge = 0.97f;
        public const float RangeRingInnerSmall = 0.90f;

        private const int WedgeResolution = 256;

        public static Sprite Dot { get; private set; }
        public static Sprite StarRing { get; private set; }
        public static Sprite RangeRingLarge { get; private set; }
        public static Sprite RangeRingSmall { get; private set; }

        private static Sprite _wedge;
        private static int _wedgeDegrees = int.MinValue;

        public static void Initialize()
        {
            Dot = Create(32, innerHole01: 0f, softPixels: 1.25f);
            StarRing = Create(48, innerHole01: 0.55f, softPixels: 1.1f);
            // Large map: thin clean stroke. Minimap: thicker + softer so it doesn't pixelate.
            RangeRingLarge = Create(256, innerHole01: RangeRingInnerLarge, softPixels: 0.35f);
            RangeRingSmall = Create(128, innerHole01: RangeRingInnerSmall, softPixels: 1.1f);
        }

        /// <summary>
        /// High-res pie wedge centered on image up (+Y). Prefer this over Image.Radial360 —
        /// Unity's filled mesh is low-poly and looks faceted when scaled large.
        /// </summary>
        public static Sprite GetWedge(float coneDegrees)
        {
            int deg = Mathf.Clamp(Mathf.RoundToInt(coneDegrees), 1, 359);
            if (_wedge != null && _wedgeDegrees == deg)
            {
                return _wedge;
            }

            DestroyWedge();
            _wedgeDegrees = deg;
            _wedge = CreateWedge(WedgeResolution, deg, softPixels: 1.75f);
            return _wedge;
        }

        public static void Shutdown()
        {
            DestroyWedge();
        }

        private static void DestroyWedge()
        {
            if (_wedge == null)
            {
                return;
            }

            Texture2D tex = _wedge.texture as Texture2D;
            Object.Destroy(_wedge);
            if (tex != null)
            {
                Object.Destroy(tex);
            }

            _wedge = null;
            _wedgeDegrees = int.MinValue;
        }

        private static Sprite Create(int resolution, float innerHole01, float softPixels)
        {
            resolution = Mathf.Max(8, resolution);
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "VariaTracking_Circle"
            };

            float center = (resolution - 1) * 0.5f;
            float outerR = center;
            float innerR = Mathf.Clamp01(innerHole01) * outerR;
            float soft = Mathf.Max(0.01f, softPixels);
            var pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerAlpha = Mathf.Clamp01((outerR - d) / soft + 0.5f);
                    float innerAlpha = innerR <= 0f
                        ? 1f
                        : Mathf.Clamp01((d - innerR) / soft + 0.5f);
                    float a = outerAlpha * innerAlpha;
                    pixels[row + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
        }

        /// <summary>Wedge centered on +Y (top), spanning coneDegrees total.</summary>
        private static Sprite CreateWedge(int resolution, int coneDegrees, float softPixels)
        {
            resolution = Mathf.Max(32, resolution);
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "VariaTracking_Wedge"
            };

            float center = (resolution - 1) * 0.5f;
            float outerR = center;
            float soft = Mathf.Max(0.01f, softPixels);
            float halfCone = coneDegrees * 0.5f;
            float edgeSoftDeg = Mathf.Max(1.2f, 180f / resolution * 4f);
            var pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerAlpha = Mathf.Clamp01((outerR - d) / soft + 0.5f);
                    if (outerAlpha <= 0f)
                    {
                        pixels[row + x] = Color.clear;
                        continue;
                    }

                    // 0 at top (+Y), positive toward +X (clockwise on the texture).
                    float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    float absAng = Mathf.Abs(ang);
                    float wedgeAlpha = Mathf.Clamp01((halfCone + edgeSoftDeg - absAng) / edgeSoftDeg);
                    float a = outerAlpha * wedgeAlpha;
                    pixels[row + x] = a > 0f ? new Color(1f, 1f, 1f, a) : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
        }
    }
}
