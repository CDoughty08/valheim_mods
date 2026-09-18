using System;
using System.Collections.Generic;
using UnityEngine;

namespace VariaChestFocus
{
    /// <summary>
    /// Classifies items using item/recipe data plus curated material families. Categories overlap.
    /// Prefab names are GameObject names (e.g. "Wood"), never localization tokens.
    /// </summary>
    internal static class CategoryDefs
    {
        private static readonly HashSet<string> WoodPrefabs = NewSet(
            "Wood", "RoundLog", "FineWood", "CoreWood", "AncientSeed", "YggdrasilWood",
            "Blackwood", "ElderBark", "Resin", "FirCone", "PineCone", "BirchSeeds");

        private static readonly HashSet<string> StonePrefabs = NewSet(
            "Stone", "Flint", "Obsidian", "Crystal", "Chitin", "BlackMarble",
            "Grausten", "Pot_Shard_Frac", "CeramicPlate");

        private static readonly HashSet<string> OrePrefabs = NewSet(
            "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap",
            "FlametalOre", "FlametalOreNew", "CopperScrap", "BronzeScrap");

        private static readonly HashSet<string> MetalPrefabs = NewSet(
            "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal",
            "FlametalNew", "BronzeNails", "IronNails", "CopperScrap");

        private static readonly HashSet<string> CookingPrefabs = NewSet(
            "Honey", "Barley", "BarleyFlour", "Cloudberry", "Blueberries", "Raspberry",
            "Mushroom", "MushroomYellow", "MushroomBlue", "MushroomMagecap", "MushroomJotunPuffs",
            "Thistle", "Dandelion", "Carrot", "Turnip", "Onion", "JuteRed", "JuteBlue",
            "Sap", "RoyalJelly", "RawMeat", "NeckTail", "Entrails", "SerpentMeat",
            "LoxMeat", "HareMeat", "AsksvinMeat", "BugMeat", "ChickenMeat", "Egg",
            "FishRaw", "Fish1", "Fish2", "Fish3", "Fish4_cave", "Fish5", "Fish6",
            "Fish7", "Fish8", "Fish9", "Fish10", "Fish11", "Fish12");

        private static readonly HashSet<string> MeatPrefabs = NewSet(
            "RawMeat", "NeckTail", "Entrails", "SerpentMeat", "LoxMeat", "HareMeat",
            "AsksvinMeat", "BugMeat", "ChickenMeat", "CookedMeat", "CookedLoxMeat",
            "CookedHareMeat", "CookedAsksvinMeat", "CookedBugMeat", "CookedChickenMeat",
            "NeckTailGrilled", "SerpentMeatCooked", "Sausages", "BloodPudding", "BoarJerky");

        private static readonly HashSet<string> SeedPrefabs = NewSet(
            "CarrotSeeds", "TurnipSeeds", "OnionSeeds", "Barley", "Flax", "Cloudberry",
            "FirCone", "PineCone", "BirchSeeds", "Acorn", "AncientSeed");

        private static readonly HashSet<string> PotionPrefabs = NewSet(
            "MeadBaseHealthMinor", "MeadBaseHealthMedium", "MeadBaseHealthMajor",
            "MeadBaseStaminaMinor", "MeadBaseStaminaMedium", "MeadBaseStaminaLingering",
            "MeadBaseEitrMinor", "MeadBaseEitrLingering", "MeadBaseTasty",
            "MeadBaseFrostResist", "MeadBasePoisonResist", "MeadBaseBugRepellent",
            "MeadHealthMinor", "MeadHealthMedium", "MeadHealthMajor",
            "MeadStaminaMinor", "MeadStaminaMedium", "MeadStaminaLingering",
            "MeadEitrMinor", "MeadEitrLingering", "MeadTasty",
            "MeadFrostResist", "MeadPoisonResist", "MeadBugRepellent",
            "Potion_hasty", "Potion_stingy", "Potion_bzerker", "Potion_light",
            "Potion_tastes_like_ash");

        private static readonly HashSet<string> HidePrefabs = NewSet(
            "LeatherScraps", "DeerHide", "WolfPelt", "LoxPelt", "ScaleHide",
            "Hare_ragdoll", "AskHide", "TrollHide", "Cloth");

        private static readonly HashSet<string> ValuablePrefabs = NewSet(
            "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "Gold",
            "TreasureChest_forest", "FishingBait", "FishingBaitAshlands",
            "FishingBaitCave", "FishingBaitDeepNorth", "FishingBaitFjords",
            "FishingBaitMistlands", "FishingBaitOcean", "FishingBaitPlains");

