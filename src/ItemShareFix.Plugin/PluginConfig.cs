using System;
using BepInEx.Configuration;
using ItemShareFix.Core;
using UnityEngine;

namespace ItemShareFix
{
    internal readonly struct MarkerVisualConfigSnapshot
    {
        public MarkerVisualConfigSnapshot(
            float markerOpacity,
            float markerBackgroundOpacity,
            bool offscreenEnabled,
            bool showOffscreenDistance,
            bool showOffscreenTotalCount,
            float offscreenScale,
            float offscreenOpacity,
            float offscreenEdgePadding,
            Color commonColor,
            Color uncommonColor,
            Color legendaryColor,
            Color bossColor,
            Color lunarColor,
            Color voidColor,
            Color equipmentColor,
            Color commandColor,
            Color neutralColor,
            Color offscreenColor)
        {
            MarkerOpacity = MarkerVisualSettingsPolicy.ClampOpacity(markerOpacity, MarkerVisualSettingsPolicy.MarkerOpacityDefault);
            MarkerBackgroundOpacity = MarkerVisualSettingsPolicy.ClampOpacity(markerBackgroundOpacity, MarkerVisualSettingsPolicy.BackgroundOpacityDefault);
            OffscreenEnabled = offscreenEnabled;
            ShowOffscreenDistance = showOffscreenDistance;
            ShowOffscreenTotalCount = showOffscreenTotalCount;
            OffscreenScale = MarkerVisualSettingsPolicy.ClampOffscreenScale(offscreenScale);
            OffscreenOpacity = MarkerVisualSettingsPolicy.ClampOpacity(offscreenOpacity, MarkerVisualSettingsPolicy.OffscreenOpacityDefault);
            OffscreenEdgePadding = MarkerVisualSettingsPolicy.ClampOffscreenEdgePadding(offscreenEdgePadding);
            CommonColor = commonColor;
            UncommonColor = uncommonColor;
            LegendaryColor = legendaryColor;
            BossColor = bossColor;
            LunarColor = lunarColor;
            VoidColor = voidColor;
            EquipmentColor = equipmentColor;
            CommandColor = commandColor;
            NeutralColor = neutralColor;
            OffscreenColor = offscreenColor;
        }

        public float MarkerOpacity { get; }
        public float MarkerBackgroundOpacity { get; }
        public bool OffscreenEnabled { get; }
        public bool ShowOffscreenDistance { get; }
        public bool ShowOffscreenTotalCount { get; }
        public float OffscreenScale { get; }
        public float OffscreenOpacity { get; }
        public float OffscreenEdgePadding { get; }
        public Color CommonColor { get; }
        public Color UncommonColor { get; }
        public Color LegendaryColor { get; }
        public Color BossColor { get; }
        public Color LunarColor { get; }
        public Color VoidColor { get; }
        public Color EquipmentColor { get; }
        public Color CommandColor { get; }
        public Color NeutralColor { get; }
        public Color OffscreenColor { get; }
    }

    internal sealed class PluginConfig
    {
        // Deterministic defaults mirror established ItemShareFix/RoR2 tier cues while making
        // every presentation color explicitly resettable and user-overridable.
        public static readonly Color DefaultCommonColor = new Color32(255, 255, 255, 255);
        public static readonly Color DefaultUncommonColor = new Color32(119, 255, 17, 255);
        public static readonly Color DefaultLegendaryColor = new Color32(255, 63, 63, 255);
        public static readonly Color DefaultBossColor = new Color32(255, 224, 64, 255);
        public static readonly Color DefaultLunarColor = new Color32(112, 187, 255, 255);
        public static readonly Color DefaultVoidColor = new Color32(215, 92, 255, 255);
        public static readonly Color DefaultEquipmentColor = new Color32(255, 138, 35, 255);
        public static readonly Color DefaultCommandColor = new Color32(89, 209, 255, 255);
        public static readonly Color DefaultNeutralColor = new Color32(255, 255, 255, 255);
        public static readonly Color DefaultOffscreenColor = new Color32(255, 255, 255, 255);

