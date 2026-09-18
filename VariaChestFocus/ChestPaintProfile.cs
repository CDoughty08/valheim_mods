using System;

namespace VariaChestFocus
{
    internal enum ChestFinish { None, Timber, FramedTimber, Wrapping, Ceramic }

    // Exact inspected vanilla atlases only. Unrecognized/modded surfaces retain dye fallback.
    internal static class ChestPaintProfile
    {
        internal static ChestFinish Get(string atlas)
        {
            switch (atlas)
            {
                case "woodchest_d":
                case "ironchest_d":
                case "strongbox_d":
                case "barrel_d": return ChestFinish.Timber;
                case "BlackMetalChest_D": return ChestFinish.FramedTimber;
                case "Julklapp_d":
                case "Julklapp_1_d":
                case "Julklapp_2_d": return ChestFinish.Wrapping;
                case "ceramicpotsgreen_d":
                case "ceramicpotsbrokengreen_d": return ChestFinish.Ceramic;
                default: return ChestFinish.None;
            }
        }

        internal static bool PreserveMaterial(string shader) => shader == "Valheim/Snow Mesh";

        internal static float Coverage(ChestFinish finish, string atlas, float red, float green,
            float blue, float metallic, float u, float v)
        {
            if (finish == ChestFinish.Timber || finish == ChestFinish.FramedTimber)
                return ChestDyeMath.PaintCoverage(red, green, blue, metallic) * ChestDyeMath.TimberMask(atlas, u, v);
            if (finish == ChestFinish.None) return 0f;

            // Paper can be white, red or green; ceramic is already glazed green. Neither is wood.
            // The gift atlases share a metal mask isolating their ribbons from the wrapping.
            float maximum = Math.Max(red, Math.Max(green, blue));
            return ChestDyeMath.Smooth(0.025f, 0.10f, maximum)
                * (1f - ChestDyeMath.Smooth(0.15f, 0.6f, metallic));
        }

        internal static float Grain(ChestFinish finish, float red, float green, float blue)
        {
            float luminance = Math.Max(0f, Math.Min(1f, 0.2126f * red + 0.7152f * green + 0.0722f * blue));
            // Keep the paper's light/dark printed pattern; don't compress it into the wood curve's highlights.
            if (finish == ChestFinish.Wrapping) return 0.15f + 0.80f * luminance;
            if (finish == ChestFinish.Ceramic) return Math.Min(0.95f, 0.25f + 1.1f * luminance);
            return ChestDyeMath.PaintGrain(red, green, blue);
        }

        internal static float Gloss(ChestFinish finish, float original) =>
            finish == ChestFinish.Timber || finish == ChestFinish.FramedTimber ? Math.Min(original, 0.12f) : original;

        internal static float ValueNoise(ChestFinish finish, float original) =>
            finish == ChestFinish.Timber ? Math.Min(original, 0.18f) : original;
    }
}