        private static readonly (CategoryId Id, string Label)[] Labels =
        {
            (CategoryId.Wood, "Wood"),
            (CategoryId.Stone, "Stone"),
            (CategoryId.Ores, "Ores"),
            (CategoryId.Metals, "Metals"),
            (CategoryId.Food, "Food"),
            (CategoryId.Cooking, "Cooking"),
            (CategoryId.Meat, "Meat"),
            (CategoryId.Seeds, "Seeds"),
            (CategoryId.Potions, "Potions / Meads"),
            (CategoryId.Ammo, "Ammo"),
            (CategoryId.Hides, "Hides"),
            (CategoryId.Trophies, "Trophies"),
            (CategoryId.Valuables, "Valuables"),
            (CategoryId.Tools, "Tools"),
            (CategoryId.Weapons, "Weapons"),
            (CategoryId.Armor, "Armor"),
            (CategoryId.Misc, "Misc")
        };

        internal static IReadOnlyList<(CategoryId Id, string Label)> AllLabels => Labels;

        internal static CategoryId Classify(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return CategoryId.Misc;
            }

            EnsureRecipeIngredients();
            CategoryId result = CategoryId.None;
            string prefab = ItemUtil.GetPrefabName(item);
            if (!string.IsNullOrEmpty(prefab))
            {
                if (WoodPrefabs.Contains(prefab))
                {
                    result |= CategoryId.Wood;
                }

                if (StonePrefabs.Contains(prefab))
                {
                    result |= CategoryId.Stone;
                }

                if (OrePrefabs.Contains(prefab))
                {
                    result |= CategoryId.Ores;
                }

                if (MetalPrefabs.Contains(prefab))
                {
                    result |= CategoryId.Metals;
                }

                if (SeedPrefabs.Contains(prefab))
                {
                    result |= CategoryId.Seeds;
                }

                if (PotionPrefabs.Contains(prefab))
                {
                    result |= CategoryId.Potions;
                }

                if (HidePrefabs.Contains(prefab))
                {
                    result |= CategoryId.Hides;
                }

                if (ValuablePrefabs.Contains(prefab))
                {
                    result |= CategoryId.Valuables;
                }

                if (MeatPrefabs.Contains(prefab))
                {
                    result |= CategoryId.Meat;
                }

                if (CookingPrefabs.Contains(prefab) || RecipeIngredients.Contains(prefab))
                {
                    result |= CategoryId.Cooking;
                }
                if (DerivedCategories.TryGetValue(prefab, out CategoryId derived)) result |= derived;
            }

            if (item.m_shared.m_value > 0) result |= CategoryId.Valuables;
            return result | ClassifyType(item, result == CategoryId.None);
        }

