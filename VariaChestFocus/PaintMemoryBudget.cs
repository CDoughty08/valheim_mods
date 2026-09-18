using System;

namespace VariaChestFocus
{
    /// <summary>Accounts for resident RGBA32 paint textures, including their mip chains.</summary>
    internal sealed class PaintMemoryBudget
    {
        internal long UsedBytes { get; private set; }

        internal static long TextureBytes(int width, int height)
        {
            long bytes = 0;
            width = Math.Max(1, width); height = Math.Max(1, height);
            while (true)
            {
                bytes += (long)width * height * 4;
                if (width == 1 && height == 1) return bytes;
                width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
            }
        }

        internal bool TryReserve(long bytes, long limit)
        {
            if (bytes <= 0 || bytes > limit - UsedBytes) return false;
            UsedBytes += bytes;
            return true;
        }

        internal void Release(long bytes) => UsedBytes = Math.Max(0, UsedBytes - bytes);
        internal void Clear() => UsedBytes = 0;
    }
}