        public PluginConfig(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Master switch for ItemShareFix.");
            ShareTemporaryItems = config.Bind("General", "ShareTemporaryItems", false, "Share temporary item pickups through ItemShare. Fresh configs default to disabled; existing saved values are preserved. Disable this to give temporary pickups vanilla first-come-first-served behavior instead of ItemShare distribution.");
            PersonalPickupVisibilityRepairEnabled = config.Bind("General", "PersonalPickupVisibilityRepairEnabled", true, "Repair ItemShare 1.7.1 personal ordinary-pickup visibility. The upstream HideCollectedOrbs preference is still respected.");
            PersonalMarkersEnabled = config.Bind("General", "PersonalMarkersEnabled", true, "Draw local-only automatic markers for shared ordinary pickups and Artifact of Command choices still pending for a local participant.");
            DeadPlayerDeferredItemsEnabled = config.Bind("General", "DeadPlayerDeferredItemsEnabled", true, "FULLY_DEAD participants do not receive ItemShare's immediate ShareToDead grant; their entitlement is deferred to the next safe restored-player point.");
            DisconnectCleanupEnabled = config.Bind("General", "DisconnectCleanupEnabled", true, "Cancel ItemShareFix pending/deferred state when a participant disconnects and prevent absence catch-up.");

            MarkerPresentationMode = config.Bind(
                "Markers",
                "MarkerPresentationMode",
                ItemShareFix.Core.MarkerPresentationMode.Detailed,
                "Marker presentation: Detailed (default truthful localized titles/composition) or Compact (minimal diamond presentation).");
            ShowMarkerDistance = config.Bind("Markers", "ShowMarkerDistance", true, "Show distance for in-FOV world marker cards.");
            MarkerScale = config.Bind(
                "Markers",
                "MarkerScale",
                MarkerClusterPresentationPolicy.MarkerScaleDefault,
                new ConfigDescription(
                    "Marker UI scale. Presentation-only; does not rebuild physical or dense world membership.",
                    new AcceptableValueRange<float>(MarkerClusterPresentationPolicy.MarkerScaleMin, MarkerClusterPresentationPolicy.MarkerScaleMax)));
            MarkerOpacity = config.Bind(
                "Markers", "MarkerOpacity", MarkerVisualSettingsPolicy.MarkerOpacityDefault,
                new ConfigDescription("World marker opacity.", new AcceptableValueRange<float>(0f, 1f)));
            MarkerBackgroundOpacity = config.Bind(
                "Markers", "MarkerBackgroundOpacity", MarkerVisualSettingsPolicy.BackgroundOpacityDefault,
                new ConfigDescription("World marker background opacity. Default 0 preserves the established transparent appearance.", new AcceptableValueRange<float>(0f, 1f)));

            ShowMarkerCategoryDiamond = config.Bind("Markers", "ShowMarkerCategoryDiamond", true, "Detailed mode: show the tier/category diamond cue.");
            // MarkerDetailRows applies to ordinary Detailed exact-item rows. The two other legacy
            // grouped-presentation controls remain bound only so existing cfg files parse cleanly.
            ShowMarkerTierComposition = config.Bind("Markers", "ShowMarkerTierComposition", true,
                "Legacy compatibility value retained for existing config files. Grouped category summaries are always truthful and this value no longer changes grouped rows.");
            MarkerDetailRows = config.Bind(
                "Markers",
                "MarkerDetailRows",
                MarkerClusterPresentationPolicy.MarkerDetailRowsDefault,
                new ConfigDescription(
                    "Ordinary Detailed mode: maximum visible distinct item rows before the localized overflow row.",
                    new AcceptableValueRange<int>(MarkerClusterPresentationPolicy.MarkerDetailRowsMin, MarkerClusterPresentationPolicy.MarkerDetailRowsMax)));

            MarkerCategorySortOrder = config.Bind(
                "Markers",
                "MarkerCategorySortOrder",
                MarkerCategorySummaryPolicy.DefaultSortOrder,
                "Grouped category display order. HighToLow (default) or exact reverse LowToHigh; presentation-only.");
            MarkerCompactShowCount = config.Bind("Markers", "MarkerCompactShowCount", true, "Compact pyramid: show each represented category subtotal on its own diamond.");
            MarkerCompactMixedStyle = config.Bind("Markers", "MarkerCompactMixedStyle", ItemShareFix.Core.MarkerCompactMixedStyle.CategoryDiamondPyramid,
                "Legacy compatibility selector retained for parsing only. Grouped Compact is always CategoryDiamondPyramid and this entry is not exposed in Risk Of Options.");

            EnableOffscreenIndicators = config.Bind("Markers", "EnableOffscreenIndicators", true, "Show one directional indicator per occupied broad off-screen direction.");
            ShowOffscreenDistance = config.Bind("Markers", "ShowOffscreenDistance", true, "Show nearest represented pending distance on each off-screen directional indicator.");
            ShowOffscreenTotalCount = config.Bind("Markers", "ShowOffscreenTotalCount", false, "Optionally show one total count per occupied off-screen direction sector.");
            OffscreenIndicatorScale = config.Bind("Markers", "OffscreenIndicatorScale", MarkerVisualSettingsPolicy.OffscreenScaleDefault,
                new ConfigDescription("Off-screen directional indicator scale.", new AcceptableValueRange<float>(MarkerVisualSettingsPolicy.OffscreenScaleMin, MarkerVisualSettingsPolicy.OffscreenScaleMax)));
            OffscreenIndicatorOpacity = config.Bind("Markers", "OffscreenIndicatorOpacity", MarkerVisualSettingsPolicy.OffscreenOpacityDefault,
                new ConfigDescription("Off-screen directional indicator opacity.", new AcceptableValueRange<float>(0f, 1f)));
            OffscreenEdgePadding = config.Bind("Markers", "OffscreenEdgePadding", MarkerVisualSettingsPolicy.OffscreenEdgePaddingDefault,
                new ConfigDescription("Minimum screen-edge padding for directional indicators.", new AcceptableValueRange<float>(MarkerVisualSettingsPolicy.OffscreenEdgePaddingMin, MarkerVisualSettingsPolicy.OffscreenEdgePaddingMax)));

            CommonMarkerColor = config.Bind("Marker Colors", "Common", DefaultCommonColor, "Common/white marker color.");
            UncommonMarkerColor = config.Bind("Marker Colors", "Uncommon", DefaultUncommonColor, "Uncommon/green marker color.");
            LegendaryMarkerColor = config.Bind("Marker Colors", "Legendary", DefaultLegendaryColor, "Legendary/red marker color.");
            BossMarkerColor = config.Bind("Marker Colors", "Boss", DefaultBossColor, "Boss marker color.");
            LunarMarkerColor = config.Bind("Marker Colors", "Lunar", DefaultLunarColor, "Lunar marker color. LunarEquipment maps to this same palette entry.");
            VoidMarkerColor = config.Bind("Marker Colors", "Void", DefaultVoidColor, "Void marker color.");
            EquipmentMarkerColor = config.Bind("Marker Colors", "Equipment", DefaultEquipmentColor, "Equipment marker color.");
            CommandMarkerColor = config.Bind("Marker Colors", "Command", DefaultCommandColor, "Artifact of Command / unresolved-choice marker color.");
            NeutralMarkerColor = config.Bind("Marker Colors", "Neutral", DefaultNeutralColor, "Mixed/unknown/other marker color. Other maps deterministically to Neutral.");
            OffscreenIndicatorColor = config.Bind("Marker Colors", "OffscreenIndicator", DefaultOffscreenColor, "Independent off-screen directional indicator color.");

            BindPresentationInvalidation(ShareTemporaryItems);
            BindPresentationInvalidation(PersonalMarkersEnabled);
            BindPresentationInvalidation(MarkerPresentationMode);
            BindPresentationInvalidation(ShowMarkerDistance);
            BindPresentationInvalidation(MarkerScale);
            BindPresentationInvalidation(MarkerOpacity);
            BindPresentationInvalidation(MarkerBackgroundOpacity);
            BindPresentationInvalidation(ShowMarkerCategoryDiamond);
            BindPresentationInvalidation(ShowMarkerTierComposition);
            BindPresentationInvalidation(MarkerDetailRows);
            BindPresentationInvalidation(MarkerCategorySortOrder);
            BindPresentationInvalidation(MarkerCompactShowCount);
            BindPresentationInvalidation(MarkerCompactMixedStyle);
            BindPresentationInvalidation(EnableOffscreenIndicators);
            BindPresentationInvalidation(ShowOffscreenDistance);
            BindPresentationInvalidation(ShowOffscreenTotalCount);
            BindPresentationInvalidation(OffscreenIndicatorScale);
            BindPresentationInvalidation(OffscreenIndicatorOpacity);
            BindPresentationInvalidation(OffscreenEdgePadding);
            BindPresentationInvalidation(CommonMarkerColor);
            BindPresentationInvalidation(UncommonMarkerColor);
            BindPresentationInvalidation(LegendaryMarkerColor);
            BindPresentationInvalidation(BossMarkerColor);
            BindPresentationInvalidation(LunarMarkerColor);
            BindPresentationInvalidation(VoidMarkerColor);
            BindPresentationInvalidation(EquipmentMarkerColor);
            BindPresentationInvalidation(CommandMarkerColor);
            BindPresentationInvalidation(NeutralMarkerColor);
            BindPresentationInvalidation(OffscreenIndicatorColor);

            DiagnosticLogging = config.Bind("Diagnostics", "DiagnosticLogging", true, "Enable bounded diagnostic logging for compatibility probes and state transitions.");
            DiagnosticLogLevel = config.Bind("Diagnostics", "DiagnosticLogLevel", "Info", "Diagnostic level: Error, Warning, Info, Debug.");
            PresentationSweepSeconds = config.Bind("Diagnostics", "PresentationSweepSeconds", 0.20f, "Bounded ItemShareFix presentation refresh interval. Not a whole-scene scan; only RoR2 InstanceTracker pickups are inspected.");
            ParticipantSweepSeconds = config.Bind("Diagnostics", "ParticipantSweepSeconds", 0.25f, "Server participant-state refresh interval.");
            RemoteOperationGraceSeconds = config.Bind("Diagnostics", "RemoteOperationGraceSeconds", 2.0f, "Reserved compatibility setting. Support Drone state uses exact CharacterMaster.GetInRemoteOp(); no heuristic grace is used.");
        }

