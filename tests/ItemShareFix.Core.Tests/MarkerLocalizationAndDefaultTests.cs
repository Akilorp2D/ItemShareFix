using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class V100A2Revision1LocalizationAndDefaultTests
    {
        private static string ReadSource(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            Assert.Fail(relativePath + " was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }

        private static string SourceSha256(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    using var sha = SHA256.Create();
                    return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(candidate)));
                }
                directory = directory.Parent;
            }
            Assert.Fail(relativePath + " was not found while calculating SHA-256.");
            return string.Empty;
        }

        private static MarkerWorldMember Member(long key, MarkerClassKind kind, string identity, string name)
            => new MarkerWorldMember(
                key,
                PersonalMarkerKind.OrdinaryPickup,
                new MarkerWorldPoint(key * 0.05f, 0f, 0f),
                identity,
                name,
                kind,
                MarkerLifetimeKind.Permanent);

        private static MarkerSemanticCluster Cluster(params MarkerWorldMember[] members)
            => new MarkerWorldClusterTracker().Update(members, 0d).Clusters.Single();

        private static MarkerPresentationSettings CompactSettings(bool showDistance)
            => new MarkerPresentationSettings(
                MarkerPresentationMode.Compact,
                showDistance,
                1f,
                5,
                showCategoryDiamond: true,
                showTierComposition: true,
                compactShowCount: true,
                compactMixedStyle: MarkerCompactMixedStyle.CategoryDiamondPyramid,
                categorySortOrder: MarkerCategorySortOrder.HighToLow);

        private static void AssertCategoryMatrix(
            MarkerLanguage language,
            string[] expectedPlural,
            string[] expectedComposition,
            string[] expectedSummary)
        {
            var categories = new[]
            {
                MarkerSemanticCategory.Tier1,
                MarkerSemanticCategory.Tier2,
                MarkerSemanticCategory.Tier3,
                MarkerSemanticCategory.Boss,
                MarkerSemanticCategory.Lunar,
                MarkerSemanticCategory.Void,
                MarkerSemanticCategory.Equipment,
                MarkerSemanticCategory.LunarEquipment,
                MarkerSemanticCategory.Other,
                MarkerSemanticCategory.Unknown,
                MarkerSemanticCategory.CommandState,
            };

            Assert.AreEqual(11, expectedPlural.Length);
            Assert.AreEqual(11, expectedComposition.Length);
            Assert.AreEqual(11, expectedSummary.Length);
            for (var i = 0; i < categories.Length; i++)
            {
                Assert.AreEqual(expectedPlural[i], MarkerTextLocalization.CategoryPlural(categories[i], language), "plural " + categories[i]);
                Assert.AreEqual(expectedComposition[i], MarkerTextLocalization.CompositionLabel(categories[i], language), "composition " + categories[i]);
                Assert.AreEqual(expectedSummary[i], MarkerTextLocalization.CategorySummaryLabel(categories[i], language), "summary " + categories[i]);
            }
        }

        private static void AssertRequiredLocaleContract(
            MarkerLanguage language,
            string white,
            string yellow,
            string lunar,
            string voidLabel,
            string equipment,
            string itemChoice,
            string bossSummary,
            string items,
            string pickup,
            string sharedPickup,
            string moreTypes3,
            string distance)
        {
            Assert.AreEqual(white, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Tier1, language));
            Assert.AreEqual(yellow, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Boss, language));
            Assert.AreEqual(lunar, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Lunar, language));
            Assert.AreEqual(voidLabel, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Void, language));
            Assert.AreEqual(equipment, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Equipment, language));
            Assert.AreEqual(itemChoice, MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Unknown, language));
            Assert.AreEqual(bossSummary, MarkerTextLocalization.CategorySummaryLabel(MarkerSemanticCategory.Boss, language));
            Assert.AreEqual(items, MarkerTextLocalization.GenericItems(language));
            Assert.AreEqual(pickup, MarkerTextLocalization.FallbackPickup(language));
            Assert.AreEqual(sharedPickup, MarkerTextLocalization.FallbackSharedPickup(language));
            Assert.AreEqual(moreTypes3, MarkerTextLocalization.FormatMoreTypes(3, language));
            Assert.AreEqual(distance, MarkerTextLocalization.DistanceUnit(language));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_01_ResolverEnglish()
        {
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage("English"));
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage("en"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_02_ResolverFrench()
        {
            Assert.AreEqual(MarkerLanguage.French, MarkerTextLocalization.ResolveLanguage("French"));
            Assert.AreEqual(MarkerLanguage.French, MarkerTextLocalization.ResolveLanguage("fr"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_03_ResolverItalian()
        {
            Assert.AreEqual(MarkerLanguage.Italian, MarkerTextLocalization.ResolveLanguage("Italian"));
            Assert.AreEqual(MarkerLanguage.Italian, MarkerTextLocalization.ResolveLanguage("it"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_04_ResolverGerman()
        {
            Assert.AreEqual(MarkerLanguage.German, MarkerTextLocalization.ResolveLanguage("German"));
            Assert.AreEqual(MarkerLanguage.German, MarkerTextLocalization.ResolveLanguage("de"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_05_ResolverSpanish()
        {
            Assert.AreEqual(MarkerLanguage.SpanishSpain, MarkerTextLocalization.ResolveLanguage("Spanish"));
            Assert.AreEqual(MarkerLanguage.SpanishSpain, MarkerTextLocalization.ResolveLanguage("es"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_06_ResolverJapanese()
        {
            Assert.AreEqual(MarkerLanguage.Japanese, MarkerTextLocalization.ResolveLanguage("Japanese"));
            Assert.AreEqual(MarkerLanguage.Japanese, MarkerTextLocalization.ResolveLanguage("ja"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_07_ResolverKorean()
        {
            Assert.AreEqual(MarkerLanguage.Korean, MarkerTextLocalization.ResolveLanguage("Korean"));
            Assert.AreEqual(MarkerLanguage.Korean, MarkerTextLocalization.ResolveLanguage("ko"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_08_ResolverPortuguese()
        {
            Assert.AreEqual(MarkerLanguage.PortugueseBrazil, MarkerTextLocalization.ResolveLanguage("Portuguese"));
            Assert.AreEqual(MarkerLanguage.PortugueseBrazil, MarkerTextLocalization.ResolveLanguage("pt"));
            Assert.AreEqual(MarkerLanguage.PortugueseBrazil, MarkerTextLocalization.ResolveLanguage("pt-BR"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_09_ResolverRussian()
        {
            Assert.AreEqual(MarkerLanguage.Russian, MarkerTextLocalization.ResolveLanguage("Russian"));
            Assert.AreEqual(MarkerLanguage.Russian, MarkerTextLocalization.ResolveLanguage("ru"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_10_ResolverChinese()
        {
            Assert.AreEqual(MarkerLanguage.SimplifiedChinese, MarkerTextLocalization.ResolveLanguage("Chinese"));
            Assert.AreEqual(MarkerLanguage.SimplifiedChinese, MarkerTextLocalization.ResolveLanguage("zh"));
            Assert.AreEqual(MarkerLanguage.SimplifiedChinese, MarkerTextLocalization.ResolveLanguage("zh-CN"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_11_ResolverTurkish()
        {
            Assert.AreEqual(MarkerLanguage.Turkish, MarkerTextLocalization.ResolveLanguage("Turkish"));
            Assert.AreEqual(MarkerLanguage.Turkish, MarkerTextLocalization.ResolveLanguage("tr"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_12_ResolverUnknownNullAndEmptyFallbackEnglish()
        {
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage(null));
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage(string.Empty));
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage("  "));
            Assert.AreEqual(MarkerLanguage.English, MarkerTextLocalization.ResolveLanguage("unsupported-locale"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_13_FrenchExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.French,
                "Blanc",
                "Jaune",
                "Lunaire",
                "Néant",
                "Équipement",
                "Choix d’objet",
                "Boss",
                "Objets",
                "Objet",
                "Objet partagé",
                "+ 3 types supplémentaires",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_14_ItalianExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.Italian,
                "Bianco",
                "Giallo",
                "Lunare",
                "Vuoto",
                "Equipaggiamento",
                "Scelta oggetto",
                "Boss",
                "Oggetti",
                "Oggetto",
                "Oggetto condiviso",
                "+ altri 3 tipi",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_15_GermanExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.German,
                "Weiß",
                "Gelb",
                "Lunar",
                "Leere",
                "Ausrüstung",
                "Gegenstandsauswahl",
                "Boss",
                "Gegenstände",
                "Gegenstand",
                "Geteilter Gegenstand",
                "+ 3 weitere Typen",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_16_SpanishSpainExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.SpanishSpain,
                "Blanco",
                "Amarillo",
                "Lunar",
                "Vacío",
                "Equipo",
                "Elección de objeto",
                "Jefe",
                "Objetos",
                "Objeto",
                "Objeto compartido",
                "+ 3 tipos más",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_17_JapaneseExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.Japanese,
                "白",
                "黄",
                "ルナ",
                "ヴォイド",
                "装備",
                "アイテム選択",
                "ボス",
                "アイテム",
                "アイテム",
                "共有アイテム",
                "+ 他3種類",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_18_KoreanExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.Korean,
                "흰색",
                "노랑",
                "루나",
                "공허",
                "장비",
                "아이템 선택",
                "보스",
                "아이템",
                "아이템",
                "공유 아이템",
                "+ 3종 더",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_19_PortugueseBrazilExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.PortugueseBrazil,
                "Branco",
                "Amarelo",
                "Lunar",
                "Vazio",
                "Equipamento",
                "Escolha de item",
                "Chefe",
                "Itens",
                "Item",
                "Item compartilhado",
                "+ mais 3 tipos",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_20_SimplifiedChineseExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.SimplifiedChinese,
                "白色",
                "黄色",
                "月球",
                "虚空",
                "装备",
                "物品选择",
                "首领",
                "物品",
                "物品",
                "共享物品",
                "+ 另外 3 种",
                "米");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_21_TurkishExactRequiredMarkerText()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.Turkish,
                "Beyaz",
                "Sarı",
                "Ay",
                "Hiçlik",
                "Ekipman",
                "Eşya seçimi",
                "Boss",
                "Eşyalar",
                "Eşya",
                "Paylaşılan eşya",
                "+ 3 tür daha",
                "m");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_22_EnglishFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.English,
                new[] { "White items", "Green items", "Red items", "Boss items", "Lunar items", "Void items", "Equipment", "Lunar equipment", "Other items", "Unknown items", "Item choice" },
                new[] { "White", "Green", "Red", "Boss", "Lunar", "Void", "Equipment", "Lunar equipment", "Other", "Unknown", "Choice" },
                new[] { "White", "Green", "Red", "Boss", "Lunar", "Void", "Equipment", "Lunar equipment", "Other", "Unknown", "Choice" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_23_FrenchFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.French,
                new[] { "Objets blancs", "Objets verts", "Objets rouges", "Objets de boss", "Objets lunaires", "Objets du Néant", "Équipement", "Équipement lunaire", "Autres objets", "Objets inconnus", "Choix d’objet" },
                new[] { "Blanc", "Vert", "Rouge", "Boss", "Lunaire", "Néant", "Équipement", "Équipement lunaire", "Autres", "Inconnu", "Choix" },
                new[] { "Blanc", "Vert", "Rouge", "Boss", "Lunaire", "Néant", "Équipement", "Équipement lunaire", "Autres", "Inconnu", "Choix" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_24_ItalianFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.Italian,
                new[] { "Oggetti bianchi", "Oggetti verdi", "Oggetti rossi", "Oggetti boss", "Oggetti lunari", "Oggetti del Vuoto", "Equipaggiamento", "Equipaggiamento lunare", "Altri oggetti", "Oggetti sconosciuti", "Scelta oggetto" },
                new[] { "Bianco", "Verde", "Rosso", "Boss", "Lunare", "Vuoto", "Equipaggiamento", "Equipaggiamento lunare", "Altro", "Sconosciuto", "Scelta" },
                new[] { "Bianco", "Verde", "Rosso", "Boss", "Lunare", "Vuoto", "Equipaggiamento", "Equipaggiamento lunare", "Altro", "Sconosciuto", "Scelta" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_25_GermanFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.German,
                new[] { "Weiße Gegenstände", "Grüne Gegenstände", "Rote Gegenstände", "Boss-Gegenstände", "Lunar-Gegenstände", "Leere-Gegenstände", "Ausrüstung", "Lunar-Ausrüstung", "Andere Gegenstände", "Unbekannte Gegenstände", "Gegenstandsauswahl" },
                new[] { "Weiß", "Grün", "Rot", "Boss", "Lunar", "Leere", "Ausrüstung", "Lunar-Ausrüstung", "Andere", "Unbekannt", "Auswahl" },
                new[] { "Weiß", "Grün", "Rot", "Boss", "Lunar", "Leere", "Ausrüstung", "Lunar-Ausrüstung", "Andere", "Unbekannt", "Auswahl" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_26_SpanishSpainFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.SpanishSpain,
                new[] { "Objetos blancos", "Objetos verdes", "Objetos rojos", "Objetos de jefe", "Objetos lunares", "Objetos del Vacío", "Equipo", "Equipo lunar", "Otros objetos", "Objetos desconocidos", "Elección de objeto" },
                new[] { "Blanco", "Verde", "Rojo", "Jefe", "Lunar", "Vacío", "Equipo", "Equipo lunar", "Otros", "Desconocido", "Elección" },
                new[] { "Blanco", "Verde", "Rojo", "Jefe", "Lunar", "Vacío", "Equipo", "Equipo lunar", "Otros", "Desconocido", "Elección" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_27_JapaneseFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.Japanese,
                new[] { "白アイテム", "緑アイテム", "赤アイテム", "ボスアイテム", "ルナアイテム", "ヴォイドアイテム", "装備", "ルナ装備", "その他のアイテム", "不明なアイテム", "アイテム選択" },
                new[] { "白", "緑", "赤", "ボス", "ルナ", "ヴォイド", "装備", "ルナ装備", "その他", "不明", "選択" },
                new[] { "白", "緑", "赤", "ボス", "ルナ", "ヴォイド", "装備", "ルナ装備", "その他", "不明", "選択" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_28_KoreanFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.Korean,
                new[] { "흰색 아이템", "초록 아이템", "빨간 아이템", "보스 아이템", "루나 아이템", "공허 아이템", "장비", "루나 장비", "기타 아이템", "알 수 없는 아이템", "아이템 선택" },
                new[] { "흰색", "초록", "빨강", "보스", "루나", "공허", "장비", "루나 장비", "기타", "알 수 없음", "선택" },
                new[] { "흰색", "초록", "빨강", "보스", "루나", "공허", "장비", "루나 장비", "기타", "알 수 없음", "선택" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_29_PortugueseBrazilFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.PortugueseBrazil,
                new[] { "Itens brancos", "Itens verdes", "Itens vermelhos", "Itens de chefe", "Itens lunares", "Itens do Vazio", "Equipamento", "Equipamento lunar", "Outros itens", "Itens desconhecidos", "Escolha de item" },
                new[] { "Branco", "Verde", "Vermelho", "Chefe", "Lunar", "Vazio", "Equipamento", "Equipamento lunar", "Outros", "Desconhecido", "Escolha" },
                new[] { "Branco", "Verde", "Vermelho", "Chefe", "Lunar", "Vazio", "Equipamento", "Equipamento lunar", "Outros", "Desconhecido", "Escolha" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_30_RussianFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.Russian,
                new[] { "Белые предметы", "Зелёные предметы", "Красные предметы", "Предметы босса", "Лунные предметы", "Предметы Бездны", "Снаряжение", "Лунное снаряжение", "Другие предметы", "Неизвестные предметы", "Выбор предмета" },
                new[] { "Белые", "Зелёные", "Красные", "Босс", "Лунные", "Бездна", "Снаряжение", "Лунное снаряжение", "Другие", "Неизвестные", "Выбор" },
                new[] { "Белый", "Зелёный", "Красный", "Босс", "Лунный", "Бездна", "Снаряжение", "Лунное снаряжение", "Другие", "Неизвестные", "Выбор" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_31_SimplifiedChineseFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.SimplifiedChinese,
                new[] { "白色物品", "绿色物品", "红色物品", "首领物品", "月球物品", "虚空物品", "装备", "月球装备", "其他物品", "未知物品", "物品选择" },
                new[] { "白色", "绿色", "红色", "首领", "月球", "虚空", "装备", "月球装备", "其他", "未知", "选择" },
                new[] { "白色", "绿色", "红色", "首领", "月球", "虚空", "装备", "月球装备", "其他", "未知", "选择" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_32_TurkishFullCategoryMatrix()
        {
            AssertCategoryMatrix(
                MarkerLanguage.Turkish,
                new[] { "Beyaz eşyalar", "Yeşil eşyalar", "Kırmızı eşyalar", "Boss eşyaları", "Ay eşyaları", "Hiçlik eşyaları", "Ekipman", "Ay ekipmanı", "Diğer eşyalar", "Bilinmeyen eşyalar", "Eşya seçimi" },
                new[] { "Beyaz", "Yeşil", "Kırmızı", "Boss", "Ay", "Hiçlik", "Ekipman", "Ay ekipmanı", "Diğer", "Bilinmeyen", "Seçim" },
                new[] { "Beyaz", "Yeşil", "Kırmızı", "Boss", "Ay", "Hiçlik", "Ekipman", "Ay ekipmanı", "Diğer", "Bilinmeyen", "Seçim" });
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_33_CanonicalTableHasExactContractKeysAndElevenLanguages()
        {
            Assert.AreEqual(11, MarkerTextLocalization.OfficialLanguages.Length);
            var source = ReadSource("src/ItemShareFix.Core/MarkerTextLocalization.cs");
            Assert.AreEqual(49, source.Split(new[] { "[\"" }, StringSplitOptions.None).Length - 1);
            Assert.IsTrue(MarkerTextLocalization.ContainsKey("readable.white"));
            Assert.IsTrue(MarkerTextLocalization.ContainsKey("overflow.more_types_format"));
            Assert.IsTrue(MarkerTextLocalization.ContainsKey("distance.unit"));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_34_EnglishAndRussianAcceptedOutputsRemainExact()
        {
            AssertRequiredLocaleContract(
                MarkerLanguage.English,
                "White", "Yellow", "Lunar", "Void", "Equipment", "Item choice",
                "Boss", "Items", "Pickup", "Shared pickup", "+ 3 more types", "m");
            AssertRequiredLocaleContract(
                MarkerLanguage.Russian,
                "Белый", "Жёлтый", "Лунный", "Бездна", "Снаряжение", "Выбор предмета",
                "Босс", "Предметы", "Предмет", "Общий предмет", "+ ещё 3 вида", "м");
            Assert.AreEqual("+ ещё 1 вид", MarkerTextLocalization.FormatMoreTypes(1, MarkerLanguage.Russian));
            Assert.AreEqual("+ ещё 2 вида", MarkerTextLocalization.FormatMoreTypes(2, MarkerLanguage.Russian));
            Assert.AreEqual("+ ещё 5 видов", MarkerTextLocalization.FormatMoreTypes(5, MarkerLanguage.Russian));
            Assert.AreEqual("+ ещё 11 видов", MarkerTextLocalization.FormatMoreTypes(11, MarkerLanguage.Russian));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_35_FreshShareTemporaryDefaultIsOffWithoutMigrationWrite()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            Assert.AreEqual(1, source.Split(new[] { "config.Bind(\"General\", \"ShareTemporaryItems\"" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(source, "ShareTemporaryItems = config.Bind(\"General\", \"ShareTemporaryItems\", false,");
            Assert.IsFalse(source.Contains("ShareTemporaryItems.Value = false", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ShareTemporaryItems.Value=false", StringComparison.Ordinal));
            StringAssert.Contains(source, "existing saved values are preserved");
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_36_TemporaryGameplaySemanticsRemainFrozen()
        {
            Assert.AreEqual("AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506", SourceSha256("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            Assert.AreEqual("3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23", SourceSha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual("E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145", SourceSha256("src/ItemShareFix.Core/TemporarySharingPolicy.cs"));
            Assert.AreEqual("43935D44596A14C91FAAF276357DD7CBBF5B226330835BE6BCF265B9531AD4A9", SourceSha256("src/ItemShareFix.Plugin/UpstreamBridge.cs"));
        }

                [TestMethod]
        public void ISF_V100_A2R1_LOC_37_ItemNamesRemainGameNativeAndMarkerTableContainsNoItemDictionary()
        {
                var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
                var localization = ReadSource("src/ItemShareFix.Core/MarkerTextLocalization.cs");
                StringAssert.Contains(client, "Language.GetString(def.nameToken)");
                StringAssert.Contains(client, "!string.Equals(localized, def.nameToken, StringComparison.Ordinal)");
                StringAssert.Contains(client, "return localized;");
                Assert.IsFalse(localization.Contains("nameToken", StringComparison.Ordinal));
                Assert.IsFalse(localization.Contains("PickupIndex", StringComparison.Ordinal));
                Assert.IsFalse(localization.Contains("ItemIndex", StringComparison.Ordinal));
        }

                [TestMethod]
        public void ISF_V100_A2R1_LOC_38_RuntimeUsesFullLocaleAndRendererCacheKeysOnFullLanguage()
        {
                var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
                var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
                StringAssert.Contains(client, "private static MarkerLanguage CurrentMarkerLanguage()");
                StringAssert.Contains(client, "MarkerTextLocalization.ResolveLanguage(Language.currentLanguageName)");
                StringAssert.Contains(client, "var markerLanguage = CurrentMarkerLanguage();");
                Assert.IsFalse(client.Contains("IsRussianUi()", StringComparison.Ordinal));
                StringAssert.Contains(renderer, "public MarkerLanguage PresentationPlanLanguage");
                StringAssert.Contains(renderer, "view.PresentationPlanLanguage == language");
                StringAssert.Contains(renderer, "view.PresentationPlanLanguage = language");
                Assert.IsFalse(renderer.Contains("PresentationPlanRussian", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_39_CompactRemainsCategoryTextFreeWithLocalizedDistanceOnly()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, MarkerClassKind.Tier1, "a", "A"),
                    Member(2, MarkerClassKind.Tier2, "b", "B"),
                    Member(3, MarkerClassKind.Boss, "c", "C")),
                CompactSettings(showDistance: true),
                25,
                expanded: false,
                language: MarkerLanguage.French);
            Assert.AreEqual("25 m", plan.Text);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.IsFalse(plan.Text.Contains("Objets", StringComparison.Ordinal));
            Assert.IsFalse(plan.Text.Contains("Blanc", StringComparison.Ordinal));
            Assert.IsFalse(plan.Text.Contains("Boss", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_LOC_40_NoVisibleLifetimeWordIsAddedToCanonicalMarkerText()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerTextLocalization.cs");
            Assert.IsFalse(source.Contains("Temporary", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Временный", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Временная", StringComparison.Ordinal));
            Assert.AreEqual(string.Empty, MarkerLifetimePolicy.BuildSummaryLine(MarkerLifetimeKind.Temporary, 5, true, false));
            Assert.AreEqual("Crowbar", MarkerLifetimePolicy.BuildDetailedItemDisplayName("Crowbar", MarkerLifetimeKind.Temporary, false));
        }

    }
}
