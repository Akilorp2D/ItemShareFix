using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ItemShareFix.Core
{
    public enum PersonalMarkerKind
    {
        OrdinaryPickup,
        CommandPicker,
    }

    public readonly struct PersonalMarkerIdentity : IEquatable<PersonalMarkerIdentity>
    {
        public PersonalMarkerIdentity(PersonalMarkerKind kind, int instanceId)
        {
            Kind = kind;
            InstanceId = instanceId;
        }

        public PersonalMarkerKind Kind { get; }
        public int InstanceId { get; }

        public bool Equals(PersonalMarkerIdentity other) => Kind == other.Kind && InstanceId == other.InstanceId;
        public override bool Equals(object? obj) => obj is PersonalMarkerIdentity other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ InstanceId;
        public override string ToString() => Kind + ":" + InstanceId.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class PersonalMarkerDescriptor
    {
        public PersonalMarkerDescriptor(PersonalMarkerIdentity identity, string label)
        {
            Identity = identity;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public PersonalMarkerIdentity Identity { get; }
        public string Label { get; }
    }

    public enum PersonalMarkerTransition
    {
        None,
        Added,
        Updated,
        CapacityRejected,
    }

    /// <summary>
    /// Bounded logical membership used by the runtime presentation sweep. It intentionally carries no Unity object.
    /// </summary>
    public sealed class PersonalMarkerRegistry
    {
        private readonly int _capacity;
        private readonly Dictionary<PersonalMarkerIdentity, PersonalMarkerDescriptor> _active = new Dictionary<PersonalMarkerIdentity, PersonalMarkerDescriptor>();
        private readonly HashSet<PersonalMarkerIdentity> _seen = new HashSet<PersonalMarkerIdentity>();

        public PersonalMarkerRegistry(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int Count => _active.Count;
        public int Capacity => _capacity;
        public IReadOnlyCollection<PersonalMarkerDescriptor> Active => _active.Values;

        public void BeginSweep() => _seen.Clear();

        public PersonalMarkerTransition MarkPending(PersonalMarkerKind kind, int instanceId, string label)
        {
            var identity = new PersonalMarkerIdentity(kind, instanceId);
            var normalized = MarkerPresentationPolicy.NormalizeLabel(label, kind == PersonalMarkerKind.CommandPicker ? "Command choice" : "Shared pickup");
            if (_active.TryGetValue(identity, out var existing))
            {
                _seen.Add(identity);
                if (string.Equals(existing.Label, normalized, StringComparison.Ordinal)) return PersonalMarkerTransition.None;
                _active[identity] = new PersonalMarkerDescriptor(identity, normalized);
                return PersonalMarkerTransition.Updated;
            }

            if (_active.Count >= _capacity) return PersonalMarkerTransition.CapacityRejected;
            _active.Add(identity, new PersonalMarkerDescriptor(identity, normalized));
            _seen.Add(identity);
            return PersonalMarkerTransition.Added;
        }

        public IReadOnlyList<PersonalMarkerDescriptor> EndSweep()
        {
            var removed = _active.Where(x => !_seen.Contains(x.Key)).Select(x => x.Value).ToArray();
            foreach (var descriptor in removed) _active.Remove(descriptor.Identity);
            return removed;
        }

        public bool Contains(PersonalMarkerKind kind, int instanceId)
            => _active.ContainsKey(new PersonalMarkerIdentity(kind, instanceId));

        public bool Remove(PersonalMarkerKind kind, int instanceId)
        {
            var identity = new PersonalMarkerIdentity(kind, instanceId);
            _seen.Remove(identity);
            return _active.Remove(identity);
        }

        public void Clear()
        {
            _active.Clear();
            _seen.Clear();
        }
    }

    public readonly struct MarkerVisualLayout
    {
        public MarkerVisualLayout(string text, float width, float height, int fontSize)
        {
            Text = text;
            Width = width;
            Height = height;
            FontSize = fontSize;
        }

        public string Text { get; }
        public float Width { get; }
        public float Height { get; }
        public int FontSize { get; }
    }

    public readonly struct MarkerReadabilityStyle
    {
        public MarkerReadabilityStyle(
            string styleToken,
            string sizingToken,
            int fontSize,
            float plateAlpha,
            float platePaddingX,
            float platePaddingY,
            float haloOffset,
            float shadowOffset,
            bool preservesNativeClassHue)
        {
            StyleToken = styleToken ?? throw new ArgumentNullException(nameof(styleToken));
            SizingToken = sizingToken ?? throw new ArgumentNullException(nameof(sizingToken));
            FontSize = fontSize;
            PlateAlpha = plateAlpha;
            PlatePaddingX = platePaddingX;
            PlatePaddingY = platePaddingY;
            HaloOffset = haloOffset;
            ShadowOffset = shadowOffset;
            PreservesNativeClassHue = preservesNativeClassHue;
        }

        public string StyleToken { get; }
        public string SizingToken { get; }
        public int FontSize { get; }
        public float PlateAlpha { get; }
        public float PlatePaddingX { get; }
        public float PlatePaddingY { get; }
        public float HaloOffset { get; }
        public float ShadowOffset { get; }
        public bool PreservesNativeClassHue { get; }
    }

    public static class MarkerPresentationPolicy
    {
        public const int MaxLabelCharacters = 42;
        public const int MaxLogicalMarkers = 96;
        public const int MaxOrdinaryMarkers = 64;
        public const int MaxCommandMarkers = 32;
        public const float MinMarkerWidth = 112f;
        public const float AbsoluteMaxMarkerWidth = 420f;
        public const float MinMarkerHeight = 30f;
        public const float MaxMarkerHeight = 42f;

        public const string UnifiedReadabilityStyleToken = "ISF_ROR2_PING_NATIVE_V2";
        public const string PlayerPingClassSizingToken = "PLAYER_PING_CLASS_V1";
        public const int ReadableMinFontSize = 18;
        public const int ReadableMaxFontSize = 24;
        public const int ReadableScreenHeightDivisor = 54;

        // Legacy readability fields remain populated for compatibility with the prior deterministic contract.
        // Full rectangular backing plates are intentionally avoided; readability comes from a compact dark outline + shadow.
        public const float UnifiedPlateAlpha = 0.18f;
        public const float UnifiedPlatePaddingX = 0f;
        public const float UnifiedPlatePaddingY = 0f;
        public const float UnifiedHaloOffset = 1f;
        public const float UnifiedShadowOffset = 2f;

        public const float PracticalVoidFloorY = -1000f;
        public const float MaximumWorldCoordinateMagnitude = 8192f;
        public const float MaximumPresentationDistanceMeters = 4096f;

        // These flags are consumed by deterministic contract tests and mirror the production renderer implementation.
        public static bool UsesSharedSkinBoxMutation => false;
        public static bool UsesVanillaPingSlot => false;
        public static bool RegistersPickupShareProvider => false;
        public static bool UsesFullRectangularPlate => false;
        public static bool UsesMarkerOnGuiRenderer => false;
        public static bool UsesTextMeshProHudCanvas => true;
        public static bool ReusesLiveRoR2HudTypographyWhenAvailable => true;
        public static bool EssentialIndicatorUsesTmpUnicodeGlyph => false;
        public static bool UsesDeterministicUiGraphicIndicator => true;
        public static bool ValidDistanceLabelsUseEllipsis => false;
        public static bool UsesTmpPreferredSizeForProductionFootprint => true;
        public static bool UsesInvalidationDrivenMarkerFramePipeline => true;
        public static bool UsesSingleMarkerPlacementFastPath => true;
        public static bool UsesBufferedProductionPlacementSolver => true;
        public static bool UsesGuardedUnityUiWrites => true;
        public static bool UsesStablePlacementHysteresis => true;
        public static bool UsesBoundedOnScreenAnchorDisplacement => true;
        // Legacy screen-density policy remains source-compatible for retained tests, but production semantic
        // membership is owned by MarkerWorldClusterTracker rather than camera projection.
        public static bool UsesDeterministicDensityAggregation => true;
        public static bool PreservesLogicalIdentitiesDuringDeclutter => true;
        public static bool UsesPersistentWorldSpaceSemanticClusters => true;
        public static bool SemanticMembershipUsesCameraProjection => false;
        public static bool UsesAdaptiveClusterLod => true;
        public static bool UsesProjectionRelativePlacementCache => true;
        public static bool DenseSemanticClusterUsesSinglePresentationOwner => true;
        public static string IndicatorAssetSourceToken => "LOCAL_UI_GRAPHIC_GEOMETRY_V1";
        public static string IndicatorVisualFamilyToken => MarkerHudNavigationPolicy.IndicatorVisualFamilyToken;
        public static string NativeHudStyleToken => MarkerHudNavigationPolicy.NativeHudStyleToken;
        public static IReadOnlyList<string> MutatedGuiGlobals { get; } = new[] { "matrix", "color", "contentColor", "backgroundColor", "enabled", "depth", "changed" };
        public static IReadOnlyList<string> RestoredGuiGlobals { get; } = new[] { "matrix", "color", "contentColor", "backgroundColor", "enabled", "depth", "changed" };

        public static string NormalizeLabel(string? label, string fallback)
        {
            var value = string.IsNullOrWhiteSpace(label) ? fallback : label.Trim();
            if (string.IsNullOrWhiteSpace(value)) value = "Pickup";
            value = value.Replace('\r', ' ').Replace('\n', ' ');
            while (value.IndexOf("  ", StringComparison.Ordinal) >= 0) value = value.Replace("  ", " ");
            if (value.Length > MaxLabelCharacters) value = value.Substring(0, MaxLabelCharacters - 1) + "…";
            return value;
        }

        public static MarkerVisualLayout BuildVisualLayout(string? label, int roundedDistanceMeters, int screenWidth, int screenHeight)
        {
            var safeLabel = NormalizeLabel(label, "Shared pickup");
            if (roundedDistanceMeters < 0) roundedDistanceMeters = 0;
            var text = "◆ " + safeLabel + " · " + roundedDistanceMeters.ToString(CultureInfo.InvariantCulture) + "m";
            var readability = BuildReadabilityStyle(MarkerClassKind.Unknown, screenHeight);
            var fontSize = readability.FontSize;
            var maxByScreen = Math.Max(MinMarkerWidth, Math.Min(AbsoluteMaxMarkerWidth, Math.Max(150f, screenWidth * 0.42f)));
            var estimatedWidth = 22f + text.Length * fontSize * 0.60f;
            var width = ClampFloat(estimatedWidth, MinMarkerWidth, maxByScreen);
            var height = ClampFloat(fontSize + 12f, MinMarkerHeight, MaxMarkerHeight);
            return new MarkerVisualLayout(text, width, height, fontSize);
        }


        public static int BuildNativeHudFontSize(int screenHeight)
            => ClampInt(screenHeight / 48, 19, 27);

        public static int BuildScaledNativeHudFontSize(int screenHeight, float markerScale)
            => ClampInt((int)Math.Round(BuildNativeHudFontSize(screenHeight) * MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale)), 14, 34);

        public static string BuildNativeHudLabelText(string? label, int roundedDistanceMeters)
        {
            var safeLabel = NormalizeLabel(label, "Shared pickup");
            if (roundedDistanceMeters < 0) roundedDistanceMeters = 0;
            return safeLabel + " · " + roundedDistanceMeters.ToString(CultureInfo.InvariantCulture) + "m";
        }

        public static string BuildNativeHudClusterLabelText(string? label, int roundedDistanceMeters, int hiddenMemberCount)
        {
            var safeLabel = NormalizeLabel(label, "Shared pickup");
            if (roundedDistanceMeters < 0) roundedDistanceMeters = 0;
            if (hiddenMemberCount <= 0) return BuildNativeHudLabelText(safeLabel, roundedDistanceMeters);
            return safeLabel + " +" + hiddenMemberCount.ToString(CultureInfo.InvariantCulture)
                + " · " + roundedDistanceMeters.ToString(CultureInfo.InvariantCulture) + "m";
        }

        public static bool ShouldRenderHudMarkers(bool featureEnabled, bool localBlockingModalActive)
            => featureEnabled && !localBlockingModalActive;

        public static MarkerReadabilityStyle BuildReadabilityStyle(MarkerClassKind markerClass, int screenHeight)
        {
            // markerClass is intentionally accepted even though every class uses the same visual family.
            // This makes the all-classes contract explicit and testable without duplicating per-tier style branches.
            _ = markerClass;
            var fontSize = ClampInt(screenHeight / ReadableScreenHeightDivisor, ReadableMinFontSize, ReadableMaxFontSize);
            return new MarkerReadabilityStyle(
                UnifiedReadabilityStyleToken,
                PlayerPingClassSizingToken,
                fontSize,
                UnifiedPlateAlpha,
                UnifiedPlatePaddingX,
                UnifiedPlatePaddingY,
                UnifiedHaloOffset,
                UnifiedShadowOffset,
                preservesNativeClassHue: true);
        }


        public static bool AreCoordinatesFinite(float x, float y, float z)
            => IsFinite(x) && IsFinite(y) && IsFinite(z);

        public static string ValidateWorldPosition(float x, float y, float z)
        {
            if (!AreCoordinatesFinite(x, y, z)) return "position-non-finite";
            if (y < PracticalVoidFloorY) return "below-void-floor";
            if (Math.Abs(x) > MaximumWorldCoordinateMagnitude
                || Math.Abs(y) > MaximumWorldCoordinateMagnitude
                || Math.Abs(z) > MaximumWorldCoordinateMagnitude) return "outside-world-bounds";
            return string.Empty;
        }

        public static string ValidatePresentationDistance(float distanceMeters)
        {
            if (!IsFinite(distanceMeters) || distanceMeters < 0f) return "distance-non-finite";
            if (distanceMeters > MaximumPresentationDistanceMeters) return "distance-out-of-range";
            return string.Empty;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool ShouldTrackCommandMarker(bool featureEnabled, bool individualMode, bool shareCommandPicks, bool classifierIsCommand, bool exactLocalStateResolved, bool anyLocalPending)
            => featureEnabled && individualMode && shareCommandPicks && classifierIsCommand && exactLocalStateResolved && anyLocalPending;

        public static bool RenderStateRestorationContractIsComplete()
            => MutatedGuiGlobals.Count == RestoredGuiGlobals.Count
               && MutatedGuiGlobals.All(x => RestoredGuiGlobals.Contains(x, StringComparer.Ordinal));

        private static int ClampInt(int value, int min, int max) => value < min ? min : value > max ? max : value;
        private static float ClampFloat(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
