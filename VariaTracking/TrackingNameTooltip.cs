using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VariaTracking
{
    /// <summary>
    /// Large-map-only name / ??? label glued above the hovered tracking dot.
    /// Hover is keyed by bound Character, not pool index.
    /// </summary>
    internal static class TrackingNameTooltip
    {
        private const float TooltipHitRadiusPx = 16f;
        private static readonly Color32 TextWhite = new(255, 255, 255, 255);

        private static RectTransform _root;
        private static Image _bg;
        private static TextMeshProUGUI _text;
        private static TMP_FontAsset _font;
        private static Material _fontMaterial;
        private static Character _hoveredCharacter;
        private static bool _visible;

        public static void Clear()
        {
            if (_hoveredCharacter == null && !_visible)
            {
                return;
            }

            _hoveredCharacter = null;
            Hide();
        }

        public static void Destroy()
        {
            _hoveredCharacter = null;
            Hide();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
                _bg = null;
                _text = null;
            }

            _visible = false;
        }

        public static void Tick(
            bool allow,
            Player player,
            TrackingUnlockState unlocks,
            TrackingConfigSnapshot cfg)
        {
            int layoutDrawn = TrackingMapMarkers.LayoutDrawn;
            if (!allow || layoutDrawn <= 0 || player == null)
            {
                Clear();
                return;
            }

            Vector2 pointer = ZInput.pointerPosition;
            TrackingMapMarkers.DotUi hitUi = null;
            float bestDistSq = TooltipHitRadiusPx * TooltipHitRadiusPx;

            for (int i = 0; i < layoutDrawn; i++)
            {
                TrackingMapMarkers.DotUi ui = TrackingMapMarkers.GetDrawn(i);
                if (ui?.Root == null || !ui.Root.gameObject.activeInHierarchy || ui.BoundCharacter == null)
                {
                    continue;
                }

                Camera cam = null;
                Canvas canvas = ui.CachedCanvas;
                if (canvas == null)
                {
                    canvas = ui.Root.GetComponentInParent<Canvas>();
                    ui.CachedCanvas = canvas;
                }

                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cam = canvas.worldCamera;
                }

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, ui.Root.position);
                float distSq = (pointer - screen).sqrMagnitude;
                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    hitUi = ui;
                }
            }

            Character hitCharacter = hitUi?.BoundCharacter;
            if (ReferenceEquals(hitCharacter, _hoveredCharacter) && hitUi != null)
            {
                Show(hitUi, player, unlocks, cfg, repositionOnly: true);
                return;
            }

            _hoveredCharacter = hitCharacter;
            if (hitUi == null)
            {
                Hide();
                return;
            }

            Show(hitUi, player, unlocks, cfg, repositionOnly: false);
        }

        private static void Show(
            TrackingMapMarkers.DotUi ui,
            Player player,
            TrackingUnlockState unlocks,
            TrackingConfigSnapshot cfg,
            bool repositionOnly)
        {
            if (ui?.Root == null || ui.BoundCharacter == null)
            {
                Hide();
                return;
            }

            int knowledge = TrackingKnowledge.Revision;
            int level = ui.BoundCharacter.GetLevel();
            bool canShowName = TrackingDisplay.CanShowRealName(ui.BoundCharacter, player, unlocks, cfg);
            if (string.IsNullOrEmpty(ui.CachedName) || ui.CachedKnowledgeRevision != knowledge
                || ui.CachedLevel != level || ui.CachedCanShowName != canShowName || !repositionOnly)
            {
                ui.CachedKnowledgeRevision = knowledge;
                ui.CachedLevel = level;
                ui.CachedCanShowName = canShowName;
                ui.CachedName = canShowName
                    ? TrackingRadar.BuildDisplayName(ui.BoundCharacter, ui.ShowStarInName)
                    : "???";
            }

            if (string.IsNullOrEmpty(ui.CachedName))
            {
                Hide();
                return;
            }

            Ensure(ui.Root);
            ApplyTextStyle();

            if (!repositionOnly || _text.text != ui.CachedName)
            {
                _text.text = ui.CachedName;
                _text.ForceMeshUpdate();

                Vector2 preferred = _text.GetPreferredValues(ui.CachedName);
                float padX = 8f;
                float padY = 4f;
                _root.sizeDelta = new Vector2(preferred.x + padX, preferred.y + padY);
                _text.rectTransform.sizeDelta = preferred;
            }

            float half = ui.Root.rect.height * 0.5f;
            float lift = Mathf.Max(half + 10f, 8f);
            _root.anchoredPosition = new Vector2(0f, lift);
            _root.SetAsLastSibling();
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }

            _visible = true;
        }

        private static void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            _visible = false;
        }

        private static void Ensure(RectTransform dotRoot)
        {
            if (_root == null)
            {
                var go = new GameObject(
                    "VariaTrackingNameTip",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                _root = go.GetComponent<RectTransform>();
                _root.anchorMin = new Vector2(0.5f, 0.5f);
                _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0.5f, 0f);
                _bg = go.GetComponent<Image>();
                _bg.color = new Color(0f, 0f, 0f, 0.78f);
                _bg.raycastTarget = false;

                var textGo = new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.SetParent(_root, worldPositionStays: false);
                textRt.anchorMin = new Vector2(0.5f, 0.5f);
                textRt.anchorMax = new Vector2(0.5f, 0.5f);
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.anchoredPosition = Vector2.zero;

                _text = textGo.GetComponent<TextMeshProUGUI>();
                _text.alignment = TextAlignmentOptions.Center;
                _text.fontSize = 14f;
                _text.overflowMode = TextOverflowModes.Overflow;
                _text.raycastTarget = false;
                _text.margin = Vector4.zero;
                ApplyTextStyle();
            }

            if (_root.parent != dotRoot)
            {
                _root.SetParent(dotRoot, worldPositionStays: false);
            }

            if (TryResolveFont(out TMP_FontAsset font, out Material material))
            {
                if (_text.font != font)
                {
                    _text.font = font;
                }

                if (material != null && _text.fontSharedMaterial != material)
                {
                    _text.fontSharedMaterial = material;
                }
            }
        }

        private static void ApplyTextStyle()
        {
            if (_text == null)
            {
                return;
            }

            _text.color = Color.white;
            _text.faceColor = TextWhite;
        }

        private static bool TryResolveFont(out TMP_FontAsset font, out Material material)
        {
            if (_font != null)
            {
                font = _font;
                material = _fontMaterial;
                return true;
            }

            font = null;
            material = null;
            TMP_FontAsset fallbackFont = null;
            Material fallbackMat = null;

            TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null || t.font == null)
                {
                    continue;
                }

                if (fallbackFont == null)
                {
                    fallbackFont = t.font;
                    fallbackMat = t.fontSharedMaterial;
                }

                Color c = t.color;
                if (c.r + c.g + c.b < 2f || c.a < 0.5f)
                {
                    continue;
                }

                _font = t.font;
                _fontMaterial = t.fontSharedMaterial;
                font = _font;
                material = _fontMaterial;
                return true;
            }

            if (fallbackFont == null)
            {
                return false;
            }

            _font = fallbackFont;
            _fontMaterial = fallbackMat;
            font = _font;
            material = _fontMaterial;
            return true;
        }
    }
}
