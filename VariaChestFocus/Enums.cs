using System;

namespace VariaChestFocus
{
    internal enum ChestPriority : byte
    {
        Never = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    [Flags]
    internal enum CategoryId : ulong
    {
        None = 0,
        Wood = 1UL << 0,
        Stone = 1UL << 1,
        Ores = 1UL << 2,
        Metals = 1UL << 3,
        Food = 1UL << 4,
        Cooking = 1UL << 5,
        Meat = 1UL << 6,
        Seeds = 1UL << 7,
        Potions = 1UL << 8,
        Ammo = 1UL << 9,
        Hides = 1UL << 10,
        Trophies = 1UL << 11,
        Valuables = 1UL << 12,
        Tools = 1UL << 13,
        Weapons = 1UL << 14,
        Armor = 1UL << 15,
        Misc = 1UL << 16
    }
}
