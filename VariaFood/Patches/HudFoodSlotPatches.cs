using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VariaFood.Patches
{
    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    internal static class HudAwakeFoodSlotsPatch
    {
        private static bool _drinkHintsPending;
        private static Hud _hud;
        private static Image[] _originalBars, _originalIcons;
        private static TMP_Text[] _originalTimes;
        private static readonly List<GameObject> Created = new List<GameObject>();
        private static readonly List<GameObject> Hints = new List<GameObject>();
        private static int _foodSlots = -1, _drinkSlots = -1;

        internal static int OriginalSlots => _originalIcons?.Length ?? 3;

        internal static void Shutdown()
        {
            if (_hud != null && _originalBars != null)
            {
                _hud.m_foodBars = _originalBars;
                _hud.m_foodIcons = _originalIcons;
                _hud.m_foodTime = _originalTimes;
            }
            DestroyCreated(Created);
            DestroyCreated(Hints);
            Created.Clear(); Hints.Clear();
            _hud = null;
            _originalBars = _originalIcons = null;
            _originalTimes = null;
            _foodSlots = _drinkSlots = -1;
            _drinkHintsPending = false;
        }

        private static void Postfix(Hud __instance) => EnsureLayout(__instance);

        internal static void EnsureLayout(Hud hud)
        {
            if (_hud != hud)
            {
                Shutdown();
                _hud = hud;
                _originalBars = hud.m_foodBars;
                _originalIcons = hud.m_foodIcons;
                _originalTimes = hud.m_foodTime;
            }
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            int foodSlots = cfg.Enabled && cfg.ExtraSlots ? cfg.FoodSlots : 0;
            int drinkSlots = cfg.Enabled && cfg.ExtraSlots ? cfg.DrinkSlots : 0;
            if (_foodSlots == foodSlots && _drinkSlots == drinkSlots) return;
            _foodSlots = foodSlots; _drinkSlots = drinkSlots;
            foreach (GameObject hint in Hints) if (hint != null) hint.SetActive(false);
            Configure(hud);
        }
        private static readonly string[] PreferredDrinkIcons =
        {
            "MeadTasty", "MeadHealthMedium", "MeadHealthMinor", "MeadStaminaMedium", "MeadStaminaMinor"
        };

        private static void Configure(Hud __instance)
        {
            try
            {
                _drinkHintsPending = false;
                FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
                if (!cfg.Enabled || !cfg.ExtraSlots)
                {
                    return;
                }

                Image[] bars = __instance.m_foodBars;
                Image[] icons = __instance.m_foodIcons;
                TMP_Text[] times = __instance.m_foodTime;
                if (bars == null || icons == null || times == null)
                {
                    return;
                }

                int target = FoodSlots.TotalSlots(cfg);
                if (bars.Length < 1)
                {
                    return;
                }

                if (icons.Length != bars.Length || times.Length != bars.Length)
                {
                    VariaFoodPlugin.Log?.LogWarning(
                        $"VariaFood: food HUD arrays mismatched (bars={bars.Length}, icons={icons.Length}, times={times.Length}); skipping expand.");
                    return;
                }

                if (bars.Length < target)
                {
                    List<GameObject> created = new List<GameObject>();
                    Image[] newBars = Expand(bars, target, created);
                    Image[] newIcons;
                    TMP_Text[] newTimes;

                    // Icons/time may each live in their own per-slot container (parents differ).
                    bool iconsGrouped = icons.Length >= 2
                        && icons[0] != null
                        && icons[1] != null
                        && icons[0].transform.parent != icons[1].transform.parent;

                    if (iconsGrouped)
                    {
                        if (!ExpandGroupedIcons(icons, times, target, created, out newIcons, out newTimes))
                        {
                            DestroyCreated(created);
                            VariaFoodPlugin.Log?.LogWarning("VariaFood: failed grouped food HUD expand; keeping vanilla count.");
                            return;
                        }
                    }
                    else
                    {
                        newIcons = Expand(icons, target, created);
                        newTimes = Expand(times, target, created);
                    }

                    if (newBars == null || newIcons == null || newTimes == null)
                    {
                        DestroyCreated(created);
                        VariaFoodPlugin.Log?.LogWarning("VariaFood: failed to expand food HUD slots; keeping vanilla count.");
                        return;
                    }

                    Created.AddRange(created);
                    __instance.m_foodBars = newBars;
                    __instance.m_foodIcons = newIcons;
                    __instance.m_foodTime = newTimes;
                    icons = newIcons;
                    VariaFoodPlugin.Log?.LogInfo($"VariaFood: expanded HUD food slots {bars.Length} -> {target}");
                }

                if (!TryDecorateDrinkSlots(icons, cfg))
                {
                    _drinkHintsPending = true;
                }
            }
            catch (System.Exception ex)
            {
                VariaFoodPlugin.Log?.LogError($"VariaFood: food HUD expand failed: {ex}");
            }
        }

        /// <summary>
        /// ObjectDB may not be ready at Hud.Awake; finish drink-slot hints on first UpdateFood.
        /// </summary>
        internal static void TryFinishPendingDrinkHints(Hud hud)
        {
            if (!_drinkHintsPending)
            {
                return;
            }

            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || !cfg.ExtraSlots || hud.m_foodIcons == null)
            {
                _drinkHintsPending = false;
                return;
            }

            if (TryDecorateDrinkSlots(hud.m_foodIcons, cfg))
            {
                _drinkHintsPending = false;
            }
        }

        /// <summary>
        /// Muted flask/mead icon behind dedicated drink slots (sibling of food icon so it
        /// stays visible when vanilla hides an empty food icon).
        /// </summary>
        private static bool TryDecorateDrinkSlots(Image[] icons, in FoodConfigSnapshot cfg)
        {
            if (cfg.DrinkSlots <= 0 || icons == null)
            {
                return true;
            }

            Sprite sprite = ResolveDrinkHintSprite();
            if (sprite == null)
            {
                // Retry only while the database is loading. A modpack without a
                // suitable sprite must not trigger a complete database scan per frame.
                return ObjectDB.instance != null && ObjectDB.instance.m_items != null
                    && ObjectDB.instance.m_items.Count > 0;
            }

            int total = FoodSlots.TotalSlots(cfg);
            int firstDrink = cfg.FoodSlots;
            for (int i = firstDrink; i < total && i < icons.Length; i++)
            {
                Image foodIcon = icons[i];
                if (foodIcon == null)
                {
                    continue;
                }

                EnsureDrinkHint(foodIcon, i, sprite);
            }

            return true;
        }

        private static Sprite ResolveDrinkHintSprite()
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }

            for (int i = 0; i < PreferredDrinkIcons.Length; i++)
            {
                Sprite icon = GetItemIcon(PreferredDrinkIcons[i]);
                if (icon != null)
                {
                    return icon;
                }
            }

            List<GameObject> items = ObjectDB.instance.m_items;
            if (items == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                GameObject go = items[i];
                if (go == null)
                {
                    continue;
                }

                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop == null || !DrinkClassifier.IsDrink(drop.m_itemData, go.name)) continue;
                Sprite icon = drop?.m_itemData?.GetIcon();
                if (icon != null)
                {
                    return icon;
                }
            }

            return null;
        }

        private static Sprite GetItemIcon(string prefabName)
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab == null)
            {
                return null;
            }

            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            return drop?.m_itemData?.GetIcon();
        }

        private static void EnsureDrinkHint(Image foodIcon, int slotIndex, Sprite sprite)
        {
            Transform parent = foodIcon.transform.parent;
            if (parent == null)
            {
                parent = foodIcon.transform;
            }

            string hintName = "VariaDrinkHint_" + slotIndex;
            Transform existing = parent.Find(hintName);
            Image hint;
            if (existing != null)
            {
                hint = existing.GetComponent<Image>();
                if (hint == null)
                {
                    return;
                }
            }
            else
            {
                GameObject go = new GameObject(hintName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Hints.Add(go);
                go.layer = foodIcon.gameObject.layer;
                go.transform.SetParent(parent, worldPositionStays: false);
                hint = go.GetComponent<Image>();
            }

            hint.sprite = sprite;
            hint.preserveAspect = true;
            hint.raycastTarget = false;
            // Soft watermark behind the active drink icon.
            hint.color = new Color(1f, 1f, 1f, 0.32f);

            RectTransform hintRt = hint.rectTransform;
            RectTransform iconRt = foodIcon.rectTransform;
            hintRt.anchorMin = iconRt.anchorMin;
            hintRt.anchorMax = iconRt.anchorMax;
            hintRt.pivot = iconRt.pivot;
            hintRt.anchoredPosition = iconRt.anchoredPosition;
            hintRt.sizeDelta = iconRt.sizeDelta;
            hintRt.localScale = iconRt.localScale;
            hintRt.localRotation = iconRt.localRotation;

            // Behind the food icon (and its timer text if they share the parent).
            hintRt.SetSiblingIndex(foodIcon.transform.GetSiblingIndex());
            foodIcon.transform.SetSiblingIndex(hintRt.GetSiblingIndex() + 1);

            if (!hint.gameObject.activeSelf)
            {
                hint.gameObject.SetActive(true);
            }
        }

        private static bool ExpandGroupedIcons(
            Image[] icons,
            TMP_Text[] times,
            int target,
            List<GameObject> created,
            out Image[] newIcons,
            out TMP_Text[] newTimes)
        {
            newIcons = null;
            newTimes = null;

            Transform c0 = icons[0].transform.parent;
            Transform c1 = icons[1].transform.parent;
            if (c0 == null || c1 == null)
            {
                return false;
            }

            string iconName = icons[0].name;
            string timeName = times[0].name;
            bool timeInContainer = times[0].transform.IsChildOf(c0);

            Vector2 p0 = c0 is RectTransform r0 ? r0.anchoredPosition : Vector2.zero;
            Vector2 delta = c1 is RectTransform r1 ? r1.anchoredPosition - p0 : Vector2.zero;
            if (delta == Vector2.zero)
            {
                return false;
            }

            newIcons = Grow(icons, target);
            newTimes = Grow(times, target);

            for (int i = icons.Length; i < target; i++)
            {
                GameObject clone = Object.Instantiate(c0.gameObject, c0.parent);
                clone.name = "Varia_FoodSlot" + i;
                foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith("VariaDrinkHint_")) { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
                created.Add(clone);

                if (clone.transform is RectTransform crt)
                {
                    crt.anchoredPosition = p0 + delta * i;
                }

                Image ni = FindComp<Image>(clone.transform, iconName);
                if (ni == null)
                {
                    return false;
                }

                newIcons[i] = ni;

                if (timeInContainer)
                {
                    TMP_Text nt = FindComp<TMP_Text>(clone.transform, timeName);
                    if (nt == null)
                    {
                        return false;
                    }

                    newTimes[i] = nt;
                }
            }

            if (!timeInContainer)
            {
                TMP_Text[] expandedTimes = Expand(times, target, created);
                if (expandedTimes == null)
                {
                    return false;
                }

                newTimes = expandedTimes;
            }

            return true;
        }

        private static T[] Expand<T>(T[] source, int target, List<GameObject> created) where T : Component
        {
            if (source == null || source.Length < 1 || source[0] == null)
            {
                return null;
            }

            T[] result = Grow(source, target);
            int last = source.Length - 1;
            T template = source[last];
            if (template == null)
            {
                return null;
            }

            Transform parent = template.transform.parent;
            bool hasLayout = parent != null && parent.GetComponent<LayoutGroup>() != null;
            Vector2 delta = Vector2.zero;
            if (!hasLayout && source.Length >= 2
                && source[0].transform is RectTransform r0
                && source[1].transform is RectTransform r1)
            {
                delta = r1.anchoredPosition - r0.anchoredPosition;
            }

            for (int i = source.Length; i < target; i++)
            {
                GameObject clone = Object.Instantiate(template.gameObject, parent);
                clone.name = template.gameObject.name + "_varia" + i;
                created.Add(clone);
                clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + (i - last));

                if (clone.transform is RectTransform crt && template.transform is RectTransform trt)
                {
                    crt.localScale = trt.localScale;
                    crt.sizeDelta = trt.sizeDelta;
                    if (!hasLayout && delta != Vector2.zero)
                    {
                        crt.anchoredPosition = trt.anchoredPosition + delta * (i - last);
                    }
                }

                T comp = clone.GetComponent<T>();
                if (comp == null)
                {
                    return null;
                }

                result[i] = comp;
            }

            return result;
        }

        private static T[] Grow<T>(T[] source, int target)
        {
            T[] result = new T[target];
            for (int i = 0; i < source.Length && i < target; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static T FindComp<T>(Transform root, string name) where T : Component
        {
            if (root.name == name)
            {
                T c = root.GetComponent<T>();
                if (c != null)
                {
                    return c;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                T found = FindComp<T>(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void DestroyCreated(List<GameObject> created)
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    created[i].SetActive(false);
                    Object.Destroy(created[i]);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Hud), "UpdateFood")]
    internal static class HudUpdateFoodDrinkHintPatch
    {
        private struct SlotVisual
        {
            internal Sprite Sprite;
            internal Color IconColor;
            internal string Time;
            internal Color TimeColor;
        }

        private static SlotVisual[] _visuals = new SlotVisual[0];
        private static int[] _sources = new int[0];
        private static readonly List<Player.Food> DisplayFoods = new List<Player.Food>(8);

        // Grow before vanilla fills the arrays so new slots have current visuals immediately.
        private static void Prefix(Hud __instance) => HudAwakeFoodSlotsPatch.EnsureLayout(__instance);

        private static void Postfix(Hud __instance, Player player)
        {
            HudAwakeFoodSlotsPatch.TryFinishPendingDrinkHints(__instance);
            FoodConfigSnapshot cfg = VariaFoodPlugin.ConfigSnapshot;
            if (!cfg.Enabled || !cfg.ExtraSlots)
            {
                for (int i = HudAwakeFoodSlotsPatch.OriginalSlots; i < __instance.m_foodIcons.Length; i++)
                {
                    __instance.m_foodIcons[i].gameObject.SetActive(false);
                    __instance.m_foodTime[i].gameObject.SetActive(false);
                    __instance.m_foodBars[i].gameObject.SetActive(false);
                }
                return;
            }

            Image[] icons = __instance.m_foodIcons;
            TMP_Text[] times = __instance.m_foodTime;
            Image[] bars = __instance.m_foodBars;
            if (icons == null || times == null || bars == null
                || icons.Length != times.Length || icons.Length != bars.Length) return;

            if (_visuals.Length != icons.Length)
            {
                _visuals = new SlotVisual[icons.Length];
                _sources = new int[icons.Length];
            }

            // Preserve vanilla/other mods' icon styling and timer formatting.
            // Snapshot before moving any visual, since destinations may overlap sources.
            for (int i = 0; i < icons.Length; i++)
            {
                _visuals[i] = new SlotVisual
                {
                    Sprite = icons[i].sprite,
                    IconColor = icons[i].color,
                    Time = times[i].text,
                    TimeColor = times[i].color
                };
            }

            List<Player.Food> foods = player.GetFoods();
            DisplayFoods.Clear();
            DisplayFoods.AddRange(foods);
            DrinkDisplay.AppendTo(player, DisplayFoods, cfg);
            FoodHudLayout.MapSlots(DisplayFoods, cfg, _sources);
            for (int i = 0; i < icons.Length; i++)
            {
                int source = _sources[i];
                bool occupied = source >= 0 && source < DisplayFoods.Count;
                icons[i].gameObject.SetActive(occupied);
                times[i].gameObject.SetActive(occupied);
                bars[i].gameObject.SetActive(occupied);
                if (!occupied) continue;

                Player.Food food = DisplayFoods[source];
                SlotVisual visual = source < foods.Count && source < _visuals.Length ? _visuals[source] : new SlotVisual
                {
                    Sprite = food.m_item.GetIcon(), IconColor = Color.white,
                    Time = FormatTime(FoodMath.IsDisplayOnlyDrink(food.m_item.m_shared)
                        ? food.m_time : food.m_time / Game.m_foodRate), TimeColor = Color.white
                };
                icons[i].sprite = visual.Sprite;
                icons[i].color = visual.IconColor;
                if (FoodMath.IsDisplayOnlyDrink(food.m_item.m_shared))
                {
                    // Vanilla divides every timer by the world's food drain rate;
                    // a potion's status-effect timer instead uses real seconds.
                    float remaining = Mathf.Max(0f, food.m_time);
                    times[i].text = remaining >= 60f
                        ? Mathf.CeilToInt(remaining / 60f) + "m"
                        : Mathf.FloorToInt(remaining) + "s";
                    times[i].color = Color.white;
                }
                else
                {
                    times[i].text = visual.Time;
                    times[i].color = visual.TimeColor;
                }
            }
            DisplayFoods.Clear();
        }

        private static string FormatTime(float seconds) => seconds >= 60f
            ? Mathf.CeilToInt(seconds / 60f) + "m" : Mathf.FloorToInt(seconds) + "s";
    }
}