        public event EventHandler? MarkerPresentationSettingChanged;

        public ConfigEntry<bool> Enabled { get; }
        public ConfigEntry<bool> ShareTemporaryItems { get; }
        public ConfigEntry<bool> PersonalPickupVisibilityRepairEnabled { get; }
        public ConfigEntry<bool> PersonalMarkersEnabled { get; }
        public ConfigEntry<bool> DeadPlayerDeferredItemsEnabled { get; }
        public ConfigEntry<bool> DisconnectCleanupEnabled { get; }

        public ConfigEntry<ItemShareFix.Core.MarkerPresentationMode> MarkerPresentationMode { get; }
        public ConfigEntry<bool> ShowMarkerDistance { get; }
        public ConfigEntry<float> MarkerScale { get; }
        public ConfigEntry<float> MarkerOpacity { get; }
        public ConfigEntry<float> MarkerBackgroundOpacity { get; }
        public ConfigEntry<bool> ShowMarkerCategoryDiamond { get; }
        public ConfigEntry<bool> ShowMarkerTierComposition { get; }
        public ConfigEntry<int> MarkerDetailRows { get; }
        public ConfigEntry<ItemShareFix.Core.MarkerCategorySortOrder> MarkerCategorySortOrder { get; }
        public ConfigEntry<bool> MarkerCompactShowCount { get; }
        public ConfigEntry<ItemShareFix.Core.MarkerCompactMixedStyle> MarkerCompactMixedStyle { get; }
        public ConfigEntry<bool> EnableOffscreenIndicators { get; }
        public ConfigEntry<bool> ShowOffscreenDistance { get; }
        public ConfigEntry<bool> ShowOffscreenTotalCount { get; }
        public ConfigEntry<float> OffscreenIndicatorScale { get; }
        public ConfigEntry<float> OffscreenIndicatorOpacity { get; }
        public ConfigEntry<float> OffscreenEdgePadding { get; }
        public ConfigEntry<Color> CommonMarkerColor { get; }
        public ConfigEntry<Color> UncommonMarkerColor { get; }
        public ConfigEntry<Color> LegendaryMarkerColor { get; }
        public ConfigEntry<Color> BossMarkerColor { get; }
        public ConfigEntry<Color> LunarMarkerColor { get; }
        public ConfigEntry<Color> VoidMarkerColor { get; }
        public ConfigEntry<Color> EquipmentMarkerColor { get; }
        public ConfigEntry<Color> CommandMarkerColor { get; }
        public ConfigEntry<Color> NeutralMarkerColor { get; }
        public ConfigEntry<Color> OffscreenIndicatorColor { get; }

