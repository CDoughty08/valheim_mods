using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VariaChestFocus.Gui
{
    internal static class ChestSettingsUi
    {
        private const string ButtonName = "VariaChestFocus_Settings";
        private const string PanelName = "VariaChestFocus_Panel";
        private const string LegacyButtonName = "VariaChestFocus_Button";
        private const int PageSize = 24;
        private const int LayoutVersion = 9;
        private static readonly ChestPriority[] Priorities = (ChestPriority[])System.Enum.GetValues(typeof(ChestPriority));

        private static InventoryGui _gui;
        private static Button _focusButton;
        private static GameObject _panel;
        private static Button _priorityButton;
        private static TextMeshProUGUI _pageLabel;
        private static TMP_InputField _search;
        private static TextMeshProUGUI _colorLabel;
        private static ChestColorPicker _colorPicker;
        private static Button _customColorButton;
        private static readonly List<TextMeshProUGUI> ColorMarkers = new List<TextMeshProUGUI>();
        private static RectTransform _categoryRoot;
        private static RectTransform _itemRoot;
        private static TextMeshProUGUI _summary;
        private static TextMeshProUGUI _emptyLabel;
        private static Button _previousPage;
        private static Button _nextPage;
        private static GameObject _clearDialog;
        private static CanvasGroup _panelInput;
        private static Button _cancelClearButton;
        private static ChestSettings _clearTarget;
        private static CatalogView _view;
        private static readonly List<Button> ViewButtons = new List<Button>();
        private static readonly List<ItemCell> ItemCells = new List<ItemCell>(PageSize);

        private sealed class ItemCell
        {
            internal CatalogEntry Entry;
            internal Button Button;
            internal Image Icon;
            internal TextMeshProUGUI Name;
            internal TextMeshProUGUI Marker;
            internal TextMeshProUGUI Placeholder;
            internal UITooltip Tooltip;
        }
        private static int _builtLayoutVersion;

        private static Container _boundContainer;
        private static ChestSettings _working;
        private static string _query = string.Empty;
        private static int _page;
        private static readonly List<CatalogEntry> SearchResults = new List<CatalogEntry>(64);
        private static readonly List<Button> CategoryButtons = new List<Button>(20);
        private static readonly List<CategoryId> CategoryIds = new List<CategoryId>(20);
        private static bool _suppressEvents;
        private static bool _built;
        private static bool _searchFocused;

        /// <summary>True while Chest Focus search should eat gameplay keys (Use/E, etc.).</summary>
        internal static bool IsCapturingText =>
            _panel != null && _panel.activeInHierarchy && VariaChestFocusPlugin.IsModEnabled
                && (_searchFocused || (_search != null && _search.isFocused)
                    || (_colorPicker != null && _colorPicker.IsTyping));

        internal static void EnsureBuilt(InventoryGui gui)
        {
            if (gui == null)
            {
                return;
            }

            _gui = gui;
            UiFactory.CaptureTemplates(gui);
            if (!UiFactory.IsReady)
            {
                return;
            }

            if (_built && _builtLayoutVersion == LayoutVersion && _focusButton != null && _panel != null)
            {
                return;
            }

            // Rebuild cleanly if a prior attempt left orphans (including legacy Focus button).
            DestroyNamed(gui.m_container, ButtonName);
            DestroyNamed(gui.m_container, LegacyButtonName);
            Transform stackParent = gui.m_stackAllButton != null ? gui.m_stackAllButton.transform.parent : null;
            DestroyNamed(stackParent, ButtonName);
            DestroyNamed(stackParent, LegacyButtonName);
            DestroyNamed(gui.transform, PanelName);
            DestroyNamed(gui.m_crafting, PanelName);
            _focusButton = null;
            _panel = null;
            _built = false;

            BuildSettingsButton(gui);
            BuildPanel(gui);
            _built = _focusButton != null && _panel != null;
            _builtLayoutVersion = _built ? LayoutVersion : 0;
            SetButtonVisible(false);
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        internal static void OnContainerChanged(InventoryGui gui)
        {
            _gui = gui;
            FitPanel(gui);
            if (!_built)
            {
                EnsureBuilt(gui);
            }

            Container container = gui != null ? gui.m_currentContainer : null;
            bool show = container != null && container.m_rootObjectOverride == null && VariaChestFocusPlugin.IsModEnabled;
            SetButtonVisible(show);

            if (!show)
            {
                HidePanel(persist: true);
                return;
            }

            if (_panel != null && _panel.activeSelf && _boundContainer != container)
            {
                Bind(container);
                RefreshAll();
            }
        }

        private static void FitPanel(InventoryGui gui)
        {
            if (_panel == null || gui == null || !(gui.transform is RectTransform viewport)) return;
            RectTransform panel = (RectTransform)_panel.transform;
            if (viewport.rect.width <= 32f || viewport.rect.height <= 32f) return;
            float scale = Mathf.Min(1f, Mathf.Min((viewport.rect.width - 32f) / panel.sizeDelta.x,
                (viewport.rect.height - 32f) / panel.sizeDelta.y));
            panel.localScale = Vector3.one * scale;
        }

        internal static void HidePanel(bool persist = true)
        {
            _colorPicker?.Cancel();
            ReleaseSearchFocus();
            CancelClear();

            if (_panel != null && _panel.activeSelf)
            {
                if (persist)
                {
                    Persist();
                }

                _panel.SetActive(false);
            }

            _boundContainer = null;
            _working = null;
        }

        internal static void Destroy()
        {
            HidePanel(persist: false);
            if (_focusButton != null) { _focusButton.gameObject.SetActive(false); Object.Destroy(_focusButton.gameObject); }
            if (_panel != null) { _panel.SetActive(false); Object.Destroy(_panel); }
            _gui = null; _focusButton = null; _panel = null; _priorityButton = null;
            _pageLabel = null; _search = null; _colorLabel = null; _colorPicker = null;
            _customColorButton = null; _categoryRoot = null; _itemRoot = null; _summary = null;
            _emptyLabel = null; _previousPage = null; _nextPage = null; _clearDialog = null;
            _panelInput = null; _cancelClearButton = null;
            ColorMarkers.Clear(); ViewButtons.Clear(); ItemCells.Clear(); SearchResults.Clear();
            CategoryButtons.Clear(); CategoryIds.Clear();
            _built = false; _builtLayoutVersion = 0;
            UiFactory.ClearTemplates();
            ModIcon.Destroy();
        }

        private static void ReleaseSearchFocus()
        {
            _searchFocused = false;
            if (_search != null)
            {
                _search.DeactivateInputField();
                if (UnityEngine.EventSystems.EventSystem.current != null
                    && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == _search.gameObject)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        private static void SetButtonVisible(bool visible)
        {
            if (_focusButton != null)
            {
                _focusButton.gameObject.SetActive(visible);
            }
        }

        private static void BuildSettingsButton(InventoryGui gui)
        {
            Button template = gui.m_stackAllButton ?? gui.m_takeAllButton;
            if (template == null)
            {
                return;
            }

            Transform parent = template.transform.parent;
            // Compact icon button — full "Settings" label overlaps the CHEST header.
            float height = 30f;
            if (template.transform is RectTransform srcSize)
            {
                height = Mathf.Max(28f, srcSize.sizeDelta.y);
            }

            Vector2 iconSize = new Vector2(height, height);
            _focusButton = UiFactory.CloneButton(ButtonName, parent, string.Empty, iconSize, TogglePanel);
            if (_focusButton == null)
            {
                return;
            }

            RectTransform src = template.transform as RectTransform;
            RectTransform rt = _focusButton.transform as RectTransform;
            if (src == null || rt == null)
            {
                return;
            }

            TMP_Text label = _focusButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            Sprite icon = ModIcon.GetSprite();
            if (icon != null)
            {
                RectTransform iconRt = UiFactory.CreateRect("Icon", rt);
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(height - 8f, height - 8f);
                Image img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
            }
            else if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text = "CF";
            }

            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            // Right-center pivot: place our right edge just left of Place stacks' left edge.
            rt.pivot = new Vector2(1f, src.pivot.y);
            rt.sizeDelta = iconSize;
            float stackLeft = src.anchoredPosition.x - (src.pivot.x * src.sizeDelta.x);
            const float gap = 16f;
            rt.anchoredPosition = new Vector2(stackLeft - gap, src.anchoredPosition.y);
            rt.SetAsLastSibling();
        }

        private static void TogglePanel()
        {
            if (_panel == null || _gui == null)
            {
                return;
            }

            if (_panel.activeSelf)
            {
                HidePanel();
                return;
            }

            Container container = _gui.m_currentContainer;
            if (container == null || container.m_rootObjectOverride != null)
            {
                return;
            }

            ItemCatalog.Invalidate();
            ItemCatalog.EnsureBuilt();
            Bind(container);
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            RefreshAll();
        }

        private static void Bind(Container container)
        {
            _colorPicker?.Cancel();
            Persist();
            CancelClear();
            _boundContainer = container;
            _working = ChestSettingsStore.Get(container).Clone();
            _page = 0;
            _view = CatalogView.Items;
            _query = string.Empty;
            _suppressEvents = true;
            if (_search != null)
            {
                _search.text = string.Empty;
            }

            _suppressEvents = false;
        }

        private static void Persist()
        {
            if (_boundContainer == null || _working == null)
            {
                return;
            }

            if (!ChestSettingsStore.TrySet(_boundContainer, _working))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Chest Focus: cannot save (not owner)");
            }
            else ChestTint.Refresh(_boundContainer);
        }

        private static void BuildPanel(InventoryGui gui)
        {
            RectTransform root = UiFactory.CreatePanelRoot(PanelName, gui.transform, new Vector2(720f, 620f));
            _panel = root.gameObject;
            _panelInput = _panel.AddComponent<CanvasGroup>();
            ItemCells.Clear();
            ViewButtons.Clear();
            TextMeshProUGUI title = UiFactory.CreateLabel("Title", root, "Chest Focus", 23f, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(500f, 30f), new Vector2(0.5f, 1f));

            _priorityButton = UiFactory.CloneButton("Priority", root, "Priority: Medium", new Vector2(182f, 28f), CyclePriority);
            PlaceButton(_priorityButton, 16f, -50f, 182f);
            PlaceButton(UiFactory.CloneButton("Pin", root, "Pin contents", new Vector2(124f, 28f), OnPin), 218f, -50f, 124f);
            PlaceButton(UiFactory.CloneButton("Copy", root, "Copy", new Vector2(64f, 28f), OnCopy), 350f, -50f, 64f);
            PlaceButton(UiFactory.CloneButton("Paste", root, "Paste", new Vector2(64f, 28f), OnPaste), 422f, -50f, 64f);
            PlaceButton(UiFactory.CloneButton("Clear", root, "Clear filters", new Vector2(112f, 28f), OnClear), 494f, -50f, 112f);
            PlaceButton(UiFactory.CloneButton("Close", root, "Close", new Vector2(88f, 28f), () => HidePanel()), 614f, -50f, 88f);

            TextMeshProUGUI catTitle = UiFactory.CreateLabel("CategoriesTitle", root, "Allow categories", 16f, TextAlignmentOptions.Left);
            Place(catTitle.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -100f), new Vector2(182f, 24f), new Vector2(0f, 1f));
            RectTransform viewport = UiFactory.CreateRect("CategoriesViewport", root);
            Place(viewport, new Vector2(0f, 1f), new Vector2(16f, -136f), new Vector2(182f, 414f), new Vector2(0f, 1f));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.3f);
            _categoryRoot = UiFactory.CreateRect("Categories", viewport);
            _categoryRoot.anchorMin = new Vector2(0f, 1f);
            _categoryRoot.anchorMax = new Vector2(1f, 1f);
            _categoryRoot.pivot = new Vector2(0.5f, 1f);
            _categoryRoot.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = _categoryRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 1f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            ContentSizeFitter fitter = _categoryRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _categoryRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            if (gui.m_recipeListScroll != null)
            {
                GameObject barGo = Object.Instantiate(gui.m_recipeListScroll.gameObject, root);
                barGo.name = "CategoryScroll";
                Place(barGo.transform as RectTransform, new Vector2(0f, 1f), new Vector2(200f, -136f), new Vector2(12f, 414f), new Vector2(0f, 1f));
                Scrollbar bar = barGo.GetComponent<Scrollbar>();
                if (bar != null)
                {
                    bar.onValueChanged = new Scrollbar.ScrollEvent();
                    bar.direction = Scrollbar.Direction.BottomToTop;
                    scroll.verticalScrollbar = bar;
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
            }
            CategoryButtons.Clear();
            CategoryIds.Clear();
            foreach ((CategoryId id, string label) in CategoryDefs.AllLabels)
            {
                CategoryId captured = id;
                Button button = UiFactory.CloneButton("Category_" + id, _categoryRoot, label, new Vector2(178f, 24f), () =>
                {
                    if (_suppressEvents || _working == null) return;
                    _working.CategoryFlags ^= captured;
                    Persist();
                    RefreshAll();
                });
                if (button == null) continue;
                LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = 23f;
                CategoryButtons.Add(button);
                CategoryIds.Add(id);
            }

            _search = UiFactory.CreateSearchField("Search", root, new Vector2(314f, 28f));
            Place(_search.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(218f, -90f), new Vector2(314f, 28f), new Vector2(0f, 1f));
            _search.onValueChanged.AddListener(OnSearchChanged);
            _search.onSelect.AddListener(_ => _searchFocused = true);
            _search.onDeselect.AddListener(_ => _searchFocused = false);
            _search.onEndEdit.AddListener(_ => _searchFocused = false);
            _previousPage = UiFactory.CloneButton("Previous", root, "<", new Vector2(32f, 28f), () => { _page--; RefreshItems(); });
            PlaceButton(_previousPage, 544f, -90f, 32f);
            _pageLabel = UiFactory.CreateLabel("Page", root, "1 / 1", 14f, TextAlignmentOptions.Center);
            Place(_pageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(578f, -90f), new Vector2(88f, 28f), new Vector2(0f, 1f));
            _nextPage = UiFactory.CloneButton("Next", root, ">", new Vector2(32f, 28f), () => { _page++; RefreshItems(); });
            PlaceButton(_nextPage, 670f, -90f, 32f);

            string[] viewLabels = { "Items", "Pinned", "No icon", "Missing" };
            for (int i = 0; i < viewLabels.Length; i++)
            {
                CatalogView view = (CatalogView)i;
                Button button = UiFactory.CloneButton("View_" + view, root, viewLabels[i], new Vector2(115f, 26f), () =>
                {
                    _view = view;
                    _page = 0;
                    RefreshItems();
                });
                PlaceButton(button, 218f + i * 123f, -126f, 115f);
                ViewButtons.Add(button);
            }
            _itemRoot = UiFactory.CreateRect("Items", root);
            Place(_itemRoot, new Vector2(0f, 1f), new Vector2(218f, -164f), new Vector2(484f, 324f), new Vector2(0f, 1f));
            GridLayoutGroup grid = _itemRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(76f, 78f);
            grid.spacing = new Vector2(5.6f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            for (int i = 0; i < PageSize; i++) ItemCells.Add(CreateItemCell());

            _emptyLabel = UiFactory.CreateLabel("Empty", root, string.Empty, 17f, TextAlignmentOptions.Center);
            Place(_emptyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(238f, -255f), new Vector2(444f, 100f), new Vector2(0f, 1f));
            _summary = UiFactory.CreateLabel("Summary", root, string.Empty, 13f, TextAlignmentOptions.TopLeft);
            Place(_summary.rectTransform, new Vector2(0f, 1f), new Vector2(218f, -502f), new Vector2(484f, 54f), new Vector2(0f, 1f));
            BuildPalette(root);
            BuildClearDialog(root);
            _colorPicker = ChestColorPicker.Create(root);
        }

        private static void BuildPalette(RectTransform root)
        {
            ColorMarkers.Clear();
            _colorLabel = UiFactory.CreateLabel("ChestColor", root, "Color: Original", 15f, TextAlignmentOptions.MidlineLeft);
            Place(_colorLabel.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -574f),
                new Vector2(166f, 28f), new Vector2(0f, 1f));
            for (int i = 0; i < ChestPalette.Colors.Length; i++)
            {
                var entry = ChestPalette.Colors[i];
                Button button = UiFactory.CreateItemCellButton("Color_" + entry.Name, root, false, () => SetColor(entry.Rgb));
                PlaceButton(button, 186f + i * 36f, -574f, 28f);
                Image swatch = (Image)button.targetGraphic;
                swatch.sprite = null;
                swatch.color = ChestTint.ToColor(entry.Rgb);
                TextMeshProUGUI marker = UiFactory.CreateLabel("Selected", button.transform, "", 18f, TextAlignmentOptions.Center);
                marker.color = entry.Rgb == 0x606570 ? Color.white : Color.black;
                Place(marker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), new Vector2(0.5f, 0.5f));
                ColorMarkers.Add(marker);
                UITooltip tooltip = UiFactory.AddTooltip(button.gameObject);
                if (tooltip != null) tooltip.Set(entry.Name, "Tint this chest " + entry.Name.ToLowerInvariant() + ".");
            }
            _customColorButton = UiFactory.CloneButton("CustomColorButton", root, "Custom", new Vector2(72f, 28f), OpenColorPicker);
            PlaceButton(_customColorButton, 550f, -574f, 72f);
            PlaceButton(UiFactory.CloneButton("OriginalColor", root, "Original", new Vector2(74f, 28f), () => SetColor(-1)),
                628f, -574f, 74f);
        }

        private static void OpenColorPicker()
        {
            if (_working == null || _boundContainer == null) return;
            ReleaseSearchFocus();
            CancelClear();
            Container target = _boundContainer;
            _panelInput.interactable = false;
            _colorPicker.Show(_working.TintRgb, rgb => _boundContainer == target && SetColor(rgb), () =>
            {
                _panelInput.interactable = true;
                _customColorButton.Select();
            });
        }

        private static bool SetColor(int rgb)
        {
            if (_working == null || _boundContainer == null) return false;
            ChestSettings next = _working.Clone();
            next.TintRgb = rgb;
            if (!ChestSettingsStore.TrySet(_boundContainer, next))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Chest Focus: cannot save color (not owner)");
                return false;
            }
            _working = next;
            ChestTint.Refresh(_boundContainer);
            RefreshColor();
            return true;
        }

        private static void RefreshColor()
        {
            if (_working == null || _colorLabel == null) return;
            _colorLabel.text = "Color: " + ChestPalette.NameOf(_working.TintRgb);
            for (int i = 0; i < ColorMarkers.Count; i++)
                ColorMarkers[i].text = _working.TintRgb == ChestPalette.Colors[i].Rgb ? "●" : "";
        }

        private static void BuildClearDialog(RectTransform parent)
        {
            RectTransform overlay = UiFactory.CreatePanelRoot("ConfirmClear", parent, parent.sizeDelta, textured: false);
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
            _clearDialog = overlay.gameObject;
            overlay.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            RectTransform dialog = UiFactory.CreatePanelRoot("Dialog", overlay, new Vector2(440f, 190f));
            TextMeshProUGUI title = UiFactory.CreateLabel("Title", dialog, "Clear this chest's filters?", 21f, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(410f, 30f), new Vector2(0.5f, 1f));
            TextMeshProUGUI body = UiFactory.CreateLabel("Body", dialog,
                "Remove all category selections and pinned items.\nThis chest will accept every item.\nPriority, color and stored items stay unchanged.", 16f, TextAlignmentOptions.Center);
            Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(410f, 70f), new Vector2(0.5f, 1f));
            _cancelClearButton = UiFactory.CloneButton("Cancel", dialog, "Cancel", new Vector2(150f, 28f), CancelClear);
            PlaceButton(_cancelClearButton, 54f, -144f, 150f);
            PlaceButton(UiFactory.CloneButton("Confirm", dialog, "Clear filters", new Vector2(150f, 28f), ConfirmClear), 236f, -144f, 150f);
            _clearDialog.SetActive(false);
        }

        private static void PlaceButton(Button button, float x, float y, float width)
        {
            if (button == null)
            {
                return;
            }

            Place(button.transform as RectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 28f), new Vector2(0f, 1f));
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Vector2 pivot)
        {
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void OnSearchChanged(string q)
        {
            if (_suppressEvents)
            {
                return;
            }

            _query = q ?? string.Empty;
            _page = 0;
            RefreshItems();
        }

        private static void RefreshAll()
        {
            RefreshColor();
            RefreshPriority();
            RefreshCategories();
            RefreshItems();
        }

        private static void RefreshPriority()
        {
            if (_priorityButton == null || _working == null)
            {
                return;
            }

            UiFactory.SetButtonLabel(_priorityButton, "Priority: " + _working.Priority);
        }

        private static void RefreshCategories()
        {
            if (_working == null)
            {
                return;
            }

            _suppressEvents = true;
            for (int i = 0; i < CategoryButtons.Count && i < CategoryIds.Count; i++)
            {
                CategoryId id = CategoryIds[i];
                string label = CategoryDefs.AllLabels[i].Label;
                bool on = (_working.CategoryFlags & id) != 0;
                UiFactory.SetButtonLabel(CategoryButtons[i], FormatCategoryLabel(label, on));
            }

            _suppressEvents = false;
        }

        private static string FormatCategoryLabel(string label, bool on)
        {
            return (on ? "[x] " : "[ ] ") + label;
        }

        private static void RefreshItems()
        {
            if (_itemRoot == null || _working == null) return;
            ItemCatalog.Browse(_query, _view, _working, SearchResults);
            int totalPages = Mathf.Max(1, (SearchResults.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            _pageLabel.text = (_page + 1) + " / " + totalPages;
            _previousPage.interactable = _page > 0;
            _nextPage.interactable = _page + 1 < totalPages;
            for (int i = 0; i < ItemCells.Count; i++)
            {
                int index = _page * PageSize + i;
                ItemCell cell = ItemCells[i];
                cell.Button.gameObject.SetActive(index < SearchResults.Count);
                if (index >= SearchResults.Count) { cell.Entry = null; continue; }
                CatalogEntry entry = SearchResults[index];
                cell.Entry = entry;
                bool pinned = _working.AllowedPrefabs.Contains(entry.PrefabName);
                bool category = (entry.Category & _working.CategoryFlags) != 0;
                cell.Name.text = entry.DisplayName;
                cell.Icon.sprite = entry.Icon;
                cell.Icon.gameObject.SetActive(entry.Icon != null);
                cell.Placeholder.gameObject.SetActive(entry.Icon == null);
                cell.Marker.text = pinned ? "PIN" : category ? "CAT" : string.Empty;
                ((Image)cell.Button.targetGraphic).color = pinned
                    ? new Color(0.3f, 0.38f, 0.16f, 1f)
                    : category ? new Color(0.16f, 0.23f, 0.19f, 1f) : new Color(0.13f, 0.12f, 0.095f, 1f);
                if (cell.Tooltip != null)
                {
                    cell.Tooltip.Set(entry.DisplayName, entry.PrefabName + "\n" + (_view == CatalogView.Missing
                        ? "Unavailable item. Click to remove its saved pin."
                        : pinned ? "Pinned. Click to remove the pin." : category
                            ? "Allowed by category. Click to also pin this item." : "Click to pin this item."));
                }
            }
            _emptyLabel.gameObject.SetActive(SearchResults.Count == 0);
            _emptyLabel.text = _query.Length > 0 ? "No matching items in this view." : _view == CatalogView.Pinned
                ? "No pinned items.\nChoose items or use Pin contents." : _view == CatalogView.Missing
                    ? "All saved items are available." : "No items in this view.";
            string[] labels = { "Items", "Pinned", "No icon", "Missing" };
            for (int i = 0; i < ViewButtons.Count; i++)
                UiFactory.SetButtonLabel(ViewButtons[i], (i == (int)_view ? "• " : "") + labels[i]);
            int categories = 0;
            foreach (var category in CategoryDefs.AllLabels) if ((_working.CategoryFlags & category.Id) != 0) categories++;
            _summary.text = SearchResults.Count + " items • " + _working.AllowedPrefabs.Count + " pinned • " + categories + " categories\n"
                + (_view == CatalogView.NoIcon ? "Iconless entries may include creature equipment or modded items."
                    : _view == CatalogView.Missing ? "Saved prefabs unavailable in this game. Click an entry to remove it."
                    : _working.IsEmpty ? "No filters: this chest accepts every item."
                    : "PIN = explicitly allowed • CAT = allowed by category. Hover for full names.");
        }

        private static ItemCell CreateItemCell()
        {
            ItemCell cell = new ItemCell();
            cell.Button = UiFactory.CreateItemCellButton("ItemCell", _itemRoot, false, () =>
            {
                if (_working == null || cell.Entry == null) return;
                string prefab = cell.Entry.PrefabName;
                if (!_working.AllowedPrefabs.Remove(prefab)) _working.AllowedPrefabs.Add(prefab);
                Persist();
                RefreshItems();
            });
            RectTransform root = cell.Button.transform as RectTransform;
            RectTransform iconRoot = UiFactory.CreateRect("Icon", root);
            Place(iconRoot, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(40f, 40f), new Vector2(0.5f, 1f));
            cell.Icon = iconRoot.gameObject.AddComponent<Image>();
            cell.Icon.preserveAspect = true;
            cell.Icon.raycastTarget = false;
            cell.Placeholder = UiFactory.CreateLabel("Placeholder", root, "?", 26f, TextAlignmentOptions.Center);
            Place(cell.Placeholder.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(40f, 40f), new Vector2(0.5f, 1f));
            cell.Marker = UiFactory.CreateLabel("State", root, "", 10f, TextAlignmentOptions.TopLeft);
            Place(cell.Marker.rectTransform, new Vector2(0f, 1f), new Vector2(3f, -2f), new Vector2(30f, 14f), new Vector2(0f, 1f));
            cell.Name = UiFactory.CreateLabel("Name", root, "", 11f, TextAlignmentOptions.Center);
            Place(cell.Name.rectTransform, new Vector2(0f, 0f), new Vector2(3f, 3f), new Vector2(70f, 30f), new Vector2(0f, 0f));
            cell.Name.textWrappingMode = TextWrappingModes.Normal;
            cell.Tooltip = UiFactory.AddTooltip(cell.Button.gameObject);
            return cell;
        }

        private static void CyclePriority()
        {
            if (_working == null)
            {
                return;
            }

            int next = (System.Array.IndexOf(Priorities, _working.Priority) + 1) % Priorities.Length;
            _working.Priority = Priorities[next];
            Persist();
            RefreshPriority();
        }

        private static void OnPin()
        {
            if (_working == null || _boundContainer == null)
            {
                return;
            }

            _working.PinFromInventory(_boundContainer.GetInventory());
            Persist();
            RefreshItems();
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Chest Focus: pinned contents");
        }

        private static void OnClear()
        {
            if (_working == null || _working.IsEmpty) return;
            ReleaseSearchFocus();
            _clearTarget = _working;
            _clearDialog.SetActive(true);
            _clearDialog.transform.SetAsLastSibling();
            _panelInput.interactable = false;
            _cancelClearButton.Select();
        }

        private static void CancelClear()
        {
            _clearTarget = null;
            if (_panelInput != null) _panelInput.interactable = true;
            if (_clearDialog != null) _clearDialog.SetActive(false);
        }

        private static void ConfirmClear()
        {
            if (_working != null && ReferenceEquals(_working, _clearTarget))
            {
                _working.ClearFilters();
                Persist();
                RefreshAll();
            }
            CancelClear();
        }

        private static void OnCopy()
        {
            if (_working == null)
            {
                return;
            }

            ChestSettingsStore.Copy(_working);
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Chest Focus: settings copied");
        }

        private static void OnPaste()
        {
            if (!ChestSettingsStore.HasClipboard)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Chest Focus: clipboard empty");
                return;
            }

            _working = ChestSettingsStore.PasteClone();
            Persist();
            RefreshAll();
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Chest Focus: settings pasted");
        }

        private static void DestroyNamed(Transform parent, string name)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui))]
    internal static class InventoryGuiChestFocusPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix(InventoryGui __instance)
        {
            ChestSettingsUi.EnsureBuilt(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryGui.Show))]
        private static void ShowPostfix(InventoryGui __instance)
        {
            ChestSettingsUi.EnsureBuilt(__instance);
            ChestSettingsUi.OnContainerChanged(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryGui.Hide))]
        private static void HidePostfix()
        {
            ChestSettingsUi.HidePanel();
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateContainer")]
        private static void UpdateContainerPostfix(InventoryGui __instance)
        {
            ChestSettingsUi.OnContainerChanged(__instance);
        }
    }

    /// <summary>
    /// While typing in Chest Focus search, swallow gameplay binds (Use/E, hotbar, etc.).
    /// </summary>
    [HarmonyPatch(typeof(ZInput))]
    internal static class ZInputSearchCapturePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ZInput.GetButtonDown))]
        private static bool GetButtonDownPrefix(ref bool __result)
        {
            if (!ChestSettingsUi.IsCapturingText)
            {
                return true;
            }

            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ZInput.GetButton))]
        private static bool GetButtonPrefix(ref bool __result)
        {
            if (!ChestSettingsUi.IsCapturingText)
            {
                return true;
            }

            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ZInput.GetButtonUp))]
        private static bool GetButtonUpPrefix(ref bool __result)
        {
            if (!ChestSettingsUi.IsCapturingText)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