        private static CategoryId ClassifyType(ItemDrop.ItemData item, bool useMisc)
        {
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Consumable:
                    if (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f || item.m_shared.m_foodEitr > 0f)
                    {
                        return CategoryId.Food;
                    }

                    return CategoryId.Potions;

                case ItemDrop.ItemData.ItemType.Trophy:
                    return CategoryId.Trophies;

                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return CategoryId.Ammo;

                case ItemDrop.ItemData.ItemType.Tool:
                    return CategoryId.Tools;

                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Shield:
                    return CategoryId.Weapons;

                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return CategoryId.Armor;

                case ItemDrop.ItemData.ItemType.Material:
                    return useMisc ? CategoryId.Misc : CategoryId.None;

                default:
                    return useMisc ? CategoryId.Misc : CategoryId.None;
            }
        }

        private static ObjectDB _recipeDb;
        private static int _recipeCount = -1;
        private static int _itemCount = -1;
        private static List<GameObject> _items;
        private static List<Recipe> _recipes;
        private static readonly HashSet<string> RecipeIngredients = NewSet();
        private static readonly Dictionary<string, CategoryId> DerivedCategories = new Dictionary<string, CategoryId>(StringComparer.OrdinalIgnoreCase);

        internal static void Invalidate() => _recipeCount = int.MinValue;

        private static void EnsureRecipeIngredients()
        {
            ObjectDB db = ObjectDB.instance;
            int count = db != null && db.m_recipes != null ? db.m_recipes.Count : -1;
            int itemCount = db?.m_items?.Count ?? -1;
            if (ReferenceEquals(db, _recipeDb) && count == _recipeCount && itemCount == _itemCount
                && ReferenceEquals(db?.m_items, _items) && ReferenceEquals(db?.m_recipes, _recipes)) return;
            _recipeDb = db;
            _recipeCount = count;
            _itemCount = itemCount;
            _items = db?.m_items;
            _recipes = db?.m_recipes;
            RecipeIngredients.Clear();
            DerivedCategories.Clear();
            if (db == null) return;

            // Inspect prefab build tables, not loaded world objects.
            ReadBuildTables(db);

            if (db.m_recipes == null) return;
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || recipe.m_resources == null) continue;
                ItemDrop.ItemData output = recipe.m_item.m_itemData;
                if (output?.m_shared == null || output.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable
                    || (output.m_shared.m_food <= 0f && output.m_shared.m_foodStamina <= 0f && output.m_shared.m_foodEitr <= 0f)) continue;
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (requirement?.m_resItem == null) continue;
                    string name = ItemUtil.GetPrefabName(requirement.m_resItem.gameObject);
                    if (!string.IsNullOrEmpty(name)) RecipeIngredients.Add(name);
                }
            }
        }

        private static bool IsFood(ItemDrop drop)
        {
            ItemDrop.ItemData.SharedData shared = drop != null ? drop.m_itemData?.m_shared : null;
            return shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable
                && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        }

        private static void AddDerived(ItemDrop drop, CategoryId category)
        {
            if (drop == null) return;
            string name = ItemUtil.GetPrefabName(drop.gameObject);
            if (string.IsNullOrEmpty(name)) return;
            DerivedCategories.TryGetValue(name, out CategoryId existing);
            DerivedCategories[name] = existing | category;
        }

        private static void ReadBuildTables(ObjectDB db)
        {
            if (db.m_items == null) return;
            HashSet<GameObject> visited = new HashSet<GameObject>();
            foreach (GameObject itemPrefab in db.m_items)
            {
                if (itemPrefab == null) continue;
                PieceTable table = itemPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
                if (table == null || table.m_pieces == null) continue;
                foreach (GameObject piecePrefab in table.m_pieces)
                {
                    if (piecePrefab == null || !visited.Add(piecePrefab)) continue;
                    Piece piece = piecePrefab.GetComponent<Piece>();
                    if (piece != null && piecePrefab.GetComponent<Plant>() != null && piece.m_resources != null)
                        foreach (Piece.Requirement requirement in piece.m_resources)
                            AddDerived(requirement?.m_resItem, CategoryId.Seeds);
                    foreach (CookingStation station in piecePrefab.GetComponentsInChildren<CookingStation>(true))
                        foreach (CookingStation.ItemConversion conversion in station.m_conversion)
                            if (conversion != null && IsFood(conversion.m_to)) AddDerived(conversion.m_from, CategoryId.Cooking);
                    foreach (Fermenter fermenter in piecePrefab.GetComponentsInChildren<Fermenter>(true))
                        foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                        {
                            if (conversion == null) continue;
                            AddDerived(conversion.m_from, CategoryId.Potions);
                            AddDerived(conversion.m_to, CategoryId.Potions);
                        }
                    foreach (Smelter smelter in piecePrefab.GetComponentsInChildren<Smelter>(true))
                    {
                        // Kilns, windmills and ovens also use Smelter. Recognize a metal
                        // station through a known ore -> metal conversion on that station.
                        bool metals = smelter.m_conversion.Exists(conversion => conversion != null
                            && conversion.m_from != null && conversion.m_to != null
                            && OrePrefabs.Contains(ItemUtil.GetPrefabName(conversion.m_from.gameObject))
                            && MetalPrefabs.Contains(ItemUtil.GetPrefabName(conversion.m_to.gameObject)));
                        foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                        {
                            if (conversion == null) continue;
                            if (IsFood(conversion.m_to)) AddDerived(conversion.m_from, CategoryId.Cooking);
                            else if (metals)
                            {
                                AddDerived(conversion.m_from, CategoryId.Ores);
                                AddDerived(conversion.m_to, CategoryId.Metals);
                            }
                        }
                    }
                }
            }
        }

        internal static bool Matches(ItemDrop.ItemData item, CategoryId flags)
        {
            if (flags == CategoryId.None || item == null)
            {
                return false;
            }

            return (Classify(item) & flags) != 0;
        }

        private static HashSet<string> NewSet(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }
    }
}
