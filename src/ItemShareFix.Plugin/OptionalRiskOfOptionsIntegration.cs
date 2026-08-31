using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace ItemShareFix
{
    /// <summary>
    /// Soft Risk Of Options integration. RiskOfOptions types are never referenced at compile time; all option/page API
    /// calls are reflected only after the optional assembly is present. Both ItemShare and ItemShareFix options wrap
    /// their original BepInEx ConfigEntry instances, so Risk Of Options never becomes a second persistence layer.
    /// </summary>
    internal static class OptionalRiskOfOptionsIntegration
    {
        public const string PluginGuid = "com.rune580.riskofoptions";
        public const string RuntimeAssemblyName = "RiskOfOptions";
        public const string StrategyToken = "REFLECTION_SOFT_BINDING_CANONICAL_CONFIGENTRY";
        public const string UpstreamPluginGuid = "com.majai.itemshare";
        public const string PageDescription = "Fixes and extends ItemShare with personal pickup markers, temporary-item sharing, multiplayer fixes, and convenient in-game configuration.";
        public const string IconResourceName = "ItemShareFix.Resources.ItemShareFixIcon.png";
        public const int CurrentMarkerOptionCount = 27;
        public const int ItemShareFixOptionCount = CurrentMarkerOptionCount;
        public const int RequiredUpstreamOptionCount = 15;
        public const int TotalOptionCount = ItemShareFixOptionCount + RequiredUpstreamOptionCount;
        public const string SharingCategory = "Sharing";
        public const string ItemTiersCategory = "Item Tiers";
        public const string MarkersCategory = "Markers";
        public const string OffscreenIndicatorsCategory = "Off-screen Indicators";
        public const string MarkerColorsCategory = "Marker Colors";

        private static readonly string[] ApprovedCategoryOrder =
        {
            SharingCategory,
            ItemTiersCategory,
            MarkersCategory,
            OffscreenIndicatorsCategory,
            MarkerColorsCategory
        };

        private static readonly string[] EnglishPresentationModeChoices = { "Detailed", "Compact" };
        private static readonly string[] EnglishSortOrderChoices = { "High to low", "Low to high" };

        private sealed class RegisteredOptionBinding
        {
            public RegisteredOptionBinding(Assembly assembly, object option, ConfigEntryBase entry, bool refreshLocalization)
            {
                Assembly = assembly;
                Option = option;
                Entry = entry;
                RefreshLocalization = refreshLocalization;
            }

            public Assembly Assembly { get; }
            public object Option { get; }
            public ConfigEntryBase Entry { get; }
            public bool RefreshLocalization { get; }
        }

        private sealed class OptionPlan
        {
            public OptionPlan(object option, ConfigEntryBase entry, bool refreshLocalization)
            {
                Option = option;
                Entry = entry;
                RefreshLocalization = refreshLocalization;
            }

            public object Option { get; }
            public ConfigEntryBase Entry { get; }
            public bool RefreshLocalization { get; }
        }

        private sealed class DisabledPredicateAdapter
        {
            private readonly Func<bool> _predicate;

            public DisabledPredicateAdapter(Func<bool> predicate) => _predicate = predicate;

            public bool Invoke() => _predicate();
        }

        private sealed class UpstreamEntrySpec
        {
            public UpstreamEntrySpec(string section, string key, bool expectsEnum, string optionTypeName, string category, Func<ConfigEntryBase, bool>? disabledWhen)
            {
                Section = section;
                Key = key;
                ExpectsEnum = expectsEnum;
                OptionTypeName = optionTypeName;
                Category = category;
                DisabledWhen = disabledWhen;
            }

            public string Section { get; }
            public string Key { get; }
            public bool ExpectsEnum { get; }
            public string OptionTypeName { get; }
            public string Category { get; }
            public Func<ConfigEntryBase, bool>? DisabledWhen { get; }
        }

        private static readonly object RegistrationLock = new object();
        private static readonly List<RegisteredOptionBinding> RegisteredOptions = new List<RegisteredOptionBinding>(TotalOptionCount);
        private static bool _registrationAttempted;
        private static bool _registrationComplete;
        private static bool _absenceLogged;
        private static string _appliedLanguageKey = string.Empty;

        private static readonly string[] IndividualOnlyUpstreamKeys =
        {
            "AnnounceProgress",
            "ShareCommandPicks",
            "PingShowsPending",
            "HideCollectedOrbs"
        };

        public static void TryRegister(PluginConfig config, ManualLogSource log)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (log == null) throw new ArgumentNullException(nameof(log));

            lock (RegistrationLock)
            {
                if (_registrationComplete || _registrationAttempted) return;

                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(x => string.Equals(x.GetName().Name, RuntimeAssemblyName, StringComparison.Ordinal));
                if (assembly == null)
                {
                    if (!_absenceLogged)
                    {
                        _absenceLogged = true;
                        log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_UI absent strategy=" + StrategyToken
                            + " canonicalConfig=True hardDependency=False deferredRetry=True");
                    }
                    return;
                }

                _registrationAttempted = true;
                try
                {
                    var manager = assembly.GetType("RiskOfOptions.ModSettingsManager", throwOnError: false);
                    if (manager == null)
                    {
                        LogRegistrationFailure(log, 0, "ModSettingsManagerUnavailable");
                        return;
                    }

                    if (!TryResolveUpstreamEntries(out var upstreamEntries, out var upstreamFailure))
                    {
                        LogRegistrationFailure(log, 0, upstreamFailure);
                        return;
                    }

                    var pickupMode = upstreamEntries["PickupMode"];
                    Func<bool> instantMode = () => IsPickupMode(pickupMode, "Instant");
                    Func<bool> individualMode = () => IsPickupMode(pickupMode, "Individual");

                    var localPlans = new List<OptionPlan>(ItemShareFixOptionCount);
                    if (!TryBuildItemShareFixPlans(assembly, config, instantMode, localPlans, out var localFailure))
                    {
                        LogRegistrationFailure(log, 0, localFailure);
                        return;
                    }
                    var upstreamPlans = new List<OptionPlan>(RequiredUpstreamOptionCount);
                    if (!TryBuildUpstreamPlans(assembly, upstreamEntries, instantMode, individualMode, upstreamPlans, out var upstreamPlanFailure))
                    {
                        LogRegistrationFailure(log, 0, upstreamPlanFailure);
                        return;
                    }

                    var plans = new List<OptionPlan>(TotalOptionCount);
                    if (!TryComposeApprovedFiveTabPlanOrder(localPlans, upstreamPlans, plans, out var compositionFailure))
                    {
                        LogRegistrationFailure(log, 0, compositionFailure);
                        return;
                    }

                    if (plans.Count != TotalOptionCount)
                    {
                        LogRegistrationFailure(log, 0, "PreflightCountMismatch:" + plans.Count + "/" + TotalOptionCount);
                        return;
                    }

                    var addOption = ResolveAddOption(manager, plans[0].Option);
                    var setDescription = ResolveSetModDescription(manager);
                    var setIcon = ResolveSetModIcon(manager);
                    var icon = CreatePageIconSprite(log);
                    if (addOption == null || setDescription == null || setIcon == null || icon == null)
                    {
                        LogRegistrationFailure(log, 0, "PageApiOrIconPreflightFailed");
                        return;
                    }

                    var registered = 0;
                    for (var i = 0; i < plans.Count; i++)
                    {
                        var plan = plans[i];
                        if (!addOption.GetParameters()[0].ParameterType.IsInstanceOfType(plan.Option))
                        {
                            LogRegistrationFailure(log, registered, "AddOptionTypeMismatch");
                            return;
                        }

                        addOption.Invoke(null, new object?[] { plan.Option, ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName });
                        if (plan.RefreshLocalization)
                        {
                            var english = ResolveEnglishLocalOptionText(plan.Entry);
                            ApplyRegisteredEnglishTokens(assembly, plan.Option, plan.Entry, english);
                        }
                        RegisteredOptions.Add(new RegisteredOptionBinding(assembly, plan.Option, plan.Entry, plan.RefreshLocalization));
                        registered++;
                    }

                    setDescription.Invoke(null, new object?[] { PageDescription, ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName });
                    setIcon.Invoke(null, new object?[] { icon, ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName });

                    if (registered != TotalOptionCount)
                    {
                        LogRegistrationFailure(log, registered, "RegistrationCountMismatch");
                        return;
                    }

                    _registrationComplete = true;
                    _appliedLanguageKey = MarkerRiskOfOptionsLocalization.CurrentLanguageKey();
                    log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_UI itemShareFix=" + ItemShareFixOptionCount + "/" + ItemShareFixOptionCount
                        + " upstream=" + RequiredUpstreamOptionCount + "/" + RequiredUpstreamOptionCount
                        + " total=" + registered + "/" + TotalOptionCount
                        + " page=" + ItemShareFixPlugin.PluginName
                        + " strategy=" + StrategyToken
                        + " canonicalConfig=True upstreamOwner=" + UpstreamPluginGuid
                        + " hardDependency=False duplicateProtection=True language=" + _appliedLanguageKey);
                }
                catch (Exception ex)
                {
                    LogRegistrationFailure(log, RegisteredOptions.Count, ex.GetType().Name);
                }
            }
        }

        public static void TryRefreshLocalization(ManualLogSource log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            lock (RegistrationLock)
            {
                if (!_registrationComplete || RegisteredOptions.Count == 0) return;
                var currentLanguageKey = MarkerRiskOfOptionsLocalization.CurrentLanguageKey();
                if (string.Equals(currentLanguageKey, _appliedLanguageKey, StringComparison.Ordinal)) return;

                var refreshed = 0;
                for (var i = 0; i < RegisteredOptions.Count; i++)
                {
                    var binding = RegisteredOptions[i];
                    if (!binding.RefreshLocalization) continue;
                    var english = ResolveEnglishLocalOptionText(binding.Entry);
                    ApplyRegisteredEnglishTokens(binding.Assembly, binding.Option, binding.Entry, english);
                    ApplyRegisteredCategory(binding.Option, ResolveItemShareFixCategory(binding.Entry));
                    refreshed++;
                }

                _appliedLanguageKey = currentLanguageKey;
                log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_UI englishTokenRefresh=" + refreshed + "/" + ItemShareFixOptionCount
                    + " language=" + currentLanguageKey + " lifecycle=postAwakeTokenRefresh");
            }
        }

        private static bool TryBuildItemShareFixPlans(
            Assembly assembly,
            PluginConfig config,
            Func<bool> instantMode,
            List<OptionPlan> plans,
            out string failure)
        {
            failure = string.Empty;
            return AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.PersonalMarkersEnabled, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ChoiceOption", config.MarkerPresentationMode, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.ShareTemporaryItems, null, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.ShowMarkerDistance, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.MarkerScale, instantMode, out failure, 0.75f, 1.25f, 0.05f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.MarkerOpacity, instantMode, out failure, 0f, 1f, 0.05f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.MarkerBackgroundOpacity, instantMode, out failure, 0f, 1f, 0.05f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.ShowMarkerCategoryDiamond, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.IntSliderOption", config.MarkerDetailRows, instantMode, out failure, 1f, 12f, null)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ChoiceOption", config.MarkerCategorySortOrder, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.MarkerCompactShowCount, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.EnableOffscreenIndicators, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.ShowOffscreenDistance, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.CheckBoxOption", config.ShowOffscreenTotalCount, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.OffscreenIndicatorScale, instantMode, out failure, 0.75f, 1.25f, 0.05f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.OffscreenIndicatorOpacity, instantMode, out failure, 0f, 1f, 0.05f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.StepSliderOption", config.OffscreenEdgePadding, instantMode, out failure, 12f, 160f, 2f)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.CommonMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.UncommonMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.LegendaryMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.BossMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.LunarMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.VoidMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.EquipmentMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.CommandMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.NeutralMarkerColor, instantMode, out failure)
                && AddLocalPlan(assembly, plans, "RiskOfOptions.Options.ColorOption", config.OffscreenIndicatorColor, instantMode, out failure);
        }

        private static bool TryComposeApprovedFiveTabPlanOrder(
            List<OptionPlan> localPlans,
            List<OptionPlan> upstreamPlans,
            List<OptionPlan> plans,
            out string failure)
        {
            failure = string.Empty;
            if (ApprovedCategoryOrder.Length != 5)
            {
                failure = "ApprovedCategoryCountMismatch:" + ApprovedCategoryOrder.Length;
                return false;
            }

            if (!TryMovePlan(upstreamPlans, plans, "PickupMode", out failure)
                || !TryMovePlan(upstreamPlans, plans, "ShareEquipment", out failure)
                || !TryMovePlan(upstreamPlans, plans, "ShareToDead", out failure)
                || !TryMovePlan(localPlans, plans, "ShareTemporaryItems", out failure)
                || !TryMovePlan(upstreamPlans, plans, "AnnounceProgress", out failure)
                || !TryMovePlan(upstreamPlans, plans, "ShareCommandPicks", out failure)
                || !TryMovePlan(upstreamPlans, plans, "PingShowsPending", out failure)
                || !TryMovePlan(upstreamPlans, plans, "HideCollectedOrbs", out failure)
                || !TryMovePlan(upstreamPlans, plans, "SilenceRemoteNotificationErrors", out failure)
                || !TryMovePlan(upstreamPlans, plans, "White", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Green", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Red", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Boss", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Lunar", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Void", out failure)
                || !TryMovePlan(upstreamPlans, plans, "Food", out failure)
                || !TryMovePlan(localPlans, plans, "PersonalMarkersEnabled", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerPresentationMode", out failure)
                || !TryMovePlan(localPlans, plans, "ShowMarkerDistance", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerScale", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerOpacity", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerBackgroundOpacity", out failure)
                || !TryMovePlan(localPlans, plans, "ShowMarkerCategoryDiamond", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerDetailRows", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerCategorySortOrder", out failure)
                || !TryMovePlan(localPlans, plans, "MarkerCompactShowCount", out failure)
                || !TryMovePlan(localPlans, plans, "EnableOffscreenIndicators", out failure)
                || !TryMovePlan(localPlans, plans, "ShowOffscreenDistance", out failure)
                || !TryMovePlan(localPlans, plans, "ShowOffscreenTotalCount", out failure)
                || !TryMovePlan(localPlans, plans, "OffscreenIndicatorScale", out failure)
                || !TryMovePlan(localPlans, plans, "OffscreenIndicatorOpacity", out failure)
                || !TryMovePlan(localPlans, plans, "OffscreenEdgePadding", out failure)
                || !TryMovePlan(localPlans, plans, "Common", out failure)
                || !TryMovePlan(localPlans, plans, "Uncommon", out failure)
                || !TryMovePlan(localPlans, plans, "Legendary", out failure)
                || !TryMovePlan(localPlans, plans, "Boss", out failure)
                || !TryMovePlan(localPlans, plans, "Lunar", out failure)
                || !TryMovePlan(localPlans, plans, "Void", out failure)
                || !TryMovePlan(localPlans, plans, "Equipment", out failure)
                || !TryMovePlan(localPlans, plans, "Command", out failure)
                || !TryMovePlan(localPlans, plans, "Neutral", out failure)
                || !TryMovePlan(localPlans, plans, "OffscreenIndicator", out failure))
                return false;

            if (localPlans.Count != 0 || upstreamPlans.Count != 0 || plans.Count != TotalOptionCount)
            {
                failure = "ApprovedFiveTabCompositionMismatch:local=" + localPlans.Count
                    + ":upstream=" + upstreamPlans.Count + ":total=" + plans.Count;
                return false;
            }

            return true;
        }

        private static bool TryMovePlan(List<OptionPlan> source, List<OptionPlan> destination, string key, out string failure)
        {
            var matches = source.Where(x => string.Equals(x.Entry.Definition.Key, key, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                failure = "ApprovedFiveTabPlanMissingOrDuplicate:" + key + ":" + matches.Length;
                return false;
            }

            destination.Add(matches[0]);
            source.Remove(matches[0]);
            failure = string.Empty;
            return true;
        }

        private static bool AddLocalPlan(
            Assembly assembly,
            List<OptionPlan> plans,
            string optionTypeName,
            ConfigEntryBase entry,
            Func<bool>? disabledWhen,
            out string failure,
            float? min = null,
            float? max = null,
            float? increment = null)
        {
            var english = ResolveEnglishLocalOptionText(entry);
            var category = ResolveItemShareFixCategory(entry);
            var option = CreateOption(assembly, optionTypeName, entry, english.Name, english.Description, category, disabledWhen, min, max, increment);
            if (option == null)
            {
                failure = "ItemShareFixOptionPreflight:" + entry.Definition.Key + ":" + optionTypeName;
                return false;
            }

            plans.Add(new OptionPlan(option, entry, refreshLocalization: true));
            failure = string.Empty;
            return true;
        }

        private static bool TryBuildUpstreamPlans(
            Assembly assembly,
            IReadOnlyDictionary<string, ConfigEntryBase> upstreamEntries,
            Func<bool> instantMode,
            Func<bool> individualMode,
            List<OptionPlan> plans,
            out string failure)
        {
            foreach (var spec in RequiredUpstreamSpecs())
            {
                var entry = upstreamEntries[spec.Key];
                Func<bool>? disabledWhen = null;
                if (string.Equals(spec.Key, "ShareEquipment", StringComparison.Ordinal)) disabledWhen = individualMode;
                else if (IndividualOnlyUpstreamKeys.Contains(spec.Key, StringComparer.Ordinal)) disabledWhen = instantMode;

                var description = entry.Description.Description ?? string.Empty;
                if (string.IsNullOrWhiteSpace(description)) description = "Original ItemShare setting.";
                if (string.Equals(spec.Key, "ShareEquipment", StringComparison.Ordinal))
                    description += " Instant-only setting; inactive in Individual mode.";
                else if (IndividualOnlyUpstreamKeys.Contains(spec.Key, StringComparer.Ordinal))
                    description += " Individual-only setting; inactive in Instant mode.";

                var option = CreateOption(
                    assembly,
                    spec.OptionTypeName,
                    entry,
                    HumanizeKey(spec.Key),
                    description,
                    spec.Category,
                    disabledWhen,
                    null,
                    null,
                    null);
                if (option == null)
                {
                    failure = "UpstreamOptionPreflight:" + spec.Section + "." + spec.Key + ":" + spec.OptionTypeName;
                    return false;
                }

                plans.Add(new OptionPlan(option, entry, refreshLocalization: false));
            }

            failure = string.Empty;
            return true;
        }

        private static IEnumerable<UpstreamEntrySpec> RequiredUpstreamSpecs()
        {
            yield return new UpstreamEntrySpec("General", "PickupMode", true, "RiskOfOptions.Options.ChoiceOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "ShareEquipment", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "ShareToDead", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "AnnounceProgress", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "ShareCommandPicks", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "PingShowsPending", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "HideCollectedOrbs", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("General", "SilenceRemoteNotificationErrors", false, "RiskOfOptions.Options.CheckBoxOption", SharingCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "White", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Green", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Red", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Boss", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Lunar", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Void", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
            yield return new UpstreamEntrySpec("Tiers", "Food", false, "RiskOfOptions.Options.CheckBoxOption", ItemTiersCategory, null);
        }

        private static bool TryResolveUpstreamEntries(out IReadOnlyDictionary<string, ConfigEntryBase> result, out string failure)
        {
            result = new Dictionary<string, ConfigEntryBase>(StringComparer.Ordinal);
            failure = string.Empty;

            if (!Chainloader.PluginInfos.TryGetValue(UpstreamPluginGuid, out var info) || info.Instance == null)
            {
                failure = "ItemSharePluginUnavailable";
                return false;
            }

            if (!string.Equals(info.Metadata.GUID, UpstreamPluginGuid, StringComparison.Ordinal))
            {
                failure = "ItemSharePluginGuidMismatch";
                return false;
            }

            var config = info.Instance.Config;
            if (config == null)
            {
                failure = "ItemShareConfigUnavailable";
                return false;
            }

            if (!string.Equals(Path.GetFileName(config.ConfigFilePath), UpstreamPluginGuid + ".cfg", StringComparison.OrdinalIgnoreCase))
            {
                failure = "ItemShareConfigPathMismatch";
                return false;
            }

            var entries = EnumerateConfigEntries(config);
            var resolved = new Dictionary<string, ConfigEntryBase>(StringComparer.Ordinal);
            foreach (var spec in RequiredUpstreamSpecs())
            {
                var matches = entries.Where(x => string.Equals(x.Definition.Section, spec.Section, StringComparison.Ordinal)
                    && string.Equals(x.Definition.Key, spec.Key, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1)
                {
                    failure = "ItemShareEntryMissingOrDuplicate:" + spec.Section + "." + spec.Key + ":" + matches.Length;
                    return false;
                }

                var entry = matches[0];
                if (spec.ExpectsEnum)
                {
                    if (!entry.SettingType.IsEnum
                        || !Enum.GetNames(entry.SettingType).Contains("Individual", StringComparer.Ordinal)
                        || !Enum.GetNames(entry.SettingType).Contains("Instant", StringComparer.Ordinal))
                    {
                        failure = "ItemShareEntryTypeMismatch:" + spec.Section + "." + spec.Key + ":enum(Individual,Instant)";
                        return false;
                    }
                }
                else if (entry.SettingType != typeof(bool))
                {
                    failure = "ItemShareEntryTypeMismatch:" + spec.Section + "." + spec.Key + ":bool";
                    return false;
                }

                resolved.Add(spec.Key, entry);
            }

            if (resolved.Count != RequiredUpstreamOptionCount)
            {
                failure = "ItemShareEntryCountMismatch:" + resolved.Count + "/" + RequiredUpstreamOptionCount;
                return false;
            }

            result = resolved;
            return true;
        }

        private static IReadOnlyList<ConfigEntryBase> EnumerateConfigEntries(ConfigFile config)
        {
            var entries = new List<ConfigEntryBase>();
            var seen = new HashSet<ConfigEntryBase>();
            for (Type? current = config.GetType(); current != null; current = current.BaseType)
            {
                var fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var field in fields)
                {
                    object? value;
                    try { value = field.GetValue(config); }
                    catch { continue; }
                    CollectConfigEntries(value, entries, seen);
                }
            }
            return entries;
        }

        private static void CollectConfigEntries(object? container, List<ConfigEntryBase> entries, HashSet<ConfigEntryBase> seen)
        {
            if (container == null || container is string) return;
            if (container is ConfigEntryBase direct)
            {
                if (seen.Add(direct)) entries.Add(direct);
                return;
            }

            if (container is IDictionary dictionary)
            {
                foreach (DictionaryEntry item in dictionary)
                    if (item.Value is ConfigEntryBase entry && seen.Add(entry)) entries.Add(entry);
                return;
            }

            if (container is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is ConfigEntryBase entry)
                    {
                        if (seen.Add(entry)) entries.Add(entry);
                        continue;
                    }

                    if (item == null) continue;
                    var valueProperty = item.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                    if (valueProperty?.GetValue(item) is ConfigEntryBase pairEntry && seen.Add(pairEntry)) entries.Add(pairEntry);
                }
            }
        }

        private static object? CreateOption(
            Assembly assembly,
            string optionTypeName,
            ConfigEntryBase entry,
            string name,
            string description,
            string category,
            Func<bool>? disabledWhen,
            float? min,
            float? max,
            float? increment)
        {
            var optionType = assembly.GetType(optionTypeName, throwOnError: false);
            if (optionType == null) return null;

            var constructor = optionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(x =>
                {
                    var p = x.GetParameters();
                    return p.Length == 2
                        && p[0].ParameterType.IsInstanceOfType(entry)
                        && p[1].ParameterType != typeof(bool)
                        && IsRiskOfOptionsConfigType(p[1].ParameterType);
                })
                .OrderBy(x => x.MetadataToken)
                .FirstOrDefault();
            if (constructor == null) return null;

            var parameters = constructor.GetParameters();
            object? optionConfig;
            try { optionConfig = Activator.CreateInstance(parameters[1].ParameterType); }
            catch { return null; }
            if (optionConfig == null) return null;

            ApplyNumericBounds(optionConfig, min, max, increment);
            SetTextMember(optionConfig, new[] { "name", "Name" }, name);
            SetTextMember(optionConfig, new[] { "description", "Description" }, description);
            SetTextMember(optionConfig, new[] { "category", "Category", "categoryName", "CategoryName" }, category);
            if (disabledWhen != null && !ApplyDisabledPredicate(optionConfig, disabledWhen)) return null;

            try { return constructor.Invoke(new object?[] { entry, optionConfig }); }
            catch { return null; }
        }

        private static bool ApplyDisabledPredicate(object config, Func<bool> predicate)
        {
            var adapter = new DisabledPredicateAdapter(predicate);
            var invokeMethod = typeof(DisabledPredicateAdapter).GetMethod(nameof(DisabledPredicateAdapter.Invoke), BindingFlags.Instance | BindingFlags.Public);
            if (invokeMethod == null) return false;

            for (Type? current = config.GetType(); current != null; current = current.BaseType)
            {
                var field = current.GetField("checkIfDisabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    try
                    {
                        field.SetValue(config, Delegate.CreateDelegate(field.FieldType, adapter, invokeMethod));
                        return true;
                    }
                    catch { return false; }
                }

                var property = current.GetProperty("checkIfDisabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.CanWrite && typeof(Delegate).IsAssignableFrom(property.PropertyType))
                {
                    try
                    {
                        property.SetValue(config, Delegate.CreateDelegate(property.PropertyType, adapter, invokeMethod));
                        return true;
                    }
                    catch { return false; }
                }
            }

            return false;
        }

        private static bool IsPickupMode(ConfigEntryBase pickupMode, string expected)
            => string.Equals(pickupMode.BoxedValue?.ToString(), expected, StringComparison.Ordinal);

        private static MethodInfo? ResolveAddOption(Type managerType, object sampleOption)
            => managerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => string.Equals(x.Name, "AddOption", StringComparison.Ordinal))
                .FirstOrDefault(x =>
                {
                    var p = x.GetParameters();
                    return p.Length == 3
                        && p[0].ParameterType.IsInstanceOfType(sampleOption)
                        && p[1].ParameterType == typeof(string)
                        && p[2].ParameterType == typeof(string);
                });

        private static MethodInfo? ResolveSetModDescription(Type managerType)
            => managerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x =>
                {
                    if (!string.Equals(x.Name, "SetModDescription", StringComparison.Ordinal)) return false;
                    var p = x.GetParameters();
                    return p.Length == 3 && p.All(y => y.ParameterType == typeof(string));
                });

        private static MethodInfo? ResolveSetModIcon(Type managerType)
            => managerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x =>
                {
                    if (!string.Equals(x.Name, "SetModIcon", StringComparison.Ordinal)) return false;
                    var p = x.GetParameters();
                    return p.Length == 3
                        && string.Equals(p[0].ParameterType.FullName, "UnityEngine.Sprite", StringComparison.Ordinal)
                        && p[1].ParameterType == typeof(string)
                        && p[2].ParameterType == typeof(string);
                });

        private static Sprite? CreatePageIconSprite(ManualLogSource log)
        {
            try
            {
                using var stream = typeof(OptionalRiskOfOptionsIntegration).Assembly.GetManifestResourceStream(IconResourceName);
                if (stream == null) return null;
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                var bytes = memory.ToArray();
                if (bytes.Length == 0) return null;

                var texture = new Texture2D(2, 2);
                var imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", throwOnError: false)
                    ?? AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("UnityEngine.ImageConversion", throwOnError: false)).FirstOrDefault(x => x != null);
                var loadImage = imageConversion?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(x =>
                    {
                        if (!string.Equals(x.Name, "LoadImage", StringComparison.Ordinal)) return false;
                        var p = x.GetParameters();
                        return p.Length == 3
                            && p[0].ParameterType == typeof(Texture2D)
                            && p[1].ParameterType == typeof(byte[])
                            && p[2].ParameterType == typeof(bool);
                    });
                if (loadImage == null || loadImage.Invoke(null, new object?[] { texture, bytes, false }) is not bool loaded || !loaded)
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = "ItemShareFix Risk Of Options Icon";
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = "ItemShareFix Risk Of Options Icon";
                UnityEngine.Object.DontDestroyOnLoad(texture);
                UnityEngine.Object.DontDestroyOnLoad(sprite);
                return sprite;
            }
            catch (Exception ex)
            {
                log.LogDebug("[ItemShareFix] Risk Of Options icon preflight failed: " + ex.GetType().Name);
                return null;
            }
        }

        private static string ResolveItemShareFixCategory(ConfigEntryBase entry)
        {
            var key = entry.Definition.Key;
            if (string.Equals(key, "ShareTemporaryItems", StringComparison.Ordinal)) return SharingCategory;
            if (key.EndsWith("Color", StringComparison.Ordinal) || string.Equals(entry.Definition.Section, "Marker Colors", StringComparison.Ordinal)) return MarkerColorsCategory;
            if (string.Equals(key, "EnableOffscreenIndicators", StringComparison.Ordinal)
                || string.Equals(key, "ShowOffscreenDistance", StringComparison.Ordinal)
                || string.Equals(key, "ShowOffscreenTotalCount", StringComparison.Ordinal)
                || string.Equals(key, "OffscreenIndicatorScale", StringComparison.Ordinal)
                || string.Equals(key, "OffscreenIndicatorOpacity", StringComparison.Ordinal)
                || string.Equals(key, "OffscreenEdgePadding", StringComparison.Ordinal))
                return OffscreenIndicatorsCategory;
            return MarkersCategory;
        }

        private static MarkerOptionLocalizedText ResolveEnglishLocalOptionText(ConfigEntryBase entry)
        {
            var english = MarkerRiskOfOptionsLocalization.ResolveForLanguage(entry.Definition.Key, 0);
            return new MarkerOptionLocalizedText(ResolveEnglishLocalOptionName(entry.Definition.Key), english.Description);
        }

        private static string ResolveEnglishLocalOptionName(string key)
        {
            switch (key)
            {
                case "PersonalMarkersEnabled": return "Enable Markers";
                case "MarkerPresentationMode": return "Marker Mode";
                case "ShareTemporaryItems": return "Share Temporary Items";
                case "ShowMarkerDistance": return "Show Distance";
                case "MarkerScale": return "Marker Scale";
                case "MarkerOpacity": return "Marker Opacity";
                case "MarkerBackgroundOpacity": return "Background Opacity";
                case "ShowMarkerCategoryDiamond": return "Show Category Diamond";
                case "MarkerDetailRows": return "Item Rows";
                case "MarkerCategorySortOrder": return "Category Sort Order";
                case "MarkerCompactShowCount": return "Compact Counts";
                case "EnableOffscreenIndicators": return "Enable Off-screen Indicators";
                case "ShowOffscreenDistance": return "Show Off-screen Distance";
                case "ShowOffscreenTotalCount": return "Show Off-screen Total Count";
                case "OffscreenIndicatorScale": return "Off-screen Scale";
                case "OffscreenIndicatorOpacity": return "Off-screen Opacity";
                case "OffscreenEdgePadding": return "Edge Padding";
                case "Common": return "Common Color";
                case "Uncommon": return "Uncommon Color";
                case "Legendary": return "Legendary Color";
                case "Boss": return "Boss Color";
                case "Lunar": return "Lunar Color";
                case "Void": return "Void Color";
                case "Equipment": return "Equipment Color";
                case "Command": return "Command Color";
                case "Neutral": return "Neutral Color";
                case "OffscreenIndicator": return "Off-screen Indicator Color";
                default: return HumanizeKey(key);
            }
        }

        private static string HumanizeKey(string key)
        {
            var chars = new List<char>(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]) && !char.IsUpper(key[i - 1])) chars.Add(' ');
                chars.Add(key[i]);
            }
            return new string(chars.ToArray());
        }

        private static void LogRegistrationFailure(ManualLogSource log, int registered, string failure)
            => log.LogWarning("[ItemShareFix] ISF_RISKOFOPTIONS_UI itemShareFix=" + Math.Min(registered, ItemShareFixOptionCount) + "/" + ItemShareFixOptionCount
                + " upstream=" + Math.Max(0, registered - ItemShareFixOptionCount) + "/" + RequiredUpstreamOptionCount
                + " total=" + registered + "/" + TotalOptionCount
                + " failure=" + failure + " strategy=" + StrategyToken + " canonicalConfig=True hardDependency=False");

        private static bool IsRiskOfOptionsConfigType(Type type)
        {
            for (Type? current = type; current != null; current = current.BaseType)
                if (string.Equals(current.FullName, "RiskOfOptions.OptionConfigs.BaseOptionConfig", StringComparison.Ordinal)) return true;
            return false;
        }

        private static void ApplyRegisteredCategory(object option, string category)
        {
            SetTextMember(option, new[] { "category", "Category", "categoryName", "CategoryName" }, category);
            for (Type? current = option.GetType(); current != null; current = current.BaseType)
            {
                var field = current.GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? current.GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
                var config = field?.GetValue(option);
                if (config != null) SetTextMember(config, new[] { "category", "Category", "categoryName", "CategoryName" }, category);
            }
        }

        private static void ApplyRegisteredEnglishTokens(Assembly assembly, object option, ConfigEntryBase entry, MarkerOptionLocalizedText localized)
        {
            var languageApi = assembly.GetType("RiskOfOptions.Lib.LanguageApi", throwOnError: false);
            if (languageApi == null) return;
            var add = languageApi.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(x =>
                {
                    if (!string.Equals(x.Name, "Add", StringComparison.Ordinal)) return false;
                    var p = x.GetParameters();
                    return p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string);
                });
            if (add == null) return;

            var optionType = option.GetType();
            var getNameToken = optionType.GetMethod("GetNameToken", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var getDescriptionToken = optionType.GetMethod("GetDescriptionToken", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getNameToken?.Invoke(option, null) is string nameToken && !string.IsNullOrEmpty(nameToken))
                add.Invoke(null, new object?[] { nameToken, localized.Name });
            if (getDescriptionToken?.Invoke(option, null) is string descriptionToken && !string.IsNullOrEmpty(descriptionToken))
                add.Invoke(null, new object?[] { descriptionToken, localized.Description });

            string[]? labels = null;
            if (string.Equals(entry.Definition.Key, "MarkerPresentationMode", StringComparison.Ordinal)) labels = EnglishPresentationModeChoices;
            else if (string.Equals(entry.Definition.Key, "MarkerCategorySortOrder", StringComparison.Ordinal)) labels = EnglishSortOrderChoices;
            if (labels == null) return;

            FieldInfo? tokenField = null;
            for (Type? current = optionType; current != null && tokenField == null; current = current.BaseType)
                tokenField = current.GetField("_nameTokens", BindingFlags.Instance | BindingFlags.NonPublic);
            var tokens = tokenField?.GetValue(option) as string[];
            if (tokens == null || tokens.Length != labels.Length) return;
            for (var i = 0; i < tokens.Length; i++)
                if (!string.IsNullOrEmpty(tokens[i])) add.Invoke(null, new object?[] { tokens[i], labels[i] });
        }

        private static void SetTextMember(object target, string[] names, string value)
        {
            var type = target.GetType();
            foreach (var name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite && property.PropertyType == typeof(string)) { property.SetValue(target, value); return; }
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(string)) { field.SetValue(target, value); return; }
            }
        }

        private static void ApplyNumericBounds(object config, float? min, float? max, float? increment)
        {
            if (min.HasValue) SetNumericMember(config, new[] { "min", "Min", "minimum", "Minimum" }, min.Value);
            if (max.HasValue) SetNumericMember(config, new[] { "max", "Max", "maximum", "Maximum" }, max.Value);
            if (increment.HasValue) SetNumericMember(config, new[] { "increment", "Increment", "step", "Step" }, increment.Value);
        }

        private static void SetNumericMember(object target, string[] names, float value)
        {
            var type = target.GetType();
            foreach (var name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite && TryConvertNumeric(value, property.PropertyType, out var convertedProperty))
                {
                    property.SetValue(target, convertedProperty);
                    return;
                }
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && TryConvertNumeric(value, field.FieldType, out var convertedField))
                {
                    field.SetValue(target, convertedField);
                    return;
                }
            }
        }

        private static bool TryConvertNumeric(float value, Type targetType, out object? converted)
        {
            if (targetType == typeof(float)) { converted = value; return true; }
            if (targetType == typeof(double)) { converted = (double)value; return true; }
            if (targetType == typeof(int)) { converted = (int)Math.Round(value); return true; }
            converted = null;
            return false;
        }
    }
}