        public ConfigEntry<bool> DiagnosticLogging { get; }
        public ConfigEntry<string> DiagnosticLogLevel { get; }
        public ConfigEntry<float> PresentationSweepSeconds { get; }
        public ConfigEntry<float> ParticipantSweepSeconds { get; }
        public ConfigEntry<float> RemoteOperationGraceSeconds { get; }

        public MarkerPresentationSettings MarkerSettingsSnapshot()
            => new MarkerPresentationSettings(
                MarkerPresentationMode.Value,
                ShowMarkerDistance.Value,
                MarkerScale.Value,
                MarkerDetailRows.Value,
                ShowMarkerCategoryDiamond.Value,
                ShowMarkerTierComposition.Value,
                MarkerCompactShowCount.Value,
                MarkerCompactMixedStyle.Value,
                MarkerCategorySortOrder.Value);

        public MarkerVisualConfigSnapshot MarkerVisualSettingsSnapshot()
            => new MarkerVisualConfigSnapshot(
                MarkerOpacity.Value,
                MarkerBackgroundOpacity.Value,
                EnableOffscreenIndicators.Value,
                ShowOffscreenDistance.Value,
                ShowOffscreenTotalCount.Value,
                OffscreenIndicatorScale.Value,
                OffscreenIndicatorOpacity.Value,
                OffscreenEdgePadding.Value,
                CommonMarkerColor.Value,
                UncommonMarkerColor.Value,
                LegendaryMarkerColor.Value,
                BossMarkerColor.Value,
                LunarMarkerColor.Value,
                VoidMarkerColor.Value,
                EquipmentMarkerColor.Value,
                CommandMarkerColor.Value,
                NeutralMarkerColor.Value,
                OffscreenIndicatorColor.Value);

        private void BindPresentationInvalidation<T>(ConfigEntry<T> entry)
        {
            entry.SettingChanged += OnMarkerPresentationSettingChanged;
        }

        private void OnMarkerPresentationSettingChanged(object sender, EventArgs args)
            => MarkerPresentationSettingChanged?.Invoke(sender, args);
    }
}
