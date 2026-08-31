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
    public sealed class C21B1TemporaryMarkerPresentationTests
    {
        private static MarkerWorldMember Member(
            long key,
            float x,
            MarkerClassKind kind,
            string identity,
            string name,
            MarkerLifetimeKind lifetime,
            PersonalMarkerKind markerKind = PersonalMarkerKind.OrdinaryPickup)
            => new MarkerWorldMember(key, markerKind, new MarkerWorldPoint(x, 0f, 0f), identity, name, kind, lifetime);

        private static MarkerSemanticCluster Cluster(params MarkerWorldMember[] members)
            => new MarkerWorldClusterTracker().Update(members, 0d).Clusters.Single();

        private static MarkerPresentationSettings Settings(
            MarkerPresentationMode mode,
            bool showDistance = false,
            bool compactShowCount = true,
            int detailRows = 5)
            => new MarkerPresentationSettings(
                mode,
                showDistance,
                1f,
                detailRows,
                showCategoryDiamond: true,
                showTierComposition: true,
                compactShowCount: compactShowCount,
                compactMixedStyle: MarkerCompactMixedStyle.CategoryDiamondPyramid,
                categorySortOrder: MarkerCategorySortOrder.HighToLow);

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

        [TestMethod]
        public void ISF_R1_C21_B1_01_TemporaryIsNotMarkerClassKind()
            => CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerClassKind)), "Temporary");

        [TestMethod]
        public void ISF_R1_C21_B1_02_TemporaryIsNotMarkerSemanticCategory()
            => CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerSemanticCategory)), "Temporary");

        [TestMethod]
        public void ISF_R1_C21_B1_03_LifetimeIsOrthogonalToCategory()
        {
            var member = Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, member.Category);
            Assert.AreEqual(MarkerLifetimeKind.Temporary, member.Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_04_OldMarkerWorldMemberCtorDefaultsPermanent()
        {
            var member = new MarkerWorldMember(1, PersonalMarkerKind.OrdinaryPickup, new MarkerWorldPoint(0f, 0f, 0f), "crowbar", "Crowbar", MarkerClassKind.Tier1);
            Assert.AreEqual(MarkerLifetimeKind.Permanent, member.Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_05_TemporaryTier1RemainsTier1()
            => Assert.AreEqual(MarkerSemanticCategory.Tier1, Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary).Category);

        [TestMethod]
        public void ISF_R1_C21_B1_06_TemporaryTier2RemainsTier2()
            => Assert.AreEqual(MarkerSemanticCategory.Tier2, Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary).Category);

        [TestMethod]
        public void ISF_R1_C21_B1_07_TemporaryTier3RemainsTier3()
            => Assert.AreEqual(MarkerSemanticCategory.Tier3, Member(1, 0f, MarkerClassKind.Tier3, "a", "A", MarkerLifetimeKind.Temporary).Category);

        [TestMethod]
        public void ISF_R1_C21_B1_08_PhysicalStableMembershipIgnoresLifetime()
        {
            var permanent = Cluster(Member(7, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Permanent));
            var temporary = Cluster(Member(7, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary));
            Assert.AreEqual(permanent.StableKey, temporary.StableKey);
            Assert.AreEqual(permanent.MemberFingerprint, temporary.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_09_SemanticRefreshObservesLifetimeChange()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = tracker.Update(new[] { Member(7, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Permanent) }, 0d);
            var second = tracker.Update(new[] { Member(7, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary) }, 0.1d);
            Assert.AreEqual(first.Clusters.Single().MemberFingerprint, second.Clusters.Single().MemberFingerprint);
            Assert.IsTrue(second.LifecycleEvents.Any(x => x.Kind == MarkerSemanticLifecycleKind.CompositionChanged));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_10_PermanentAndTemporarySameIdentitySplitAggregates()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary));
            Assert.AreEqual(2, cluster.ItemRows.Count);
            CollectionAssert.AreEquivalent(new[] { MarkerLifetimeKind.Permanent, MarkerLifetimeKind.Temporary }, cluster.ItemRows.Select(x => x.Lifetime).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C21_B1_11_TwoTemporarySameIdentityAggregateTogether()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary),
                Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary));
            Assert.AreEqual(1, cluster.ItemRows.Count);
            Assert.AreEqual(2, cluster.ItemRows[0].Count);
            Assert.AreEqual(MarkerLifetimeKind.Temporary, cluster.ItemRows[0].Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_12_TwoPermanentSameIdentityAggregateTogether()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent));
            Assert.AreEqual(1, cluster.ItemRows.Count);
            Assert.AreEqual(2, cluster.ItemRows[0].Count);
            Assert.AreEqual(MarkerLifetimeKind.Permanent, cluster.ItemRows[0].Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_13_DenseSummaryPreservesPermanentTemporarySplit()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[]
            {
                Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                Member(2, 8f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary),
            }, 0d).Clusters;
            Assert.AreEqual(2, physical.Count);
            var dense = new MarkerDenseAreaSummaryTracker().Update(physical, 0d).Nodes.Single().PresentationCluster;
            Assert.AreEqual(2, dense.ItemRows.Count);
            Assert.AreEqual(1, dense.TemporaryPhysicalMemberCount);
            Assert.AreEqual(MarkerLifetimeKind.Mixed, dense.LifetimeSummary);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_14_CategoryCompositionCountUnaffectedByLifetimeSplit()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary));
            Assert.AreEqual(1, cluster.Composition.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, cluster.Composition[0].Category);
            Assert.AreEqual(2, cluster.Composition[0].Count);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_15_DetailedTemporaryRowHasEnglishPrefix()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual("Crowbar ×1", plan.Text);
            Assert.AreEqual("Crowbar", plan.DetailedItemRows[0].DisplayLabel);
            Assert.IsTrue(plan.DetailedItemRows[0].LifetimeIndicator.Visible);
            Assert.AreEqual(string.Empty, plan.DetailedItemRows[0].LifetimeIndicator.CountText);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_16_DetailedTemporaryRowHasRussianPrefix()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Лом", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, true);
            Assert.AreEqual("Лом ×1", plan.Text);
            Assert.IsTrue(plan.DetailedItemRows[0].LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_17_PermanentDetailedRowRemainsUnchanged()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual("Crowbar ×1", plan.Text);
            Assert.AreEqual("Crowbar", plan.DetailedItemRows[0].DisplayLabel);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_18_TemporaryDetailedRowRetainsOriginalCategory()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "atg", "ATG", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, plan.DetailedItemRows[0].Category);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_19_RendererUsesOriginalItemSemanticKeyNotSyntheticTempIdentity()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "string.Equals(input.ItemSemanticKey, row.ItemIdentity, StringComparison.Ordinal)");
            StringAssert.Contains(source, "input.Lifetime == row.Lifetime");
            Assert.IsFalse(source.Contains("TEMP:" + " + row.ItemIdentity"));
        }

                [TestMethod]
        public void ISF_R1_C21_B1_20_LongNameBoundingUsesLifetimePrefixedDisplayLabel()
        {
                var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
                var policy = ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs");
                StringAssert.Contains(renderer, "BuildBoundedDetailedItemLine(label, row.DisplayLabel, row.Count, detailedItemWidthLimit)");
                StringAssert.Contains(renderer, "HasDetailedItemLifetimeIndicator(plan)");
                StringAssert.Contains(policy, "MarkerLifetimePolicy.BuildDetailedItemDisplayName(");
                StringAssert.Contains(policy, "MarkerTextLocalization.FallbackPickup(language)");
                StringAssert.Contains(policy, "language == MarkerLanguage.Russian");
                Assert.AreEqual("A very long item name", MarkerLifetimePolicy.BuildDetailedItemDisplayName("A very long item name", MarkerLifetimeKind.Temporary, false));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_21_DetailedSameItemPermanentTemporaryYieldsTwoVisibleRows()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed, detailRows: 5), 0, false, false);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            Assert.AreEqual("Crowbar ×1\nCrowbar ×1", plan.Text);
            Assert.AreEqual(MarkerLifetimeKind.Temporary, plan.DetailedItemRows[0].Lifetime);
            Assert.IsTrue(plan.DetailedItemRows[0].LifetimeIndicator.Visible);
            Assert.AreEqual(MarkerLifetimeKind.Permanent, plan.DetailedItemRows[1].Lifetime);
            Assert.IsFalse(plan.DetailedItemRows[1].LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_22_CompactHasNoTemporaryCategoryEntry()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries[0].Category);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_23_CompactCategoryTopologyOneThroughElevenUnchanged()
        {
            var expected = new[] { "1", "1,1", "1,2", "1,2,1", "1,3,1", "1,2,2,1", "1,2,3,1", "1,2,2,2,1", "1,2,3,2,1", "1,2,3,3,1", "1,3,3,3,1" };
            for (var n = 1; n <= 11; n++)
            {
                var actual = string.Join(",", MarkerCategorySummaryPolicy.BuildCompactLayout(n).GroupBy(x => x.Row).Select(x => x.Count()));
                Assert.AreEqual(expected[n - 1], actual, "n=" + n);
            }
        }

        [TestMethod]
        public void ISF_R1_C21_B1_24_CompactLifetimeLineAppearsWhenTemporaryPresent()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries[0].GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_25_CompactCountsOnMayShowTemporaryCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "b", "B", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 0, false, false);
            Assert.IsTrue(plan.RenderCategorySubcounts);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(2, plan.CategoryEntries[0].Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries[0].GlyphKind);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_26_CompactCountsOffKeepsLifetimeWordOnly()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "b", "B", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: false), 0, false, false);
            Assert.IsFalse(plan.RenderCategorySubcounts);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(string.Empty, plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_27_DistanceRemainsBelowLifetimeLine()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 37, false, false);
            Assert.AreEqual("37 m", plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "showLifetimeIndicator: false");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_28_CompactRendererAccountsForBoundedLifetimeTextBand()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)");
            StringAssert.Contains(source, "=> 1;");
            StringAssert.Contains(source, "MarkerCategorySummaryPolicy.BuildCompactLayout(CompactDisplayedGlyphCount(group))");
            StringAssert.Contains(source, "MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount)");
            StringAssert.Contains(source, "\"×\" + spec.Count.ToString");
            Assert.IsFalse(source.Contains("CompactDisplayedGlyphCount(group.Count)", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("CompactDisplayedGlyphCount(spec.Count)", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ExtendFootprintForCompactLifetimeIndicator", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_29_OrdinaryExactSourceIsPickupUniquePickupIsTempItem()
            => StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "MarkerLifetimePolicy.FromTemporaryFlag(pickup.pickup.isTempItem)");

        [TestMethod]
        public void ISF_R1_C21_B1_30_ShareTemporaryItemsDoesNotGateLifetimeRecognition()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            const string recognitionText = "var lifetime = MarkerLifetimePolicy.FromTemporaryFlag(pickup.pickup.isTempItem);";
            const string eligibilityText = "MarkerLifetimePolicy.IsMarkerEligible(lifetime, _config.ShareTemporaryItems.Value)";
            var recognition = source.IndexOf(recognitionText, StringComparison.Ordinal);
            var eligibility = source.IndexOf(eligibilityText, recognition, StringComparison.Ordinal);
            var commandRecognition = source.IndexOf("_upstream.TryGetCommandChoiceLifetime(", StringComparison.Ordinal);
            var commandEligibility = source.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(commandLifetime, _config.ShareTemporaryItems.Value)", commandRecognition, StringComparison.Ordinal);

            Assert.IsTrue(recognition >= 0);
            Assert.IsTrue(eligibility > recognition);
            Assert.IsTrue(commandRecognition >= 0);
            Assert.IsTrue(commandEligibility > commandRecognition);
            Assert.AreEqual(MarkerLifetimeKind.Temporary, MarkerLifetimePolicy.FromTemporaryFlag(true));
            Assert.AreEqual(MarkerLifetimeKind.Permanent, MarkerLifetimePolicy.FromTemporaryFlag(false));
            Assert.IsFalse(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Temporary, false));
            Assert.AreEqual(MarkerLifetimeKind.Temporary, MarkerLifetimePolicy.FromTemporaryFlag(true));
            StringAssert.Contains(upstream, "if (exactNestedPickup.isTempItem) sawTemporary = true;");
            StringAssert.Contains(upstream, "lifetime = MarkerLifetimePolicy.FromExactOptionKinds(sawTemporary, sawPermanent);");
            Assert.IsFalse(upstream.Contains("ShareTemporaryItems", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ShareTemporaryItems.Value ? MarkerLifetimePolicy.FromTemporaryFlag", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ShareTemporaryItems.Value ? MarkerLifetimeKind", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_31_PersonalMarkersEnabledOnlyGatesPresentationTracking()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(source, "if (_config.PersonalMarkersEnabled.Value");
            StringAssert.Contains(source, "MarkerLifetimePolicy.FromTemporaryFlag(pickup.pickup.isTempItem)");
            Assert.IsFalse(source.Contains("PersonalMarkersEnabled.Value ? MarkerLifetime"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_32_CommandAllExactNestedTemporaryMapsTemporary()
            => Assert.AreEqual(MarkerLifetimeKind.Temporary, MarkerLifetimePolicy.FromExactOptionKinds(true, false));

        [TestMethod]
        public void ISF_R1_C21_B1_33_CommandAllExactNestedPermanentMapsPermanent()
            => Assert.AreEqual(MarkerLifetimeKind.Permanent, MarkerLifetimePolicy.FromExactOptionKinds(false, true));

        [TestMethod]
        public void ISF_R1_C21_B1_34_CommandExactNestedMixedMapsMixed()
            => Assert.AreEqual(MarkerLifetimeKind.Mixed, MarkerLifetimePolicy.FromExactOptionKinds(true, true));

        [TestMethod]
        public void ISF_R1_C21_B1_35_CommandDirectPickupIndexFallbackWithoutNestedPickupIsUnknown()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            StringAssert.Contains(source, "pickup is not UniquePickup exactNestedPickup");
            StringAssert.Contains(source, "unresolvedAvailableOptionCount++");
            StringAssert.Contains(source, "lifetime = MarkerLifetimeKind.Unknown;");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_36_CommandUnknownDoesNotAssertTemporary()
        {
            Assert.AreEqual(string.Empty, MarkerLifetimePolicy.BuildSummaryLine(MarkerLifetimeKind.Unknown, 0, true, false));
            Assert.AreEqual(string.Empty, MarkerLifetimePolicy.BuildSummaryLine(MarkerLifetimeKind.Unknown, 0, true, true));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_37_NoDecayValueThresholdUsedForMarkerLifetime()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs")
                + ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs")
                + ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            Assert.IsFalse(source.Contains("decayValue"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_38_NoItemIndexNamePrefabTierHeuristicUsedForLifetime()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs") + ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            Assert.IsFalse(source.Contains("name.Contains"));
            Assert.IsFalse(source.Contains("prefab"));
            Assert.IsFalse(source.Contains("ItemTier"));
            Assert.IsFalse(source.Contains("pickupIndex.ToString"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_39_NoCountdownOrDurationOutputIntroduced()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs") + ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs");
            Assert.IsFalse(source.Contains("80 s"));
            Assert.IsFalse(source.Contains("countdown", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(source.Contains("duration", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_40_NoNewNetworkProtocolIntroduced()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs")
                + ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs")
                + ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs")
                + ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            Assert.IsFalse(source.Contains("NetworkMessage"));
            Assert.IsFalse(source.Contains("MessageBase"));
            Assert.IsFalse(source.Contains("RegisterHandler"));
            Assert.IsFalse(source.Contains("NetworkWriter"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_41_A1RuntimeGameplayFilesRemainByteIdentical()
        {
            Assert.AreEqual("AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506", SourceSha256("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            Assert.AreEqual("3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23", SourceSha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual("E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145", SourceSha256("src/ItemShareFix.Core/TemporarySharingPolicy.cs"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_42_A1ShareTemporaryConfigBehaviorRemainsByteIdentical()
        {
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", SourceSha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            StringAssert.Contains(config, "ShareTemporaryItems = config.Bind(\"General\", \"ShareTemporaryItems\", false");
            StringAssert.Contains(config, "BindPresentationInvalidation(ShareTemporaryItems)");
            Assert.AreEqual("269649CB98EF42783EAA90E236AEEAACE1FD1F2FA16BF33AEF8C2D536ACC5DB2", SourceSha256("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs"));
            Assert.AreEqual("EBB72984DD1888AACA01712EE98FCE639A656F0E7AAF4074A06370A14434E043", SourceSha256("src/ItemShareFix.Plugin/MarkerRiskOfOptionsLocalization.cs"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_43_LocalVisibilityRepairSeamStillUsesAcceptedPolicy()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(source, "PersonalPickupVisibilityRepairEnabled.Value");
            StringAssert.Contains(source, "LocalPickupSuppressionPolicy.ShouldSuppressInteractor(");
            StringAssert.Contains(source, "NormalizeUpstreamForGate(pickup);");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_44_PermanentDetailedC20SnapshotRemainsUnchanged()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual("Crowbar ×1", plan.Text);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual(MarkerLifetimeKind.Permanent, plan.DetailedItemRows[0].Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_45_PermanentCompactC20SnapshotRemainsUnchanged()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries[0].Category);
            Assert.AreEqual(1, plan.CategoryEntries[0].Count);
        }


        [TestMethod]
        public void ISF_R1_C21_B1_C3_01_TemporaryOrdinaryRowRetainsPermanentTextLabel()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual("Crowbar ×1", plan.Text);
            Assert.AreEqual("Crowbar", plan.DetailedItemRows[0].DisplayLabel);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_02_TemporaryOrdinaryRowCarriesStructuredIndicatorMetadata()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "atg", "ATG", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.DetailedItemRows.Single().GlyphKind);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, plan.DetailedItemRows.Single().Category);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_03_PermanentOrdinaryRowHasNoTimerIndicator()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "atg", "ATG", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual(MarkerPresentationGlyphKind.Diamond, plan.DetailedItemRows.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_04_SameIdentityPermanentTemporaryRemainsTwoRowsWithOnlyTemporaryIcon()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEquivalent(
                new[] { MarkerPresentationGlyphKind.Diamond, MarkerPresentationGlyphKind.Clock },
                plan.DetailedItemRows.Select(x => x.GlyphKind).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_05_CompactVisibleTextContainsNoLifetimeWordInEnglishOrRussian()
        {
            var cluster = Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary));
            var english = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact), 0, false, false).Text;
            var russian = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact), 0, false, true).Text;
            Assert.IsFalse(english.Contains("Temporary", StringComparison.Ordinal));
            Assert.IsFalse(russian.Contains("Временный", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_06_CompactTimerIndicatorAppearsForTemporary()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_07_CompactTimerCountFollowsCompactCountSetting()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary),
                Member(2, 0.2f, MarkerClassKind.Tier1, "b", "B", MarkerLifetimeKind.Temporary));
            var on = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact, compactShowCount: true), 0, false, false);
            var off = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact, compactShowCount: false), 0, false, false);
            Assert.IsTrue(on.RenderCategorySubcounts);
            Assert.IsFalse(off.RenderCategorySubcounts);
            Assert.AreEqual(2, on.CategoryEntries.Single().Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, off.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_08_MixedLifetimeCommunicatesExactTemporarySubsetCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "b", "B", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 0, false, false);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            Assert.AreEqual(1, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Clock).Count);
            Assert.AreEqual(1, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Diamond).Count);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_09_IntrinsicallyMixedMemberDoesNotInventTemporarySubsetCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Unknown, "COMMAND:1", "Choice", MarkerLifetimeKind.Mixed, PersonalMarkerKind.CommandPicker)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 0, false, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Diamond, plan.CategoryEntries[0].GlyphKind);
            Assert.AreNotEqual(MarkerLifetimeKind.Temporary, plan.CategoryEntries[0].Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_10_NoTemporaryCategoryOrDiamondIsIntroduced()
        {
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerSemanticCategory)), "Temporary");
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerClassKind)), "Temporary");
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries.Single().Category);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_11_DetailedCommandCategoryRowCarriesMixedSubsetIndicator()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier1, "COMMAND:1", "Choice", MarkerLifetimeKind.Temporary, PersonalMarkerKind.CommandPicker),
                    Member(2, 0.2f, MarkerClassKind.Tier1, "COMMAND:2", "Choice", MarkerLifetimeKind.Permanent, PersonalMarkerKind.CommandPicker)),
                Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.IsTrue(plan.ShowDetailedCategoryRowDiamonds);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            CollectionAssert.AreEquivalent(
                new[] { MarkerPresentationGlyphKind.Diamond, MarkerPresentationGlyphKind.Clock },
                plan.CategoryEntries.Select(x => x.GlyphKind).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_12_LocalMaskableGraphicClockFallbackIsDeterministic()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "class MarkerLifetimeIndicatorGraphic : MaskableGraphic");
            StringAssert.Contains(source, "AssetSourceToken = \"local-maskablegraphic-clock\"");
            StringAssert.Contains(source, "RingSegments = 20");
            Assert.IsFalse(source.Contains("Resources.Load", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Addressables", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_13_NoUnicodeClockGlyphIsUsed()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs")
                + ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs");
            foreach (var glyph in new[] { "🕐", "🕑", "🕒", "🕓", "🕔", "🕕", "🕖", "🕗", "🕘", "🕙", "🕚", "🕛", "⏱", "⏲" })
                Assert.IsFalse(source.Contains(glyph, StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_14_RendererPlacesClockBetweenDetailedIconOrDiamondAndText()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "row.GlyphKind");
            StringAssert.Contains(source, "ResolveOrdinaryItemRowColor(cluster, row)");
            StringAssert.Contains(source, "ConfigureCategoryGlyph(");
            StringAssert.Contains(source, "spec.GlyphKind");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_15_CompactDistanceBandAndTimerBandAreSeparate()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "showLifetimeIndicator: false");
            StringAssert.Contains(source, "CompactGlyphGroupExtent(");
            Assert.IsFalse(source.Contains("CompactLifetimeBandHeight", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_16_OffscreenDirectionalAggregationPreservesLifetimeCounts()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "var exactSingleClusterLifetime = sector.RepresentedNodeCount == 1;");
            StringAssert.Contains(source, "exactSingleClusterLifetime ? cluster.LifetimeSummary : MarkerLifetimeKind.Unknown");
            StringAssert.Contains(source, "exactSingleClusterLifetime ? cluster.TemporaryPhysicalMemberCount : 0");

            var temporary = MarkerClusterPresentationPolicy.BuildOffscreen(10, 3, true, true, false, MarkerLifetimeKind.Temporary, 3, 0, 0);
            Assert.IsTrue(temporary.LifetimeIndicator.Visible);
            Assert.AreEqual("×3", temporary.LifetimeIndicator.CountText);

            var mixed = MarkerClusterPresentationPolicy.BuildOffscreen(10, 3, true, true, false, MarkerLifetimeKind.Mixed, 2, 0, 0);
            Assert.IsFalse(mixed.LifetimeIndicator.Visible);
            Assert.AreEqual(string.Empty, mixed.LifetimeIndicator.CountText);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_17_RarityAndNativeItemColorPathsRemainIndependentOfLifetimeIcon()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "ResolveCategoryColor(cluster, spec.Category)");
            StringAssert.Contains(renderer, "ResolveOrdinaryItemRowColor(cluster, row)");
            StringAssert.Contains(renderer, "MarkerPresentationGlyphKind.Clock");
            Assert.IsFalse(renderer.Contains("temporaryColor", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_18_C21AGameplayAndShareTemporaryDefaultRemainFrozen()
        {
            Assert.AreEqual("AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506", SourceSha256("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            Assert.AreEqual("3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23", SourceSha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual("E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145", SourceSha256("src/ItemShareFix.Core/TemporarySharingPolicy.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", SourceSha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs"), "ShareTemporaryItems = config.Bind(\"General\", \"ShareTemporaryItems\", false");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C3_19_NoGroundExpirationOrGameplayTimerImplementationAdded()
        {
            var presentationOnly = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs")
                + ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs")
                + ReadSource("src/ItemShareFix.Core/MarkerDirectionalAggregationPolicy.cs")
                + ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(presentationOnly.Contains("Destroy(pickup", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(presentationOnly.Contains("decayValue", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(presentationOnly.Contains("ground expiration", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(presentationOnly.Contains("delayed destroy", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_01_AllTemporaryCompactSuppressesRedundantClockCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Temporary),
                    Member(3, 0.4f, MarkerClassKind.Tier2, "c", "C", MarkerLifetimeKind.Temporary),
                    Member(4, 0.6f, MarkerClassKind.Tier2, "d", "D", MarkerLifetimeKind.Temporary),
                    Member(5, 0.8f, MarkerClassKind.Tier2, "e", "E", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 24, false, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(5, plan.CategoryEntries[0].Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries[0].GlyphKind);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_02_PartialTemporarySubsetCountIsPreservedWhenCountsOn()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Temporary),
                    Member(3, 0.4f, MarkerClassKind.Tier2, "c", "C", MarkerLifetimeKind.Permanent),
                    Member(4, 0.6f, MarkerClassKind.Tier2, "d", "D", MarkerLifetimeKind.Permanent),
                    Member(5, 0.8f, MarkerClassKind.Tier2, "e", "E", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 24, false, false);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            Assert.AreEqual(2, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Clock).Count);
            Assert.AreEqual(3, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Diamond).Count);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_03_PartialTemporarySubsetCountIsHiddenWhenCountsOff()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(
                    Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary),
                    Member(2, 0.2f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: false), 24, false, false);
            Assert.IsFalse(plan.RenderCategorySubcounts);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            Assert.IsTrue(plan.CategoryEntries.Any(x => x.GlyphKind == MarkerPresentationGlyphKind.Clock));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_04_UnknownTemporarySubsetNeverInventsClockCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Unknown)),
                Settings(MarkerPresentationMode.Compact, compactShowCount: true), 24, false, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerPresentationGlyphKind.Diamond, plan.CategoryEntries[0].GlyphKind);
            Assert.AreNotEqual(MarkerLifetimeKind.Temporary, plan.CategoryEntries[0].Lifetime);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_05_CompactCategoryDistanceUsesNeutralHudColorPath()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "plan.DetailedItemRows.Count > 0 || plan.ShowDetailedCategoryRowDiamonds || plan.ShowCompactCategoryDiamonds");
            StringAssert.Contains(source, "SanitizeColor(_visualSettings.NeutralColor)");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_06_Tier2CompactCannotRouteDistanceThroughGreenMainColor()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, false);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, plan.CategoryEntries.Single().Category);
            Assert.AreEqual("24 m", plan.Text);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "|| plan.ShowCompactCategoryDiamonds)\n                ? SanitizeColor(_visualSettings.NeutralColor)");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_07_DetailedCategoryDistanceNeutralPathRemainsPresent()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "plan.ShowDetailedCategoryRowDiamonds || plan.ShowCompactCategoryDiamonds");
            StringAssert.Contains(source, "UpdateDetailedCategoryRowDiamonds(view, cluster, plan, footprint);");
            StringAssert.Contains(source, "ConfigureCategoryGlyph(");
            StringAssert.Contains(source, "ResolveCategoryColor(cluster, spec.Category)");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_08_CompactCategoryDiamondKeepsCategoryColor()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "ResolveCategoryColor(cluster, spec.Category)");
            StringAssert.Contains(source, "var showClock = glyphKind == MarkerPresentationGlyphKind.Clock;");
            Assert.IsFalse(source.Contains("temporaryColor", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_09_CompactCategoryCountStillFollowsCompactShowCountOnly()
        {
            var cluster = Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary));
            var on = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact, compactShowCount: true), 0, false, false);
            var off = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact, compactShowCount: false), 0, false, false);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, on.CountRenderSource);
            Assert.AreEqual(MarkerCountRenderSource.None, off.CountRenderSource);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_10_TimerAndDistanceUseSameMetadataRowYExpression()
        {
            var cluster = Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary));
            var plan = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, false);
            Assert.AreEqual("24 m", plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);

            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "showLifetimeIndicator: false");
            StringAssert.Contains(source, "plan.ShowCompactCategoryDiamonds");
            StringAssert.Contains(source, "HideSummaryLifetimeIndicator(view);");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_11_TimerCountDistanceGroupIsCenteredAsOneUnit()
        {
            var cluster = Cluster(
                Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary),
                Member(2, 0.1f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Temporary),
                Member(3, 0.2f, MarkerClassKind.Tier2, "c", "C", MarkerLifetimeKind.Temporary));
            var plan = MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact), 0, false, false);
            var group = plan.CategoryEntries.Single();
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, group.GlyphKind);
            Assert.AreEqual(3, group.Count);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);

            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)");
            StringAssert.Contains(source, "=> 1;");
            StringAssert.Contains(source, "MarkerCategorySummaryPolicy.BuildCompactLayout(CompactDisplayedGlyphCount(spec))");
            StringAssert.Contains(source, "\"×\" + spec.Count.ToString");
            Assert.IsFalse(source.Contains("CompactDisplayedGlyphCount(group.Count)", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_12_NoSeparateCompactLifetimeVerticalBandRemains()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(source.Contains("CompactLifetimeBandHeight", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ExtendFootprintForCompactLifetimeIndicator", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("textBandHeight + lifetimeBandHeight", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_13_CompactFootprintReservesOnlyOneMetadataRow()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "EnsureCompactMetadataRowFootprint(");
            StringAssert.Contains(source, "var height = Math.Max(footprint.Height, rowHeight + 8f * scale);");
            Assert.IsFalse(source.Contains("footprint.Height + bandHeight", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_14_TimerOnlyMetadataRowWorksWhenDistanceDisabled()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, showDistance: false), 24, false, false);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_15_PermanentCompactUsesDistanceOnlyMetadataRow()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, false);
            Assert.AreEqual("24 m", plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_16_CompactDoesNotIntroduceCategoryNameText()
        {
            var english = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, false).Text;
            var russian = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, true).Text;
            Assert.AreEqual("24 m", english);
            Assert.AreEqual("24 м", russian);
            Assert.IsFalse(russian.Contains("Зелёный", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_17_NoTemporaryWordIsReintroducedIntoCompact()
        {
            var plan = MarkerClusterPresentationPolicy.Build(
                Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary)),
                Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, true);
            Assert.IsFalse(plan.Text.Contains("Temporary", StringComparison.Ordinal));
            Assert.IsFalse(plan.Text.Contains("Временный", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_18_DetailedLayoutSeamsRemainUnchangedByCompactMetadataHelpers()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "UpdateDetailedItemLifetimeIndicators(view, cluster, plan, footprint, markerScale);");
            StringAssert.Contains(source, "row.GlyphKind");
            StringAssert.Contains(source, "firstLineY - i * lineHeight");
            StringAssert.Contains(source, "UpdateDetailedCategoryRowDiamonds(view, cluster, plan, footprint);");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_19_C21AGameplayAndConfigRemainExact()
        {
            Assert.AreEqual("AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506", SourceSha256("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            Assert.AreEqual("3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23", SourceSha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual("E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145", SourceSha256("src/ItemShareFix.Core/TemporarySharingPolicy.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", SourceSha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_20_NoGroundExpirationCountdownOrNetworkProtocolIsAdded()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs")
                + ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            foreach (var token in new[] { "Destroy(pickup", "decayValue", "ground expiration", "delayed destroy", "NetworkMessage", "MessageBase", "RegisterHandler", "NetworkWriter" })
                Assert.IsFalse(source.Contains(token, StringComparison.OrdinalIgnoreCase), token);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C4_21_LifetimePolicyRemainsByteIdenticalToR32Parent()
            => Assert.AreEqual("565B016B003AC02EA911C6712D223AF56C7ECEC7FA1904A23063E11F08C0103A", SourceSha256("src/ItemShareFix.Core/MarkerLifetimePolicy.cs"));

        [TestMethod]
        public void ISF_R1_C21_B1_C4_22_CategoryPyramidTopologyStillMatchesAcceptedOneThroughEleven()
        {
            var expected = new[] { "1", "1,1", "1,2", "1,2,1", "1,3,1", "1,2,2,1", "1,2,3,1", "1,2,2,2,1", "1,2,3,2,1", "1,2,3,3,1", "1,3,3,3,1" };
            for (var n = 1; n <= 11; n++)
                Assert.AreEqual(expected[n - 1], string.Join(",", MarkerCategorySummaryPolicy.BuildCompactLayout(n).GroupBy(x => x.Row).Select(x => x.Count())));
        }


        [TestMethod]
        public void ISF_R1_C21_B1_C5_01_MixedTier2FiveAndFiveProducesSeparateGroups()
        {
            var members = Enumerable.Range(0, 5).Select(i => Member(i + 1, i * 0.1f, MarkerClassKind.Tier2, "p" + i, "P", MarkerLifetimeKind.Permanent))
                .Concat(Enumerable.Range(0, 5).Select(i => Member(i + 21, 0.6f + i * 0.1f, MarkerClassKind.Tier2, "t" + i, "T", MarkerLifetimeKind.Temporary))).ToArray();
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(members), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            Assert.AreEqual(5, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Diamond).Count);
            Assert.AreEqual(5, plan.CategoryEntries.Single(x => x.GlyphKind == MarkerPresentationGlyphKind.Clock).Count);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_02_PermanentGroupGlyphIsDiamond()
            => Assert.AreEqual(MarkerPresentationGlyphKind.Diamond, MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent)), Settings(MarkerPresentationMode.Compact), 0, false, false).CategoryEntries.Single().GlyphKind);

        [TestMethod]
        public void ISF_R1_C21_B1_C5_03_TemporaryGroupGlyphIsClock()
            => Assert.AreEqual(MarkerPresentationGlyphKind.Clock, MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false).CategoryEntries.Single().GlyphKind);

        [TestMethod]
        public void ISF_R1_C21_B1_C5_04_TemporaryTier2ClockRetainsGreenCategory()
        {
            var group = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false).CategoryEntries.Single();
            Assert.AreEqual(MarkerSemanticCategory.Tier2, group.Category);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, group.GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_05_TemporaryTier1UsesWhiteClockCategoryPath()
            => Assert.AreEqual(MarkerSemanticCategory.Tier1, MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier1, "a", "A", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false).CategoryEntries.Single().Category);

        [TestMethod]
        public void ISF_R1_C21_B1_C5_06_TemporaryTier3UsesRedClockCategoryPath()
            => Assert.AreEqual(MarkerSemanticCategory.Tier3, MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier3, "a", "A", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false).CategoryEntries.Single().Category);

        [TestMethod]
        public void ISF_R1_C21_B1_C5_07_AllTemporaryCategoryHasNoDiamondGroup()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary), Member(2, 0.1f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.IsFalse(plan.CategoryEntries.Any(x => x.GlyphKind == MarkerPresentationGlyphKind.Diamond));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_08_AllPermanentCategoryHasNoClockGroup()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Permanent), Member(2, 0.1f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Permanent)), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.IsFalse(plan.CategoryEntries.Any(x => x.GlyphKind == MarkerPresentationGlyphKind.Clock));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_09_DetailedSameIdentityHasDiamondAndClockRows()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Permanent), Member(2, 0.1f, MarkerClassKind.Tier1, "crowbar", "Crowbar", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Detailed), 0, false, false);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEquivalent(new[] { MarkerPresentationGlyphKind.Diamond, MarkerPresentationGlyphKind.Clock }, plan.DetailedItemRows.Select(x => x.GlyphKind).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_10_NoVisibleLifetimeWordEnglishOrRussian()
        {
            var cluster = Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "Item", MarkerLifetimeKind.Temporary));
            foreach (var text in new[] { MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact), 0, false, false).Text, MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Compact), 0, false, true).Text, MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Detailed), 0, false, false).Text, MarkerClusterPresentationPolicy.Build(cluster, Settings(MarkerPresentationMode.Detailed), 0, false, true).Text })
            {
                Assert.IsFalse(text.Contains("Temporary", StringComparison.Ordinal));
                Assert.IsFalse(text.Contains("Временный", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_11_CompactTopologyOneThroughElevenRemainsBounded()
        {
            for (var n = 1; n <= 11; n++) Assert.AreEqual(n, MarkerCategorySummaryPolicy.BuildCompactLayout(n).Length);
            Assert.AreEqual(11, MarkerCategorySummaryPolicy.BuildCompactLayout(99).Length);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_12_TemporaryCompactUsesSameTopologyFunctionAsDiamondGroups()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "private static int CompactDisplayedGlyphCount(MarkerCompactCategoryBadge group)");
            StringAssert.Contains(source, "=> 1;");
            StringAssert.Contains(source, "MarkerCategorySummaryPolicy.BuildCompactLayout(CompactDisplayedGlyphCount(spec))");
            StringAssert.Contains(source, "MarkerCategorySummaryPolicy.BuildCompactLayout(categoryCount)");
            Assert.IsFalse(source.Contains("CompactDisplayedGlyphCount(group.Count)", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_13_CountsOffHidesTextButKeepsClockGroup()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary), Member(2, 0.1f, MarkerClassKind.Tier2, "b", "B", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact, compactShowCount: false), 0, false, false);
            Assert.IsFalse(plan.RenderCategorySubcounts);
            Assert.AreEqual(MarkerPresentationGlyphKind.Clock, plan.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_14_DistanceIsNeutralSingularAndHasNoLifetimeIndicator()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "a", "A", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact, showDistance: true), 24, false, false);
            Assert.AreEqual("24 m", plan.Text);
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "? SanitizeColor(_visualSettings.NeutralColor)");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_15_RendererDoesNotPlaceCategoryLifetimeClockInDistanceRow()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "showLifetimeIndicator: false");
            StringAssert.Contains(source, "var spec = plan.CompactBadges[groupIndex];");
            StringAssert.Contains(source, "ConfigureCategoryGlyph(");
            StringAssert.Contains(source, "spec.GlyphKind,");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_16_UnknownLifetimeNeverGetsClockByHeuristic()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier3, "suspicious_temp_name", "Temp", MarkerLifetimeKind.Unknown)), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(MarkerPresentationGlyphKind.Diamond, plan.CategoryEntries.Single().GlyphKind);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_17_TemporaryStillNotClassOrCategoryEnum()
        {
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerClassKind)), "Temporary");
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(MarkerSemanticCategory)), "Temporary");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_18_FrozenC21AFilesRemainExact()
        {
            Assert.AreEqual("AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506", SourceSha256("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            Assert.AreEqual("3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23", SourceSha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual("E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145", SourceSha256("src/ItemShareFix.Core/TemporarySharingPolicy.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", SourceSha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_19_NoGroundExpirationOrCountdownAdded()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs") + ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            foreach (var token in new[] { "Destroy(pickup", "decayValue", "ground expiration", "countdown", "delayed destroy" }) Assert.IsFalse(source.Contains(token, StringComparison.OrdinalIgnoreCase), token);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_20_NoNetworkProtocolAdded()
        {
            var source = ReadSource("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs") + ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            foreach (var token in new[] { "NetworkMessage", "MessageBase", "RegisterHandler", "NetworkWriter" }) Assert.IsFalse(source.Contains(token, StringComparison.Ordinal), token);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_21_ClockColorComesFromCategoryOrNativeRowColor()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "ResolveCategoryColor(cluster, spec.Category)");
            StringAssert.Contains(source, "ResolveOrdinaryItemRowColor(cluster, row)");
            Assert.IsFalse(source.Contains("TemporaryColor", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_22_MixedFiveFivePlusWhiteProducesThreeGroups()
        {
            var members = Enumerable.Range(0, 5).Select(i => Member(i + 1, i * 0.05f, MarkerClassKind.Tier2, "gp" + i, "GP", MarkerLifetimeKind.Permanent))
                .Concat(Enumerable.Range(0, 5).Select(i => Member(i + 21, 0.4f + i * 0.05f, MarkerClassKind.Tier2, "gt" + i, "GT", MarkerLifetimeKind.Temporary)))
                .Concat(new[] { Member(99, 0.9f, MarkerClassKind.Tier1, "white", "White", MarkerLifetimeKind.Permanent) }).ToArray();
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(members), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.AreEqual(5, plan.CategoryEntries.Single(x => x.Category == MarkerSemanticCategory.Tier2 && x.GlyphKind == MarkerPresentationGlyphKind.Diamond).Count);
            Assert.AreEqual(5, plan.CategoryEntries.Single(x => x.Category == MarkerSemanticCategory.Tier2 && x.GlyphKind == MarkerPresentationGlyphKind.Clock).Count);
            Assert.AreEqual(1, plan.CategoryEntries.Single(x => x.Category == MarkerSemanticCategory.Tier1).Count);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_23_OldTotalCategoryPlusTemporarySubsetModelIsGone()
        {
            var plan = MarkerClusterPresentationPolicy.Build(Cluster(Member(1, 0f, MarkerClassKind.Tier2, "p", "P", MarkerLifetimeKind.Permanent), Member(2, 0.1f, MarkerClassKind.Tier2, "t", "T", MarkerLifetimeKind.Temporary)), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.IsFalse(plan.CategoryEntries.Any(x => x.Count == 2));
            Assert.IsFalse(plan.LifetimeIndicator.Visible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_24_GlyphViewSupportsDiamondAndClockWithoutUnicode()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(source, "MarkerIndicatorGraphic Diamond");
            StringAssert.Contains(source, "MarkerLifetimeIndicatorGraphic Clock");
            foreach (var glyph in new[] { "🕐", "🕒", "⏱", "⏲" }) Assert.IsFalse(source.Contains(glyph, StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C5_25_MixedLifetimeAcrossAllOrdinaryCategoriesDoesNotDropSecondGroups()
        {
            var kinds = new[]
            {
                MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3, MarkerClassKind.Boss,
                MarkerClassKind.Lunar, MarkerClassKind.Void, MarkerClassKind.Equipment, MarkerClassKind.LunarEquipment,
                MarkerClassKind.Other, MarkerClassKind.Unknown,
            };
            var members = new List<MarkerWorldMember>();
            long key = 1;
            for (var i = 0; i < kinds.Length; i++)
            {
                members.Add(Member(key++, i * 0.01f, kinds[i], "p" + i, "P" + i, MarkerLifetimeKind.Permanent));
                members.Add(Member(key++, i * 0.01f + 0.001f, kinds[i], "t" + i, "T" + i, MarkerLifetimeKind.Temporary));
            }

            var plan = MarkerClusterPresentationPolicy.Build(Cluster(members.ToArray()), Settings(MarkerPresentationMode.Compact), 0, false, false);
            Assert.AreEqual(kinds.Length * 2, plan.CategoryEntries.Count);
            foreach (var kind in kinds)
            {
                var category = MarkerSemanticCategoryPolicy.From(PersonalMarkerKind.OrdinaryPickup, kind);
                Assert.AreEqual(1, plan.CategoryEntries.Count(x => x.Category == category && x.GlyphKind == MarkerPresentationGlyphKind.Diamond));
                Assert.AreEqual(1, plan.CategoryEntries.Count(x => x.Category == category && x.GlyphKind == MarkerPresentationGlyphKind.Clock));
            }
            Assert.AreEqual(MarkerClusterPresentationPolicy.MaxCompactCategoryBadges * 2, MarkerClusterPresentationPolicy.MaxCompactLifetimePresentationGroups);
        }

    }
}
