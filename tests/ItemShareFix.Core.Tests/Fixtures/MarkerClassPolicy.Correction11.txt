using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemShareFix.Core
{
    public enum MarkerClassKind
    {
        Unknown,
        Tier1,
        Tier2,
        Tier3,
        Boss,
        Lunar,
        Void,
        Equipment,
        LunarEquipment,
        Other,
    }

    public readonly struct CommandClassPresentation
    {
        public CommandClassPresentation(MarkerClassKind kind, string label, bool exactClass)
        {
            Kind = kind;
            Label = string.IsNullOrWhiteSpace(label) ? "Item choice" : label;
            ExactClass = exactClass;
        }

        public MarkerClassKind Kind { get; }
        public string Label { get; }
        public bool ExactClass { get; }
    }

    /// <summary>
    /// Pure presentation classification. Runtime color remains authoritative PickupDef.baseColor;
    /// this policy only converts proven item/equipment metadata into bounded user-facing class names.
    /// </summary>
    public static class MarkerClassPolicy
    {
        public static bool UsesCatalogPickupBaseColor => true;
        public static bool UsesArtifactCommandName => false;
        public static string CommandChoiceSource => "PickupPickerController.options/PickupIndex";
        public static string AuthoritativeCommandChoiceSource => "PickupPickerController.options/option.pickup.pickupIndex";

        public static MarkerClassKind Classify(string? itemTierName, bool isEquipment, bool isLunarEquipment)
        {
            if (isEquipment) return isLunarEquipment ? MarkerClassKind.LunarEquipment : MarkerClassKind.Equipment;

            var tier = itemTierName ?? string.Empty;
            if (string.Equals(tier, "Tier1", StringComparison.Ordinal)) return MarkerClassKind.Tier1;
            if (string.Equals(tier, "Tier2", StringComparison.Ordinal)) return MarkerClassKind.Tier2;
            if (string.Equals(tier, "Tier3", StringComparison.Ordinal)) return MarkerClassKind.Tier3;
            if (string.Equals(tier, "Boss", StringComparison.Ordinal)) return MarkerClassKind.Boss;
            if (string.Equals(tier, "Lunar", StringComparison.Ordinal)) return MarkerClassKind.Lunar;
            if (tier.StartsWith("Void", StringComparison.Ordinal)) return MarkerClassKind.Void;
            if (string.IsNullOrWhiteSpace(tier) || string.Equals(tier, "NoTier", StringComparison.Ordinal)) return MarkerClassKind.Unknown;
            return MarkerClassKind.Other;
        }

        public static string DiagnosticClassName(MarkerClassKind kind)
        {
            switch (kind)
            {
                case MarkerClassKind.Tier1: return "WHITE";
                case MarkerClassKind.Tier2: return "GREEN";
                case MarkerClassKind.Tier3: return "RED";
                case MarkerClassKind.Boss: return "YELLOW";
                case MarkerClassKind.Lunar: return "LUNAR_BLUE";
                case MarkerClassKind.Void: return "VOID_PURPLE";
                case MarkerClassKind.Equipment: return "EQUIPMENT";
                case MarkerClassKind.LunarEquipment: return "LUNAR_EQUIPMENT";
                case MarkerClassKind.Other: return "OTHER";
                default: return "UNKNOWN";
            }
        }

        public static string LocalizedClassLabel(MarkerClassKind kind, bool russian)
        {
            if (russian)
            {
                switch (kind)
                {
                    case MarkerClassKind.Tier1: return "Белый";
                    case MarkerClassKind.Tier2: return "Зелёный";
                    case MarkerClassKind.Tier3: return "Красный";
                    case MarkerClassKind.Boss: return "Жёлтый";
                    case MarkerClassKind.Lunar: return "Синий";
                    case MarkerClassKind.Void: return "Фиолетовый";
                    case MarkerClassKind.Equipment: return "Снаряжение";
                    case MarkerClassKind.LunarEquipment: return "Лунное снаряжение";
                    default: return "Выбор предмета";
                }
            }

            switch (kind)
            {
                case MarkerClassKind.Tier1: return "White";
                case MarkerClassKind.Tier2: return "Green";
                case MarkerClassKind.Tier3: return "Red";
                case MarkerClassKind.Boss: return "Yellow";
                case MarkerClassKind.Lunar: return "Blue";
                case MarkerClassKind.Void: return "Purple";
                case MarkerClassKind.Equipment: return "Equipment";
                case MarkerClassKind.LunarEquipment: return "Lunar equipment";
                default: return "Item choice";
            }
        }

        public static CommandClassPresentation ResolveCommandClass(IEnumerable<MarkerClassKind>? choiceClasses, bool russian)
        {
            if (choiceClasses == null) return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedClassLabel(MarkerClassKind.Unknown, russian), false);
            var all = choiceClasses.ToArray();
            if (all.Length == 0 || all.Any(x => x == MarkerClassKind.Unknown))
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedClassLabel(MarkerClassKind.Unknown, russian), false);

            var distinct = all.Distinct().ToArray();
            if (distinct.Length != 1)
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedClassLabel(MarkerClassKind.Unknown, russian), false);

            var kind = distinct[0];
            if (kind == MarkerClassKind.Other)
                return new CommandClassPresentation(MarkerClassKind.Other, LocalizedClassLabel(MarkerClassKind.Other, russian), false);

            return new CommandClassPresentation(kind, LocalizedClassLabel(kind, russian), true);
        }

        // The compatibility API above intentionally remains stable for retained callers and regression coverage.
        // Production presentation uses this semantic-label API.
        public static string LocalizedSemanticClassLabel(MarkerClassKind kind, bool russian)
        {
            if (russian)
            {
                switch (kind)
                {
                    case MarkerClassKind.Tier1: return "Белый";
                    case MarkerClassKind.Tier2: return "Зелёный";
                    case MarkerClassKind.Tier3: return "Красный";
                    case MarkerClassKind.Boss: return "Жёлтый";
                    case MarkerClassKind.Lunar: return "Лунный";
                    case MarkerClassKind.Void: return "Предмет Бездны";
                    case MarkerClassKind.Equipment: return "Снаряжение";
                    case MarkerClassKind.LunarEquipment: return "Лунное снаряжение";
                    default: return "Выбор предмета";
                }
            }

            switch (kind)
            {
                case MarkerClassKind.Tier1: return "White";
                case MarkerClassKind.Tier2: return "Green";
                case MarkerClassKind.Tier3: return "Red";
                case MarkerClassKind.Boss: return "Yellow";
                case MarkerClassKind.Lunar: return "Lunar";
                case MarkerClassKind.Void: return "Void";
                case MarkerClassKind.Equipment: return "Equipment";
                case MarkerClassKind.LunarEquipment: return "Lunar equipment";
                default: return "Item choice";
            }
        }

        public static CommandClassPresentation ResolveCommandClassForPresentation(IEnumerable<MarkerClassKind>? choiceClasses, bool russian)
        {
            if (choiceClasses == null) return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedSemanticClassLabel(MarkerClassKind.Unknown, russian), false);
            var all = choiceClasses.ToArray();
            if (all.Length == 0 || all.Any(x => x == MarkerClassKind.Unknown))
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedSemanticClassLabel(MarkerClassKind.Unknown, russian), false);

            var distinct = all.Distinct().ToArray();
            if (distinct.Length != 1)
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedSemanticClassLabel(MarkerClassKind.Unknown, russian), false);

            var kind = distinct[0];
            if (kind == MarkerClassKind.Other)
                return new CommandClassPresentation(MarkerClassKind.Other, LocalizedSemanticClassLabel(MarkerClassKind.Other, russian), false);

            return new CommandClassPresentation(kind, LocalizedSemanticClassLabel(kind, russian), true);
        }

        // The semantic presentation API above intentionally remains stable for retained regression coverage.
        // Production uses this concise/readable label API.
        public static string LocalizedReadableClassLabel(MarkerClassKind kind, bool russian)
            => LocalizedReadableClassLabel(kind, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static string LocalizedReadableClassLabel(MarkerClassKind kind, MarkerLanguage language)
            => MarkerTextLocalization.ReadableClassLabel(kind, language);

        public static CommandClassPresentation ResolveCommandClassForReadablePresentation(IEnumerable<MarkerClassKind>? choiceClasses, bool russian)
            => ResolveCommandClassForReadablePresentation(choiceClasses, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static CommandClassPresentation ResolveCommandClassForReadablePresentation(IEnumerable<MarkerClassKind>? choiceClasses, MarkerLanguage language)
        {
            if (choiceClasses == null) return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedReadableClassLabel(MarkerClassKind.Unknown, language), false);
            var all = choiceClasses.ToArray();
            if (all.Length == 0 || all.Any(x => x == MarkerClassKind.Unknown))
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedReadableClassLabel(MarkerClassKind.Unknown, language), false);

            var distinct = all.Distinct().ToArray();
            if (distinct.Length != 1)
                return new CommandClassPresentation(MarkerClassKind.Unknown, LocalizedReadableClassLabel(MarkerClassKind.Unknown, language), false);

            var kind = distinct[0];
            if (kind == MarkerClassKind.Other)
                return new CommandClassPresentation(MarkerClassKind.Other, LocalizedReadableClassLabel(MarkerClassKind.Other, language), false);

            return new CommandClassPresentation(kind, LocalizedReadableClassLabel(kind, language), true);
        }

    }
}
