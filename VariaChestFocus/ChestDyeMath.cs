using System;

namespace VariaChestFocus
{
    internal static class ChestDyeMath
    {
        // Paint only warm timber. Neutral fittings, metallic pixels and deep seams stay exposed.
        internal static float PaintCoverage(float red, float green, float blue, float metallic)
        {
            float maximum = Math.Max(red, Math.Max(green, blue));
            if (maximum <= 0 || red < green || green < blue) return 0;
            float warmth = (red - blue) / maximum;
            return Smooth(0.18f, 0.38f, warmth) * Smooth(0.045f, 0.16f, maximum)
                * (1f - Smooth(0.15f, 0.6f, metallic));
        }

        internal static float PaintGrain(float red, float green, float blue)
        {
            // Keep dark grain and worn edges distinct without losing the selected hue.
            float luminance = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
            float value = 0.31f + 1.4f * Math.Max(0f, Math.Min(1f, luminance));
            // Roll off bright pixels smoothly instead of flattening them against a clamp.
            return value <= 0.88f ? value : 0.88f + 0.08f * (1f - (float)Math.Exp(-(value - 0.88f) / 0.2f));
        }

        // Authored outlines in the vanilla atlas: both halves of the front ring share these UVs.
        // Extend the left edge beyond the atlas so the mirrored seam doesn't become a stripe.
        private static readonly float[,] FrontRingOutside = {
            { -0.02f, 0.693f }, { 0.011f, 0.693f }, { 0.045f, 0.686f },
            { 0.069f, 0.661f }, { 0.069f, 0.624f }, { 0.049f, 0.599f },
            { 0.014f, 0.589f }, { -0.02f, 0.589f }
        };
        private static readonly float[,] FrontRingInside = {
            { -0.02f, 0.677f }, { 0.010f, 0.677f }, { 0.040f, 0.671f },
            { 0.053f, 0.656f }, { 0.055f, 0.630f }, { 0.043f, 0.616f },
            { 0.012f, 0.608f }, { -0.02f, 0.608f }
        };

        internal static float TimberMask(string atlas, float u, float v)
        {
            if (atlas != "woodchest_d") return 1f;
            // Side handles are packed above the timber; the front ring is in the left strip.
            float x = (u - 0.615f) / 0.15f, y = (v - 0.90f) / 0.16f;
            float handles = Smooth(1f, 1.18f, x * x + y * y);
            if (u > 0.072f || v < 0.586f || v > 0.696f) return handles;
            float ring = InsideOutline(FrontRingOutside, u, v) * (1f - InsideOutline(FrontRingInside, u, v));
            return handles * (1f - ring);
        }

        private static float InsideOutline(float[,] outline, float u, float v)
        {
            // Clockwise convex outlines; soften by less than half a texel on the 128px atlas.
            float distance = float.MaxValue;
            for (int i = 0; i < outline.GetLength(0); ++i)
            {
                int next = (i + 1) % outline.GetLength(0);
                float dx = outline[next, 0] - outline[i, 0], dy = outline[next, 1] - outline[i, 1];
                float inward = (dy * (u - outline[i, 0]) - dx * (v - outline[i, 1]))
                    / (float)Math.Sqrt(dx * dx + dy * dy);
                distance = Math.Min(distance, inward);
            }
            return Smooth(-0.002f, 0.002f, distance);
        }

        internal static float PaintChannel(float original, float chosen, float grain, float coverage)
        {
            return original + (chosen * grain - original) * coverage;
        }

        internal static float Smooth(float low, float high, float value)
        {
            float t = Math.Max(0f, Math.Min(1f, (value - low) / (high - low)));
            return t * t * (3f - 2f * t);
        }

        // Work in linear light, then return sRGB for the generated color texture.
        // The curve lifts dark grain without adding a constant glow or clipping highlights.
        internal static float NeutralAlbedo(float red, float green, float blue)
        {
            double luminance = 0.2126 * ToLinear(red) + 0.7152 * ToLinear(green) + 0.0722 * ToLinear(blue);
            double lifted = Math.Pow(luminance, 0.78);
            return (float)(lifted <= 0.0031308 ? lifted * 12.92 : 1.055 * Math.Pow(lifted, 1.0 / 2.4) - 0.055);
        }

        private static double ToLinear(float channel)
        {
            double value = Math.Max(0, Math.Min(1, channel));
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}
