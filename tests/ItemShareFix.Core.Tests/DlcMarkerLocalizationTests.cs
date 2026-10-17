using System;
using System.IO;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public class DlcMarkerLocalizationTests
    {
        [DataTestMethod]
        [DataRow(MarkerLanguage.English, "Hallowed", "Hex", "N'Tormat Pages")]
        [DataRow(MarkerLanguage.French, "Sacrés", "Maléfices", "Pages de N'tormat")]
        [DataRow(MarkerLanguage.Italian, "Consacrati", "Malefici", "Pagine del N'tormat")]
        [DataRow(MarkerLanguage.German, "Geheiligte", "Verhexungen", "N’tormat-Seiten")]
        [DataRow(MarkerLanguage.SpanishSpain, "Sagrados", "Maldiciones", "Páginas del N'tormat")]
        [DataRow(MarkerLanguage.Japanese, "神聖なアイテム", "ヘックス", "ン・トーマットのページ")]
        [DataRow(MarkerLanguage.Korean, "거룩한 아이템", "사술", "은토맛 페이지")]
        [DataRow(MarkerLanguage.PortugueseBrazil, "Santificados", "Maldições", "Páginas do N'tormat")]
        [DataRow(MarkerLanguage.Russian, "Освящённые", "Проклятия", "Страницы Н'тормата")]
        [DataRow(MarkerLanguage.SimplifiedChinese, "神圣物品", "诅咒", "恩托马特书页")]
        [DataRow(MarkerLanguage.Turkish, "Kutsal", "Büyüler", "N'tormat Sayfaları")]
        public void DlcLabelsFollowGameLanguageAcrossCommandAndClusterText(MarkerLanguage language, string hallowed, string hex, string pages)
        {
            var kinds = new[] { MarkerClassKind.Hallowed, MarkerClassKind.Hex, MarkerClassKind.Page };
            var expected = new[] { hallowed, hex, pages };
            for (var i = 0; i < kinds.Length; i++)
            {
                var command = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { kinds[i], kinds[i] }, language);
                Assert.IsTrue(command.ExactClass); Assert.AreEqual(expected[i], command.Label);
                var category = MarkerSemanticCategoryPolicy.From(PersonalMarkerKind.CommandPicker, kinds[i]);
                Assert.AreEqual(expected[i], MarkerTextLocalization.CategoryPlural(category, language));
                Assert.AreEqual(expected[i], MarkerTextLocalization.CompositionLabel(category, language));
                Assert.AreEqual(expected[i], MarkerTextLocalization.CategorySummaryLabel(category, language));
            }
        }

        [TestMethod]
        public void LanguageChangeDoesNotLeaveEnglishDlcLabelsAndUnknownLanguageFallsBack()
        {
            Assert.AreEqual("Hallowed", MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Hallowed, MarkerLanguage.English));
            Assert.AreEqual("Освящённые", MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Hallowed, MarkerTextLocalization.ResolveLanguage("RU")));
            Assert.AreEqual("Hex", MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Hex, (MarkerLanguage)(-1)));
            Assert.AreEqual("N'Tormat Pages", MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Page, (MarkerLanguage)999));
            Assert.AreEqual("Hex", MarkerTextLocalization.ReadableClassLabel(MarkerClassKind.Hex, MarkerTextLocalization.ResolveLanguage("unsupported")));
        }

        [TestMethod]
        public void CommandCategoryTextUsesSameNativeColorResolverAsCategoryGlyph()
        {
            var renderer = Read("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var text = C2HistoricalFreezeReconciliation.MethodBody(renderer, "private string BuildRenderedText(");
            StringAssert.Contains(text, "rowColor = ResolveCategoryColor(cluster, plan.CategoryEntries[i].Category);");
            Assert.IsFalse(text.Contains("ResolveConfiguredCategoryColor(plan.CategoryEntries[i].Category)", StringComparison.Ordinal));
            StringAssert.Contains(text, "ColorHex(rowColor)");
            var native = C2HistoricalFreezeReconciliation.MethodBody(renderer, "private Color ResolveCategoryColor(");
            StringAssert.Contains(native, "IsDlcCategory(category)"); StringAssert.Contains(native, "SanitizeColor(input.NativeColor)");
            StringAssert.Contains(native, "return ResolveConfiguredCategoryColor(category);");
            var client = Read("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(client, "Language.GetString(def.nameToken)");
            StringAssert.Contains(client, "Language.currentLanguage");
        }

        [TestMethod]
        public void MarkerLocaleDeltaPreservesParentAndRejectsSourceDrift()
        {
            const string path = "src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs";
            var raw = Read(path); Assert.AreNotEqual(raw, MarkerLocaleReconciliation.Normalize(path, raw));
            Assert.ThrowsException<AssertFailedException>(() => MarkerLocaleReconciliation.Normalize(path, raw + "// unreviewed"));
            Assert.ThrowsException<AssertFailedException>(() => MarkerLocaleReconciliation.Normalize(path,
                raw.Replace("rowColor = ResolveCategoryColor(cluster, plan.CategoryEntries[i].Category);", "rowColor = Color.white;", StringComparison.Ordinal)));
        }

        private static string Read(string path) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));
    }
}
