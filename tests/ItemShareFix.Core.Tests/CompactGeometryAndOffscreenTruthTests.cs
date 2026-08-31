using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class ZZ_C21B1Correction9Revision1GeometryAndOffscreenTruthTests
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

        private static float ExpectedDetailedGap(float scale, float indicator, float font)
        {
            var glyph = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, scale, indicator, font);
            var textLine = font * 1.18f;
            var stride = Math.Max(textLine, glyph + 2f * scale);
            return Math.Max(0f, stride - (glyph + textLine) * 0.5f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_01_CompactDistanceGapMatchesDetailedReferenceAtScale100()
        {
            var actual = MarkerCategorySummaryPolicy.BuildDetailedCategoryDistanceGap(1920f, 1080f, 1f, 32f, 24f);
            Assert.AreEqual(ExpectedDetailedGap(1f, 32f, 24f), actual, 0.0001f);
            Assert.IsTrue(actual > 0f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_02_CompactDistanceGapMatchesDetailedReferenceAtScale075()
        {
            var actual = MarkerCategorySummaryPolicy.BuildDetailedCategoryDistanceGap(1920f, 1080f, 0.75f, 24f, 18f);
            Assert.AreEqual(ExpectedDetailedGap(0.75f, 24f, 18f), actual, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_03_CompactDistanceGapMatchesDetailedReferenceAtScale125()
        {
            var actual = MarkerCategorySummaryPolicy.BuildDetailedCategoryDistanceGap(1920f, 1080f, 1.25f, 40f, 30f);
            Assert.AreEqual(ExpectedDetailedGap(1.25f, 40f, 30f), actual, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_04_RendererAnchorsDistanceGapFromActualVisibleBottomIncludingCounts()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "var maxBottomExtent = geometry.BadgeSize * 0.5f;");
            StringAssert.Contains(source, "CompactGroupBottomExtent(");
            StringAssert.Contains(source, "var bottomPadding = CompactMetadataBottomPadding(_presentationSettings.Scale);");
            StringAssert.Contains(source, "var visibleBottomY = -footprint.Height * 0.5f + bottomPadding + metadataRowHeight + distanceGap;");
            StringAssert.Contains(source, "-footprint.Height * 0.5f + metadataRowHeight * 0.5f + bottomPadding");
            StringAssert.Contains(source, "var groupCenterY = visibleBottomY + maxBottomExtent + outerCenterOffset + categorySlot.YUnits * categoryStrideY;");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_05_DistanceHiddenAddsNoSyntheticGapBand()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "var distanceGap = string.IsNullOrEmpty(plan.Text)\n                ? 0f");
            Assert.IsFalse(source.Contains("CompactLifetimeBandHeight", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_06_ThreeDistinctCategoriesRemainExactCenteredOneOverTwoTopology()
        {
            var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(3);
            Assert.AreEqual(3, slots.Length);
            Assert.AreEqual(0f, slots[0].XUnits, 0.0001f);
            Assert.AreEqual(0.5f, slots[0].YUnits, 0.0001f);
            Assert.AreEqual(-0.5f, slots[1].XUnits, 0.0001f);
            Assert.AreEqual(-0.5f, slots[1].YUnits, 0.0001f);
            Assert.AreEqual(0.5f, slots[2].XUnits, 0.0001f);
            Assert.AreEqual(-0.5f, slots[2].YUnits, 0.0001f);
            Assert.AreEqual(slots[0].XUnits, (slots[1].XUnits + slots[2].XUnits) * 0.5f, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_07_OuterCategoryStrideIsCanonicalAndIndependentOfMixedGroupWidth()
        {
            var stride = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(1920f, 1080f, 1f, 32f, 24f, 3);
            var geometry = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, 1f, 32f, 24f, showCount: false);
            var accepted = geometry.HorizontalStride * MarkerCategorySummaryPolicy.PyramidHorizontalSpacingFactor(3);
            Assert.AreEqual(accepted, stride, 0.0001f, "Three-category 1/2 topology must retain the accepted canonical outer stride.");
            Assert.IsTrue(stride > geometry.BadgeSize);
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "BuildCompactCategoryCenterHorizontalStride(");
            Assert.IsFalse(source.Contains("categoryStrideX = maxCategoryWidth", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("maxCategoryWidth * 0.22f", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_08_SameCategoryDiamondClockOffsetsStayLocalToOneOuterSlot()
        {
            var glyph = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1f, 32f, 24f);
            var gap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(1920f, 1080f, 1f, 32f, 24f);
            var apex = MarkerCategorySummaryPolicy.BuildCompactLayout(3)[0];
            var first = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(apex, 0, 2, glyph, gap);
            var second = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(apex, 1, 2, glyph, gap);
            Assert.AreEqual(0f, (first + second) * 0.5f, 0.0001f);
            Assert.AreEqual(glyph + gap, second - first, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_09_PhysicalCountDoesNotChangeOuterCategoryCardinalityOrStride()
        {
            var oneEach = MarkerCategorySummaryPolicy.BuildCategoryEntries(
                new[] { new MarkerCategoryCount(MarkerSemanticCategory.Tier3, 1), new MarkerCategoryCount(MarkerSemanticCategory.Tier2, 1), new MarkerCategoryCount(MarkerSemanticCategory.Tier1, 1) },
                MarkerCategorySortOrder.HighToLow);
            var manyEach = MarkerCategorySummaryPolicy.BuildCategoryEntries(
                new[] { new MarkerCategoryCount(MarkerSemanticCategory.Tier3, 9), new MarkerCategoryCount(MarkerSemanticCategory.Tier2, 7), new MarkerCategoryCount(MarkerSemanticCategory.Tier1, 5) },
                MarkerCategorySortOrder.HighToLow);
            Assert.AreEqual(3, oneEach.Length);
            Assert.AreEqual(3, manyEach.Length);
            var a = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(1920f, 1080f, 1f, 32f, 24f, oneEach.Length);
            var b = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(1920f, 1080f, 1f, 32f, 24f, manyEach.Length);
            Assert.AreEqual(a, b, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_10_CanonicalThreeCategoryMixedLifetimeCaseHasNoSameRowGlyphOverlap()
        {
            var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(3);
            var glyph = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1f, 32f, 24f);
            var lifetimeGap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(1920f, 1080f, 1f, 32f, 24f);
            var stride = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(1920f, 1080f, 1f, 32f, 24f, 3);
            var greenCenter = slots[1].XUnits * stride;
            var whiteCenter = slots[2].XUnits * stride;
            var whiteDiamond = whiteCenter + MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(slots[2], 0, 2, glyph, lifetimeGap);
            var whiteClock = whiteCenter + MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(slots[2], 1, 2, glyph, lifetimeGap);
            Assert.IsTrue(whiteDiamond - greenCenter >= glyph - 0.0001f);
            Assert.IsTrue(whiteClock - whiteDiamond >= glyph + lifetimeGap - 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_11_CenteredMixedSlotInThreeWideRowCannotCollideWithNeighbors()
        {
            var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(5);
            var row = slots.Where(x => x.RowSize == 3).ToArray();
            Assert.AreEqual(3, row.Length);
            var glyph = MarkerCategorySummaryPolicy.BuildCategoryGlyphSize(1920f, 1080f, 1f, 32f, 24f);
            var lifetimeGap = MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupGap(1920f, 1080f, 1f, 32f, 24f);
            var stride = MarkerCategorySummaryPolicy.BuildCompactCategoryCenterHorizontalStride(1920f, 1080f, 1f, 32f, 24f, 5);
            var accepted = MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f, 1080f, 1f, 32f, 24f, showCount: false).HorizontalStride
                * MarkerCategorySummaryPolicy.PyramidHorizontalSpacingFactor(5);
            Assert.IsTrue(stride >= accepted);
            var leftCenter = row[0].XUnits * stride;
            var middleCenter = row[1].XUnits * stride;
            var rightCenter = row[2].XUnits * stride;
            var middleLeft = middleCenter + MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(row[1], 0, 2, glyph, lifetimeGap);
            var middleRight = middleCenter + MarkerCategorySummaryPolicy.BuildCompactLifetimeGroupOffsetX(row[1], 1, 2, glyph, lifetimeGap);
            Assert.IsTrue(middleLeft - leftCenter >= glyph - 0.0001f);
            Assert.IsTrue(rightCenter - middleRight >= glyph - 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_12_FourThroughElevenCategoryLayoutsRemainCenteredAndBounded()
        {
            for (var n = 4; n <= 11; n++)
            {
                var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(n);
                Assert.AreEqual(n, slots.Length, "n=" + n);
                foreach (var row in slots.GroupBy(x => x.Row))
                    Assert.AreEqual(0f, row.Average(x => x.XUnits), 0.0001f, "row center n=" + n + " row=" + row.Key);
                Assert.IsTrue(slots.All(x => Math.Abs(x.XUnits) <= 1f), "bounded X n=" + n);
            }
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_13_OffscreenPermanentOnlySuppressesTemporaryClock()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(25, 5, true, true, false, MarkerLifetimeKind.Permanent, 0, 0, 0);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(string.Empty, plan.LifetimeIndicator.CountText);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_14_OffscreenTemporaryOnlyAllowsClockAndExactCount()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(25, 5, true, true, false, MarkerLifetimeKind.Temporary, 5, 0, 0);
            Assert.IsTrue(plan.LifetimeIndicator.Visible);
            Assert.IsTrue(plan.LifetimeIndicator.ShowCount);
            Assert.AreEqual("×5", plan.LifetimeIndicator.CountText);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_15_OffscreenMixedSuppressesTemporaryOnlyClockAndCount()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(25, 12, true, true, false, MarkerLifetimeKind.Mixed, 5, 0, 0);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.IsFalse(plan.LifetimeIndicator.ShowCount);
            Assert.AreEqual(string.Empty, plan.LifetimeIndicator.CountText);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_16_OffscreenUnknownSuppressesTemporaryOnlyClock()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(25, 12, true, true, false, MarkerLifetimeKind.Unknown, 0, 0, 12);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_17_MultiNodeDirectionalSectorRemainsLifetimeNeutral()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "var exactSingleClusterLifetime = sector.RepresentedNodeCount == 1;");
            StringAssert.Contains(renderer, "exactSingleClusterLifetime ? cluster.LifetimeSummary : MarkerLifetimeKind.Unknown");
            StringAssert.Contains(renderer, "exactSingleClusterLifetime ? cluster.TemporaryPhysicalMemberCount : 0");
            StringAssert.Contains(renderer, "exactSingleClusterLifetime ? cluster.UnknownLifetimeMemberCount : 1");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C9R1_18_C7CardinalityC8GlyphParityAndDetailedRendererRemainLocked()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)\n            => 1;");
            StringAssert.Contains(renderer, "var categorySlots = MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount);");
            Assert.AreEqual(2, renderer.Split(new[] { "BuildCategoryGlyphSize(" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(renderer, "var lineHeight = Math.Max(fontSize * 1.18f, badgeSize + 2f * scale);");
            StringAssert.Contains(renderer, "firstLineY - i * lineHeight");
        }
    }
}
