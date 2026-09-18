using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace VariaTracking
{
    /// <summary>
    /// Client-side radar: throttled discovery, live candidate validation; Layout applies cone and MaxDots.
    /// XP uses in-range trackables (not cone-gated).
    /// </summary>
    internal static class TrackingRadar
    {
        public static int LastTrackableCount => Targets.Items.Count;

        public static int LastStarredInRange => Targets.StarredCount;

        private static readonly List<Character> QueryBuffer = new(64);
        private static readonly TrackingTargets Targets = new();

        private static float _refreshTimer;
        private static float _scanRadius;
        private static Minimap _boundMinimap;
        private static bool _isHidden;
        private static Player _boundPlayer;

        public static void Initialize()
        {
            CircleSprites.Initialize();
        }

        public static void Shutdown()
        {
            DestroyUi();
            Targets.Clear();
            QueryBuffer.Clear();
            _isHidden = true;
            TrackingPosture.Reset();
            TrackingExperience.Reset();
            TrackingRange.InvalidateCache();
            TrackingKnowledge.ResetSession();
            _boundPlayer = null;
            _refreshTimer = 0f;
            TrackingMapMarkers.ClearConeHysteresis();
            CircleSprites.Shutdown();
        }

        public static void HideAll()
        {
            if (_isHidden)
            {
                return;
            }

            _isHidden = true;
            _refreshTimer = 0f;
            TrackingExperience.Reset();
            TrackingPosture.Reset();
            TrackingMapMarkers.ClearConeHysteresis();
            TrackingNameTooltip.Clear();
            Targets.Clear();
            QueryBuffer.Clear();
            TrackingMapMarkers.HideAll();
            TrackingRangeRing.Hide();
        }

        public static void Tick(Player player, TrackingConfigSnapshot cfg)
        {
            Minimap minimap = Minimap.instance;
            if (player == null || player.IsDead() || player.IsTeleporting())
            {
                HideAll();
                return;
            }

            bool forceRefresh = _isHidden || !ReferenceEquals(_boundPlayer, player);
            if (!ReferenceEquals(_boundPlayer, player))
            {
                HideAll();
                TrackingKnowledge.ResetSession();
                TrackingRange.InvalidateCache();
                _boundPlayer = player;
            }
            _isHidden = false;

            float baseLevel;
            float baseRadius = TrackingRange.GetRadius(player, cfg, out baseLevel);
            TrackingUnlockState unlocks = TrackingUnlocks.GetState(baseLevel, cfg);
            TrackingKnowledge.NotifySkillLevel(unlocks.SkillLevel);

            float radius = TrackingPosture.GetEffectiveRadius(player, cfg, unlocks, out float skillLevel);
            bool pierceFog = unlocks.PierceFog;

            _refreshTimer += Time.deltaTime;
            // Include the possible posture bonus in discovery so smoothing needs no extra global scans.
            float discoveryRadius = Mathf.Max(radius, baseRadius * unlocks.PostureRangeMul);
            bool due = forceRefresh || discoveryRadius > _scanRadius || _refreshTimer >= cfg.UpdateIntervalSeconds;
            if (due)
            {
                _refreshTimer = 0f;
                _scanRadius = discoveryRadius;
                QueryBuffer.Clear();
                Character.GetCharactersInRange(player.transform.position, discoveryRadius, QueryBuffer);
            }
            Targets.Update(QueryBuffer, player, minimap, cfg, unlocks, radius);

            if (minimap == null)
            {
                TrackingMapMarkers.HideAll();
                TrackingRangeRing.Hide();
                return;
            }

            if (!ReferenceEquals(_boundMinimap, minimap))
            {
                DestroyUi();
                _boundMinimap = minimap;
                _isHidden = false;
            }

            Minimap.MapMode mode = minimap.m_mode;
            bool showThisMode =
                (mode == Minimap.MapMode.Small && cfg.ShowOnMinimap)
                || (mode == Minimap.MapMode.Large && cfg.ShowOnLargeMap);

            if (mode == Minimap.MapMode.None || !showThisMode)
            {
                TrackingNameTooltip.Clear();
                TrackingMapMarkers.HideAll();
                TrackingRangeRing.Hide();
                return;
            }

            Vector3 forward = GetTrackingForward(player);
            TrackingMapMarkers.Layout(minimap, player, Targets.Items, cfg, unlocks, pierceFog, radius, forward);
            TrackingRangeRing.Update(player, minimap, cfg, radius, unlocks.ConeDegrees, forward);

            bool tooltips = cfg.ShowTooltips && mode == Minimap.MapMode.Large;
            TrackingNameTooltip.Tick(tooltips, player, unlocks, cfg);
        }

        /// <summary>Match minimap player arrow: prefer camera yaw, else body forward.</summary>
        internal static Vector3 GetTrackingForward(Player player)
        {
            Vector3 forward;
            if (GameCamera.instance != null)
            {
                forward = GameCamera.instance.transform.forward;
            }
            else
            {
                forward = player.transform.forward;
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = player.transform.forward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        /// <summary>Hover tooltip name; allocated only when a tip is shown.</summary>
        internal static string BuildDisplayName(Character character, bool showStar)
        {
            if (character == null)
            {
                return string.Empty;
            }

            string name = character.GetHoverName();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = character.name.Replace("(Clone)", string.Empty).Trim();
            }

            if (showStar && character.GetLevel() > 1)
            {
                int stars = character.GetLevel() - 1;
                return name + " - " + stars + (stars == 1 ? " Star" : " Stars");
            }

            return name;
        }

        internal static RawImage GetMapImage(Minimap minimap)
        {
            return minimap.m_mode == Minimap.MapMode.Large
                ? minimap.m_mapImageLarge
                : minimap.m_mapImageSmall;
        }

        internal static RectTransform GetPinRoot(Minimap minimap)
        {
            return minimap.m_mode == Minimap.MapMode.Large
                ? minimap.m_pinRootLarge
                : minimap.m_pinRootSmall;
        }

        /// <summary>
        /// Vanilla MapPointToLocalGuiPos returns coordinates from the pin-root bottom-left.
        /// Center-anchored elements interpret that as an offset from mid-map (huge visual error).
        /// </summary>
        internal static void ApplyMapLocalAnchors(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void DestroyUi()
        {
            TrackingNameTooltip.Destroy();
            TrackingMapMarkers.Destroy();
            TrackingRangeRing.Destroy();
            _boundMinimap = null;
        }
    }

    [HarmonyPatch(typeof(Minimap), "OnDestroy")]
    internal static class MinimapOnDestroyPatch
    {
        private static void Prefix()
        {
            TrackingRadar.Shutdown();
        }
    }
}
