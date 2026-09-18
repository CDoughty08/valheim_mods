using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VariaTracking
{
    /// <summary>Pooled map dots + star rings; applies cone, hysteresis, MaxDots, jitter at layout.</summary>
    internal static class TrackingMapMarkers
    {
        private static readonly Color StarRingColor = new(1f, 0.95f, 0.4f, 0.9f);

        internal sealed class DotUi
        {
            public RectTransform Root;
            public Image Dot;
            public Image StarRing;
            public Character BoundCharacter;
            public bool CreatureStarred;
            public bool ShowStarInName;
            public string CachedName;
            public int CachedKnowledgeRevision;
            public int CachedLevel;
            public bool CachedCanShowName;
            public Canvas CachedCanvas;
            public float AppliedDotSize = -1f;
            public float AppliedStarSize = -1f;
            public Color AppliedColor;
            public Color AppliedStarColor;
            public bool AppliedStar;
            public bool AppliedRaycast;
            public bool AppliedStarRaycast;
            public Vector2 JitterUnit;
            public bool HasJitterUnit;
        }

        private static readonly List<DotUi> Pool = new(64);
        private static readonly List<TrackedTarget> VisibleBuffer = new(64);
        private static readonly Dictionary<int, float> LastInConeTime = new(64);
        private static readonly List<int> HysteresisRemove = new(16);
        private static int _layoutDrawn;

        public static int LayoutDrawn => _layoutDrawn;

        public static DotUi GetDrawn(int index)
        {
            return index >= 0 && index < Pool.Count ? Pool[index] : null;
        }

        public static void ClearConeHysteresis()
        {
            LastInConeTime.Clear();
        }

        public static void HideAll()
        {
            _layoutDrawn = 0;
            SetPoolActiveCount(0);
        }

        public static void Destroy()
        {
            for (int i = 0; i < Pool.Count; i++)
            {
                DotUi ui = Pool[i];
                if (ui?.Root != null)
                {
                    Object.Destroy(ui.Root.gameObject);
                }
            }

            Pool.Clear();
            VisibleBuffer.Clear();
            LastInConeTime.Clear();
            _layoutDrawn = 0;
        }

        public static void Layout(
            Minimap minimap,
            Player player,
            IReadOnlyList<TrackedTarget> tracked,
            TrackingConfigSnapshot cfg,
            TrackingUnlockState unlocks,
            bool pierceFog,
            float radius,
            Vector3 forwardXZ)
        {
            RawImage mapImage = TrackingRadar.GetMapImage(minimap);
            RectTransform pinRoot = TrackingRadar.GetPinRoot(minimap);
            if (mapImage == null || pinRoot == null || player == null)
            {
                HideAll();
                return;
            }

            Vector3 playerPos = player.transform.position;
            float now = Time.unscaledTime;
            float halfCone = unlocks.ConeDegrees * 0.5f;
            float cosHalfCone = Mathf.Cos(halfCone * Mathf.Deg2Rad);
            bool fullCircle = unlocks.ConeDegrees >= 359.5f;
            float hyst = cfg.ConeHysteresisSeconds;

            VisibleBuffer.Clear();
            HysteresisRemove.Clear();

            for (int i = 0, count = tracked.Count; i < count; i++)
            {
                TrackedTarget target = tracked[i];
                Character character = target.Character;
                // Radar already validated range, fog, and classification for this frame.
                Vector3 worldPos = target.WorldPos;

                if (!minimap.IsPointVisible(worldPos, mapImage))
                {
                    continue;
                }

                Vector3 delta = worldPos - playerPos;
                delta.y = 0f;
                bool inCone = fullCircle || TrackingGeometry.IsInsideCone(delta, forwardXZ, cosHalfCone);
                int id = target.InstanceId;

                if (inCone)
                {
                    LastInConeTime[id] = now;
                }
                else if (hyst > 0f && LastInConeTime.TryGetValue(id, out float last) && now - last <= hyst)
                {
                    // cone-exit hysteresis
                }
                else
                {
                    continue;
                }

                VisibleBuffer.Add(target);
            }

            // Prune hysteresis entries for despawned / long-gone ids (cheap bound).
            if (LastInConeTime.Count > VisibleBuffer.Count + 32)
            {
                foreach (KeyValuePair<int, float> kv in LastInConeTime)
                {
                    if (now - kv.Value > hyst + 1f)
                    {
                        HysteresisRemove.Add(kv.Key);
                    }
                }

                for (int i = 0; i < HysteresisRemove.Count; i++)
                {
                    LastInConeTime.Remove(HysteresisRemove[i]);
                }
            }

            TrackingTargets.KeepNearest(VisibleBuffer, unlocks.MaxDotsCap);
            int take = VisibleBuffer.Count;
            int drawn = 0;
            for (int i = 0; i < take; i++)
            {
                TrackedTarget target = VisibleBuffer[i];
                Character character = target.Character;
                DotUi ui = EnsureDot(drawn, pinRoot);

                Color color = TrackingDisplay.ResolveColor(target.Kind, unlocks, cfg);
                bool showStar = TrackingDisplay.ShowStarRing(target.Starred, unlocks, cfg);
                if (!ReferenceEquals(ui.BoundCharacter, character)
                    || ui.CreatureStarred != target.Starred
                    || ui.ShowStarInName != showStar)
                {
                    if (!ReferenceEquals(ui.BoundCharacter, character))
                    {
                        ui.HasJitterUnit = false;
                    }

                    ui.BoundCharacter = character;
                    ui.CreatureStarred = target.Starred;
                    ui.ShowStarInName = showStar;
                    // Names are only needed for the hovered large-map dot.
                    ui.CachedName = null;
                    EnsureJitterUnit(ui, character);
                }

                ApplyVisual(ui, color, cfg.DotSize, showStar, cfg.StarRingSize);

                Vector3 drawPos = target.WorldPos;
                if (unlocks.JitterMeters > 0.01f && ui.HasJitterUnit)
                {
                    Vector3 jittered = TrackingGeometry.JitterPosition(
                        drawPos, playerPos, ui.JitterUnit, unlocks.JitterMeters, radius);
                    // Keep the original valid position if blur crosses a visibility boundary.
                    if ((pierceFog || minimap.IsExplored(jittered))
                        && minimap.IsPointVisible(jittered, mapImage)
                        && (fullCircle || TrackingGeometry.IsInsideCone(jittered - playerPos, forwardXZ, cosHalfCone)))
                    {
                        drawPos = jittered;
                    }
                }

                minimap.WorldToMapPoint(drawPos, out float mx, out float my);
                Vector2 anchored = minimap.MapPointToLocalGuiPos(mx, my, mapImage);
                ui.Root.anchoredPosition = anchored;
                drawn++;
            }

            _layoutDrawn = drawn;
            SetPoolActiveCount(drawn);
        }

        private static void EnsureJitterUnit(DotUi ui, Character character)
        {
            if (ui.HasJitterUnit)
            {
                return;
            }

            int hash;
            ZNetView nview = character.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.GetZDO() != null)
            {
                ZDOID id = nview.GetZDO().m_uid;
                hash = id.GetHashCode();
            }
            else
            {
                hash = character.GetInstanceID();
            }

            // Deterministic unit vector in XZ from hash.
            float angle = (hash & 0xFFFF) / 65535f * Mathf.PI * 2f;
            ui.JitterUnit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ui.HasJitterUnit = true;
        }

        private static void ApplyVisual(DotUi ui, Color color, float dotSize, bool showStar, float starSize)
        {
            if (ui.AppliedColor != color)
            {
                ui.Dot.color = color;
                ui.AppliedColor = color;
            }

            if (!ui.AppliedRaycast)
            {
                ui.Dot.raycastTarget = false;
                ui.AppliedRaycast = true;
            }

            if (!Mathf.Approximately(ui.AppliedDotSize, dotSize))
            {
                ui.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dotSize);
                ui.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dotSize);
                ui.AppliedDotSize = dotSize;
            }

            if (ui.StarRing == null)
            {
                return;
            }

            if (ui.AppliedStar != showStar)
            {
                ui.StarRing.gameObject.SetActive(showStar);
                ui.AppliedStar = showStar;
            }

            if (!showStar)
            {
                return;
            }

            if (ui.AppliedStarColor != StarRingColor)
            {
                ui.StarRing.color = StarRingColor;
                ui.AppliedStarColor = StarRingColor;
            }

            if (!ui.AppliedStarRaycast)
            {
                ui.StarRing.raycastTarget = false;
                ui.AppliedStarRaycast = true;
            }

            if (!Mathf.Approximately(ui.AppliedStarSize, starSize))
            {
                RectTransform starRt = ui.StarRing.rectTransform;
                starRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, starSize);
                starRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, starSize);
                ui.AppliedStarSize = starSize;
            }
        }

        private static DotUi EnsureDot(int index, RectTransform parent)
        {
            while (Pool.Count <= index)
            {
                Pool.Add(CreateDot(parent));
            }

            DotUi ui = Pool[index];
            if (ui.Root == null)
            {
                Pool[index] = ui = CreateDot(parent);
            }
            else if (ui.Root.parent != parent)
            {
                ui.Root.SetParent(parent, worldPositionStays: false);
                TrackingRadar.ApplyMapLocalAnchors(ui.Root);
                ui.CachedCanvas = null;
            }

            return ui;
        }

        private static DotUi CreateDot(RectTransform parent)
        {
            var go = new GameObject("VariaTrackingDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var root = go.GetComponent<RectTransform>();
            root.SetParent(parent, worldPositionStays: false);
            TrackingRadar.ApplyMapLocalAnchors(root);
            root.sizeDelta = new Vector2(10f, 10f);

            var dot = go.GetComponent<Image>();
            dot.sprite = CircleSprites.Dot;
            dot.raycastTarget = false;

            var starGo = new GameObject("StarRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var starRt = starGo.GetComponent<RectTransform>();
            starRt.SetParent(root, worldPositionStays: false);
            starRt.anchorMin = new Vector2(0.5f, 0.5f);
            starRt.anchorMax = new Vector2(0.5f, 0.5f);
            starRt.pivot = new Vector2(0.5f, 0.5f);
            starRt.sizeDelta = new Vector2(16f, 16f);
            starRt.SetAsFirstSibling();

            var star = starGo.GetComponent<Image>();
            star.sprite = CircleSprites.StarRing;
            star.color = StarRingColor;
            star.raycastTarget = false;
            starGo.SetActive(false);

            return new DotUi
            {
                Root = root,
                Dot = dot,
                StarRing = star,
                AppliedStarColor = StarRingColor,
                AppliedStarRaycast = true
            };
        }

        private static void SetPoolActiveCount(int active)
        {
            for (int i = 0; i < Pool.Count; i++)
            {
                DotUi ui = Pool[i];
                if (ui?.Root == null)
                {
                    continue;
                }

                bool shouldBeActive = i < active;
                if (ui.Root.gameObject.activeSelf != shouldBeActive)
                {
                    ui.Root.gameObject.SetActive(shouldBeActive);
                }
            }
        }
    }
}
