using System;
using System.IO;
using System.Linq;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class ZZ_C21B1Correction8GlyphSizeParityTests
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

        [TestMethod]
        public void ISF_R1_C21_B1_C8_01_CanonicalGlyphSizeMatchesAcceptedDetailedFormulaAt1080Scale100()
        {
            var expected = Math.Max(12f, Math.Min(32f * 0.58f, 24f * 0.78f));
            var canonical = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1f, 32f, 24f);
            var compact = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, 1f, 32f, 24f, false);
            Assert.AreEqual(expected, canonical, 0.0001f);
            Assert.AreEqual(canonical, compact.BadgeSize, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_02_CanonicalCompactParityHoldsAtMarkerScale075()
        {
            var canonical = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 0.75f, 24f, 18f);
            var compact = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, 0.75f, 24f, 18f, true);
            Assert.AreEqual(canonical, compact.BadgeSize, 0.0001f);
            Assert.AreEqual(Math.Max(9f, Math.Min(24f * 0.58f, 18f * 0.78f)), canonical, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_03_CanonicalCompactParityHoldsAtMarkerScale125()
        {
            var canonical = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1.25f, 40f, 30f);
            var compact = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, 1.25f, 40f, 30f, true);
            Assert.AreEqual(canonical, compact.BadgeSize, 0.0001f);
            Assert.AreEqual(Math.Max(15f, Math.Min(40f * 0.58f, 30f * 0.78f)), canonical, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_04_CanonicalCompactParityHoldsAtSmallerViewport()
        {
            var canonical = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1280f, 720f, 1f, 24f, 18f);
            var compact = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1280f, 720f, 1f, 24f, 18f, false);
            Assert.AreEqual(canonical, compact.BadgeSize, 0.0001f);
            Assert.IsTrue(canonical >= 8f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_05_CountToggleCannotChangeCategoryGlyphSize()
        {
            foreach (var scale in new[] { 0.75f, 1f, 1.25f })
            {
                var on = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, scale, 32f * scale, 24f * scale, true);
                var off = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, scale, 32f * scale, 24f * scale, false);
                Assert.AreEqual(off.BadgeSize, on.BadgeSize, 0.0001f, "scale=" + scale);
            }
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_06_EnlargedCompactGeometryKeepsGlyphAndCountFootprintsSeparated()
        {
            var viewports = new[] { new[] { 1280f, 720f }, new[] { 1920f, 1080f } };
            foreach (var viewport in viewports)
            foreach (var scale in new[] { 0.75f, 1f, 1.25f })
            foreach (var showCount in new[] { false, true })
            {
                var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(
                    viewport[0], viewport[1], scale, 32f * scale, 24f * scale, showCount);
                Assert.IsTrue(geometry.CellWidth >= geometry.BadgeSize);
                Assert.IsTrue(geometry.HorizontalStride > geometry.CellWidth);
                Assert.IsTrue(geometry.VerticalStride >= geometry.BadgeSize);
                if (showCount)
                {
                    Assert.IsTrue(geometry.CountGap > 0f);
                    Assert.IsTrue(geometry.CountWidth > 0f);
                }
            }
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_07_DetailedAndCompactSourceConsumeOneCanonicalGlyphSizePolicy()
        {
            var core = ReadSource("src/ItemShareFix.Core/MarkerCategorySummaryPolicy.cs");
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(core, "public static float BuildCategoryGlyphSize(");
            StringAssert.Contains(core, "var badgeSize = BuildCategoryGlyphSize(width, height, boundedScale, safeIndicator, safeFont);");
            StringAssert.Contains(renderer, "BuildCategoryGlyphSize(Screen.width, Screen.height, markerScale, footprint.IndicatorSize, fontSize)");
            StringAssert.Contains(renderer, "BuildCategoryGlyphSize(Screen.width, Screen.height, _presentationSettings.Scale, footprint.IndicatorSize, fontSize)");
            Assert.AreEqual(2, renderer.Split(new[] { "BuildCategoryGlyphSize(" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_08_R38DistinctCategoryCardinalityAndOneGlyphPerLifetimeSubsetRemainIntact()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)\n            => 1;");
            StringAssert.Contains(renderer, "var categorySlots = MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount);");
            Assert.IsFalse(renderer.Contains("BuildCompactLayout(spec.Count)", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("BuildCompactLayout(group.Count)", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_09_SameCategoryDiamondClockGroupsRetainPositiveSeparation()
        {
            var glyph = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1f, 32f, 24f);
            var gap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(1920f, 1080f, 1f, 32f, 24f);
            var centered = new MarkerCompactLayoutSlot(0, 0, 1, 0f, 0f);
            var left = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(centered, 0, 2, glyph, gap);
            var right = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(centered, 1, 2, glyph, gap);
            Assert.IsTrue(gap > 0f);
            Assert.IsTrue(right - left >= glyph + gap - 0.0001f);

            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "BuildCompactLifetimeGroupGap(");
            StringAssert.Contains(renderer, "BuildCompactLifetimeGroupOffsetX(");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C8_10_NoCompactOnlyGlyphScaleSettingIsIntroduced()
        {
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            var rto = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            Assert.IsFalse(config.Contains("CompactGlyphScale", StringComparison.Ordinal));
            Assert.IsFalse(rto.Contains("CompactGlyphScale", StringComparison.Ordinal));
            StringAssert.Contains(config, "MarkerScale");
        }
    }
}
