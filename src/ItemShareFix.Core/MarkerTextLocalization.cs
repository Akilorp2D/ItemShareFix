using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemShareFix.Core
{
    public enum MarkerLanguage
    {
        English,
        French,
        Italian,
        German,
        SpanishSpain,
        Japanese,
        Korean,
        PortugueseBrazil,
        Russian,
        SimplifiedChinese,
        Turkish,
    }

    /// <summary>
    /// Pure ItemShareFix-owned marker text localization. Actual RoR2 item names remain game-native.
    /// </summary>
    public static class MarkerTextLocalization
    {
        public static readonly MarkerLanguage[] OfficialLanguages =
        {
            MarkerLanguage.English, MarkerLanguage.French, MarkerLanguage.Italian, MarkerLanguage.German,
            MarkerLanguage.SpanishSpain, MarkerLanguage.Japanese, MarkerLanguage.Korean, MarkerLanguage.PortugueseBrazil,
            MarkerLanguage.Russian, MarkerLanguage.SimplifiedChinese, MarkerLanguage.Turkish,
        };

        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["readable.white"] = new[] { "White", "Blanc", "Bianco", "Weiß", "Blanco", "白", "흰색", "Branco", "Белый", "白色", "Beyaz" },
            ["readable.green"] = new[] { "Green", "Vert", "Verde", "Grün", "Verde", "緑", "초록", "Verde", "Зелёный", "绿色", "Yeşil" },
            ["readable.red"] = new[] { "Red", "Rouge", "Rosso", "Rot", "Rojo", "赤", "빨강", "Vermelho", "Красный", "红色", "Kırmızı" },
            ["readable.boss_yellow"] = new[] { "Yellow", "Jaune", "Giallo", "Gelb", "Amarillo", "黄", "노랑", "Amarelo", "Жёлтый", "黄色", "Sarı" },
            ["readable.lunar"] = new[] { "Lunar", "Lunaire", "Lunare", "Lunar", "Lunar", "ルナ", "루나", "Lunar", "Лунный", "月球", "Ay" },
            ["readable.void"] = new[] { "Void", "Néant", "Vuoto", "Leere", "Vacío", "ヴォイド", "공허", "Vazio", "Бездна", "虚空", "Hiçlik" },
            ["readable.equipment"] = new[] { "Equipment", "Équipement", "Equipaggiamento", "Ausrüstung", "Equipo", "装備", "장비", "Equipamento", "Снаряжение", "装备", "Ekipman" },
            ["readable.lunar_equipment"] = new[] { "Equipment", "Équipement", "Equipaggiamento", "Ausrüstung", "Equipo", "装備", "장비", "Equipamento", "Снаряжение", "装备", "Ekipman" },
            ["readable.item_choice"] = new[] { "Item choice", "Choix d’objet", "Scelta oggetto", "Gegenstandsauswahl", "Elección de objeto", "アイテム選択", "아이템 선택", "Escolha de item", "Выбор предмета", "物品选择", "Eşya seçimi" },
            ["plural.white_items"] = new[] { "White items", "Objets blancs", "Oggetti bianchi", "Weiße Gegenstände", "Objetos blancos", "白アイテム", "흰색 아이템", "Itens brancos", "Белые предметы", "白色物品", "Beyaz eşyalar" },
            ["plural.green_items"] = new[] { "Green items", "Objets verts", "Oggetti verdi", "Grüne Gegenstände", "Objetos verdes", "緑アイテム", "초록 아이템", "Itens verdes", "Зелёные предметы", "绿色物品", "Yeşil eşyalar" },
            ["plural.red_items"] = new[] { "Red items", "Objets rouges", "Oggetti rossi", "Rote Gegenstände", "Objetos rojos", "赤アイテム", "빨간 아이템", "Itens vermelhos", "Красные предметы", "红色物品", "Kırmızı eşyalar" },
            ["plural.boss_items"] = new[] { "Boss items", "Objets de boss", "Oggetti boss", "Boss-Gegenstände", "Objetos de jefe", "ボスアイテム", "보스 아이템", "Itens de chefe", "Предметы босса", "首领物品", "Boss eşyaları" },
            ["plural.lunar_items"] = new[] { "Lunar items", "Objets lunaires", "Oggetti lunari", "Lunar-Gegenstände", "Objetos lunares", "ルナアイテム", "루나 아이템", "Itens lunares", "Лунные предметы", "月球物品", "Ay eşyaları" },
            ["plural.void_items"] = new[] { "Void items", "Objets du Néant", "Oggetti del Vuoto", "Leere-Gegenstände", "Objetos del Vacío", "ヴォイドアイテム", "공허 아이템", "Itens do Vazio", "Предметы Бездны", "虚空物品", "Hiçlik eşyaları" },
            ["plural.equipment"] = new[] { "Equipment", "Équipement", "Equipaggiamento", "Ausrüstung", "Equipo", "装備", "장비", "Equipamento", "Снаряжение", "装备", "Ekipman" },
            ["plural.lunar_equipment"] = new[] { "Lunar equipment", "Équipement lunaire", "Equipaggiamento lunare", "Lunar-Ausrüstung", "Equipo lunar", "ルナ装備", "루나 장비", "Equipamento lunar", "Лунное снаряжение", "月球装备", "Ay ekipmanı" },
            ["plural.other_items"] = new[] { "Other items", "Autres objets", "Altri oggetti", "Andere Gegenstände", "Otros objetos", "その他のアイテム", "기타 아이템", "Outros itens", "Другие предметы", "其他物品", "Diğer eşyalar" },
            ["plural.item_choice"] = new[] { "Item choice", "Choix d’objet", "Scelta oggetto", "Gegenstandsauswahl", "Elección de objeto", "アイテム選択", "아이템 선택", "Escolha de item", "Выбор предмета", "物品选择", "Eşya seçimi" },
            ["plural.unknown_items"] = new[] { "Unknown items", "Objets inconnus", "Oggetti sconosciuti", "Unbekannte Gegenstände", "Objetos desconocidos", "不明なアイテム", "알 수 없는 아이템", "Itens desconhecidos", "Неизвестные предметы", "未知物品", "Bilinmeyen eşyalar" },
            ["composition.white"] = new[] { "White", "Blanc", "Bianco", "Weiß", "Blanco", "白", "흰색", "Branco", "Белые", "白色", "Beyaz" },
            ["composition.green"] = new[] { "Green", "Vert", "Verde", "Grün", "Verde", "緑", "초록", "Verde", "Зелёные", "绿色", "Yeşil" },
            ["composition.red"] = new[] { "Red", "Rouge", "Rosso", "Rot", "Rojo", "赤", "빨강", "Vermelho", "Красные", "红色", "Kırmızı" },
            ["composition.boss"] = new[] { "Boss", "Boss", "Boss", "Boss", "Jefe", "ボス", "보스", "Chefe", "Босс", "首领", "Boss" },
            ["composition.lunar"] = new[] { "Lunar", "Lunaire", "Lunare", "Lunar", "Lunar", "ルナ", "루나", "Lunar", "Лунные", "月球", "Ay" },
            ["composition.void"] = new[] { "Void", "Néant", "Vuoto", "Leere", "Vacío", "ヴォイド", "공허", "Vazio", "Бездна", "虚空", "Hiçlik" },
            ["composition.equipment"] = new[] { "Equipment", "Équipement", "Equipaggiamento", "Ausrüstung", "Equipo", "装備", "장비", "Equipamento", "Снаряжение", "装备", "Ekipman" },
            ["composition.lunar_equipment"] = new[] { "Lunar equipment", "Équipement lunaire", "Equipaggiamento lunare", "Lunar-Ausrüstung", "Equipo lunar", "ルナ装備", "루나 장비", "Equipamento lunar", "Лунное снаряжение", "月球装备", "Ay ekipmanı" },
            ["composition.other"] = new[] { "Other", "Autres", "Altro", "Andere", "Otros", "その他", "기타", "Outros", "Другие", "其他", "Diğer" },
            ["composition.choice"] = new[] { "Choice", "Choix", "Scelta", "Auswahl", "Elección", "選択", "선택", "Escolha", "Выбор", "选择", "Seçim" },
            ["composition.unknown"] = new[] { "Unknown", "Inconnu", "Sconosciuto", "Unbekannt", "Desconocido", "不明", "알 수 없음", "Desconhecido", "Неизвестные", "未知", "Bilinmeyen" },
            ["summary.white"] = new[] { "White", "Blanc", "Bianco", "Weiß", "Blanco", "白", "흰색", "Branco", "Белый", "白色", "Beyaz" },
            ["summary.green"] = new[] { "Green", "Vert", "Verde", "Grün", "Verde", "緑", "초록", "Verde", "Зелёный", "绿色", "Yeşil" },
            ["summary.red"] = new[] { "Red", "Rouge", "Rosso", "Rot", "Rojo", "赤", "빨강", "Vermelho", "Красный", "红色", "Kırmızı" },
            ["summary.boss"] = new[] { "Boss", "Boss", "Boss", "Boss", "Jefe", "ボス", "보스", "Chefe", "Босс", "首领", "Boss" },
            ["summary.lunar"] = new[] { "Lunar", "Lunaire", "Lunare", "Lunar", "Lunar", "ルナ", "루나", "Lunar", "Лунный", "月球", "Ay" },
            ["summary.void"] = new[] { "Void", "Néant", "Vuoto", "Leere", "Vacío", "ヴォイド", "공허", "Vazio", "Бездна", "虚空", "Hiçlik" },
            ["summary.equipment"] = new[] { "Equipment", "Équipement", "Equipaggiamento", "Ausrüstung", "Equipo", "装備", "장비", "Equipamento", "Снаряжение", "装备", "Ekipman" },
            ["summary.lunar_equipment"] = new[] { "Lunar equipment", "Équipement lunaire", "Equipaggiamento lunare", "Lunar-Ausrüstung", "Equipo lunar", "ルナ装備", "루나 장비", "Equipamento lunar", "Лунное снаряжение", "月球装备", "Ay ekipmanı" },
            ["summary.other"] = new[] { "Other", "Autres", "Altro", "Andere", "Otros", "その他", "기타", "Outros", "Другие", "其他", "Diğer" },
            ["summary.choice"] = new[] { "Choice", "Choix", "Scelta", "Auswahl", "Elección", "選択", "선택", "Escolha", "Выбор", "选择", "Seçim" },
            ["summary.unknown"] = new[] { "Unknown", "Inconnu", "Sconosciuto", "Unbekannt", "Desconocido", "不明", "알 수 없음", "Desconhecido", "Неизвестные", "未知", "Bilinmeyen" },
            ["generic.items"] = new[] { "Items", "Objets", "Oggetti", "Gegenstände", "Objetos", "アイテム", "아이템", "Itens", "Предметы", "物品", "Eşyalar" },
            ["fallback.pickup"] = new[] { "Pickup", "Objet", "Oggetto", "Gegenstand", "Objeto", "アイテム", "아이템", "Item", "Предмет", "物品", "Eşya" },
            ["fallback.shared_pickup"] = new[] { "Shared pickup", "Objet partagé", "Oggetto condiviso", "Geteilter Gegenstand", "Objeto compartido", "共有アイテム", "공유 아이템", "Item compartilhado", "Общий предмет", "共享物品", "Paylaşılan eşya" },
            ["fallback.command_choice"] = new[] { "Command choice", "Choix de Commande", "Scelta Comando", "Command-Auswahl", "Elección de Comando", "コマンド選択", "지휘 선택", "Escolha de Comando", "Выбор предмета", "命令选择", "Komut seçimi" },
            ["overflow.physical_items_format"] = new[] { "+{0} items", "+{0} objets", "+{0} oggetti", "+{0} Gegenstände", "+{0} objetos", "+{0} アイテム", "+{0} 아이템", "+{0} itens", "+{0} предметов", "+{0} 个物品", "+{0} eşya" },
            ["overflow.more_types_format"] = new[] { "+ {0} more types", "+ {0} types supplémentaires", "+ altri {0} tipi", "+ {0} weitere Typen", "+ {0} tipos más", "+ 他{0}種類", "+ {0}종 더", "+ mais {0} tipos", "+ ещё {0} {RU_TYPE_WORD}", "+ 另外 {0} 种", "+ {0} tür daha" },
            ["distance.unit"] = new[] { "m", "m", "m", "m", "m", "m", "m", "m", "м", "米", "m" },
        };

        public static MarkerLanguage ResolveLanguage(string? raw)
        {
            var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "english":
                case "en": return MarkerLanguage.English;
                case "french":
                case "fr": return MarkerLanguage.French;
                case "italian":
                case "it": return MarkerLanguage.Italian;
                case "german":
                case "de": return MarkerLanguage.German;
                case "spanish":
                case "es": return MarkerLanguage.SpanishSpain;
                case "japanese":
                case "ja": return MarkerLanguage.Japanese;
                case "korean":
                case "ko": return MarkerLanguage.Korean;
                case "portuguese":
                case "pt":
                case "pt-br": return MarkerLanguage.PortugueseBrazil;
                case "russian":
                case "ru": return MarkerLanguage.Russian;
                case "chinese":
                case "zh":
                case "zh-cn": return MarkerLanguage.SimplifiedChinese;
                case "turkish":
                case "tr": return MarkerLanguage.Turkish;
                default: return MarkerLanguage.English;
            }
        }

        public static MarkerLanguage FromRussianCompatibility(bool russian)
            => russian ? MarkerLanguage.Russian : MarkerLanguage.English;

        public static string ReadableClassLabel(MarkerClassKind kind, MarkerLanguage language)
        {
            switch (kind)
            {
                case MarkerClassKind.Tier1: return Get("readable.white", language);
                case MarkerClassKind.Tier2: return Get("readable.green", language);
                case MarkerClassKind.Tier3: return Get("readable.red", language);
                case MarkerClassKind.Boss: return Get("readable.boss_yellow", language);
                case MarkerClassKind.Lunar: return Get("readable.lunar", language);
                case MarkerClassKind.Void: return Get("readable.void", language);
                case MarkerClassKind.Equipment: return Get("readable.equipment", language);
                case MarkerClassKind.LunarEquipment: return Get("readable.lunar_equipment", language);
                default: return Get("readable.item_choice", language);
            }
        }

        public static string CategoryPlural(MarkerSemanticCategory category, MarkerLanguage language)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier1: return Get("plural.white_items", language);
                case MarkerSemanticCategory.Tier2: return Get("plural.green_items", language);
                case MarkerSemanticCategory.Tier3: return Get("plural.red_items", language);
                case MarkerSemanticCategory.Boss: return Get("plural.boss_items", language);
                case MarkerSemanticCategory.Lunar: return Get("plural.lunar_items", language);
                case MarkerSemanticCategory.Void: return Get("plural.void_items", language);
                case MarkerSemanticCategory.Equipment: return Get("plural.equipment", language);
                case MarkerSemanticCategory.LunarEquipment: return Get("plural.lunar_equipment", language);
                case MarkerSemanticCategory.Other: return Get("plural.other_items", language);
                case MarkerSemanticCategory.CommandState: return Get("plural.item_choice", language);
                default: return Get("plural.unknown_items", language);
            }
        }

        public static string CompositionLabel(MarkerSemanticCategory category, MarkerLanguage language)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier1: return Get("composition.white", language);
                case MarkerSemanticCategory.Tier2: return Get("composition.green", language);
                case MarkerSemanticCategory.Tier3: return Get("composition.red", language);
                case MarkerSemanticCategory.Boss: return Get("composition.boss", language);
                case MarkerSemanticCategory.Lunar: return Get("composition.lunar", language);
                case MarkerSemanticCategory.Void: return Get("composition.void", language);
                case MarkerSemanticCategory.Equipment: return Get("composition.equipment", language);
                case MarkerSemanticCategory.LunarEquipment: return Get("composition.lunar_equipment", language);
                case MarkerSemanticCategory.Other: return Get("composition.other", language);
                case MarkerSemanticCategory.CommandState: return Get("composition.choice", language);
                default: return Get("composition.unknown", language);
            }
        }

        public static string CategorySummaryLabel(MarkerSemanticCategory category, MarkerLanguage language)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier1: return Get("summary.white", language);
                case MarkerSemanticCategory.Tier2: return Get("summary.green", language);
                case MarkerSemanticCategory.Tier3: return Get("summary.red", language);
                case MarkerSemanticCategory.Boss: return Get("summary.boss", language);
                case MarkerSemanticCategory.Lunar: return Get("summary.lunar", language);
                case MarkerSemanticCategory.Void: return Get("summary.void", language);
                case MarkerSemanticCategory.Equipment: return Get("summary.equipment", language);
                case MarkerSemanticCategory.LunarEquipment: return Get("summary.lunar_equipment", language);
                case MarkerSemanticCategory.Other: return Get("summary.other", language);
                case MarkerSemanticCategory.CommandState: return Get("summary.choice", language);
                default: return Get("summary.unknown", language);
            }
        }

        public static string GenericItems(MarkerLanguage language) => Get("generic.items", language);
        public static string FallbackPickup(MarkerLanguage language) => Get("fallback.pickup", language);
        public static string FallbackSharedPickup(MarkerLanguage language) => Get("fallback.shared_pickup", language);
        public static string FallbackCommandChoice(MarkerLanguage language) => Get("fallback.command_choice", language);
        public static string DistanceUnit(MarkerLanguage language) => Get("distance.unit", language);

        public static string FormatPhysicalItems(int count, MarkerLanguage language)
            => string.Format(CultureInfo.InvariantCulture, Get("overflow.physical_items_format", language), Math.Max(0, count));

        public static string FormatMoreTypes(int count, MarkerLanguage language)
        {
            var safe = Math.Max(0, count);
            var template = Get("overflow.more_types_format", language);
            if (language == MarkerLanguage.Russian)
                template = template.Replace("{RU_TYPE_WORD}", RussianDistinctTypeWord(safe));
            return string.Format(CultureInfo.InvariantCulture, template, safe);
        }

        public static string RussianDistinctTypeWord(int count)
        {
            var safe = Math.Abs(count);
            var lastTwo = safe % 100;
            if (lastTwo >= 11 && lastTwo <= 14) return "видов";
            switch (safe % 10)
            {
                case 1: return "вид";
                case 2:
                case 3:
                case 4: return "вида";
                default: return "видов";
            }
        }

        public static bool ContainsKey(string key) => key != null && Text.ContainsKey(key);

        private static string Get(string key, MarkerLanguage language)
        {
            if (!Text.TryGetValue(key, out var values) || values.Length == 0) return string.Empty;
            var index = (int)language;
            if (index < 0 || index >= values.Length) index = 0;
            return values[index];
        }
    }
}
