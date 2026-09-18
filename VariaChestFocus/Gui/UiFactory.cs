using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VariaChestFocus.Gui
{
    /// <summary>
    /// Builds UI by cloning Valheim InventoryGui widgets (AzuEPI-style) so sprites/fonts work.
    /// </summary>
    internal static class UiFactory
    {
        private static Transform _buttonTemplate;
        private static Sprite _panelSprite;
        private static Sprite _imageSprite;
        private static TMP_FontAsset _font;
        private static Material _fontMaterial;
        private static GameObject _tooltipPrefab;
        private static Color _buttonImageColor = Color.white;

        internal static void ClearTemplates()
        {
            _buttonTemplate = null; _panelSprite = _imageSprite = null;
            _font = null; _fontMaterial = null; _tooltipPrefab = null;
        }

        internal static bool IsReady => _buttonTemplate != null && _font != null;

        internal static void CaptureTemplates(InventoryGui gui)
        {
            if (gui == null)
            {
                return;
            }

            UITooltip tooltip = gui.m_playerGrid != null && gui.m_playerGrid.m_elementPrefab != null
                ? gui.m_playerGrid.m_elementPrefab.GetComponentInChildren<UITooltip>(true) : null;
            if (tooltip != null) _tooltipPrefab = tooltip.m_tooltipPrefab;
            if (_tooltipPrefab == null)
            {
                foreach (UITooltip candidate in gui.GetComponentsInChildren<UITooltip>(true))
                    if (candidate.m_tooltipPrefab != null) { _tooltipPrefab = candidate.m_tooltipPrefab; break; }
            }

            Button srcBtn = gui.m_stackAllButton ?? gui.m_takeAllButton ?? gui.m_craftButton;
            if (srcBtn != null)
            {
                _buttonTemplate = srcBtn.transform;
                Image btnImg = srcBtn.GetComponent<Image>();
                if (btnImg != null)
                {
                    _imageSprite = btnImg.sprite;
                    _buttonImageColor = btnImg.color;
                }
            }

            TMP_Text sample = srcBtn != null
                ? srcBtn.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (sample == null && gui.m_containerName != null)
            {
                sample = gui.m_containerName;
            }

            if (sample != null)
            {
                _font = sample.font;
                _fontMaterial = sample.fontSharedMaterial;
            }

            // Prefer crafting Bkg sprite for panel chrome.
            if (gui.m_crafting != null)
            {
                Transform bkg = gui.m_crafting.Find("Bkg");
                if (bkg != null)
                {
                    Image bkgImg = bkg.GetComponent<Image>();
                    if (bkgImg != null && bkgImg.sprite != null)
                    {
                        _panelSprite = bkgImg.sprite;
                    }
                }
            }

            if (_panelSprite == null)
            {
                _panelSprite = _imageSprite;
            }
        }

        internal static Button CloneButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            UnityAction onClick)
        {
            if (_buttonTemplate == null || parent == null)
            {
                return null;
            }

            Transform clone = Object.Instantiate(_buttonTemplate, parent);
            clone.name = name;
            clone.gameObject.SetActive(true);
            clone.SetAsLastSibling();

            // Strip gamepad hint clutter from action buttons we reuse.
            DisableChildrenContaining(clone, "KeyHints");
            DisableChildrenContaining(clone, "gamepad");

            RectTransform rt = (RectTransform)clone;
            rt.localScale = Vector3.one;
            rt.sizeDelta = size;

            Button btn = clone.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                if (onClick != null)
                {
                    btn.onClick.AddListener(onClick);
                }
            }

            TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.text = label;
                text.enabled = true;
                if (_font != null)
                {
                    text.font = _font;
                    if (_fontMaterial != null)
                    {
                        text.fontSharedMaterial = _fontMaterial;
                    }
                }
            }

            UITooltip hint = AddTooltip(clone.gameObject);
            if (hint != null)
            {
                hint.m_topic = label;
                hint.m_text = string.Empty;
            }
            return btn;
        }

        internal static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
            UITooltip tooltip = button.GetComponent<UITooltip>();
            if (tooltip != null) tooltip.m_topic = label;
        }

        internal static RectTransform CreatePanelRoot(string name, Transform parent, Vector2 size, bool textured = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            Image img = go.GetComponent<Image>();
            img.raycastTarget = true;
            img.color = new Color(0.055f, 0.04f, 0.028f, 1f);
            if (textured && _panelSprite != null)
            {
                // Preserve the vanilla paper silhouette and soft edge. An inset backing
                // stops inventory text showing through the translucent center of its sprite.
                img.sprite = _panelSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0f, 0f, 0f, 0.8f);
                Shadow shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
                shadow.effectDistance = new Vector2(4f, -6f);

                RectTransform backing = CreateRect("ReadableBacking", rt);
                Stretch(backing);
                backing.offsetMin = new Vector2(12f, 12f);
                backing.offsetMax = new Vector2(-12f, -12f);
                Image backingImage = backing.gameObject.AddComponent<Image>();
                backingImage.color = new Color(0.055f, 0.04f, 0.028f, 1f);
                backingImage.raycastTarget = false;

                RectTransform paper = CreateRect("PaperTexture", rt);
                Stretch(paper);
                Image paperImage = paper.gameObject.AddComponent<Image>();
                paperImage.sprite = _panelSprite;
                paperImage.type = Image.Type.Sliced;
                paperImage.color = new Color(0.18f, 0.14f, 0.1f, 0.95f);
                paperImage.raycastTarget = false;
            }
            return rt;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        internal static Image CreateImage(RectTransform rt, Color color, Sprite sprite = null)
        {
            Image img = rt.gameObject.GetComponent<Image>();
            if (img == null)
            {
                img = rt.gameObject.AddComponent<Image>();
            }

            img.sprite = sprite != null ? sprite : _imageSprite;
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        internal static TextMeshProUGUI CreateLabel(
            string name,
            Transform parent,
            string text,
            float fontSize,
            TextAlignmentOptions align)
        {
            RectTransform rt = CreateRect(name, parent);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(tmp);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = new Color(1f, 0.92f, 0.72f, 1f);
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        internal static void ApplyFont(TMP_Text tmp)
        {
            if (tmp == null || _font == null)
            {
                return;
            }

            tmp.font = _font;
            if (_fontMaterial != null)
            {
                tmp.fontSharedMaterial = _fontMaterial;
            }
        }

        internal static TMP_InputField CreateSearchField(string name, Transform parent, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent);
            rt.sizeDelta = size;
            Image bg = CreateImage(rt, new Color(0.08f, 0.08f, 0.1f, 0.95f));

            RectTransform textArea = CreateRect("Text Area", rt);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(8f, 4f);
            textArea.offsetMax = new Vector2(-8f, -4f);
            textArea.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateLabel("Text", textArea, string.Empty, 16f, TextAlignmentOptions.MidlineLeft);
            text.raycastTarget = true;
            text.color = Color.white;
            Stretch(text.rectTransform);

            TextMeshProUGUI placeholder = CreateLabel("Placeholder", textArea, "Search items...", 16f, TextAlignmentOptions.MidlineLeft);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            Stretch(placeholder.rectTransform);

            TMP_InputField field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = textArea;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = _font;
            field.caretColor = new Color(1f, 0.85f, 0.4f, 1f);
            field.selectionColor = new Color(0.4f, 0.5f, 0.2f, 0.5f);
            field.customCaretColor = true;
            field.shouldHideMobileInput = true;
            field.richText = false;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.enabled = true;
            field.interactable = true;
            return field;
        }

        internal static Button CreateItemCellButton(string name, Transform parent, bool selected, UnityAction onClick)
        {
            RectTransform rt = CreateRect(name, parent);
            Image bg = CreateImage(
                rt,
                selected ? new Color(0.45f, 0.55f, 0.25f, 0.95f) : new Color(0.18f, 0.18f, 0.2f, 0.95f));
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        internal static UITooltip AddTooltip(GameObject target)
        {
            if (_tooltipPrefab == null) return null;
            UITooltip tooltip = target.GetComponent<UITooltip>() ?? target.AddComponent<UITooltip>();
            tooltip.m_tooltipPrefab = _tooltipPrefab;
            return tooltip;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void DisableChildrenContaining(Transform root, string contains)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}
