using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Logging;
using ItemShareFix.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemShareFix
{
    internal readonly struct MarkerRenderInput
    {
        public MarkerRenderInput(
            PersonalMarkerIdentity identity,
            Vector3 worldPosition,
            int distanceMeters,
            string label,
            string itemSemanticKey,
            string className,
            MarkerClassKind kind,
            Color nativeColor,
            Sprite? nativeIcon = null,
            MarkerLifetimeKind lifetime = MarkerLifetimeKind.Permanent)
        {
            Identity = identity;
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            Label = label ?? string.Empty;
            ItemSemanticKey = itemSemanticKey ?? string.Empty;
            ClassName = className ?? "UNKNOWN";
            Kind = kind;
            NativeColor = nativeColor;
            NativeIcon = nativeIcon;
            Lifetime = lifetime;
        }

        public PersonalMarkerIdentity Identity { get; }
        public Vector3 WorldPosition { get; }
        public int DistanceMeters { get; }
        public string Label { get; }
        public string ItemSemanticKey { get; }
        public string ClassName { get; }
        public MarkerClassKind Kind { get; }
        public Color NativeColor { get; }
        public Sprite? NativeIcon { get; }
        public MarkerLifetimeKind Lifetime { get; }
        public long StableKey => ((long)(int)Identity.Kind << 32) | (uint)Identity.InstanceId;
    }

    internal readonly struct MarkerRenderDiagnostic
    {
        public MarkerRenderDiagnostic(
            MarkerRenderInput input,
            long clusterKey,
            string memberFingerprint,
            int clusterTotal,
            string semanticText,
            MarkerHudPlacement placement,
            MarkerHudVisualFootprint footprint,
            float labelPreferredWidth,
            bool usedMeasurementFallback,
            MarkerHudProjection sourceProjection)
        {
            Input = input;
            ClusterKey = clusterKey;
            MemberFingerprint = memberFingerprint ?? string.Empty;
            ClusterTotal = Math.Max(0, clusterTotal);
            SemanticText = semanticText ?? string.Empty;
            Placement = placement;
            Footprint = footprint;
            LabelPreferredWidth = labelPreferredWidth;
            UsedMeasurementFallback = usedMeasurementFallback;
            SourceProjection = sourceProjection;
        }

        public MarkerRenderInput Input { get; }
        public long ClusterKey { get; }
        public string MemberFingerprint { get; }
        public int ClusterTotal { get; }
        public string SemanticText { get; }
        public MarkerHudPlacement Placement { get; }
        public MarkerHudVisualFootprint Footprint { get; }
        public float LabelPreferredWidth { get; }
        public bool UsedMeasurementFallback { get; }
        public MarkerHudProjection SourceProjection { get; }
        public int ClusterHiddenCount => Math.Max(0, ClusterTotal - 1);
        public int StackSlot => Placement.StackSlot;
    }

    public enum MarkerIndicatorShape
    {
        AnchorDiamond,
        DirectionArrow,
    }

    public sealed class MarkerIndicatorGraphic : MaskableGraphic
    {
        private MarkerIndicatorShape _shape = MarkerIndicatorShape.AnchorDiamond;

        public MarkerIndicatorShape Shape
        {
            get => _shape;
            set
            {
                if (_shape == value) return;
                _shape = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var halfW = rect.width * 0.5f;
            var halfH = rect.height * 0.5f;
            var tint = (Color32)color;

            if (_shape == MarkerIndicatorShape.AnchorDiamond)
            {
                AddVertex(vh, 0f, halfH, tint);
                AddVertex(vh, halfW, 0f, tint);
                AddVertex(vh, 0f, -halfH, tint);
                AddVertex(vh, -halfW, 0f, tint);
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(0, 2, 3);
                return;
            }

            AddVertex(vh, 0f, halfH, tint);
            AddVertex(vh, halfW, -halfH * 0.12f, tint);
            AddVertex(vh, halfW * 0.28f, -halfH * 0.12f, tint);
            AddVertex(vh, halfW * 0.28f, -halfH, tint);
            AddVertex(vh, -halfW * 0.28f, -halfH, tint);
            AddVertex(vh, -halfW * 0.28f, -halfH * 0.12f, tint);
            AddVertex(vh, -halfW, -halfH * 0.12f, tint);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 5);
            vh.AddTriangle(0, 5, 6);
            vh.AddTriangle(2, 3, 4);
            vh.AddTriangle(2, 4, 5);
        }

        private static void AddVertex(VertexHelper vh, float x, float y, Color32 color)
            => vh.AddVert(new Vector3(x, y, 0f), color, Vector2.zero);
    }

    public sealed class MarkerAssociationCueGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var tint = (Color32)color;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax, 0f), tint, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }

    public sealed class MarkerLifetimeIndicatorGraphic : MaskableGraphic
    {
        public const string AssetSourceToken = "local-maskablegraphic-clock";
        private const int RingSegments = 20;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.46f);
            var ringThickness = Mathf.Max(1f, radius * 0.16f);
            var innerRadius = Mathf.Max(0.5f, radius - ringThickness);
            var center = rect.center;
            var tint = (Color32)color;

            for (var i = 0; i < RingSegments; i++)
            {
                var a0 = (float)(i * (Mathf.PI * 2f / RingSegments));
                var a1 = (float)((i + 1) * (Mathf.PI * 2f / RingSegments));
                var outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                var inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;
                AddQuad(vh, inner0, outer0, outer1, inner1, tint);
            }

            var handThickness = Mathf.Max(1f, radius * 0.13f);
            AddHand(vh, center, new Vector2(0f, radius * 0.55f), handThickness, tint);
            AddHand(vh, center, new Vector2(radius * 0.42f, -radius * 0.18f), handThickness, tint);

            var hub = Mathf.Max(1f, radius * 0.16f);
            AddQuad(
                vh,
                center + new Vector2(-hub, -hub),
                center + new Vector2(hub, -hub),
                center + new Vector2(hub, hub),
                center + new Vector2(-hub, hub),
                tint);
        }

        private static void AddHand(VertexHelper vh, Vector2 start, Vector2 delta, float thickness, Color32 tint)
        {
            var length = delta.magnitude;
            if (length <= 0.001f) return;
            var normal = new Vector2(-delta.y, delta.x) / length * (thickness * 0.5f);
            AddQuad(vh, start - normal, start + normal, start + delta + normal, start + delta - normal, tint);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x, a.y, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(b.x, b.y, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(c.x, c.y, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(d.x, d.y, 0f), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <summary>
    /// Renderer for world-space semantic marker clusters. Membership is owned by MarkerWorldClusterTracker and therefore depends
    /// only on stable identity + world-space relation. Projection, FOV, HUD collision and adaptive expansion are
    /// presentation-only. One retained uGUI view exists per semantic cluster, not per dense physical pickup.
    /// </summary>
    internal sealed class NativeHudMarkerRenderer : IDisposable
    {
        private sealed class BadgeView
        {
            public BadgeView(GameObject root, MarkerIndicatorGraphic diamond, MarkerLifetimeIndicatorGraphic clock, TextMeshProUGUI count)
            {
                Root = root;
                Diamond = diamond;
                Clock = clock;
                Count = count;
            }

            public GameObject Root { get; }
            public MarkerIndicatorGraphic Diamond { get; }
            public MarkerLifetimeIndicatorGraphic Clock { get; }
            public TextMeshProUGUI Count { get; }
        }

        private sealed class ItemIconView
        {
            public ItemIconView(GameObject root, Image image) { Root = root; Image = image; }
            public GameObject Root { get; }
            public Image Image { get; }
        }

        private sealed class LifetimeIndicatorView
        {
            public LifetimeIndicatorView(GameObject root, MarkerIndicatorGraphic diamond, MarkerLifetimeIndicatorGraphic clock, TextMeshProUGUI count)
            {
                Root = root;
                Diamond = diamond;
                Clock = clock;
                Count = count;
            }

            public GameObject Root { get; }
            public MarkerIndicatorGraphic Diamond { get; }
            public MarkerLifetimeIndicatorGraphic Clock { get; }
            public TextMeshProUGUI Count { get; }
        }

        private sealed class MarkerView
        {
            public MarkerView(GameObject root, RectTransform rect, CanvasGroup group, Image background, MarkerIndicatorGraphic indicator, MarkerAssociationCueGraphic associationCue, TextMeshProUGUI label)
            {
                Root = root;
                Rect = rect;
                Group = group;
                Background = background;
                Indicator = indicator;
                AssociationCue = associationCue;
                Label = label;
            }

            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public CanvasGroup Group { get; }
            public Image Background { get; }
            public MarkerIndicatorGraphic Indicator { get; }
            public MarkerAssociationCueGraphic AssociationCue { get; }
            public TextMeshProUGUI Label { get; }
            public List<BadgeView> Badges { get; } = new List<BadgeView>(MarkerClusterPresentationPolicy.MaxCompactCategoryBadges);
            public List<ItemIconView> ItemIcons { get; } = new List<ItemIconView>(MarkerClusterPresentationPolicy.MarkerDetailRowsMax);
            public List<LifetimeIndicatorView> RowLifetimeIndicators { get; } = new List<LifetimeIndicatorView>(MarkerClusterPresentationPolicy.MarkerDetailRowsMax);
            public LifetimeIndicatorView? SummaryLifetimeIndicator { get; set; }
            public bool HasMeasurement { get; set; }
            public MarkerMeasurementCacheKey MeasurementKey { get; set; }
            public VisualMeasurement CachedMeasurement { get; set; }
            public int AppliedTypographyRevision { get; set; } = int.MinValue;
            public int AppliedFontSize { get; set; } = -1;
            public bool MeasurementFallbackLogged { get; set; }
            public int LastMeasuredCompactBadgeCount { get; set; } = -1;
            public int LastMeasuredDetailedRowDiamondCount { get; set; } = -1;
            public int LastMeasuredLifetimeLayoutSignature { get; set; } = int.MinValue;
            public bool HasPresentationPlan { get; set; }
            public MarkerSemanticCluster? PresentationPlanCluster { get; set; }
            public MarkerClusterPresentationPlan? CachedPresentationPlan { get; set; }
            public MarkerPresentationMode PresentationPlanMode { get; set; }
            public bool PresentationPlanShowDistance { get; set; }
            public int PresentationPlanDetailRows { get; set; } = -1;
            public int PresentationPlanDistanceMeters { get; set; } = -1;
            public bool PresentationPlanExpanded { get; set; }
            public MarkerLanguage PresentationPlanLanguage { get; set; }
            public MarkerClusterPresentationPlan? AppliedBadgePlan { get; set; }
            public bool HasLayout { get; set; }
            public bool HasSolvedProjection { get; set; }
            public MarkerHudProjection LastSolvedProjection { get; set; }
            public bool HasPlacement { get; set; }
            public MarkerHudPlacement LastPlacement { get; set; }
            public bool HasRelativePlacement { get; set; }
            public MarkerRelativePlacement RelativePlacement { get; set; }
            public bool HasAppliedPosition { get; set; }
            public Vector2 LastAppliedPosition { get; set; }
            public bool HasAppliedRotation { get; set; }
            public float LastAppliedRotationDegrees { get; set; }
            public MarkerIndicatorShape LastAppliedShape { get; set; } = (MarkerIndicatorShape)(-1);
            public bool HasColor { get; set; }
            public Color32 LastColor { get; set; }
            public bool DiagnosticDirty { get; set; } = true;
            public bool LastDiagnosticMeasurementFallback { get; set; }
            public int LastDiagnosticClusterTotal { get; set; } = int.MinValue;
            public string LastMemberFingerprint { get; set; } = string.Empty;
            public bool HasAssociationCueVector { get; set; }
            public Vector2 LastAssociationCueVector { get; set; }
        }

        private readonly struct ClusterFrame
        {
            public ClusterFrame(
                long presentationKey,
                bool directional,
                MarkerSemanticCluster cluster,
                MarkerRenderInput representative,
                Vector3 worldAnchor,
                int distanceMeters,
                MarkerHudProjection projection,
                MarkerClusterPresentationPlan plan,
                Color mainColor,
                VisualMeasurement measurement,
                MarkerView view)
            {
                PresentationKey = presentationKey;
                Directional = directional;
                Cluster = cluster;
                Representative = representative;
                WorldAnchor = worldAnchor;
                DistanceMeters = distanceMeters;
                Projection = projection;
                Plan = plan;
                MainColor = mainColor;
                Measurement = measurement;
                View = view;
            }

            public long PresentationKey { get; }
            public bool Directional { get; }
            public MarkerSemanticCluster Cluster { get; }
            public MarkerRenderInput Representative { get; }
            public Vector3 WorldAnchor { get; }
            public int DistanceMeters { get; }
            public MarkerHudProjection Projection { get; }
            public MarkerClusterPresentationPlan Plan { get; }
            public Color MainColor { get; }
            public VisualMeasurement Measurement { get; }
            public MarkerView View { get; }
        }

        private readonly struct VisualMeasurement
        {
            public VisualMeasurement(MarkerHudVisualFootprint footprint, float labelPreferredWidth, bool usedFallback, bool measurementChanged, string renderedText)
            {
                Footprint = footprint;
                LabelPreferredWidth = labelPreferredWidth;
                UsedFallback = usedFallback;
                MeasurementChanged = measurementChanged;
                RenderedText = renderedText ?? string.Empty;
            }

            public MarkerHudVisualFootprint Footprint { get; }
            public float LabelPreferredWidth { get; }
            public bool UsedFallback { get; }
            public bool MeasurementChanged { get; }
            public string RenderedText { get; }
            public VisualMeasurement AsCacheHit() => new VisualMeasurement(Footprint, LabelPreferredWidth, UsedFallback, false, RenderedText);
        }

        private readonly ManualLogSource _log;
        private readonly MarkerRuntimePerformanceCounters _performance;
        private readonly MarkerWorldClusterTracker _semanticTracker = new MarkerWorldClusterTracker();
        private readonly MarkerDenseAreaSummaryTracker _denseTracker = new MarkerDenseAreaSummaryTracker();
        private readonly MarkerFovPresentationHysteresisPolicy _fovTracker = new MarkerFovPresentationHysteresisPolicy();
        private readonly MarkerAdaptiveLodTracker _lodTracker = new MarkerAdaptiveLodTracker();
        private readonly Dictionary<long, MarkerRenderInput> _inputByStableKey = new Dictionary<long, MarkerRenderInput>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly Dictionary<long, MarkerView> _views = new Dictionary<long, MarkerView>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerWorldMember> _worldMembers = new List<MarkerWorldMember>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<ClusterFrame> _clusterFrames = new List<ClusterFrame>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerExpansionCandidate> _expansionCandidates = new List<MarkerExpansionCandidate>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerHudPlacementCandidate> _placementCandidates = new List<MarkerHudPlacementCandidate>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerHudPlacementCandidate> _orderedPlacementBuffer = new List<MarkerHudPlacementCandidate>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerHudRect> _occupiedPlacementBuffer = new List<MarkerHudRect>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerHudPlacement> _placementResultBuffer = new List<MarkerHudPlacement>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerDirectionalInput> _directionalInputs = new List<MarkerDirectionalInput>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly Dictionary<long, MarkerDenseAreaPresentationNode> _denseNodeByKey = new Dictionary<long, MarkerDenseAreaPresentationNode>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly HashSet<long> _activeWorldPresentationKeys = new HashSet<long>();
        private readonly Dictionary<long, MarkerHudPlacement> _placementByKey = new Dictionary<long, MarkerHudPlacement>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly Dictionary<long, int> _placementRankByKey = new Dictionary<long, int>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly HashSet<long> _activeClusterKeys = new HashSet<long>();
        private readonly List<long> _staleKeys = new List<long>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerHudExclusionZone> _cachedDynamicHudZones = new List<MarkerHudExclusionZone>(2);
        private GameObject? _canvasObject;
        private Canvas? _canvas;
        private RectTransform? _canvasRect;
        private TMP_FontAsset? _nativeFont;
        private Material? _nativeFontMaterial;
        private bool _typographyResolved;
        private int _typographyRevision;
        private bool _indicatorSourceLogged;
        private bool _presentationSuppressed;
        private bool _placementCacheValid;
        private int _cachedScreenWidth = -1;
        private int _cachedScreenHeight = -1;
        private float _nextMultiMarkerMotionSolve;
        private double _nextSemanticSolveAt;
        private bool _hasSemanticInputSignature;
        private ulong _semanticInputSignature;
        private MarkerPresentationSettings _presentationSettings = new MarkerPresentationSettings(MarkerPresentationMode.Detailed, true, 1f, 5);
        private MarkerVisualConfigSnapshot _visualSettings = new MarkerVisualConfigSnapshot(
            MarkerVisualSettingsPolicy.MarkerOpacityDefault,
            MarkerVisualSettingsPolicy.BackgroundOpacityDefault,
            true,
            true,
            false,
            MarkerVisualSettingsPolicy.OffscreenScaleDefault,
            MarkerVisualSettingsPolicy.OffscreenOpacityDefault,
            MarkerVisualSettingsPolicy.OffscreenEdgePaddingDefault,
            PluginConfig.DefaultCommonColor,
            PluginConfig.DefaultUncommonColor,
            PluginConfig.DefaultLegendaryColor,
            PluginConfig.DefaultBossColor,
            PluginConfig.DefaultLunarColor,
            PluginConfig.DefaultVoidColor,
            PluginConfig.DefaultEquipmentColor,
            PluginConfig.DefaultCommandColor,
            PluginConfig.DefaultNeutralColor,
            PluginConfig.DefaultOffscreenColor);

        public NativeHudMarkerRenderer(ManualLogSource log, MarkerRuntimePerformanceCounters performance)
        {
            _log = log;
            _performance = performance ?? throw new ArgumentNullException(nameof(performance));
        }

        public void InvalidatePresentationSettings(MarkerPresentationSettings settings, MarkerVisualConfigSnapshot visualSettings)
        {
            _presentationSettings = settings;
            _visualSettings = visualSettings;
            _placementCacheValid = false;
            foreach (var view in _views.Values)
            {
                view.HasMeasurement = false;
                view.HasLayout = false;
                view.HasPresentationPlan = false;
                view.DiagnosticDirty = true;
            }
            _log.LogInfo("ISF_MARKER_PRESENTATION_CONFIG mode=" + settings.Mode
                + " distance=" + settings.ShowDistance
                + " scale=" + settings.Scale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " detailRows=" + settings.DetailRows
                + " categorySort=" + settings.CategorySortOrder
                + " categorySummary=" + settings.UseCategorySummaryPresentation
                + " compactMixed=" + settings.CompactMixedStyle
                + " compactCount=" + settings.CompactShowCount
                + " offscreen=" + visualSettings.OffscreenEnabled
                + " membershipUnchanged=true physicalClusters=" + _semanticTracker.Clusters.Count
                + " denseNodes=" + _denseTracker.Nodes.Count);
        }

        public void Render(
            Camera camera,
            IReadOnlyList<MarkerRenderInput> inputs,
            MarkerPresentationSettings settings,
            MarkerVisualConfigSnapshot visualSettings,
            MarkerLanguage language,
            Action<MarkerRenderDiagnostic> diagnosticSink,
            IReadOnlyList<MarkerHudExclusionZone>? dynamicHudZones = null)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (diagnosticSink == null) throw new ArgumentNullException(nameof(diagnosticSink));
            if (_presentationSuppressed) return;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                Clear();
                return;
            }

            EnsureCanvas();
            if (_canvas == null || _canvasRect == null) return;
            if (!_canvas.enabled) _canvas.enabled = true;
            if (!_typographyResolved) ResolveNativeTypography();

            if (!SettingsEqual(_presentationSettings, settings) || !VisualSettingsEqual(_visualSettings, visualSettings))
                InvalidatePresentationSettings(settings, visualSettings);

            var screenChanged = _cachedScreenWidth != Screen.width || _cachedScreenHeight != Screen.height;
            if (screenChanged)
            {
                _cachedScreenWidth = Screen.width;
                _cachedScreenHeight = Screen.height;
                _placementCacheValid = false;
            }
            var hudChanged = DynamicHudZonesChanged(dynamicHudZones);
            if (hudChanged) _placementCacheValid = false;

            _inputByStableKey.Clear();
            var signature = ComputeSemanticInputSignature(inputs);
            for (var i = 0; i < inputs.Count && i < MarkerPresentationPolicy.MaxLogicalMarkers; i++)
            {
                var input = inputs[i];
                if (!IsFinite(input.WorldPosition.x) || !IsFinite(input.WorldPosition.y) || !IsFinite(input.WorldPosition.z)) continue;
                _inputByStableKey[input.StableKey] = input;
            }

            var now = (double)Time.unscaledTime;
            var structuralMembershipInputChanged = !_hasSemanticInputSignature || signature != _semanticInputSignature;
            var semanticDue = structuralMembershipInputChanged || now >= _nextSemanticSolveAt || _semanticTracker.Clusters.Count == 0;
            var denseMembershipChanged = false;
            if (semanticDue)
            {
                BuildWorldMembers(language);
                var update = _semanticTracker.Update(_worldMembers, now);
                var denseUpdate = _denseTracker.Update(_semanticTracker.Clusters, now);
                denseMembershipChanged = denseUpdate.MembershipChanged;
                _semanticInputSignature = signature;
                _hasSemanticInputSignature = true;
                _nextSemanticSolveAt = now + MarkerWorldClusterTracker.RecommendedSemanticSolveIntervalSeconds;
                EmitSemanticLifecycle(update);
                EmitDenseLifecycle(denseUpdate);
                if (update.MembershipChanged || denseMembershipChanged || structuralMembershipInputChanged) _placementCacheValid = false;
            }

            ClassifyPresentationNodes(camera);
            BuildExpansionCandidates(camera);
            var expandedKey = _lodTracker.Update(_expansionCandidates, now);

            _clusterFrames.Clear();
            _placementCandidates.Clear();
            _placementByKey.Clear();
            _activeClusterKeys.Clear();
            var structuralInvalidated = screenChanged || hudChanged || structuralMembershipInputChanged || denseMembershipChanged;
            var projectionInvalidated = false;

            var nodes = _denseTracker.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!_activeWorldPresentationKeys.Contains(node.StableKey)) continue;
                if (!TryBuildCurrentClusterFrame(camera, node, expandedKey.HasValue && expandedKey.Value == node.StableKey, language, out var frame))
                    continue;

                if (frame.Measurement.MeasurementChanged) structuralInvalidated = true;
                if (!frame.View.HasSolvedProjection || MarkerFramePipelinePolicy.ProjectionMateriallyChanged(frame.View.LastSolvedProjection, frame.Projection))
                    projectionInvalidated = true;
                if (!string.Equals(frame.View.LastMemberFingerprint, node.MemberFingerprint, StringComparison.Ordinal))
                {
                    frame.View.LastMemberFingerprint = node.MemberFingerprint;
                    frame.View.DiagnosticDirty = true;
                    structuralInvalidated = true;
                }

                _clusterFrames.Add(frame);
                _placementCandidates.Add(new MarkerHudPlacementCandidate(frame.PresentationKey, frame.Projection, frame.Measurement.Footprint));
                _activeClusterKeys.Add(frame.PresentationKey);
            }

            if (_visualSettings.OffscreenEnabled && _directionalInputs.Count > 0)
            {
                var sectors = MarkerDirectionalAggregationPolicy.Aggregate(_directionalInputs);
                for (var i = 0; i < sectors.Count; i++)
                {
                    if (!TryBuildDirectionalFrame(sectors[i], language, out var frame)) continue;
                    if (frame.Measurement.MeasurementChanged) structuralInvalidated = true;
                    if (!frame.View.HasSolvedProjection || MarkerFramePipelinePolicy.ProjectionMateriallyChanged(frame.View.LastSolvedProjection, frame.Projection))
                        projectionInvalidated = true;
                    _clusterFrames.Add(frame);
                    _placementCandidates.Add(new MarkerHudPlacementCandidate(frame.PresentationKey, frame.Projection, frame.Measurement.Footprint));
                    _activeClusterKeys.Add(frame.PresentationKey);
                }
            }

            if (_clusterFrames.Count != _views.Count) structuralInvalidated = true;
            ResolveCurrentPlacements(structuralInvalidated, projectionInvalidated);

            for (var i = 0; i < _clusterFrames.Count; i++)
            {
                var frame = _clusterFrames[i];
                if (!_placementByKey.TryGetValue(frame.PresentationKey, out var placement)) continue;
                var fastFollow = frame.View.HasSolvedProjection
                    && MarkerProjectionRelativePlacementPolicy.RequiresFastFollow(frame.View.LastSolvedProjection, frame.Projection, Screen.width, Screen.height);
                ApplyView(frame.View, frame.Cluster, frame.Representative, frame.Plan, frame.MainColor, frame.Projection, placement, frame.Measurement, fastFollow, frame.Directional);
                frame.View.LastSolvedProjection = frame.Projection;
                frame.View.HasSolvedProjection = true;

                if (frame.View.LastDiagnosticClusterTotal != frame.Cluster.TotalCount)
                {
                    frame.View.LastDiagnosticClusterTotal = frame.Cluster.TotalCount;
                    frame.View.DiagnosticDirty = true;
                }
                if (frame.View.LastDiagnosticMeasurementFallback != frame.Measurement.UsedFallback)
                {
                    frame.View.LastDiagnosticMeasurementFallback = frame.Measurement.UsedFallback;
                    frame.View.DiagnosticDirty = true;
                }
                if (frame.View.DiagnosticDirty)
                {
                    frame.View.DiagnosticDirty = false;
                    diagnosticSink(new MarkerRenderDiagnostic(
                        frame.Representative,
                        frame.PresentationKey,
                        frame.Directional ? "DIRECTION:" + frame.PresentationKey : frame.Cluster.MemberFingerprint,
                        frame.Cluster.TotalCount,
                        frame.Plan.Text,
                        placement,
                        frame.Measurement.Footprint,
                        frame.Measurement.LabelPreferredWidth,
                        frame.Measurement.UsedFallback,
                        frame.Projection));
                }
            }

            _staleKeys.Clear();
            foreach (var key in _views.Keys)
                if (!_activeClusterKeys.Contains(key)) _staleKeys.Add(key);
            if (_staleKeys.Count > 0) _placementCacheValid = false;
            for (var i = 0; i < _staleKeys.Count; i++) RemoveView(_staleKeys[i]);

            _staleKeys.Clear();
            foreach (var key in _placementRankByKey.Keys)
                if (!_activeClusterKeys.Contains(key)) _staleKeys.Add(key);
            for (var i = 0; i < _staleKeys.Count; i++) _placementRankByKey.Remove(_staleKeys[i]);
        }

        private void EmitDenseLifecycle(MarkerDenseAreaUpdate update)
        {
            if (!update.MembershipChanged) return;
            var summaryCount = 0;
            for (var i = 0; i < update.Nodes.Count; i++) if (update.Nodes[i].IsDenseSummary) summaryCount++;
            _log.LogInfo("ISF_MARKER_DENSE nodes=" + update.Nodes.Count
                + " summaries=" + summaryCount
                + " merge=" + MarkerDenseAreaSummaryTracker.MergeRadiusMeters.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " split=" + MarkerDenseAreaSummaryTracker.SplitRadiusMeters.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " dwell=" + MarkerDenseAreaSummaryTracker.ThresholdTransitionDwellSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " membershipChanged=true cameraIndependent=true");
        }

        private void BuildWorldMembers(MarkerLanguage language)
        {
            _worldMembers.Clear();
            foreach (var pair in _inputByStableKey)
            {
                var input = pair.Value;
                _worldMembers.Add(new MarkerWorldMember(
                    input.StableKey,
                    input.Identity.Kind,
                    new MarkerWorldPoint(input.WorldPosition.x, input.WorldPosition.y, input.WorldPosition.z),
                    input.ItemSemanticKey,
                    MarkerPresentationPolicy.NormalizeLabel(input.Label, MarkerTextLocalization.FallbackPickup(language)),
                    input.Kind,
                    input.Lifetime));
            }
        }

        private void ClassifyPresentationNodes(Camera camera)
        {
            _directionalInputs.Clear();
            _denseNodeByKey.Clear();
            _activeWorldPresentationKeys.Clear();
            var nodes = _denseTracker.Nodes;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.5f;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                _denseNodeByKey[node.StableKey] = node;
                if (!TryCurrentWorldAnchor(node.PresentationCluster, out var anchor)) continue;
                if (!TryProject(camera, anchor, out var projection)) continue;
                var toTarget = anchor - camera.transform.position;
                var distance = toTarget.magnitude;
                if (!IsFinite(distance) || distance < 0f) continue;
                var angle = Vector3.Angle(camera.transform.forward, toTarget);
                var projectedInside = projection.Mode == MarkerHudMode.OnScreenWorldAnchor;
                var inFov = _fovTracker.Update(node.StableKey, angle, projectedInside);
                if (inFov)
                {
                    _activeWorldPresentationKeys.Add(node.StableKey);
                    continue;
                }

                if (!_visualSettings.OffscreenEnabled) continue;
                var directionX = projection.DirectionX;
                var directionY = projection.DirectionY;
                if (Math.Abs(directionX) < 0.0001f && Math.Abs(directionY) < 0.0001f)
                {
                    directionX = (projection.X - centerX) / Math.Max(1f, centerX);
                    directionY = (projection.Y - centerY) / Math.Max(1f, centerY);
                }
                _directionalInputs.Add(new MarkerDirectionalInput(
                    node.StableKey,
                    directionX,
                    directionY,
                    distance,
                    node.TotalCount));
            }

            _fovTracker.Prune(_denseNodeByKey.Keys);
        }

        private void BuildExpansionCandidates(Camera camera)
        {
            _expansionCandidates.Clear();
            var nodes = _denseTracker.Nodes;
            var resolutionScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (!IsFinite(resolutionScale) || resolutionScale <= 0f) resolutionScale = 1f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.5f;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!_activeWorldPresentationKeys.Contains(node.StableKey)) continue;
                var cluster = node.PresentationCluster;
                if (!TryCurrentWorldAnchor(cluster, out var anchor)) continue;
                if (!TryProject(camera, anchor, out var projection)) continue;
                var distance = Vector3.Distance(camera.transform.position, anchor);
                if (!IsFinite(distance) || distance < 0f) continue;
                var dx = projection.X - centerX;
                var dy = projection.Y - centerY;
                var reticle1080 = Mathf.Sqrt(dx * dx + dy * dy) / resolutionScale;
                _expansionCandidates.Add(new MarkerExpansionCandidate(node.StableKey, cluster.TotalCount, distance, reticle1080));
            }
        }

        private bool TryBuildCurrentClusterFrame(
            Camera camera,
            MarkerDenseAreaPresentationNode node,
            bool expanded,
            MarkerLanguage language,
            out ClusterFrame frame)
        {
            frame = default;
            var cluster = node.PresentationCluster;
            if (!TryCurrentWorldAnchor(cluster, out var anchor)) return false;
            if (!TryProject(camera, anchor, out var projection)) return false;
            var distanceFloat = Vector3.Distance(camera.transform.position, anchor);
            if (!IsFinite(distanceFloat) || distanceFloat < 0f) return false;
            var distance = Mathf.Max(0, Mathf.RoundToInt(distanceFloat));
            if (!TryRepresentative(cluster, out var representative)) return false;

            var view = GetOrCreateView(node.StableKey);
            var plan = GetOrBuildPresentationPlan(view, cluster, distance, expanded, language);
            var measurement = GetOrMeasureVisualFootprint(view, cluster, plan, _presentationSettings);
            var mainColor = ResolveMainColor(cluster, representative, plan.NeutralMainSemantic);
            frame = new ClusterFrame(node.StableKey, false, cluster, representative, anchor, distance, projection, plan, mainColor, measurement, view);
            return true;
        }

        private bool TryBuildDirectionalFrame(MarkerDirectionalSectorSummary sector, MarkerLanguage language, out ClusterFrame frame)
        {
            frame = default;
            if (!_denseNodeByKey.TryGetValue(sector.NearestPresentationKey, out var nearestNode)) return false;
            var cluster = nearestNode.PresentationCluster;
            if (!TryRepresentative(cluster, out var representative)) return false;

            var projection = BuildDirectionalProjection(sector.DirectionX, sector.DirectionY, _visualSettings.OffscreenEdgePadding);
            if (!projection.Valid) return false;
            var distance = Mathf.Max(0, Mathf.RoundToInt(sector.NearestDistanceMeters));
            var view = GetOrCreateView(sector.PresentationKey);
            var exactSingleClusterLifetime = sector.RepresentedNodeCount == 1;
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(
                distance,
                sector.TotalCount,
                _visualSettings.ShowOffscreenDistance,
                _visualSettings.ShowOffscreenTotalCount,
                language,
                exactSingleClusterLifetime ? cluster.LifetimeSummary : MarkerLifetimeKind.Unknown,
                exactSingleClusterLifetime ? cluster.TemporaryPhysicalMemberCount : 0,
                exactSingleClusterLifetime ? cluster.MixedLifetimeMemberCount : 0,
                exactSingleClusterLifetime ? cluster.UnknownLifetimeMemberCount : 1);
            var measurementSettings = new MarkerPresentationSettings(MarkerPresentationMode.Compact, false, _visualSettings.OffscreenScale, 1);
            var measurement = GetOrMeasureVisualFootprint(view, cluster, plan, measurementSettings);
            var anchor = new Vector3(cluster.WorldAnchor.X, cluster.WorldAnchor.Y, cluster.WorldAnchor.Z);
            frame = new ClusterFrame(sector.PresentationKey, true, cluster, representative, anchor, distance, projection, plan, _visualSettings.OffscreenColor, measurement, view);
            return true;
        }

        private static MarkerHudProjection BuildDirectionalProjection(float directionX, float directionY, float edgePadding)
        {
            if (!IsFinite(directionX) || !IsFinite(directionY)) return default;
            var projection = MarkerHudNavigationPolicy.ResolveProjection(
                0.5f + directionX * 2f,
                0.5f + directionY * 2f,
                1f,
                directionX,
                directionY,
                1f,
                Screen.width,
                Screen.height);
            if (!projection.Valid) return projection;

            var padding = MarkerVisualSettingsPolicy.ClampOffscreenEdgePadding(edgePadding);
            var x = Mathf.Clamp(projection.X, padding, Math.Max(padding, Screen.width - padding));
            var y = Mathf.Clamp(projection.Y, padding, Math.Max(padding, Screen.height - padding));
            return new MarkerHudProjection(
                true,
                MarkerHudMode.OffScreenEdge,
                projection.Edge,
                x,
                y,
                directionX,
                directionY,
                projection.ArrowRotationDegrees);
        }

        private MarkerClusterPresentationPlan GetOrBuildPresentationPlan(
            MarkerView view,
            MarkerSemanticCluster cluster,
            int distanceMeters,
            bool expanded,
            MarkerLanguage language)
        {
            var effectiveDistance = _presentationSettings.ShowDistance ? distanceMeters : -1;
            if (view.HasPresentationPlan
                && ReferenceEquals(view.PresentationPlanCluster, cluster)
                && view.PresentationPlanMode == _presentationSettings.Mode
                && view.PresentationPlanShowDistance == _presentationSettings.ShowDistance
                && view.PresentationPlanDetailRows == _presentationSettings.DetailRows
                && view.PresentationPlanDistanceMeters == effectiveDistance
                && view.PresentationPlanExpanded == expanded
                && view.PresentationPlanLanguage == language
                && view.CachedPresentationPlan != null)
            {
                return view.CachedPresentationPlan;
            }

            var plan = MarkerClusterPresentationPolicy.Build(cluster, _presentationSettings, distanceMeters, expanded, language);
            view.HasPresentationPlan = true;
            view.PresentationPlanCluster = cluster;
            view.CachedPresentationPlan = plan;
            view.PresentationPlanMode = _presentationSettings.Mode;
            view.PresentationPlanShowDistance = _presentationSettings.ShowDistance;
            view.PresentationPlanDetailRows = _presentationSettings.DetailRows;
            view.PresentationPlanDistanceMeters = effectiveDistance;
            view.PresentationPlanExpanded = expanded;
            view.PresentationPlanLanguage = language;
            return plan;
        }

        private void ResolveCurrentPlacements(bool structuralInvalidated, bool projectionInvalidated)
        {
            if (_clusterFrames.Count == 0)
            {
                _placementCacheValid = false;
                return;
            }

            if (_clusterFrames.Count == 1)
            {
                _performance.RecordSingleMarkerFastPath();
                var frame = _clusterFrames[0];
                MarkerHudPlacement placement;
                var relativeUsable = _placementCacheValid && frame.View.HasRelativePlacement && !structuralInvalidated;
                if (relativeUsable)
                {
                    placement = MarkerProjectionRelativePlacementPolicy.Apply(
                        frame.PresentationKey,
                        frame.Projection,
                        frame.Measurement.Footprint,
                        frame.View.RelativePlacement);
                    if (!PlacementStillValid(placement))
                    {
                        placement = MarkerHudNavigationPolicy.ResolveSinglePlacement(_placementCandidates[0], Screen.width, Screen.height, _cachedDynamicHudZones);
                        StoreSolvedPlacement(frame.View, frame.Projection, placement);
                    }
                }
                else
                {
                    placement = MarkerHudNavigationPolicy.ResolveSinglePlacement(_placementCandidates[0], Screen.width, Screen.height, _cachedDynamicHudZones);
                    StoreSolvedPlacement(frame.View, frame.Projection, placement);
                }
                _placementByKey[frame.PresentationKey] = placement;
                _placementRankByKey[frame.PresentationKey] = 0;
                _placementCacheValid = true;
                return;
            }

            var now = Time.unscaledTime;
            var anyRelativeInvalid = false;
            if (_placementCacheValid && !structuralInvalidated)
            {
                for (var i = 0; i < _clusterFrames.Count; i++)
                {
                    var frame = _clusterFrames[i];
                    if (!frame.View.HasRelativePlacement) { anyRelativeInvalid = true; break; }
                    if (frame.View.HasSolvedProjection
                        && (frame.View.LastSolvedProjection.Mode != frame.Projection.Mode || frame.View.LastSolvedProjection.Edge != frame.Projection.Edge))
                    {
                        anyRelativeInvalid = true;
                        break;
                    }
                    var provisional = MarkerProjectionRelativePlacementPolicy.Apply(
                        frame.PresentationKey,
                        frame.Projection,
                        frame.Measurement.Footprint,
                        frame.View.RelativePlacement);
                    if (!PlacementStillValid(provisional)) { anyRelativeInvalid = true; break; }
                }
            }

            var runFullSolve = MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(
                _placementCacheValid,
                structuralInvalidated || anyRelativeInvalid,
                projectionInvalidated,
                now,
                _nextMultiMarkerMotionSolve);
            if (runFullSolve)
            {
                var started = Stopwatch.GetTimestamp();
                MarkerHudNavigationPolicy.ResolvePlacementsBufferedStable(
                    _placementCandidates,
                    Screen.width,
                    Screen.height,
                    _cachedDynamicHudZones,
                    _placementRankByKey,
                    _orderedPlacementBuffer,
                    _occupiedPlacementBuffer,
                    _placementResultBuffer);
                _performance.RecordFullPlacementSolve(Stopwatch.GetTimestamp() - started);
                _nextMultiMarkerMotionSolve = now + MarkerFramePipelinePolicy.MultiMarkerMotionSolveIntervalSeconds;
                for (var i = 0; i < _orderedPlacementBuffer.Count; i++) _placementRankByKey[_orderedPlacementBuffer[i].StableKey] = i;
                for (var i = 0; i < _placementResultBuffer.Count; i++)
                {
                    var placement = _placementResultBuffer[i];
                    _placementByKey[placement.StableKey] = placement;
                }
                for (var i = 0; i < _clusterFrames.Count; i++)
                {
                    var frame = _clusterFrames[i];
                    if (_placementByKey.TryGetValue(frame.PresentationKey, out var placement))
                        StoreSolvedPlacement(frame.View, frame.Projection, placement);
                }
                _placementCacheValid = true;
                return;
            }

            for (var i = 0; i < _clusterFrames.Count; i++)
            {
                var frame = _clusterFrames[i];
                if (!frame.View.HasRelativePlacement) continue;
                var placement = MarkerProjectionRelativePlacementPolicy.Apply(
                    frame.PresentationKey,
                    frame.Projection,
                    frame.Measurement.Footprint,
                    frame.View.RelativePlacement);
                _placementByKey[frame.PresentationKey] = placement;
            }
        }

        private void StoreSolvedPlacement(MarkerView view, MarkerHudProjection projection, MarkerHudPlacement placement)
        {
            if (!view.HasPlacement || PlacementDiagnosticStateChanged(view.LastPlacement, placement)) view.DiagnosticDirty = true;
            view.LastPlacement = placement;
            view.HasPlacement = true;
            view.RelativePlacement = MarkerProjectionRelativePlacementPolicy.Capture(projection, placement);
            view.HasRelativePlacement = true;
        }

        private bool PlacementStillValid(MarkerHudPlacement placement)
        {
            var rect = placement.FinalRect;
            if (rect.Left < 0f || rect.Right > Screen.width || rect.Bottom < 0f || rect.Top > Screen.height) return false;
            if (MarkerHudNavigationPolicy.IntersectsReservedHud(rect, Screen.width, Screen.height)) return false;
            if (MarkerHudNavigationPolicy.IntersectsDynamicHud(rect, _cachedDynamicHudZones, Screen.width, Screen.height)) return false;
            return true;
        }

        private bool TryCurrentWorldAnchor(MarkerSemanticCluster cluster, out Vector3 anchor)
        {
            anchor = default;
            if (cluster.MemberStableKeys.Count == 0) return false;
            double x = 0d, y = 0d, z = 0d;
            var count = 0;
            for (var i = 0; i < cluster.MemberStableKeys.Count; i++)
            {
                if (!_inputByStableKey.TryGetValue(cluster.MemberStableKeys[i], out var input)) continue;
                x += input.WorldPosition.x;
                y += input.WorldPosition.y;
                z += input.WorldPosition.z;
                count++;
            }
            if (count <= 0) return false;
            anchor = new Vector3((float)(x / count), (float)(y / count), (float)(z / count));
            return IsFinite(anchor.x) && IsFinite(anchor.y) && IsFinite(anchor.z);
        }

        private static bool TryProject(Camera camera, Vector3 worldAnchor, out MarkerHudProjection projection)
        {
            projection = default;
            try
            {
                var viewport = camera.WorldToViewportPoint(worldAnchor);
                var local = camera.transform.InverseTransformPoint(worldAnchor);
                projection = MarkerHudNavigationPolicy.ResolveProjection(
                    viewport.x, viewport.y, viewport.z,
                    local.x, local.y, local.z,
                    Screen.width, Screen.height);
                return projection.Valid;
            }
            catch
            {
                return false;
            }
        }

        private bool TryRepresentative(MarkerSemanticCluster cluster, out MarkerRenderInput representative)
        {
            for (var i = 0; i < cluster.MemberStableKeys.Count; i++)
            {
                if (_inputByStableKey.TryGetValue(cluster.MemberStableKeys[i], out representative)) return true;
            }
            representative = default;
            return false;
        }

        private Color ResolveMainColor(MarkerSemanticCluster cluster, MarkerRenderInput representative, bool neutral)
        {
            if (neutral || cluster.IsMixedCategory) return SanitizeColor(_visualSettings.NeutralColor);
            return ResolveConfiguredCategoryColor(cluster.HomogeneousCategory);
        }

        private Color ResolveCategoryColor(MarkerSemanticCluster cluster, MarkerSemanticCategory category)
            => ResolveConfiguredCategoryColor(category);

        private Color ResolveConfiguredCategoryColor(MarkerSemanticCategory category)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier1: return SanitizeColor(_visualSettings.CommonColor);
                case MarkerSemanticCategory.Tier2: return SanitizeColor(_visualSettings.UncommonColor);
                case MarkerSemanticCategory.Tier3: return SanitizeColor(_visualSettings.LegendaryColor);
                case MarkerSemanticCategory.Boss: return SanitizeColor(_visualSettings.BossColor);
                case MarkerSemanticCategory.Lunar:
                case MarkerSemanticCategory.LunarEquipment: return SanitizeColor(_visualSettings.LunarColor);
                case MarkerSemanticCategory.Void: return SanitizeColor(_visualSettings.VoidColor);
                case MarkerSemanticCategory.Equipment: return SanitizeColor(_visualSettings.EquipmentColor);
                case MarkerSemanticCategory.CommandState: return SanitizeColor(_visualSettings.CommandColor);
                case MarkerSemanticCategory.Other:
                case MarkerSemanticCategory.Unknown:
                default: return SanitizeColor(_visualSettings.NeutralColor);
            }
        }

        private VisualMeasurement GetOrMeasureVisualFootprint(MarkerView view, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, MarkerPresentationSettings settings)
        {
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, settings.Scale);
            EnsureTypography(view, fontSize);
            var compactBadgeCount = settings.Mode == MarkerPresentationMode.Compact && plan.ShowCompactCategoryDiamonds ? plan.CategoryEntries.Count : 0;
            var detailedRowDiamondCount = settings.Mode == MarkerPresentationMode.Detailed && plan.ShowDetailedCategoryRowDiamonds ? plan.CategoryEntries.Count : 0;
            var detailedItemRowCount = settings.Mode == MarkerPresentationMode.Detailed ? plan.DetailedItemRows.Count : 0;
            var lifetimeLayoutSignature = LifetimeLayoutSignature(plan, compactBadgeCount, detailedRowDiamondCount, detailedItemRowCount);
            var detailedItemWidthLimit = detailedItemRowCount > 0
                ? MarkerClusterPresentationPolicy.BuildDetailedItemLabelWidthLimit(Screen.width, Screen.height, settings.Scale)
                : float.PositiveInfinity;
            var renderedText = BuildRenderedText(view.Label, cluster, plan, detailedItemWidthLimit);
            var desiredOverflow = detailedItemRowCount > 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            if (view.Label.overflowMode != desiredOverflow)
            {
                view.Label.overflowMode = desiredOverflow;
                view.HasMeasurement = false;
            }
            var measurementKey = new MarkerMeasurementCacheKey(
                renderedText,
                InstanceIdentity(view.Label.font),
                InstanceIdentity(view.Label.fontSharedMaterial),
                fontSize,
                Screen.width,
                Screen.height,
                _typographyRevision);
            if (view.LastMeasuredCompactBadgeCount == compactBadgeCount
                && view.LastMeasuredDetailedRowDiamondCount == detailedRowDiamondCount
                && view.LastMeasuredLifetimeLayoutSignature == lifetimeLayoutSignature
                && MarkerRuntimeHotPathPolicy.CanReuseMeasurement(view.HasMeasurement, view.MeasurementKey, measurementKey))
                return view.CachedMeasurement.AsCacheHit();

            if (!string.Equals(view.Label.text, renderedText, StringComparison.Ordinal)) view.Label.text = renderedText;
            var preferredWidth = float.NaN;
            var preferredHeight = float.NaN;
            var usedFallback = false;
            try
            {
                _performance.RecordTmpPreferredMeasurement();
                var preferred = view.Label.GetPreferredValues(renderedText);
                preferredWidth = preferred.x;
                preferredHeight = preferred.y;
                if (detailedItemRowCount > 0 && IsFinite(preferredWidth) && preferredWidth > detailedItemWidthLimit)
                    preferredWidth = detailedItemWidthLimit;
                if (!IsFinite(preferredWidth) || preferredWidth <= 0f || !IsFinite(preferredHeight) || preferredHeight <= 0f) usedFallback = true;
            }
            catch (Exception ex)
            {
                usedFallback = true;
                if (!view.MeasurementFallbackLogged)
                {
                    view.MeasurementFallbackLogged = true;
                    _log.LogDebug("[ItemShareFix] TMP preferred-size unavailable; conservative footprint used: " + ex.GetType().Name);
                }
            }

            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(
                preferredWidth, preferredHeight, Screen.width, Screen.height, settings.Scale);
            if (compactBadgeCount > 0)
            {
                footprint = EnsureCompactMetadataRowFootprint(
                    footprint, plan, fontSize, settings.Scale, preferredWidth);
                footprint = ExtendFootprintForCompactBadges(footprint, plan, fontSize, settings.Scale);
            }
            else if (detailedRowDiamondCount > 0)
            {
                footprint = ExtendFootprintForDetailedRowDiamonds(
                    footprint,
                    detailedRowDiamondCount,
                    fontSize,
                    settings.Scale,
                    HasDetailedCategoryLifetimeIndicator(plan),
                    MaxDetailedCategoryLifetimeCountCharacters(plan));
            }
            else if (detailedItemRowCount > 0)
            {
                footprint = ExtendFootprintForDetailedItemIcons(
                    footprint,
                    detailedItemRowCount,
                    fontSize,
                    settings.Scale,
                    HasDetailedItemLifetimeIndicator(plan));
            }
            else if (settings.Mode == MarkerPresentationMode.Detailed
                && plan.CountRenderSource == MarkerCountRenderSource.DetailedCategoryRows
                && !plan.ShowMainDiamond)
            {
                footprint = CollapseDetailedFootprintWithoutDiamondGutter(footprint);
            }

            if (plan.LifetimeIndicator.Visible && compactBadgeCount == 0 && detailedRowDiamondCount == 0 && detailedItemRowCount == 0)
                footprint = ExtendFootprintForSummaryLifetimeIndicator(footprint, fontSize, settings.Scale, plan.LifetimeIndicator.ShowCount);

            var measurement = new VisualMeasurement(footprint, preferredWidth, usedFallback, true, renderedText);
            view.MeasurementKey = measurementKey;
            view.CachedMeasurement = measurement;
            view.LastMeasuredCompactBadgeCount = compactBadgeCount;
            view.LastMeasuredDetailedRowDiamondCount = detailedRowDiamondCount;
            view.LastMeasuredLifetimeLayoutSignature = lifetimeLayoutSignature;
            view.HasMeasurement = true;
            return measurement;
        }

        private static float CompactTextBandHeight(string text, int fontSize, float indicatorSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var lineHeight = Math.Max(indicatorSize * 0.72f, fontSize * 1.18f);
            return Math.Max(1, CountPresentationLines(text)) * lineHeight;
        }

        private static int LifetimeLayoutSignature(
            MarkerClusterPresentationPlan plan,
            int compactBadgeCount,
            int detailedRowDiamondCount,
            int detailedItemRowCount)
        {
            unchecked
            {
                var signature = plan.LifetimeIndicator.Visible ? 17 : 3;
                signature = signature * 31 + (plan.LifetimeIndicator.ShowCount ? plan.LifetimeIndicator.TemporaryCount + 1 : 0);
                signature = signature * 31 + compactBadgeCount;
                signature = signature * 31 + detailedRowDiamondCount;
                signature = signature * 31 + detailedItemRowCount;
                for (var i = 0; i < plan.DetailedItemRows.Count; i++)
                    signature = signature * 31 + (int)plan.DetailedItemRows[i].GlyphKind + 1;
                for (var i = 0; i < plan.CategoryEntries.Count; i++)
                {
                    var entry = plan.CategoryEntries[i];
                    signature = signature * 31 + (int)entry.GlyphKind + 1;
                    signature = signature * 31 + entry.Count;
                }
                return signature;
            }
        }

        private static bool HasDetailedItemLifetimeIndicator(MarkerClusterPresentationPlan plan)
            => plan.DetailedItemRows.Count > 0;

        private static bool HasDetailedCategoryLifetimeIndicator(MarkerClusterPresentationPlan plan)
            => false;

        private static int MaxDetailedCategoryLifetimeCountCharacters(MarkerClusterPresentationPlan plan)
            => 0;


        private static float LifetimeIndicatorSize(int fontSize, float markerScale)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            return Mathf.Max(10f * scale, fontSize * 0.66f);
        }

        private static float LifetimeCountWidth(int fontSize, int characters)
            => Math.Max(0, characters) * Math.Max(4f, fontSize * 0.48f);

        private static float CompactMetadataRowHeight(string text, int fontSize, float indicatorSize, float markerScale, bool showLifetimeIndicator)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var textHeight = CompactTextBandHeight(text, fontSize, indicatorSize);
            var lifetimeHeight = showLifetimeIndicator
                ? LifetimeIndicatorSize(fontSize, markerScale) + 4f * scale
                : 0f;
            return Math.Max(textHeight, lifetimeHeight);
        }

        private static float CompactMetadataDistanceWidth(string text, float preferredWidth, MarkerHudVisualFootprint footprint)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (IsFinite(preferredWidth) && preferredWidth > 0f) return Math.Min(footprint.LabelWidth, preferredWidth);
            return footprint.LabelWidth;
        }

        private static float CompactMetadataGap(MarkerHudVisualFootprint footprint)
            => Math.Max(3f, footprint.Gap * 0.55f);

        private static float CompactMetadataGroupWidth(
            MarkerClusterPresentationPlan plan,
            MarkerHudVisualFootprint footprint,
            int fontSize,
            float markerScale,
            float preferredDistanceWidth)
        {
            var distanceWidth = CompactMetadataDistanceWidth(plan.Text, preferredDistanceWidth, footprint);
            if (!plan.LifetimeIndicator.Visible) return distanceWidth;
            var indicatorSize = LifetimeIndicatorSize(fontSize, markerScale);
            var countWidth = plan.LifetimeIndicator.ShowCount
                ? LifetimeCountWidth(fontSize, plan.LifetimeIndicator.CountText.Length)
                : 0f;
            var lifetimeWidth = indicatorSize + (countWidth > 0f ? 3f + countWidth : 0f);
            return lifetimeWidth + (distanceWidth > 0f ? CompactMetadataGap(footprint) + distanceWidth : 0f);
        }

        private static MarkerHudVisualFootprint EnsureCompactMetadataRowFootprint(
            MarkerHudVisualFootprint footprint,
            MarkerClusterPresentationPlan plan,
            int fontSize,
            float markerScale,
            float preferredDistanceWidth)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var rowHeight = CompactMetadataRowHeight(plan.Text, fontSize, footprint.IndicatorSize, markerScale, plan.LifetimeIndicator.Visible);
            var groupWidth = CompactMetadataGroupWidth(plan, footprint, fontSize, markerScale, preferredDistanceWidth);
            var width = Math.Max(footprint.Width, footprint.PaddingX * 2f + groupWidth);
            var height = Math.Max(footprint.Height, rowHeight + 8f * scale);
            return new MarkerHudVisualFootprint(width, height, footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }

        private static int CompactDisplayedGlyphCount(int logicalCount)
            => Math.Max(1, Math.Min(MarkerCategorySummaryPolicy.MaxCategories, Math.Max(0, logicalCount)));

        private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)
            => 1;

        private static void CompactGlyphGroupExtent(
            MarkerCompactCategoryBadge group,
            int fontSize,
            float markerScale,
            float indicatorSize,
            bool showCount,
            out float width,
            out float height)
        {
            var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                Screen.width, Screen.height, markerScale, indicatorSize, fontSize, showCount: false);
            var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(CompactDisplayedGlyphCount(group));
            var maxRowSize = 1;
            var maxRow = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].RowSize > maxRowSize) maxRowSize = slots[i].RowSize;
                if (slots[i].Row > maxRow) maxRow = slots[i].Row;
            }
            var glyphGap = Math.Max(4f, geometry.BadgeSize * 0.22f);
            width = maxRowSize * geometry.BadgeSize + Math.Max(0, maxRowSize - 1) * glyphGap;
            height = (maxRow + 1) * geometry.VerticalStride;
            if (showCount) height += Math.Max(8f, fontSize * 0.82f);
        }

        private static float CompactMetadataBottomPadding(float markerScale)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            return 2f * scale;
        }

        private static float CompactGroupBottomExtent(
            MarkerCompactCategoryBadge group,
            int fontSize,
            float markerScale,
            float indicatorSize,
            bool showCount)
        {
            var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                Screen.width, Screen.height, markerScale, indicatorSize, fontSize, showCount: false);
            if (!showCount) return geometry.BadgeSize * 0.5f;
            CompactGlyphGroupExtent(group, fontSize, markerScale, indicatorSize, showCount: true, out _, out var groupHeight);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var countHeight = fontSize * 1.1f;
            var countCenterYOffset = -groupHeight * 0.5f + Math.Max(4f * scale, fontSize * 0.28f);
            return Math.Max(geometry.BadgeSize * 0.5f, -countCenterYOffset + countHeight * 0.5f);
        }

        private static float CompactGroupHalfWidth(
            int fontSize,
            float markerScale,
            float indicatorSize,
            bool showCount)
        {
            var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                Screen.width, Screen.height, markerScale, indicatorSize, fontSize, showCount: false);
            if (!showCount) return geometry.BadgeSize * 0.5f;
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var countWidth = Math.Max(24f * scale, fontSize * 2.2f);
            return Math.Max(geometry.BadgeSize, countWidth) * 0.5f;
        }

        private static MarkerHudVisualFootprint ExtendFootprintForCompactBadges(
            MarkerHudVisualFootprint footprint,
            MarkerClusterPresentationPlan plan,
            int fontSize,
            float markerScale)
        {
            var groupCount = Math.Min(plan.CompactBadges.Count, MarkerClusterPresentationPolicy.MaxCompactLifetimePresentationGroups);
            if (groupCount <= 0) return footprint;

            var categoryIndices = new int[groupCount];
            var categoryOrdinals = new int[groupCount];
            var categoryGroupCounts = new int[MarkerClusterPresentationPolicy.MaxCompactCategoryBadges];
            var categoryCount = 0;
            var hasCategory = false;
            var lastCategory = MarkerSemanticCategory.Unknown;
            for (var i = 0; i < groupCount; i++)
            {
                var group = plan.CompactBadges[i];
                if (!hasCategory || group.Category != lastCategory)
                {
                    lastCategory = group.Category;
                    hasCategory = true;
                    categoryCount++;
                }
                var categoryIndex = categoryCount - 1;
                categoryIndices[i] = categoryIndex;
                if (categoryIndex >= 0 && categoryIndex < categoryGroupCounts.Length)
                {
                    categoryOrdinals[i] = categoryGroupCounts[categoryIndex];
                    categoryGroupCounts[categoryIndex]++;
                }
            }
            categoryCount = Math.Min(categoryCount, MarkerClusterPresentationPolicy.MaxCompactCategoryBadges);

            var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize, showCount: false);
            var categorySlots = MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount);
            var categoryStrideX = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(
                Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize, categoryCount);
            var categoryStrideY = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterVerticalStride(
                Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize);
            var lifetimeGroupGap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(
                Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize);

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxBottomExtent = geometry.BadgeSize * 0.5f;
            var groupHalfWidth = CompactGroupHalfWidth(fontSize, markerScale, footprint.IndicatorSize, plan.RenderCategorySubcounts);
            for (var i = 0; i < groupCount; i++)
            {
                var categoryIndex = categoryIndices[i];
                if (categoryIndex < 0 || categoryIndex >= categoryCount) continue;
                var slot = categorySlots[categoryIndex];
                var localOffset = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(
                    slot,
                    categoryOrdinals[i],
                    categoryGroupCounts[categoryIndex],
                    geometry.BadgeSize,
                    lifetimeGroupGap);
                var centerX = slot.XUnits * categoryStrideX + localOffset;
                minX = Math.Min(minX, centerX - groupHalfWidth);
                maxX = Math.Max(maxX, centerX + groupHalfWidth);
                var bottomExtent = CompactGroupBottomExtent(
                    plan.CompactBadges[i], fontSize, markerScale, footprint.IndicatorSize, plan.RenderCategorySubcounts);
                if (bottomExtent > maxBottomExtent) maxBottomExtent = bottomExtent;
            }

            var outerRowCount = 0;
            for (var i = 0; i < categorySlots.Length; i++)
                if (categorySlots[i].Row + 1 > outerRowCount) outerRowCount = categorySlots[i].Row + 1;
            var pyramidVisibleHeight = geometry.BadgeSize * 0.5f
                + maxBottomExtent
                + Math.Max(0, outerRowCount - 1) * categoryStrideY;
            var metadataRowHeight = CompactMetadataRowHeight(
                plan.Text, fontSize, footprint.IndicatorSize, markerScale, showLifetimeIndicator: false);
            var distanceGap = string.IsNullOrEmpty(plan.Text)
                ? 0f
                : MarkerCategorySummaryPolicy.BuildDetailedCategoryDistanceGap(
                    Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var bottomPadding = CompactMetadataBottomPadding(markerScale);
            var width = footprint.Width;
            if (!float.IsPositiveInfinity(minX) && !float.IsNegativeInfinity(maxX))
                width = Math.Max(width, footprint.PaddingX * 2f + Math.Max(0f, maxX - minX));
            var height = bottomPadding + metadataRowHeight + distanceGap + pyramidVisibleHeight + 4f * scale;
            return new MarkerHudVisualFootprint(width, Math.Max(1f, height), footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }


        private static MarkerHudVisualFootprint ExtendFootprintForDetailedItemIcons(
            MarkerHudVisualFootprint footprint,
            int rowCount,
            int fontSize,
            float markerScale,
            bool showLifetimeIndicator)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var iconSize = Mathf.Max(12f * scale, fontSize * 0.82f);
            var lifetimeSize = showLifetimeIndicator ? LifetimeIndicatorSize(fontSize, markerScale) : 0f;
            var lifetimeGutter = showLifetimeIndicator ? footprint.Gap * 0.55f + lifetimeSize : 0f;
            var width = footprint.PaddingX * 2f + iconSize + lifetimeGutter + footprint.Gap + footprint.LabelWidth;
            var lineHeight = Math.Max(fontSize * 1.18f, Math.Max(iconSize, lifetimeSize) + 2f * scale);
            var minimumRowsHeight = Math.Max(1, rowCount) * lineHeight + 8f * scale;
            return new MarkerHudVisualFootprint(Math.Max(footprint.Width, width), Math.Max(footprint.Height, minimumRowsHeight), footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }

        private static MarkerHudVisualFootprint ExtendFootprintForDetailedRowDiamonds(
            MarkerHudVisualFootprint footprint,
            int rowCount,
            int fontSize,
            float markerScale,
            bool showLifetimeIndicator,
            int maxLifetimeCountCharacters)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var badgeSize = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize);
            var gutterWidth = Math.Max(footprint.IndicatorSize, badgeSize);
            if (showLifetimeIndicator)
            {
                gutterWidth += footprint.Gap * 0.55f + LifetimeIndicatorSize(fontSize, markerScale);
                if (maxLifetimeCountCharacters > 0)
                    gutterWidth += 3f * scale + LifetimeCountWidth(fontSize, maxLifetimeCountCharacters);
            }
            var width = footprint.PaddingX * 2f + gutterWidth + footprint.Gap + footprint.LabelWidth;
            var lineHeight = Math.Max(fontSize * 1.18f, badgeSize + 2f * scale);
            var minimumRowsHeight = Math.Max(1, rowCount) * lineHeight + 8f * scale;
            var height = Math.Max(footprint.Height, minimumRowsHeight);
            return new MarkerHudVisualFootprint(width, height, footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }

        private static MarkerHudVisualFootprint ExtendFootprintForSummaryLifetimeIndicator(
            MarkerHudVisualFootprint footprint,
            int fontSize,
            float markerScale,
            bool showCount)
        {
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var extra = footprint.Gap * 0.55f + LifetimeIndicatorSize(fontSize, markerScale);
            if (showCount) extra += 3f * scale + LifetimeCountWidth(fontSize, 4);
            return new MarkerHudVisualFootprint(footprint.Width + extra, footprint.Height, footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }

        private static MarkerHudVisualFootprint CollapseDetailedFootprintWithoutDiamondGutter(MarkerHudVisualFootprint footprint)
        {
            var width = footprint.PaddingX * 2f + footprint.LabelWidth;
            return new MarkerHudVisualFootprint(width, footprint.Height, footprint.IndicatorSize, footprint.LabelWidth, footprint.PaddingX, footprint.Gap);
        }

        private void ApplyView(
            MarkerView view,
            MarkerSemanticCluster cluster,
            MarkerRenderInput representative,
            MarkerClusterPresentationPlan plan,
            Color mainColor,
            MarkerHudProjection sourceProjection,
            MarkerHudPlacement placement,
            VisualMeasurement measurement,
            bool fastFollow,
            bool directional)
        {
            if (_canvasRect == null) return;
            var footprint = measurement.Footprint;
            var color = (Color32)SanitizeColor(mainColor);
            if (!view.HasColor || !view.LastColor.Equals(color))
            {
                view.LastColor = color;
                view.HasColor = true;
                view.Indicator.color = color;
                var cueColor = color;
                cueColor.a = Math.Min((byte)150, color.a);
                view.AssociationCue.color = cueColor;
                view.Label.color = (plan.DetailedItemRows.Count > 0 || plan.ShowDetailedCategoryRowDiamonds || plan.ShowCompactCategoryDiamonds)
                    ? SanitizeColor(_visualSettings.NeutralColor)
                    : color;
            }

            var desiredLabelColor = (plan.DetailedItemRows.Count > 0 || plan.ShowDetailedCategoryRowDiamonds || plan.ShowCompactCategoryDiamonds)
                ? SanitizeColor(_visualSettings.NeutralColor)
                : (Color)color;
            if (!view.Label.color.Equals(desiredLabelColor)) view.Label.color = desiredLabelColor;

            var targetAlpha = directional ? _visualSettings.OffscreenOpacity : _visualSettings.MarkerOpacity;
            if (Math.Abs(view.Group.alpha - targetAlpha) > 0.0001f) view.Group.alpha = targetAlpha;
            var backgroundAlpha = directional ? 0f : _visualSettings.MarkerBackgroundOpacity;
            var backgroundColor = new Color(0.02f, 0.03f, 0.05f, backgroundAlpha);
            if (!view.Background.color.Equals(backgroundColor)) view.Background.color = backgroundColor;

            var renderedText = measurement.RenderedText;
            if (!string.Equals(view.Label.text, renderedText, StringComparison.Ordinal)) view.Label.text = renderedText;
            UpdateDetailedItemIcons(view, cluster, plan, footprint);
            var layoutChanged = !view.HasLayout || measurement.MeasurementChanged;
            if (!ReferenceEquals(view.AppliedBadgePlan, plan) || layoutChanged)
            {
                UpdateCategoryBadges(view, cluster, plan, footprint);
                view.AppliedBadgePlan = plan;
            }
            UpdateLifetimeIndicators(view, cluster, plan, footprint, measurement, directional ? _visualSettings.OffscreenScale : _presentationSettings.Scale);

            var hasRenderedCompactBadges = plan.ShowCompactCategoryDiamonds && plan.CategoryEntries.Count > 0;
            var hasRenderedDetailedRowDiamonds = plan.ShowDetailedCategoryRowDiamonds && plan.CategoryEntries.Count > 0;
            var hasDetailedItemLifetimeIndicator = plan.DetailedItemRows.Count > 0 && HasDetailedItemLifetimeIndicator(plan);
            var hasDetailedCategoryLifetimeIndicator = hasRenderedDetailedRowDiamonds && HasDetailedCategoryLifetimeIndicator(plan);
            var hasSummaryLifetimeIndicator = plan.LifetimeIndicator.Visible
                && !hasRenderedCompactBadges
                && plan.DetailedItemRows.Count == 0
                && !hasRenderedDetailedRowDiamonds;
            if (layoutChanged)
            {
                view.HasLayout = true;
                view.Rect.sizeDelta = new Vector2(footprint.Width, footprint.Height);
                _performance.RecordUiLayoutWrite();

                var backgroundRect = view.Background.rectTransform;
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;

                var indicatorRectForLayout = view.Indicator.rectTransform;
                indicatorRectForLayout.anchorMin = new Vector2(0.5f, 0.5f);
                indicatorRectForLayout.anchorMax = new Vector2(0.5f, 0.5f);
                indicatorRectForLayout.pivot = new Vector2(0.5f, 0.5f);
                indicatorRectForLayout.sizeDelta = new Vector2(footprint.IndicatorSize, footprint.IndicatorSize);
                indicatorRectForLayout.anchoredPosition = new Vector2(
                    -footprint.Width * 0.5f + footprint.PaddingX + footprint.IndicatorSize * 0.5f,
                    hasRenderedCompactBadges ? footprint.IndicatorSize * 0.28f : 0f);

                var labelRect = view.Label.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                if (hasRenderedCompactBadges)
                {
                    var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale);
                    var metadataRowHeight = CompactMetadataRowHeight(
                        plan.Text, fontSize, footprint.IndicatorSize, _presentationSettings.Scale, plan.LifetimeIndicator.Visible);
                    var distanceWidth = CompactMetadataDistanceWidth(plan.Text, measurement.LabelPreferredWidth, footprint);
                    var groupWidth = CompactMetadataGroupWidth(plan, footprint, fontSize, _presentationSettings.Scale, measurement.LabelPreferredWidth);
                    var labelX = plan.LifetimeIndicator.Visible && distanceWidth > 0f
                        ? groupWidth * 0.5f - distanceWidth * 0.5f
                        : 0f;
                    labelRect.sizeDelta = new Vector2(Math.Max(1f, distanceWidth), Math.Max(1f, metadataRowHeight));
                    var bottomPadding = CompactMetadataBottomPadding(_presentationSettings.Scale);
                    labelRect.anchoredPosition = new Vector2(
                        labelX,
                        -footprint.Height * 0.5f + metadataRowHeight * 0.5f + bottomPadding);
                    view.Label.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    var usesDiamondGutter = plan.ShowMainDiamond || hasRenderedDetailedRowDiamonds;
                    var usesItemIconGutter = plan.DetailedItemRows.Count > 0;
                    labelRect.sizeDelta = new Vector2(footprint.LabelWidth, footprint.Height);
                    if (usesDiamondGutter)
                    {
                        var x = -footprint.Width * 0.5f + footprint.PaddingX + footprint.IndicatorSize;
                        if (hasDetailedCategoryLifetimeIndicator || hasSummaryLifetimeIndicator)
                        {
                            var lifetimeSize = LifetimeIndicatorSize(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), _presentationSettings.Scale);
                            x += footprint.Gap * 0.55f + lifetimeSize;
                            if (hasDetailedCategoryLifetimeIndicator)
                            {
                                var countCharacters = MaxDetailedCategoryLifetimeCountCharacters(plan);
                                if (countCharacters > 0) x += 3f + LifetimeCountWidth(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), countCharacters);
                            }
                            else if (plan.LifetimeIndicator.ShowCount)
                            {
                                x += 3f + LifetimeCountWidth(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), plan.LifetimeIndicator.CountText.Length);
                            }
                        }
                        labelRect.anchoredPosition = new Vector2(x + footprint.Gap + footprint.LabelWidth * 0.5f, 0f);
                    }
                    else if (usesItemIconGutter)
                    {
                        var itemIconSize = Mathf.Max(12f * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * _presentationSettings.Scale, MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale) * 0.82f);
                        var x = -footprint.Width * 0.5f + footprint.PaddingX + itemIconSize;
                        if (hasDetailedItemLifetimeIndicator)
                            x += footprint.Gap * 0.55f + LifetimeIndicatorSize(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), _presentationSettings.Scale);
                        labelRect.anchoredPosition = new Vector2(x + footprint.Gap + footprint.LabelWidth * 0.5f, 0f);
                    }
                    else if (hasSummaryLifetimeIndicator)
                    {
                        var lifetimeSize = LifetimeIndicatorSize(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), _presentationSettings.Scale);
                        var x = -footprint.Width * 0.5f + footprint.PaddingX + lifetimeSize;
                        if (plan.LifetimeIndicator.ShowCount)
                            x += 3f + LifetimeCountWidth(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale), plan.LifetimeIndicator.CountText.Length);
                        labelRect.anchoredPosition = new Vector2(x + footprint.Gap + footprint.LabelWidth * 0.5f, 0f);
                    }
                    else labelRect.anchoredPosition = Vector2.zero;
                    view.Label.alignment = TextAlignmentOptions.Left;
                }
                _performance.RecordUiLayoutWrite(10);
            }

            var indicatorRect = view.Indicator.rectTransform;
            var showPrimaryIndicator = directional || plan.ShowMainDiamond;
            if (view.Indicator.enabled != showPrimaryIndicator) view.Indicator.enabled = showPrimaryIndicator;
            var desiredShape = directional ? MarkerIndicatorShape.DirectionArrow : MarkerIndicatorShape.AnchorDiamond;
            if (view.LastAppliedShape != desiredShape)
            {
                view.LastAppliedShape = desiredShape;
                view.Indicator.Shape = desiredShape;
            }

            var desiredRotation = directional ? placement.ArrowRotationDegrees : 0f;
            if (MarkerFramePipelinePolicy.ShouldWriteRotation(view.HasAppliedRotation, view.LastAppliedRotationDegrees, desiredRotation))
            {
                view.HasAppliedRotation = true;
                view.LastAppliedRotationDegrees = desiredRotation;
                indicatorRect.localRotation = desiredRotation == 0f ? Quaternion.identity : Quaternion.Euler(0f, 0f, desiredRotation);
                _performance.RecordUiLayoutWrite();
            }

            var targetAnchoredX = placement.X - Screen.width * 0.5f;
            var targetAnchoredY = placement.Y - Screen.height * 0.5f;
            var anchoredX = (!fastFollow && view.HasAppliedPosition)
                ? MarkerPlacementStabilityPolicy.SmoothCoordinate(view.LastAppliedPosition.x, targetAnchoredX, Time.unscaledDeltaTime)
                : targetAnchoredX;
            var anchoredY = (!fastFollow && view.HasAppliedPosition)
                ? MarkerPlacementStabilityPolicy.SmoothCoordinate(view.LastAppliedPosition.y, targetAnchoredY, Time.unscaledDeltaTime)
                : targetAnchoredY;

            if (!directional
                && placement.Mode == MarkerHudMode.OnScreenWorldAnchor
                && sourceProjection.Valid
                && !placement.HudRelocated
                && !placement.MessageHudRelocated)
            {
                var sourceAnchoredX = sourceProjection.X - Screen.width * 0.5f;
                var sourceAnchoredY = sourceProjection.Y - Screen.height * 0.5f;
                var maxDisplacement = MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(footprint, Screen.width, Screen.height);
                MarkerPlacementStabilityPolicy.ClampDisplacementFromAnchor(
                    sourceAnchoredX, sourceAnchoredY, anchoredX, anchoredY, maxDisplacement,
                    out anchoredX, out anchoredY);
            }

            if (MarkerFramePipelinePolicy.ShouldWriteScreenPosition(view.HasAppliedPosition, view.LastAppliedPosition.x, view.LastAppliedPosition.y, anchoredX, anchoredY))
            {
                view.HasAppliedPosition = true;
                view.LastAppliedPosition = new Vector2(anchoredX, anchoredY);
                view.Rect.anchoredPosition = view.LastAppliedPosition;
                _performance.RecordUiLayoutWrite();
            }

            if (directional)
            {
                view.HasAssociationCueVector = false;
                if (view.AssociationCue.gameObject.activeSelf) view.AssociationCue.gameObject.SetActive(false);
            }
            else
            {
                ApplyAssociationCue(view, sourceProjection, placement, anchoredX, anchoredY);
            }
            if (!view.Root.activeSelf) view.Root.SetActive(true);
        }

        private string BuildRenderedText(TextMeshProUGUI label, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, float detailedItemWidthLimit)
        {
            if (plan.DetailedItemRows.Count == 0 && !plan.ShowDetailedCategoryRowDiamonds) return plan.Text;
            var lines = (plan.Text ?? string.Empty).Split('\n');
            var coloredRows = plan.DetailedItemRows.Count > 0 ? plan.DetailedItemRows.Count : plan.CategoryEntries.Count;
            for (var i = 0; i < lines.Length && i < coloredRows; i++)
            {
                Color rowColor;
                if (plan.DetailedItemRows.Count > 0)
                {
                    var row = plan.DetailedItemRows[i];
                    lines[i] = BuildBoundedDetailedItemLine(label, row.DisplayLabel, row.Count, detailedItemWidthLimit);
                    rowColor = ResolveOrdinaryItemRowColor(cluster, row);
                }
                else
                {
                    rowColor = ResolveConfiguredCategoryColor(plan.CategoryEntries[i].Category);
                }
                lines[i] = "<color=#" + ColorHex(rowColor) + ">" + lines[i] + "</color>";
            }
            return string.Join("\n", lines);
        }

        private string BuildBoundedDetailedItemLine(TextMeshProUGUI label, string displayLabel, int count, float maxWidth)
        {
            var name = displayLabel ?? string.Empty;
            var suffix = " ×" + Math.Max(0, count).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var full = name + suffix;
            if (!IsFinite(maxWidth) || maxWidth <= 0f) return full;
            if (PreferredTextWidth(label, full) <= maxWidth) return full;

            const string ellipsis = "…";
            if (PreferredTextWidth(label, ellipsis + suffix) > maxWidth) return suffix.TrimStart();
            var low = 0;
            var high = name.Length;
            var best = string.Empty;
            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                var prefix = name.Substring(0, mid).TrimEnd();
                var candidate = prefix + ellipsis + suffix;
                if (PreferredTextWidth(label, candidate) <= maxWidth)
                {
                    best = prefix;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return best + ellipsis + suffix;
        }

        private float PreferredTextWidth(TextMeshProUGUI label, string text)
        {
            try
            {
                _performance.RecordTmpPreferredMeasurement();
                var value = label.GetPreferredValues(text).x;
                return IsFinite(value) && value > 0f ? value : float.PositiveInfinity;
            }
            catch
            {
                return float.PositiveInfinity;
            }
        }

        private Color ResolveOrdinaryItemRowColor(MarkerSemanticCluster cluster, MarkerDetailedItemRow row)
        {
            for (var i = 0; i < cluster.MemberStableKeys.Count; i++)
            {
                if (!_inputByStableKey.TryGetValue(cluster.MemberStableKeys[i], out var input)) continue;
                if (string.Equals(input.ItemSemanticKey, row.ItemIdentity, StringComparison.Ordinal)
                    && input.Lifetime == row.Lifetime)
                    return SanitizeColor(input.NativeColor);
            }
            return ResolveConfiguredCategoryColor(row.Category);
        }

        private static string ColorHex(Color value)
        {
            var c = (Color32)SanitizeColor(value);
            return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2") + c.a.ToString("X2");
        }

        private void UpdateDetailedItemIcons(MarkerView view, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, MarkerHudVisualFootprint footprint)
        {
            var count = plan.DetailedItemRows.Count;
            EnsureItemIconCapacity(view, count);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * _presentationSettings.Scale;
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale);
            var iconSize = Mathf.Max(12f * scale, fontSize * 0.82f);
            var lineHeight = Math.Max(fontSize * 1.18f, iconSize + 2f * scale);
            var textLineCount = CountPresentationLines(plan.Text);
            var firstLineY = (Math.Max(1, textLineCount) - 1) * lineHeight * 0.5f;
            var x = -footprint.Width * 0.5f + footprint.PaddingX + iconSize * 0.5f;

            for (var i = 0; i < view.ItemIcons.Count; i++)
            {
                var iconView = view.ItemIcons[i];
                if (i >= count || !TryResolveOrdinaryItemIcon(cluster, plan.DetailedItemRows[i], out var sprite) || sprite == null)
                {
                    if (iconView.Root.activeSelf) iconView.Root.SetActive(false);
                    continue;
                }
                iconView.Image.sprite = sprite;
                iconView.Image.preserveAspect = true;
                var rect = iconView.Image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(iconSize, iconSize);
                rect.anchoredPosition = new Vector2(x, firstLineY - i * lineHeight);
                if (!iconView.Root.activeSelf) iconView.Root.SetActive(true);
            }
        }

        private bool TryResolveOrdinaryItemIcon(MarkerSemanticCluster cluster, MarkerDetailedItemRow row, out Sprite? sprite)
        {
            for (var i = 0; i < cluster.MemberStableKeys.Count; i++)
            {
                if (!_inputByStableKey.TryGetValue(cluster.MemberStableKeys[i], out var input)) continue;
                if (!string.Equals(input.ItemSemanticKey, row.ItemIdentity, StringComparison.Ordinal)
                    || input.Lifetime != row.Lifetime) continue;
                sprite = input.NativeIcon;
                return sprite != null;
            }
            sprite = null;
            return false;
        }

        private void EnsureItemIconCapacity(MarkerView view, int count)
        {
            var bounded = Math.Min(count, MarkerClusterPresentationPolicy.MarkerDetailRowsMax);
            while (view.ItemIcons.Count < bounded)
            {
                var root = new GameObject("DetailedItemIcon." + view.ItemIcons.Count, typeof(RectTransform), typeof(Image));
                root.layer = ResolveUiLayer();
                root.transform.SetParent(view.Root.transform, false);
                var image = root.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = Color.white;
                view.ItemIcons.Add(new ItemIconView(root, image));
            }
        }

        private void UpdateLifetimeIndicators(
            MarkerView view,
            MarkerSemanticCluster cluster,
            MarkerClusterPresentationPlan plan,
            MarkerHudVisualFootprint footprint,
            VisualMeasurement measurement,
            float markerScale)
        {
            if (plan.DetailedItemRows.Count > 0)
            {
                HideSummaryLifetimeIndicator(view);
                UpdateDetailedItemLifetimeIndicators(view, cluster, plan, footprint, markerScale);
                return;
            }

            // Category groups render lifetime directly in the glyph slot (diamond vs clock).
            // Do not add a second metadata/lifetime clock next to Detailed rows or Compact distance.
            if ((plan.ShowDetailedCategoryRowDiamonds || plan.ShowCompactCategoryDiamonds) && plan.CategoryEntries.Count > 0)
            {
                HideRowLifetimeIndicators(view);
                HideSummaryLifetimeIndicator(view);
                return;
            }

            HideRowLifetimeIndicators(view);
            if (!plan.LifetimeIndicator.Visible)
            {
                HideSummaryLifetimeIndicator(view);
                return;
            }

            var summary = GetOrCreateSummaryLifetimeIndicator(view);
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, markerScale);
            var indicatorSize = LifetimeIndicatorSize(fontSize, markerScale);
            var countText = plan.LifetimeIndicator.CountText;
            var left = -footprint.Width * 0.5f + footprint.PaddingX;
            var xInline = left + (plan.ShowMainDiamond ? footprint.IndicatorSize + footprint.Gap * 0.55f : 0f) + indicatorSize * 0.5f;
            ConfigureLifetimeIndicator(
                summary,
                xInline,
                0f,
                indicatorSize,
                countText,
                markerScale,
                MarkerPresentationGlyphKind.Clock,
                SanitizeColor(_visualSettings.NeutralColor));
        }

        private void UpdateDetailedItemLifetimeIndicators(
            MarkerView view,
            MarkerSemanticCluster cluster,
            MarkerClusterPresentationPlan plan,
            MarkerHudVisualFootprint footprint,
            float markerScale)
        {
            EnsureRowLifetimeIndicatorCapacity(view, plan.DetailedItemRows.Count);
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, markerScale);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var itemIconSize = Mathf.Max(12f * scale, fontSize * 0.82f);
            var indicatorSize = LifetimeIndicatorSize(fontSize, markerScale);
            var lineHeight = Math.Max(fontSize * 1.18f, Math.Max(itemIconSize, indicatorSize) + 2f * scale);
            var textLineCount = CountPresentationLines(plan.Text);
            var firstLineY = (Math.Max(1, textLineCount) - 1) * lineHeight * 0.5f;
            var x = -footprint.Width * 0.5f + footprint.PaddingX + itemIconSize + footprint.Gap * 0.55f + indicatorSize * 0.5f;

            for (var i = 0; i < view.RowLifetimeIndicators.Count; i++)
            {
                var indicatorView = view.RowLifetimeIndicators[i];
                if (i >= plan.DetailedItemRows.Count)
                {
                    if (indicatorView.Root.activeSelf) indicatorView.Root.SetActive(false);
                    continue;
                }
                var row = plan.DetailedItemRows[i];
                ConfigureLifetimeIndicator(
                    indicatorView,
                    x,
                    firstLineY - i * lineHeight,
                    indicatorSize,
                    string.Empty,
                    markerScale,
                    row.GlyphKind,
                    ResolveOrdinaryItemRowColor(cluster, row));
            }
        }


        private void EnsureRowLifetimeIndicatorCapacity(MarkerView view, int count)
        {
            var bounded = Math.Min(count, MarkerClusterPresentationPolicy.MarkerDetailRowsMax);
            while (view.RowLifetimeIndicators.Count < bounded)
                view.RowLifetimeIndicators.Add(CreateLifetimeIndicatorView(view.Root.transform, "RowLifetimeIndicator." + view.RowLifetimeIndicators.Count));
        }

        private LifetimeIndicatorView GetOrCreateSummaryLifetimeIndicator(MarkerView view)
        {
            if (view.SummaryLifetimeIndicator != null) return view.SummaryLifetimeIndicator;
            view.SummaryLifetimeIndicator = CreateLifetimeIndicatorView(view.Root.transform, "SummaryLifetimeIndicator");
            return view.SummaryLifetimeIndicator;
        }

        private LifetimeIndicatorView CreateLifetimeIndicatorView(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = ResolveUiLayer();
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var diamondObject = new GameObject("Diamond", typeof(RectTransform), typeof(MarkerIndicatorGraphic), typeof(Outline));
            diamondObject.layer = ResolveUiLayer();
            diamondObject.transform.SetParent(root.transform, false);
            var diamond = diamondObject.GetComponent<MarkerIndicatorGraphic>();
            diamond.Shape = MarkerIndicatorShape.AnchorDiamond;
            diamond.raycastTarget = false;
            var diamondOutline = diamondObject.GetComponent<Outline>();
            diamondOutline.effectColor = new Color32(4, 6, 10, 238);
            diamondOutline.effectDistance = new Vector2(1f, -1f);
            diamondOutline.useGraphicAlpha = true;

            var clockObject = new GameObject("Clock", typeof(RectTransform), typeof(MarkerLifetimeIndicatorGraphic), typeof(Outline));
            clockObject.layer = ResolveUiLayer();
            clockObject.transform.SetParent(root.transform, false);
            var clock = clockObject.GetComponent<MarkerLifetimeIndicatorGraphic>();
            clock.raycastTarget = false;
            var clockOutline = clockObject.GetComponent<Outline>();
            clockOutline.effectColor = new Color32(4, 6, 10, 238);
            clockOutline.effectDistance = new Vector2(1f, -1f);
            clockOutline.useGraphicAlpha = true;

            var count = CreateText(root.transform, "Count");
            count.alignment = TextAlignmentOptions.Left;
            count.color = Color.white;
            return new LifetimeIndicatorView(root, diamond, clock, count);
        }

        private void ConfigureLifetimeIndicator(
            LifetimeIndicatorView view,
            float x,
            float y,
            float indicatorSize,
            string countText,
            float markerScale,
            MarkerPresentationGlyphKind glyphKind,
            Color glyphColor)
        {
            var color = SanitizeColor(glyphColor);
            var showClock = glyphKind == MarkerPresentationGlyphKind.Clock;
            if (view.Clock.gameObject.activeSelf != showClock) view.Clock.gameObject.SetActive(showClock);
            if (view.Diamond.gameObject.activeSelf == showClock) view.Diamond.gameObject.SetActive(!showClock);
            var graphic = showClock ? (MaskableGraphic)view.Clock : view.Diamond;
            if (!graphic.color.Equals(color)) graphic.color = color;
            var graphicRect = graphic.rectTransform;
            graphicRect.anchorMin = graphicRect.anchorMax = graphicRect.pivot = new Vector2(0.5f, 0.5f);
            graphicRect.sizeDelta = new Vector2(indicatorSize, indicatorSize);
            graphicRect.anchoredPosition = new Vector2(x, y);

            if (!string.Equals(view.Count.text, countText, StringComparison.Ordinal)) view.Count.text = countText;
            var showCount = !string.IsNullOrEmpty(countText);
            if (view.Count.enabled != showCount) view.Count.enabled = showCount;
            if (showCount)
            {
                EnsureLifetimeTypography(view.Count, markerScale);
                var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, markerScale);
                var countWidth = LifetimeCountWidth(fontSize, countText.Length);
                var countRect = view.Count.rectTransform;
                countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(0.5f, 0.5f);
                countRect.sizeDelta = new Vector2(countWidth, indicatorSize + fontSize * 0.35f);
                countRect.anchoredPosition = new Vector2(x + indicatorSize * 0.5f + 3f + countWidth * 0.5f, y);
            }
            if (!view.Root.activeSelf) view.Root.SetActive(true);
        }


        private static void HideRowLifetimeIndicators(MarkerView view)
        {
            for (var i = 0; i < view.RowLifetimeIndicators.Count; i++)
                if (view.RowLifetimeIndicators[i].Root.activeSelf) view.RowLifetimeIndicators[i].Root.SetActive(false);
        }

        private static void HideSummaryLifetimeIndicator(MarkerView view)
        {
            if (view.SummaryLifetimeIndicator != null && view.SummaryLifetimeIndicator.Root.activeSelf)
                view.SummaryLifetimeIndicator.Root.SetActive(false);
        }

        private void UpdateCategoryBadges(MarkerView view, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, MarkerHudVisualFootprint footprint)
        {
            if (plan.ShowDetailedCategoryRowDiamonds)
            {
                UpdateDetailedCategoryRowDiamonds(view, cluster, plan, footprint);
                return;
            }
            UpdateCompactBadges(view, cluster, plan, footprint);
        }

        private void UpdateDetailedCategoryRowDiamonds(MarkerView view, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, MarkerHudVisualFootprint footprint)
        {
            var count = plan.CategoryEntries.Count;
            EnsureBadgeCapacity(view, count);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * _presentationSettings.Scale;
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale);
            var badgeSize = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize);
            var lineHeight = Math.Max(fontSize * 1.18f, badgeSize + 2f * scale);
            var textLineCount = CountPresentationLines(plan.Text);
            var firstLineY = (Math.Max(1, textLineCount) - 1) * lineHeight * 0.5f;
            var gutterCenterX = -footprint.Width * 0.5f + footprint.PaddingX + footprint.IndicatorSize * 0.5f;

            for (var i = 0; i < view.Badges.Count; i++)
            {
                var badge = view.Badges[i];
                if (i >= count)
                {
                    if (badge.Root.activeSelf) badge.Root.SetActive(false);
                    continue;
                }

                var spec = plan.CategoryEntries[i];
                ConfigureCategoryGlyph(
                    badge,
                    spec.GlyphKind,
                    ResolveCategoryColor(cluster, spec.Category),
                    badgeSize,
                    gutterCenterX,
                    firstLineY - i * lineHeight);
                if (!string.IsNullOrEmpty(badge.Count.text)) badge.Count.text = string.Empty;
                if (badge.Count.enabled) badge.Count.enabled = false;
                if (!badge.Root.activeSelf) badge.Root.SetActive(true);
            }
        }


        private static int CountPresentationLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var lines = 1;
            for (var i = 0; i < text.Length; i++)
                if (text[i] == '\n') lines++;
            return lines;
        }

        private void UpdateCompactBadges(MarkerView view, MarkerSemanticCluster cluster, MarkerClusterPresentationPlan plan, MarkerHudVisualFootprint footprint)
        {
            var groupCount = plan.ShowCompactCategoryDiamonds
                ? Math.Min(plan.CompactBadges.Count, MarkerClusterPresentationPolicy.MaxCompactLifetimePresentationGroups)
                : 0;
            var requiredGlyphs = 0;
            for (var i = 0; i < groupCount; i++)
                requiredGlyphs += CompactDisplayedGlyphCount(plan.CompactBadges[i]);
            EnsureBadgeCapacity(view, requiredGlyphs);

            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale);
            var glyphGeometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize, showCount: false);
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * _presentationSettings.Scale;
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;

            var categoryIndices = new int[groupCount];
            var categoryOrdinals = new int[groupCount];
            var categoryGroupCounts = new int[MarkerClusterPresentationPolicy.MaxCompactCategoryBadges];
            var categoryCount = 0;
            var hasCategory = false;
            var lastCategory = MarkerSemanticCategory.Unknown;
            for (var i = 0; i < groupCount; i++)
            {
                var spec = plan.CompactBadges[i];
                if (!hasCategory || spec.Category != lastCategory)
                {
                    lastCategory = spec.Category;
                    hasCategory = true;
                    categoryCount++;
                }
                var categoryIndex = categoryCount - 1;
                categoryIndices[i] = categoryIndex;
                if (categoryIndex >= 0 && categoryIndex < categoryGroupCounts.Length)
                {
                    categoryOrdinals[i] = categoryGroupCounts[categoryIndex];
                    categoryGroupCounts[categoryIndex]++;
                }
            }
            categoryCount = Math.Min(categoryCount, MarkerClusterPresentationPolicy.MaxCompactCategoryBadges);

            var categorySlots = MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount);
            var categoryStrideX = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(
                Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize, categoryCount);
            var categoryStrideY = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterVerticalStride(
                Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize);
            var lifetimeGroupGap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(
                Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize);
            var metadataRowHeight = CompactMetadataRowHeight(
                plan.Text, fontSize, footprint.IndicatorSize, _presentationSettings.Scale, showLifetimeIndicator: false);
            var distanceGap = string.IsNullOrEmpty(plan.Text)
                ? 0f
                : MarkerCategorySummaryPolicy.BuildDetailedCategoryDistanceGap(
                    Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize);
            var maxBottomExtent = glyphGeometry.BadgeSize * 0.5f;
            for (var i = 0; i < groupCount; i++)
            {
                var bottomExtent = CompactGroupBottomExtent(
                    plan.CompactBadges[i], fontSize, _presentationSettings.Scale, footprint.IndicatorSize, plan.RenderCategorySubcounts);
                if (bottomExtent > maxBottomExtent) maxBottomExtent = bottomExtent;
            }
            var outerRowCount = 0;
            for (var i = 0; i < categorySlots.Length; i++)
                if (categorySlots[i].Row + 1 > outerRowCount) outerRowCount = categorySlots[i].Row + 1;
            var outerCenterOffset = Math.Max(0, outerRowCount - 1) * categoryStrideY * 0.5f;
            var bottomPadding = CompactMetadataBottomPadding(_presentationSettings.Scale);
            var visibleBottomY = -footprint.Height * 0.5f + bottomPadding + metadataRowHeight + distanceGap;

            var badgeIndex = 0;
            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var spec = plan.CompactBadges[groupIndex];
                var categoryIndex = categoryIndices[groupIndex];
                if (categoryIndex < 0 || categoryIndex >= categoryCount) continue;
                var categorySlot = categorySlots[categoryIndex];
                var localOffsetX = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(
                    categorySlot,
                    categoryOrdinals[groupIndex],
                    categoryGroupCounts[categoryIndex],
                    glyphGeometry.BadgeSize,
                    lifetimeGroupGap);
                var groupCenterX = categorySlot.XUnits * categoryStrideX + localOffsetX;
                var groupCenterY = visibleBottomY + maxBottomExtent + outerCenterOffset + categorySlot.YUnits * categoryStrideY;

                var inner = MarkerCategorySummaryPolicy.BuildCompactLayout(CompactDisplayedGlyphCount(spec));
                var innerRowCount = 0;
                for (var i = 0; i < inner.Length; i++) if (inner[i].Row + 1 > innerRowCount) innerRowCount = inner[i].Row + 1;
                var glyphGap = Math.Max(4f, glyphGeometry.BadgeSize * 0.22f);
                var glyphStrideX = glyphGeometry.BadgeSize + glyphGap;
                var innerCenterOffsetY = (Math.Max(1, innerRowCount) - 1) * glyphGeometry.VerticalStride * 0.5f;

                for (var glyphIndex = 0; glyphIndex < inner.Length; glyphIndex++, badgeIndex++)
                {
                    var badge = view.Badges[badgeIndex];
                    var slot = inner[glyphIndex];
                    var x = groupCenterX + slot.XUnits * glyphStrideX;
                    var y = groupCenterY + innerCenterOffsetY + slot.YUnits * glyphGeometry.VerticalStride;
                    ConfigureCategoryGlyph(
                        badge,
                        spec.GlyphKind,
                        ResolveCategoryColor(cluster, spec.Category),
                        glyphGeometry.BadgeSize,
                        x,
                        y);

                    var isGroupCountOwner = glyphIndex == 0 && plan.RenderCategorySubcounts;
                    var countText = isGroupCountOwner
                        ? "×" + spec.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : string.Empty;
                    if (!string.Equals(badge.Count.text, countText, StringComparison.Ordinal)) badge.Count.text = countText;
                    if (badge.Count.enabled != isGroupCountOwner) badge.Count.enabled = isGroupCountOwner;
                    if (isGroupCountOwner)
                    {
                        EnsureBadgeTypography(badge.Count);
                        CompactGlyphGroupExtent(
                            spec,
                            fontSize,
                            _presentationSettings.Scale,
                            footprint.IndicatorSize,
                            showCount: true,
                            out _,
                            out var groupHeight);
                        var countRect = badge.Count.rectTransform;
                        countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(0.5f, 0.5f);
                        countRect.sizeDelta = new Vector2(Math.Max(24f * scale, fontSize * 2.2f), fontSize * 1.1f);
                        countRect.anchoredPosition = new Vector2(
                            groupCenterX,
                            groupCenterY - groupHeight * 0.5f + Math.Max(4f * scale, fontSize * 0.28f));
                    }
                    if (!badge.Root.activeSelf) badge.Root.SetActive(true);
                }
            }

            for (var i = badgeIndex; i < view.Badges.Count; i++)
                if (view.Badges[i].Root.activeSelf) view.Badges[i].Root.SetActive(false);
        }


        private static void ConfigureCategoryGlyph(
            BadgeView badge,
            MarkerPresentationGlyphKind glyphKind,
            Color color,
            float size,
            float x,
            float y)
        {
            var showClock = glyphKind == MarkerPresentationGlyphKind.Clock;
            if (badge.Clock.gameObject.activeSelf != showClock) badge.Clock.gameObject.SetActive(showClock);
            if (badge.Diamond.gameObject.activeSelf == showClock) badge.Diamond.gameObject.SetActive(!showClock);
            var graphic = showClock ? (MaskableGraphic)badge.Clock : badge.Diamond;
            if (!graphic.color.Equals(color)) graphic.color = color;
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, y);
        }


        private void EnsureBadgeCapacity(MarkerView view, int count)
        {
            var bounded = Math.Min(count, MarkerClusterPresentationPolicy.MaxCompactLifetimePresentationGroups * MarkerCategorySummaryPolicy.MaxCategories);
            while (view.Badges.Count < bounded)
            {
                var index = view.Badges.Count;
                var root = new GameObject("CategoryBadge." + index, typeof(RectTransform));
                root.layer = ResolveUiLayer();
                root.transform.SetParent(view.Root.transform, false);
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                var diamondObject = new GameObject("Diamond", typeof(RectTransform), typeof(MarkerIndicatorGraphic), typeof(Outline));
                diamondObject.layer = ResolveUiLayer();
                diamondObject.transform.SetParent(root.transform, false);
                var diamond = diamondObject.GetComponent<MarkerIndicatorGraphic>();
                diamond.Shape = MarkerIndicatorShape.AnchorDiamond;
                diamond.raycastTarget = false;
                var outline = diamondObject.GetComponent<Outline>();
                outline.effectColor = new Color32(4, 6, 10, 238);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;

                var clockObject = new GameObject("Clock", typeof(RectTransform), typeof(MarkerLifetimeIndicatorGraphic), typeof(Outline));
                clockObject.layer = ResolveUiLayer();
                clockObject.transform.SetParent(root.transform, false);
                var clock = clockObject.GetComponent<MarkerLifetimeIndicatorGraphic>();
                clock.raycastTarget = false;
                var clockOutline = clockObject.GetComponent<Outline>();
                clockOutline.effectColor = new Color32(4, 6, 10, 238);
                clockOutline.effectDistance = new Vector2(1f, -1f);
                clockOutline.useGraphicAlpha = true;
                clockObject.SetActive(false);

                var countLabel = CreateText(root.transform, "Count");
                countLabel.alignment = TextAlignmentOptions.Center;
                countLabel.color = Color.white;
                view.Badges.Add(new BadgeView(root, diamond, clock, countLabel));
            }
        }

        private void EnsureBadgeTypography(TextMeshProUGUI text)
        {
            var fontSize = Math.Max(12, (int)Math.Round(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, _presentationSettings.Scale) * 0.78f));
            if (_nativeFont != null && text.font != _nativeFont) text.font = _nativeFont;
            if (_nativeFontMaterial != null && text.fontSharedMaterial != _nativeFontMaterial) text.fontSharedMaterial = _nativeFontMaterial;
            if (Math.Abs(text.fontSize - fontSize) > 0.01f) text.fontSize = fontSize;
        }

        private void EnsureLifetimeTypography(TextMeshProUGUI text, float markerScale)
        {
            var fontSize = Math.Max(11, (int)Math.Round(MarkerPresentationPolicy.BuildScaledNativeHudFontSize(Screen.height, markerScale) * 0.72f));
            if (_nativeFont != null && text.font != _nativeFont) text.font = _nativeFont;
            if (_nativeFontMaterial != null && text.fontSharedMaterial != _nativeFontMaterial) text.fontSharedMaterial = _nativeFontMaterial;
            if (Math.Abs(text.fontSize - fontSize) > 0.01f) text.fontSize = fontSize;
        }

        private void EmitSemanticLifecycle(MarkerSemanticUpdate update)
        {
            for (var i = 0; i < update.LifecycleEvents.Count; i++)
            {
                var evt = update.LifecycleEvents[i];
                var cluster = evt.Cluster;
                _log.LogInfo("ISF_MARKER_SEMANTIC event=" + LifecycleToken(evt.Kind)
                    + " clusterKey=" + cluster.StableKey
                    + " members=" + cluster.TotalCount
                    + " fingerprint=" + cluster.MemberFingerprint
                    + " anchor=" + cluster.WorldAnchor
                    + " composition=" + CompositionToken(cluster)
                    + " items=" + ItemRowsToken(cluster)
                    + " reason=" + evt.Reason);
            }
        }

        private static string LifecycleToken(MarkerSemanticLifecycleKind kind)
        {
            switch (kind)
            {
                case MarkerSemanticLifecycleKind.Created: return "CREATED";
                case MarkerSemanticLifecycleKind.MembershipChanged: return "MEMBERSHIP_CHANGED";
                case MarkerSemanticLifecycleKind.Merged: return "MERGED";
                case MarkerSemanticLifecycleKind.Split: return "SPLIT";
                case MarkerSemanticLifecycleKind.CompositionChanged: return "COMPOSITION_CHANGED";
                default: return "REMOVED";
            }
        }

        private static string CompositionToken(MarkerSemanticCluster cluster)
        {
            var result = string.Empty;
            for (var i = 0; i < cluster.Composition.Count; i++)
            {
                if (i > 0) result += ",";
                result += cluster.Composition[i].Category + ":" + cluster.Composition[i].Count;
            }
            return result;
        }

        private static string ItemRowsToken(MarkerSemanticCluster cluster)
        {
            var result = string.Empty;
            for (var i = 0; i < cluster.ItemRows.Count; i++)
            {
                if (i > 0) result += ";";
                var row = cluster.ItemRows[i];
                result += row.ItemIdentity + "|" + row.Category + "|" + row.Count + "|" + row.LocalizedName;
            }
            return result;
        }

        public void SetPresentationSuppressed(bool suppressed)
        {
            if (_presentationSuppressed == suppressed) return;
            _presentationSuppressed = suppressed;
            _placementCacheValid = false;
            if (_canvas != null && _canvas.enabled == suppressed) _canvas.enabled = !suppressed;
        }

        public void Clear()
        {
            _staleKeys.Clear();
            foreach (var key in _views.Keys) _staleKeys.Add(key);
            for (var i = 0; i < _staleKeys.Count; i++) RemoveView(_staleKeys[i]);
            _placementRankByKey.Clear();
            _activeClusterKeys.Clear();
            _semanticTracker.Clear();
            _denseTracker.Clear();
            _fovTracker.Clear();
            _lodTracker.Clear();
            _directionalInputs.Clear();
            _denseNodeByKey.Clear();
            _activeWorldPresentationKeys.Clear();
            _inputByStableKey.Clear();
            _worldMembers.Clear();
            _clusterFrames.Clear();
            _expansionCandidates.Clear();
            _placementCacheValid = false;
            _hasSemanticInputSignature = false;
            _semanticInputSignature = 0UL;
            _nextSemanticSolveAt = 0d;
        }

        public void Dispose()
        {
            Clear();
            if (_canvasObject != null) UnityEngine.Object.Destroy(_canvasObject);
            _canvasObject = null;
            _canvas = null;
            _canvasRect = null;
            _nativeFont = null;
            _nativeFontMaterial = null;
            _typographyResolved = false;
            _typographyRevision = 0;
            _indicatorSourceLogged = false;
            _presentationSuppressed = false;
            _cachedScreenWidth = -1;
            _cachedScreenHeight = -1;
            _cachedDynamicHudZones.Clear();
        }

        private void EnsureCanvas()
        {
            if (_canvasObject != null && _canvas != null && _canvasRect != null) return;
            _canvasObject = new GameObject("ItemShareFix.NativeHudMarkerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.layer = ResolveUiLayer();
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);
            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 95;
            _canvas.pixelPerfect = false;
            _canvas.enabled = !_presentationSuppressed;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasRect = _canvasObject.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
            if (!_indicatorSourceLogged)
            {
                _indicatorSourceLogged = true;
                _log.LogInfo("[ItemShareFix] indicator asset source=" + MarkerPresentationPolicy.IndicatorAssetSourceToken
                    + " family=" + MarkerPresentationPolicy.IndicatorVisualFamilyToken
                    + " semantic=world-space-cluster adaptiveLod=true style=" + MarkerHudNavigationPolicy.NativeHudStyleToken);
            }
        }

        private MarkerView GetOrCreateView(long clusterKey)
        {
            if (_views.TryGetValue(clusterKey, out var existing) && existing.Root != null) return existing;
            var root = new GameObject("ISF.SemanticCluster." + clusterKey, typeof(RectTransform), typeof(CanvasGroup));
            root.layer = ResolveUiLayer();
            root.transform.SetParent(_canvasRect!, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 58f);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.layer = ResolveUiLayer();
            backgroundObject.transform.SetParent(root.transform, false);
            var background = backgroundObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0.02f, 0.03f, 0.05f, 0f);
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var cueObject = new GameObject("AssociationCue", typeof(RectTransform), typeof(MarkerAssociationCueGraphic));
            cueObject.layer = ResolveUiLayer();
            cueObject.transform.SetParent(root.transform, false);
            var associationCue = cueObject.GetComponent<MarkerAssociationCueGraphic>();
            associationCue.raycastTarget = false;
            associationCue.gameObject.SetActive(false);

            var indicatorObject = new GameObject("IndicatorGraphic", typeof(RectTransform), typeof(MarkerIndicatorGraphic), typeof(Outline));
            indicatorObject.layer = ResolveUiLayer();
            indicatorObject.transform.SetParent(root.transform, false);
            var indicator = indicatorObject.GetComponent<MarkerIndicatorGraphic>();
            indicator.raycastTarget = false;
            var outline = indicatorObject.GetComponent<Outline>();
            outline.effectColor = new Color32(4, 6, 10, 238);
            outline.effectDistance = new Vector2(1.35f, -1.35f);
            outline.useGraphicAlpha = true;

            var view = new MarkerView(root, rect, group, background, indicator, associationCue, CreateText(root.transform, "SemanticLabel"));
            _views[clusterKey] = view;
            _placementCacheValid = false;
            return view;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = ResolveUiLayer();
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.14f;
            text.outlineColor = new Color32(4, 6, 10, 238);
            text.color = Color.white;
            return text;
        }

        private void ApplyAssociationCue(MarkerView view, MarkerHudProjection sourceProjection, MarkerHudPlacement placement, float appliedAnchoredX, float appliedAnchoredY)
        {
            var cue = view.AssociationCue;
            var shouldShow = placement.Mode == MarkerHudMode.OnScreenWorldAnchor
                && sourceProjection.Valid
                && MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(
                    sourceProjection,
                    new MarkerHudPlacement(
                        placement.StableKey, placement.Mode, placement.Edge,
                        appliedAnchoredX + Screen.width * 0.5f, appliedAnchoredY + Screen.height * 0.5f,
                        placement.ArrowRotationDegrees, placement.LaneSlot, placement.RailSlot, placement.FinalRect,
                        placement.HudRelocated, placement.CollisionRelocated, placement.MessageHudRelocated)) > 12f;
            if (!shouldShow)
            {
                view.HasAssociationCueVector = false;
                if (cue.gameObject.activeSelf) cue.gameObject.SetActive(false);
                return;
            }

            var sourceAnchoredX = sourceProjection.X - Screen.width * 0.5f;
            var sourceAnchoredY = sourceProjection.Y - Screen.height * 0.5f;
            var dx = sourceAnchoredX - appliedAnchoredX;
            var dy = sourceAnchoredY - appliedAnchoredY;
            var length = Mathf.Sqrt(dx * dx + dy * dy);
            if (!IsFinite(length) || length <= 1f)
            {
                view.HasAssociationCueVector = false;
                if (cue.gameObject.activeSelf) cue.gameObject.SetActive(false);
                return;
            }

            var vector = new Vector2(dx, dy);
            var changed = !view.HasAssociationCueVector
                || Math.Abs(view.LastAssociationCueVector.x - vector.x) > MarkerFramePipelinePolicy.UiPositionWriteEpsilonPixels
                || Math.Abs(view.LastAssociationCueVector.y - vector.y) > MarkerFramePipelinePolicy.UiPositionWriteEpsilonPixels;
            if (changed)
            {
                view.HasAssociationCueVector = true;
                view.LastAssociationCueVector = vector;
                var cueRect = cue.rectTransform;
                cueRect.anchorMin = cueRect.anchorMax = cueRect.pivot = new Vector2(0.5f, 0.5f);
                cueRect.anchoredPosition = new Vector2(dx * 0.5f, dy * 0.5f);
                cueRect.sizeDelta = new Vector2(length, 1.5f);
                cueRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                cue.transform.SetAsFirstSibling();
                _performance.RecordUiLayoutWrite(6);
            }
            if (!cue.gameObject.activeSelf) cue.gameObject.SetActive(true);
        }

        private void EnsureTypography(MarkerView view, int fontSize)
        {
            if (!MarkerRuntimeHotPathPolicy.ShouldApplyTypography(view.AppliedTypographyRevision, view.AppliedFontSize, _typographyRevision, fontSize)) return;
            var text = view.Label;
            if (_nativeFont != null && text.font != _nativeFont) text.font = _nativeFont;
            if (_nativeFontMaterial != null && text.fontSharedMaterial != _nativeFontMaterial) text.fontSharedMaterial = _nativeFontMaterial;
            if (Math.Abs(text.fontSize - fontSize) > 0.01f) text.fontSize = fontSize;
            if (Math.Abs(text.outlineWidth - 0.14f) > 0.001f) text.outlineWidth = 0.14f;
            var outlineColor = new Color32(4, 6, 10, 238);
            if (!text.outlineColor.Equals(outlineColor)) text.outlineColor = outlineColor;
            view.AppliedTypographyRevision = _typographyRevision;
            view.AppliedFontSize = fontSize;
            view.HasMeasurement = false;
        }

        private static int InstanceIdentity(UnityEngine.Object? value)
        {
            try { return value != null ? value.GetInstanceID() : 0; }
            catch { return 0; }
        }

        private void ResolveNativeTypography()
        {
            _typographyResolved = true;
            _typographyRevision = _typographyRevision == int.MaxValue ? 1 : _typographyRevision + 1;
            TextMeshProUGUI? best = null;
            var bestScore = int.MinValue;
            try
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                {
                    if (candidate == null || candidate.font == null || candidate.gameObject == null) continue;
                    if (_canvasObject != null && candidate.transform.IsChildOf(_canvasObject.transform)) continue;
                    if (!candidate.gameObject.scene.IsValid()) continue;
                    var name = (candidate.gameObject.name ?? string.Empty).ToLowerInvariant();
                    var score = candidate.gameObject.activeInHierarchy ? 4 : 0;
                    if (name.Contains("ping")) score += 12;
                    if (name.Contains("objective")) score += 9;
                    if (name.Contains("hud")) score += 7;
                    if (name.Contains("money") || name.Contains("level")) score += 5;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug("[ItemShareFix] HUD typography scan failed; TMP default will be used: " + ex.GetType().Name);
            }

            if (best != null)
            {
                _nativeFont = best.font;
                _nativeFontMaterial = best.fontSharedMaterial;
                _log.LogInfo("[ItemShareFix] native HUD typography source=" + best.gameObject.name + " style=" + MarkerHudNavigationPolicy.NativeHudStyleToken);
            }
            else
            {
                try { _nativeFont = TMP_Settings.defaultFontAsset; } catch { _nativeFont = null; }
                _nativeFontMaterial = null;
                _log.LogInfo("[ItemShareFix] native HUD typography source=TMP-default style=" + MarkerHudNavigationPolicy.NativeHudStyleToken);
            }

            foreach (var view in _views.Values)
            {
                view.AppliedTypographyRevision = int.MinValue;
                view.HasMeasurement = false;
            }
        }

        private static ulong ComputeSemanticInputSignature(IReadOnlyList<MarkerRenderInput> inputs)
        {
            ulong xor = 0UL;
            ulong sum = 0UL;
            var count = Math.Min(inputs.Count, MarkerPresentationPolicy.MaxLogicalMarkers);
            for (var i = 0; i < count; i++)
            {
                var item = inputs[i];
                var h = Mix64(unchecked((ulong)item.StableKey) ^ ((ulong)(uint)item.Kind << 48) ^ StableStringHash(item.ItemSemanticKey));
                xor ^= h;
                sum += h * 1099511628211UL;
            }
            return Mix64(xor ^ sum ^ (ulong)(uint)count);
        }

        private static ulong StableStringHash(string value)
        {
            ulong hash = 14695981039346656037UL;
            if (value == null) return hash;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                hash ^= (byte)(ch & 0xFF);
                hash *= 1099511628211UL;
                hash ^= (byte)(ch >> 8);
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static ulong Mix64(ulong x)
        {
            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9UL;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebUL;
            x ^= x >> 31;
            return x;
        }

        private static bool SettingsEqual(MarkerPresentationSettings left, MarkerPresentationSettings right)
            => left.Mode == right.Mode
               && left.ShowDistance == right.ShowDistance
               && Math.Abs(left.Scale - right.Scale) <= 0.0001f
               && left.DetailRows == right.DetailRows
               && left.ShowCategoryDiamond == right.ShowCategoryDiamond
               && left.ShowTierComposition == right.ShowTierComposition
               && left.CompactShowCount == right.CompactShowCount
               && left.CompactMixedStyle == right.CompactMixedStyle
               && left.CategorySortOrder == right.CategorySortOrder
               && left.UseCategorySummaryPresentation == right.UseCategorySummaryPresentation;

        private static bool VisualSettingsEqual(MarkerVisualConfigSnapshot left, MarkerVisualConfigSnapshot right)
            => Math.Abs(left.MarkerOpacity - right.MarkerOpacity) <= 0.0001f
               && Math.Abs(left.MarkerBackgroundOpacity - right.MarkerBackgroundOpacity) <= 0.0001f
               && left.OffscreenEnabled == right.OffscreenEnabled
               && left.ShowOffscreenDistance == right.ShowOffscreenDistance
               && left.ShowOffscreenTotalCount == right.ShowOffscreenTotalCount
               && Math.Abs(left.OffscreenScale - right.OffscreenScale) <= 0.0001f
               && Math.Abs(left.OffscreenOpacity - right.OffscreenOpacity) <= 0.0001f
               && Math.Abs(left.OffscreenEdgePadding - right.OffscreenEdgePadding) <= 0.0001f
               && left.CommonColor.Equals(right.CommonColor)
               && left.UncommonColor.Equals(right.UncommonColor)
               && left.LegendaryColor.Equals(right.LegendaryColor)
               && left.BossColor.Equals(right.BossColor)
               && left.LunarColor.Equals(right.LunarColor)
               && left.VoidColor.Equals(right.VoidColor)
               && left.EquipmentColor.Equals(right.EquipmentColor)
               && left.CommandColor.Equals(right.CommandColor)
               && left.NeutralColor.Equals(right.NeutralColor)
               && left.OffscreenColor.Equals(right.OffscreenColor);

        private static bool PlacementDiagnosticStateChanged(MarkerHudPlacement previous, MarkerHudPlacement current)
            => previous.Mode != current.Mode
               || previous.Edge != current.Edge
               || previous.LaneSlot != current.LaneSlot
               || previous.RailSlot != current.RailSlot
               || previous.HudRelocated != current.HudRelocated
               || previous.MessageHudRelocated != current.MessageHudRelocated
               || previous.CollisionRelocated != current.CollisionRelocated;

        private bool DynamicHudZonesChanged(IReadOnlyList<MarkerHudExclusionZone>? zones)
        {
            var count = zones?.Count ?? 0;
            if (_cachedDynamicHudZones.Count != count)
            {
                CopyDynamicHudZones(zones);
                return true;
            }
            for (var i = 0; i < count; i++)
            {
                var left = _cachedDynamicHudZones[i];
                var right = zones![i];
                if (!string.Equals(left.Token, right.Token, StringComparison.Ordinal)
                    || Math.Abs(left.Left - right.Left) > MarkerFramePipelinePolicy.HudRectEpsilonPixels
                    || Math.Abs(left.Right - right.Right) > MarkerFramePipelinePolicy.HudRectEpsilonPixels
                    || Math.Abs(left.Bottom - right.Bottom) > MarkerFramePipelinePolicy.HudRectEpsilonPixels
                    || Math.Abs(left.Top - right.Top) > MarkerFramePipelinePolicy.HudRectEpsilonPixels)
                {
                    CopyDynamicHudZones(zones);
                    return true;
                }
            }
            return false;
        }

        private void CopyDynamicHudZones(IReadOnlyList<MarkerHudExclusionZone>? zones)
        {
            _cachedDynamicHudZones.Clear();
            if (zones == null) return;
            for (var i = 0; i < zones.Count; i++) _cachedDynamicHudZones.Add(zones[i]);
        }

        private void RemoveView(long clusterKey)
        {
            if (!_views.TryGetValue(clusterKey, out var view)) return;
            if (view.Root != null) UnityEngine.Object.Destroy(view.Root);
            _views.Remove(clusterKey);
            _placementCacheValid = false;
        }

        private static int ResolveUiLayer()
        {
            var layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }

        private static Color SanitizeColor(Color color)
        {
            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b) || !IsFinite(color.a)) return Color.white;
            var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (max < 0.22f) color = Color.Lerp(color, Color.white, 0.55f);
            color.a = Mathf.Clamp(color.a <= 0.05f ? 1f : color.a, 0.82f, 1f);
            return color;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
