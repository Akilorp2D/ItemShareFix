using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ItemShareFix
{
    /// <summary>
    /// True soft integration: this file contains no compile-time RiskOfOptions type reference. When the assembly is
    /// present, reflected option wrappers bind directly to the canonical BepInEx ConfigEntry instances. If any API
    /// shape differs, ItemShareFix remains fully functional through BepInEx config and reports the optional UI skip.
    /// </summary>
    internal static class OptionalRiskOfOptionsIntegration
    {
        public const string PluginGuid = "com.rune580.riskofoptions";
        public const string RuntimeAssemblyName = "RiskOfOptions";
        public const string StrategyToken = "REFLECTION_SOFT_BINDING_CANONICAL_CONFIGENTRY";
        public const int CurrentMarkerOptionCount = 27;

        private sealed class RegisteredOptionBinding
        {
            public RegisteredOptionBinding(Assembly assembly, object option, ConfigEntryBase entry)
            {
                Assembly = assembly; Option = option; Entry = entry;
            }
            public Assembly Assembly { get; }
            public object Option { get; }
            public ConfigEntryBase Entry { get; }
        }

        private static readonly object RegistrationLock = new object();
        private static readonly List<RegisteredOptionBinding> RegisteredOptions = new List<RegisteredOptionBinding>(CurrentMarkerOptionCount);
        private static bool _registrationAttempted;
        private static bool _registrationComplete;
        private static bool _absenceLogged;
        private static string _appliedLanguageKey = string.Empty;

        public static void TryRegister(PluginConfig config, ManualLogSource log)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (log == null) throw new ArgumentNullException(nameof(log));

            lock (RegistrationLock)
            {
                if (_registrationComplete || _registrationAttempted) return;

                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(x => string.Equals(x.GetName().Name, RuntimeAssemblyName, StringComparison.Ordinal));
                    if (assembly == null)
                    {
                        if (!_absenceLogged)
                        {
                            _absenceLogged = true;
                            log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI absent strategy=" + StrategyToken
                                + " canonicalConfig=True hardDependency=False deferredRetry=True");
                        }
                        return;
                    }
                    _registrationAttempted = true;

                    var manager = assembly.GetType("RiskOfOptions.ModSettingsManager", throwOnError: false);
                    if (manager == null)
                    {
                        log.LogWarning("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI registered=0/" + CurrentMarkerOptionCount + " failure=ModSettingsManagerUnavailable"
                            + " canonicalConfig=True hardDependency=False");
                        return;
                    }

                    var expected = CurrentMarkerOptionCount;
                    var registered = 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.PersonalMarkersEnabled, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ChoiceOption", config.MarkerPresentationMode, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.ShareTemporaryItems, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.ShowMarkerDistance, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.MarkerScale, log,
                        min: 0.75f, max: 1.25f, increment: 0.05f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.MarkerOpacity, log,
                        min: 0f, max: 1f, increment: 0.05f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.MarkerBackgroundOpacity, log,
                        min: 0f, max: 1f, increment: 0.05f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.ShowMarkerCategoryDiamond, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.MarkerDetailRows, log, min: 1f, max: 12f, increment: 1f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ChoiceOption", config.MarkerCategorySortOrder, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.MarkerCompactShowCount, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.EnableOffscreenIndicators, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.ShowOffscreenDistance, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.CheckBoxOption", config.ShowOffscreenTotalCount, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.OffscreenIndicatorScale, log,
                        min: 0.75f, max: 1.25f, increment: 0.05f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.OffscreenIndicatorOpacity, log,
                        min: 0f, max: 1f, increment: 0.05f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.StepSliderOption", config.OffscreenEdgePadding, log,
                        min: 12f, max: 160f, increment: 2f) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.CommonMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.UncommonMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.LegendaryMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.BossMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.LunarMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.VoidMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.EquipmentMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.CommandMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.NeutralMarkerColor, log) ? 1 : 0;
                    registered += RegisterOption(assembly, manager, "RiskOfOptions.Options.ColorOption", config.OffscreenIndicatorColor, log) ? 1 : 0;

                    if (registered == expected)
                    {
                        _registrationComplete = true;
                        _appliedLanguageKey = MarkerRiskOfOptionsLocalization.CurrentLanguageKey();
                        log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI registered=" + registered + "/" + expected
                            + " strategy=" + StrategyToken + " canonicalConfig=True hardDependency=False duplicateProtection=True language=" + _appliedLanguageKey);
                    }
                    else
                    {
                        log.LogWarning("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI registered=" + registered + "/" + expected
                            + " failure=ApiShapeMismatch strategy=" + StrategyToken + " canonicalConfig=True hardDependency=False");
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI registered=0/" + CurrentMarkerOptionCount + " failure=" + ex.GetType().Name
                        + " canonicalConfig=True hardDependency=False");
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
                    var localized = MarkerRiskOfOptionsLocalization.Resolve(binding.Entry);
                    ApplyRegisteredLanguageTokens(binding.Assembly, binding.Option, binding.Entry, localized);
                    ApplyRegisteredCategory(binding.Option, binding.Entry);
                    refreshed++;
                }
                _appliedLanguageKey = currentLanguageKey;
                log.LogInfo("[ItemShareFix] ISF_RISKOFOPTIONS_MARKER_UI localizationRefresh=" + refreshed + "/" + RegisteredOptions.Count
                    + " language=" + currentLanguageKey + " lifecycle=postAwakeTokenRefresh");
            }
        }

        private static bool RegisterOption(
            Assembly assembly,
            Type managerType,
            string optionTypeName,
            ConfigEntryBase entry,
            ManualLogSource log,
            float? min = null,
            float? max = null,
            float? increment = null)
        {
            var optionType = assembly.GetType(optionTypeName, throwOnError: false);
            if (optionType == null) return false;
            var localized = MarkerRiskOfOptionsLocalization.Resolve(entry);
            var category = MarkerRiskOfOptionsLocalization.ResolveCategory(entry);
            var option = CreateLocalizedOption(optionType, entry, localized, category, min, max, increment);
            if (option == null)
            {
                log.LogDebug("[ItemShareFix] Risk Of Options typed config constructor not matched for " + optionTypeName + ".");
                return false;
            }

            // Prefer the typed AddOption(BaseOption, string modGuid, string modName) API shape when available;
            // this keeps registration deterministic instead of depending on reflection order.
            var addOption = managerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => string.Equals(x.Name, "AddOption", StringComparison.Ordinal))
                .FirstOrDefault(x =>
                {
                    var p = x.GetParameters();
                    return p.Length == 3
                        && p[0].ParameterType.IsInstanceOfType(option)
                        && p[1].ParameterType == typeof(string)
                        && p[2].ParameterType == typeof(string);
                });
            if (addOption == null)
            {
                log.LogDebug("[ItemShareFix] Risk Of Options canonical AddOption overload not matched for " + optionTypeName + ".");
                return false;
            }

            addOption.Invoke(null, new object?[] { option, ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName });
            ApplyRegisteredLanguageTokens(assembly, option, entry, localized);
            ApplyRegisteredCategory(option, entry);
            RegisteredOptions.Add(new RegisteredOptionBinding(assembly, option, entry));
            return true;
        }

        private static object? CreateLocalizedOption(
            Type optionType,
            ConfigEntryBase entry,
            MarkerOptionLocalizedText localized,
            string category,
            float? min,
            float? max,
            float? increment)
        {
            // Deliberately select the two-argument typed OptionConfig constructor. The bool restart overload is never
            // accepted as a localized registration path. Unsupported API shapes fail softly; BepInEx config remains authoritative.
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
            object? config;
            try { config = Activator.CreateInstance(parameters[1].ParameterType); }
            catch { return null; }
            if (config == null) return null;

            ApplyNumericBounds(config, min, max, increment);
            ApplyLocalizedOptionConfig(config, localized, category);
            try { return constructor.Invoke(new object?[] { entry, config }); }
            catch { return null; }
        }

        private static bool IsRiskOfOptionsConfigType(Type type)
        {
            for (Type? current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, "RiskOfOptions.OptionConfigs.BaseOptionConfig", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ApplyLocalizedOptionConfig(object config, MarkerOptionLocalizedText localized, string category)
        {
            SetTextMember(config, new[] { "name", "Name" }, localized.Name);
            SetTextMember(config, new[] { "description", "Description" }, localized.Description);
            SetTextMember(config, new[] { "category", "Category", "categoryName", "CategoryName" }, category);
        }

        private static void ApplyRegisteredCategory(object option, ConfigEntryBase entry)
        {
            var category = MarkerRiskOfOptionsLocalization.ResolveCategory(entry);
            SetTextMember(option, new[] { "category", "Category", "categoryName", "CategoryName" }, category);

            for (Type? current = option.GetType(); current != null; current = current.BaseType)
            {
                var field = current.GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? current.GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
                var config = field?.GetValue(option);
                if (config != null) SetTextMember(config, new[] { "category", "Category", "categoryName", "CategoryName" }, category);
            }
        }

        private static void ApplyRegisteredLanguageTokens(
            Assembly assembly,
            object option,
            ConfigEntryBase entry,
            MarkerOptionLocalizedText localized)
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
            if (string.Equals(entry.Definition.Key, "MarkerPresentationMode", StringComparison.Ordinal))
                labels = MarkerRiskOfOptionsLocalization.PresentationModeChoices();
            else if (string.Equals(entry.Definition.Key, "MarkerCategorySortOrder", StringComparison.Ordinal))
                labels = MarkerRiskOfOptionsLocalization.SortChoices();
            if (labels == null) return;

            // ChoiceOption.RegisterChoiceTokens() creates the real 2.8.5 token array before AddOption returns.
            // Reuse those exact tokens rather than inventing a missing string-array member on the typed choice config.
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
