using System.Collections.Generic;
using UnityEngine;

namespace VariaChestFocus
{
    // Each chest/debris renderer owns its material copy and a share of its generated texture.
    internal class ChestTintMaterial
    {
        private static readonly Dictionary<Material, ChestTintMaterial> Owned = new Dictionary<Material, ChestTintMaterial>();
        internal Material Original;
        internal Material Tint;
        internal Texture AlbedoSource;
        internal ChestPaintTextures.Lease Paint;
        private bool _released;

        internal ChestTintMaterial(Material original)
        {
            Original = original;
            Tint = new Material(original);
            Owned.Add(Tint, this);
        }

        internal static ChestTintMaterial CloneForDebris(Material material)
        {
            if (material == null || !Owned.TryGetValue(material, out ChestTintMaterial source)) return null;
            var copy = new ChestTintMaterial(material) { Original = source.Original };
            // Snapshot the current finish: later recoloring/resetting the chest cannot affect debris.
            copy.Paint = ChestPaintTextures.Retain(source.Paint);
            copy.AlbedoSource = ChestDyeTextures.Retain(source.AlbedoSource);
            return copy;
        }

        internal void Release()
        {
            if (_released) return;
            _released = true;
            Owned.Remove(Tint);
            if (Tint != null) UnityEngine.Object.Destroy(Tint);
            ChestDyeTextures.Release(AlbedoSource);
            ChestPaintTextures.Release(Paint);
            // The last chest may disappear before its debris. Never clear textures still leased by it.
            if (Owned.Count == 0) { ChestDyeTextures.Clear(); ChestPaintTextures.Clear(); }
        }
    }
}
