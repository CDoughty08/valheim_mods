using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VariaChestFocus.Gui
{
    // A reusable modal: changes remain local to its preview until Apply succeeds.
    internal sealed class ChestColorPicker : MonoBehaviour
    {
        private Texture2D _shadeTexture;
        private Texture2D _hueTexture;
        private readonly Color[] _pixels = new Color[128 * 128];
        private RectTransform _cursor;
        private Image _current;
        private Image _preview;
        private Slider _hueSlider;
        private Slider _strengthSlider;
        private Slider _brightnessSlider;
        private TMP_InputField _hex;
        private TextMeshProUGUI _status;
        private Button _applyButton;
        private Button _cancelButton;
        private Func<int, bool> _apply;
        private Action _closed;
        private float _hue, _strength, _brightness;
        private int _rgb;
        private bool _refreshing;

        internal bool IsTyping => gameObject.activeInHierarchy && _hex != null && _hex.isFocused;

        internal static ChestColorPicker Create(RectTransform parent)
        {
            RectTransform overlay = UiFactory.CreatePanelRoot("CustomColor", parent, parent.sizeDelta, textured: false);
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            overlay.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            ChestColorPicker picker = overlay.gameObject.AddComponent<ChestColorPicker>();
            picker.Build(overlay);
            overlay.gameObject.SetActive(false);
            return picker;
        }

        internal void Show(int rgb, Func<int, bool> apply, Action closed)
        {
            _apply = apply;
            _closed = closed;
            // Original has no custom tint; white is the neutral starting point.
            _rgb = rgb >= 0 && rgb <= 0xFFFFFF ? rgb : 0xFFFFFF;
            _current.color = ChestTint.ToColor(_rgb);
            SetRgb(_rgb);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _hueSlider.Select();
        }

        internal void Cancel()
        {
            if (!gameObject.activeSelf) return;
            _hex.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false);
            _apply = null;
            Action closed = _closed;
            _closed = null;
            closed?.Invoke();
        }

        private void Apply()
        {
            if (!ChestPalette.TryParseHex(_hex.text, out int rgb)) return;
            if (_apply != null && _apply(rgb)) Cancel();
            else _status.text = "Cannot save. Reopen the chest and try again.";
        }

        private void Build(RectTransform overlay)
        {
            RectTransform dialog = UiFactory.CreatePanelRoot("Picker", overlay, new Vector2(568f, 414f));
            Label(dialog, "Custom chest color", 20, 16, 528, 30, 23);
            Label(dialog, "Choose a hue, then click or drag to find your shade.", 24, 48, 520, 24, 15);

            RectTransform shade = Rect(dialog, "Shade", 24, 82, 272, 210);
            _shadeTexture = Texture(128, 128, "ChestFocus_Shades");
            shade.gameObject.AddComponent<RawImage>().texture = _shadeTexture;
            shade.gameObject.AddComponent<ColorShadeDrag>().Changed = (strength, brightness) =>
            {
                _strength = strength;
                _brightness = brightness;
                RefreshPreview();
            };
            TextMeshProUGUI cursor = UiFactory.CreateLabel("Cursor", shade, "+", 25f, TextAlignmentOptions.Center);
            cursor.color = Color.white;
            Outline outline = cursor.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            _cursor = cursor.rectTransform;
            _cursor.sizeDelta = new Vector2(24f, 24f);

            Label(dialog, "Hue", 24, 300, 272, 22, 15);
            _hueSlider = CreateSlider(dialog, "Hue", 24, 326, 272, value =>
            {
                if (_refreshing) return;
                _hue = value;
                RefreshShades();
                RefreshPreview();
            });
            _hueTexture = Texture(256, 1, "ChestFocus_Hues");
            Color[] hues = new Color[256];
            for (int i = 0; i < hues.Length; i++) hues[i] = Color.HSVToRGB(i / 255f, 1f, 1f);
            _hueTexture.SetPixels(hues);
            _hueTexture.Apply(false);
            RectTransform rainbow = Rect(_hueSlider.transform, "Rainbow", 0, 5, 272, 14);
            RawImage rainbowImage = rainbow.gameObject.AddComponent<RawImage>();
            rainbowImage.texture = _hueTexture;
            rainbowImage.raycastTarget = false;
            rainbow.SetAsFirstSibling();

            Label(dialog, "Current", 320, 82, 100, 24, 15);
            Label(dialog, "New", 432, 82, 100, 24, 15);
            _current = Rect(dialog, "CurrentSwatch", 320, 110, 100, 48).gameObject.AddComponent<Image>();
            _preview = Rect(dialog, "NewSwatch", 432, 110, 100, 48).gameObject.AddComponent<Image>();
            Label(dialog, "Color strength", 320, 168, 220, 24, 15);
            _strengthSlider = CreateSlider(dialog, "Strength", 320, 194, 220, value =>
            {
                if (_refreshing) return;
                _strength = value;
                RefreshPreview();
            });
            Label(dialog, "Brightness", 320, 228, 220, 24, 15);
            _brightnessSlider = CreateSlider(dialog, "Brightness", 320, 254, 220, value =>
            {
                if (_refreshing) return;
                _brightness = value;
                RefreshPreview();
            });
            Label(dialog, "Hex color (optional)", 320, 288, 220, 24, 15);
            _hex = UiFactory.CreateSearchField("HexColor", dialog, new Vector2(220f, 28f));
            Position((RectTransform)_hex.transform, 320, 316, 220, 28);
            ((TMP_Text)_hex.placeholder).text = "#RRGGBB";
            _hex.characterLimit = 7;
            _hex.onValueChanged.AddListener(OnHexChanged);
            _status = Label(dialog, "", 24, 348, 520, 22, 13);
            _cancelButton = UiFactory.CloneButton("Cancel", dialog, "Cancel", new Vector2(120f, 28f), Cancel);
            Position((RectTransform)_cancelButton.transform, 284, 376, 120, 28);
            _applyButton = UiFactory.CloneButton("Apply", dialog, "Apply color", new Vector2(136f, 28f), Apply);
            Position((RectTransform)_applyButton.transform, 412, 376, 136, 28);

            // Keep controller/keyboard navigation inside the modal. Left/right adjust sliders.
            Selectable[] controls = { _hueSlider, _strengthSlider, _brightnessSlider, _hex, _applyButton, _cancelButton };
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = controls[(i + controls.Length - 1) % controls.Length],
                    selectOnDown = controls[(i + 1) % controls.Length]
                };
            }
        }

        private void OnHexChanged(string text)
        {
            if (_refreshing) return;
            if (ChestPalette.TryParseHex(text, out int rgb)) SetRgb(rgb, updateHex: false);
            else
            {
                _applyButton.interactable = false;
                _status.text = "Enter six hex digits, e.g. #709FE0, or use the picker.";
            }
        }

        private void SetRgb(int rgb, bool updateHex = true)
        {
            Color.RGBToHSV(ChestTint.ToColor(rgb), out _hue, out _strength, out _brightness);
            RefreshShades();
            RefreshPreview(updateHex);
        }

        private void RefreshPreview(bool updateHex = true)
        {
            Color color = Color.HSVToRGB(_hue, _strength, _brightness);
            Color32 bytes = color;
            _rgb = (bytes.r << 16) | (bytes.g << 8) | bytes.b;
            _preview.color = color;
            _cursor.anchorMin = _cursor.anchorMax = new Vector2(_strength, _brightness);
            _cursor.anchoredPosition = Vector2.zero;
            _refreshing = true;
            _hueSlider.value = _hue;
            _strengthSlider.value = _strength;
            _brightnessSlider.value = _brightness;
            if (updateHex) _hex.text = "#" + _rgb.ToString("X6");
            _refreshing = false;
            _applyButton.interactable = true;
            _status.text = "Preview shows the tint; chest texture and lighting affect the result.";
        }

        private void RefreshShades()
        {
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    _pixels[y * 128 + x] = Color.HSVToRGB(_hue, x / 127f, y / 127f);
            _shadeTexture.SetPixels(_pixels);
            _shadeTexture.Apply(false);
        }

        private static Slider CreateSlider(Transform parent, string name, float x, float y, float width,
            UnityEngine.Events.UnityAction<float> changed)
        {
            RectTransform root = Rect(parent, name, x, y, width, 24);
            root.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.14f, 0.11f, 1f);
            RectTransform handles = Rect(root, "HandleArea", 0, 0, width, 24);
            RectTransform handle = Rect(handles, "Handle", 0, 0, 10, 24);
            handle.pivot = new Vector2(0.5f, 0.5f);
            // Slider stretches the handle vertically; keep the extra height at zero.
            handle.sizeDelta = new Vector2(10f, 0f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1f, 0.94f, 0.75f, 1f);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        private static Texture2D Texture(int width, int height, string name) => new Texture2D(width, height,
            TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        private static TextMeshProUGUI Label(Transform parent, string text, float x, float y, float width, float height, float size)
        {
            TextMeshProUGUI label = UiFactory.CreateLabel("Label", parent, text, size, TextAlignmentOptions.MidlineLeft);
            Position(label.rectTransform, x, y, width, height);
            return label;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rect = UiFactory.CreateRect(name, parent);
            Position(rect, x, y, width, height);
            return rect;
        }

        private static void Position(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void OnDestroy()
        {
            if (_shadeTexture != null) Destroy(_shadeTexture);
            if (_hueTexture != null) Destroy(_hueTexture);
            _apply = null;
            _closed = null;
        }
    }

    internal sealed class ColorShadeDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        internal Action<float, float> Changed;
        public void OnPointerDown(PointerEventData data) => Pick(data);
        public void OnDrag(PointerEventData data) => Pick(data);

        private void Pick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            RectTransform rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position,
                data.pressEventCamera, out Vector2 point))
            {
                Changed?.Invoke(Mathf.Clamp01((point.x - rect.rect.xMin) / rect.rect.width),
                    Mathf.Clamp01((point.y - rect.rect.yMin) / rect.rect.height));
            }
        }
    }
}
