using UnityEngine;
using UnityEngine.UI;

namespace VariaTracking
{
    /// <summary>Tracking radius wedge (cone) or full ring on mini / large map.</summary>
    internal static class TrackingRangeRing
    {
        private static RectTransform _root;
        private static Image _fillImage;
        private static Image _ringImage;
        private static bool _needsSiblingOrder = true;
        private static Minimap.MapMode _appliedMode = (Minimap.MapMode)(-1);
        private static Color _appliedColor;
        private static bool _hasAppliedColor;
        private static float _appliedYaw = float.NaN;
        private static bool _fullCircleMode;
        /// <summary>False until ApplyConeVisual has configured fill vs ring for the current mode.</summary>
        private static bool _visualConfigured;

        public static void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }
        }

        public static void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
                _fillImage = null;
                _ringImage = null;
            }

            _needsSiblingOrder = true;
            _appliedMode = (Minimap.MapMode)(-1);
            _hasAppliedColor = false;
            _appliedYaw = float.NaN;
            _visualConfigured = false;
        }

        public static void Update(
            Player player,
            Minimap minimap,
            TrackingConfigSnapshot cfg,
            float radius,
            float coneDegrees,
            Vector3 forwardXZ)
        {
            if (!cfg.ShowRangeCircle || radius <= 0f)
            {
                Hide();
                return;
            }

            RawImage mapImage = TrackingRadar.GetMapImage(minimap);
            RectTransform pinRoot = TrackingRadar.GetPinRoot(minimap);
            if (mapImage == null || pinRoot == null)
            {
                return;
            }

            Ensure(pinRoot, minimap.m_mode);
            Vector3 pos = player.transform.position;
            if (!minimap.IsPointVisible(pos, mapImage))
            {
                Hide();
                return;
            }

            bool fullCircle = coneDegrees >= 359.5f;
            float diameter;
            if (fullCircle)
            {
                float innerHole = minimap.m_mode == Minimap.MapMode.Small
                    ? CircleSprites.RangeRingInnerSmall
                    : CircleSprites.RangeRingInnerLarge;
                diameter = radius * 2f / Mathf.Max(0.01f, innerHole);
            }
            else
            {
                // Solid disc — outer edge is the radius.
                diameter = radius * 2f;
            }

            Vector2 sizeMap = new(
                diameter / minimap.m_pixelSize / minimap.m_textureSize,
                diameter / minimap.m_pixelSize / minimap.m_textureSize);
            Vector2 guiSize = minimap.MapSizeToLocalGuiSize(sizeMap, mapImage);

            minimap.WorldToMapPoint(pos, out float mx, out float my);
            Vector2 anchored = minimap.MapPointToLocalGuiPos(mx, my, mapImage);
            _root.anchoredPosition = anchored;
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, guiSize.x);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, guiSize.y);

            // Match vanilla player arrow on this map mode (same transform the arrow uses).
            float facingZ = GetMapFacingZ(minimap, forwardXZ);
            ApplyConeVisual(cfg.CircleColor, coneDegrees, facingZ, fullCircle, minimap.m_mode);

            if (_needsSiblingOrder)
            {
                _root.SetAsFirstSibling();
                _needsSiblingOrder = false;
            }

            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }
        }

        private static void ApplyConeVisual(Color color, float coneDegrees, float facingZ, bool fullCircle, Minimap.MapMode mode)
        {
            // First paint (or mode/FOV style change) must configure images — Ensure leaves both disabled
            // and sets _appliedMode early, so we cannot rely on mode inequality alone.
            if (!_visualConfigured || _fullCircleMode != fullCircle || _appliedMode != mode)
            {
                _visualConfigured = true;
                _fullCircleMode = fullCircle;
                _appliedMode = mode;
                _hasAppliedColor = false;
                _appliedYaw = float.NaN;

                if (fullCircle)
                {
                    _fillImage.enabled = false;
                    _ringImage.enabled = true;
                    _ringImage.type = Image.Type.Simple;
                    _ringImage.sprite = mode == Minimap.MapMode.Small
                        ? CircleSprites.RangeRingSmall
                        : CircleSprites.RangeRingLarge;
                    _root.localEulerAngles = Vector3.zero;
                }
                else
                {
                    _ringImage.enabled = false;
                    _fillImage.enabled = true;
                    // Baked high-res wedge (not Image.Radial360 — that mesh facets when large).
                    _fillImage.sprite = CircleSprites.GetWedge(coneDegrees);
                    _fillImage.type = Image.Type.Simple;
                    _fillImage.fillAmount = 1f;
                }
            }
            else if (!fullCircle)
            {
                // Cone degrees change with skill — refresh baked wedge when FOV moves.
                Sprite wedge = CircleSprites.GetWedge(coneDegrees);
                if (_fillImage.sprite != wedge)
                {
                    _fillImage.sprite = wedge;
                }
            }

            if (!_hasAppliedColor || _appliedColor != color)
            {
                if (_fillImage.enabled)
                {
                    // Solid fill reads denser than a thin ring at the same alpha.
                    Color fill = color;
                    fill.a = Mathf.Clamp01(color.a * 0.45f);
                    _fillImage.color = fill;
                }

                if (_ringImage.enabled)
                {
                    _ringImage.color = color;
                }

                _appliedColor = color;
                _hasAppliedColor = true;
            }

            if (!fullCircle)
            {
                // Wedge texture is already centered on image up — rotate to player arrow only.
                float rot = facingZ;
                if (float.IsNaN(_appliedYaw) || !Mathf.Approximately(_appliedYaw, rot))
                {
                    _root.localEulerAngles = new Vector3(0f, 0f, rot);
                    _appliedYaw = rot;
                }
            }
            else if (!float.IsNaN(_appliedYaw))
            {
                _root.localEulerAngles = Vector3.zero;
                _appliedYaw = float.NaN;
            }
        }

        /// <summary>
        /// UI Z rotation that points "up" on the image along the vanilla player arrow.
        /// Prefer the live marker transform so we stay locked to what the player sees.
        /// </summary>
        private static float GetMapFacingZ(Minimap minimap, Vector3 forwardXZ)
        {
            RectTransform marker = minimap.m_mode == Minimap.MapMode.Large
                ? minimap.m_largeMarker
                : minimap.m_smallMarker;

            if (marker != null)
            {
                return marker.localEulerAngles.z;
            }

            float yaw = Mathf.Atan2(forwardXZ.x, forwardXZ.z) * Mathf.Rad2Deg;
            return -yaw;
        }

        private static void Ensure(RectTransform parent, Minimap.MapMode mode)
        {
            if (_root != null)
            {
                if (_root.parent != parent)
                {
                    _root.SetParent(parent, worldPositionStays: false);
                    TrackingRadar.ApplyMapLocalAnchors(_root);
                    _needsSiblingOrder = true;
                }

                if (_appliedMode != mode)
                {
                    // Defer image swap to ApplyConeVisual via _visualConfigured.
                    _appliedMode = mode;
                    _needsSiblingOrder = true;
                    _visualConfigured = false;
                    _hasAppliedColor = false;
                }

                return;
            }

            var go = new GameObject("VariaTrackingRange", typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(parent, worldPositionStays: false);
            TrackingRadar.ApplyMapLocalAnchors(_root);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.SetParent(_root, worldPositionStays: false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            _fillImage = fillGo.GetComponent<Image>();
            // ApplyConeVisual chooses the current configured FOV on first paint.
            _fillImage.raycastTarget = false;
            _fillImage.type = Image.Type.Simple;
            _fillImage.enabled = false;

            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var ringRt = ringGo.GetComponent<RectTransform>();
            ringRt.SetParent(_root, worldPositionStays: false);
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero;
            ringRt.offsetMax = Vector2.zero;
            _ringImage = ringGo.GetComponent<Image>();
            _ringImage.sprite = CircleSprites.RangeRingLarge;
            _ringImage.raycastTarget = false;
            _ringImage.enabled = false;

            _appliedMode = mode;
            _needsSiblingOrder = true;
            _hasAppliedColor = false;
            _visualConfigured = false;
            go.SetActive(false);
        }
    }
}
