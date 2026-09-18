using System.Globalization;

namespace VariaChestFocus
{
    internal static class ChestPalette
    {
        internal static readonly (string Name, int Rgb)[] Colors =
        {
            ("Red", 0xDC493B), ("Orange", 0xE58A32), ("Gold", 0xDBB83C),
            ("Green", 0x58A647), ("Teal", 0x24BBA5), ("Blue", 0x397BCB),
            ("Purple", 0x965DC4), ("Pink", 0xD66B9C), ("White", 0xFFFFFF),
            ("Charcoal", 0x606570)
        };

        internal static string NameOf(int rgb)
        {
            if (rgb < 0 || rgb > 0xFFFFFF) return "Original";
            foreach (var entry in Colors) if (entry.Rgb == rgb) return entry.Name;
            return "#" + rgb.ToString("X6");
        }

        internal static bool TryParseHex(string text, out int rgb)
        {
            rgb = -1;
            string hex = (text ?? string.Empty).Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            return hex.Length == 6 && int.TryParse(hex, NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out rgb);
        }
    }
}
