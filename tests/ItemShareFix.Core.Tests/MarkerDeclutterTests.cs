using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class MarkerDeclutterTests
    {
        private static MarkerScreenCandidate[] Crowd(int count = 96)
            => Enumerable.Range(1, count).Select(i => new MarkerScreenCandidate(i, (i % 12) / 11f, (i % 7) / 6f, 1)).ToArray();
        private static string Fingerprint(MarkerScreenDeclutterTracker t)
            => string.Join(";", t.Groups.Select(g => g.Cell + ":" + string.Join(",", g.Keys)));
        private static int CellOf(MarkerScreenDeclutterTracker t, long key) => t.Groups.Single(g => g.Keys.Contains(key)).Cell;
        private static string Raw(string path) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));

        [TestMethod]
        [DataRow("surv2", "MAP_SURV2_TITLE")]
        [DataRow("sad1", "MAP_SAD1_TITLE")]
        [DataRow("soul1", "MAP_SOUL1_TITLE")]
        public void TrialSuppressionRequiresExactScenePairAndHonorsLiveToggle(string scene, string token)
        {
            Assert.IsTrue(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene, token));
            Assert.IsFalse(MarkerSceneVisibilityPolicy.ShouldSuppress(false, scene, token));
            Assert.IsFalse(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene + "_custom", token));
            Assert.IsFalse(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene, token + "_custom"));
            Assert.IsFalse(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene.ToUpperInvariant(), token));
            Assert.IsTrue(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene, token));
        }

        [TestMethod]
        [DataRow(null, null)] [DataRow("", "")]
        [DataRow("bazaar", "MAP_BAZAAR_TITLE")]
        [DataRow("artifactworld", "MAP_ARTIFACTWORLD_TITLE")]
        [DataRow("golemplains", "MAP_GOLEMPLAINS_TITLE")]
        [DataRow("surv2", "MAP_SAD1_TITLE")]
        public void UnknownAndOrdinaryScenesKeepMarkers(string? scene, string? token)
            => Assert.IsFalse(MarkerSceneVisibilityPolicy.ShouldSuppress(true, scene, token));

        [TestMethod]
        [DataRow(4)] [DataRow(5)] [DataRow(7)] [DataRow(8)] [DataRow(12)] [DataRow(16)] [DataRow(1)] [DataRow(99)]
        public void ScreenBudgetPreservesEveryPendingIdentityExactlyOnce(int limit)
        {
            var t = new MarkerScreenDeclutterTracker();
            var input = Crowd(); t.Update(input, true, limit);
            Assert.IsTrue(t.Active);
            Assert.IsTrue(t.Groups.Count <= MarkerScreenDeclutterTracker.ClampLimit(limit));
            var represented = t.Groups.SelectMany(g => g.Keys).ToArray();
            Assert.AreEqual(input.Length, represented.Length);
            Assert.AreEqual(input.Length, represented.Distinct().Count());
            CollectionAssert.AreEquivalent(input.Select(c => c.Key).ToArray(), represented);
            var signature = Fingerprint(t);
            t.Update(input, true, limit);
            Assert.IsFalse(t.MembershipChanged);
            Assert.AreEqual(signature, Fingerprint(t));
        }

        [TestMethod]
        public void ScreenGroupingIsIndependentOfInputOrder()
        {
            var a = new MarkerScreenDeclutterTracker(); var b = new MarkerScreenDeclutterTracker();
            var input = Crowd(); a.Update(input, true, 8); b.Update(input.Reverse().ToArray(), true, 8);
            Assert.AreEqual(Fingerprint(a), Fingerprint(b));
        }

        [TestMethod]
        public void CellHysteresisPreventsBorderJitterAndAllowsRealMovement()
        {
            var t = new MarkerScreenDeclutterTracker(); var input = Crowd(20);
            input[0] = new MarkerScreenCandidate(1, 0.249f, 0.1f, 1); t.Update(input, true, 8);
            Assert.AreEqual(0, CellOf(t, 1));
            input[0] = new MarkerScreenCandidate(1, 0.251f, 0.1f, 1); t.Update(input, true, 8);
            Assert.AreEqual(0, CellOf(t, 1));
            input[0] = new MarkerScreenCandidate(1, 0.30f, 0.1f, 1); t.Update(input, true, 8);
            Assert.AreEqual(1, CellOf(t, 1));
        }

        [TestMethod]
        public void LiveDisableLimitChangeRemovalAndClearDoNotRetainOldMembers()
        {
            var t = new MarkerScreenDeclutterTracker(); var input = Crowd();
            t.Update(input, true, 16); t.Update(input, true, 4);
            Assert.IsTrue(t.MembershipChanged); Assert.IsTrue(t.Groups.Count <= 4);
            t.Update(input.Take(30).ToArray(), true, 4);
            CollectionAssert.AreEquivalent(input.Take(30).Select(c => c.Key).ToArray(), t.Groups.SelectMany(g => g.Keys).ToArray());
            t.Update(input, false, 4); Assert.IsFalse(t.Active); Assert.AreEqual(0, t.Groups.Count);
            t.Update(input, true, 4); Assert.IsTrue(t.Active);
            t.Clear(); Assert.IsFalse(t.Active); Assert.AreEqual(0, t.Groups.Count);
            t.Update(Array.Empty<MarkerScreenCandidate>(), true, 8); Assert.IsFalse(t.Active);
        }

        [TestMethod]
        public void SparseSceneIsUnchangedButOneLargePileUsesCollapsedPresentation()
        {
            var t = new MarkerScreenDeclutterTracker();
            t.Update(Crowd(4), true, 8); Assert.IsFalse(t.Active);
            t.Update(new[] { new MarkerScreenCandidate(77, 0.5f, 0.5f, 64) }, true, 8);
            Assert.IsTrue(t.Active); Assert.AreEqual(1, t.Groups.Count);
            Assert.AreEqual(77L, t.Groups[0].Keys.Single());
            t.Update(Crowd(4), true, 8); Assert.IsFalse(t.Active);
        }

        [TestMethod]
        public void PressureHysteresisKeepsGroupsAtLimitAndReleasesBelowExitThreshold()
        {
            var t = new MarkerScreenDeclutterTracker();
            t.Update(Crowd(8), true, 8); Assert.IsFalse(t.Active);
            t.Update(Crowd(9), true, 8); Assert.IsTrue(t.Active);
            t.Update(Crowd(8), true, 8); Assert.IsTrue(t.Active);
            t.Update(Crowd(6), true, 8); Assert.IsFalse(t.Active);
            t.Update(new[] {new MarkerScreenCandidate(1, 0.5f, 0.5f, 16)}, true, 8); Assert.IsFalse(t.Active);
            t.Update(new[] {new MarkerScreenCandidate(1, 0.5f, 0.5f, 17)}, true, 8); Assert.IsTrue(t.Active);
        }

        [TestMethod]
        public void DuplicateInvalidAndOutsideCoordinatesCannotLoseValidIdentities()
        {
            var t = new MarkerScreenDeclutterTracker(); var input = Crowd(20).ToList();
            input.Add(input[0]); input.Add(new MarkerScreenCandidate(999, float.NaN, 0, 1));
            input.Add(new MarkerScreenCandidate(998, 0, float.PositiveInfinity, 1));
            input.Add(new MarkerScreenCandidate(997, 0, 0, 0));
            input.Add(new MarkerScreenCandidate(996, -100, 100, 1));
            t.Update(input, true, 8);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 20).Select(i => (long)i).Append(996L).ToArray(),
                t.Groups.SelectMany(g => g.Keys).ToArray());
            Assert.ThrowsException<ArgumentNullException>(() => t.Update(null!, true, 8));
        }

        [TestMethod]
        public void WarmScreenGroupingHasBoundedAllocations()
        {
            var t = new MarkerScreenDeclutterTracker(); var input = Crowd();
            t.Update(input, true, 8); t.Update(input, true, 8);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) t.Update(input, true, 8);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allocated < 8192, "Warm grouping allocated " + allocated);
            Assert.AreEqual(96, t.Groups.Sum(g => g.Keys.Count));
        }

        [TestMethod]
        public void ScreenSummaryPreservesDlcCategoriesNamesTemporaryLifetimeAndPhysicalOwnership()
        {
            var world = new MarkerWorldClusterTracker();
            var physical = world.Update(new[] {
                new MarkerWorldMember(1, PersonalMarkerKind.OrdinaryPickup, new MarkerWorldPoint(0,0,0), "hex", "Проклятие", MarkerClassKind.Hex, MarkerLifetimeKind.Temporary),
                new MarkerWorldMember(2, PersonalMarkerKind.OrdinaryPickup, new MarkerWorldPoint(100,0,0), "page", "Страница", MarkerClassKind.Page, MarkerLifetimeKind.Permanent)
            }, 0).Clusters;
            var dense = new MarkerDenseAreaSummaryTracker(); var nodes = dense.Update(physical, 0).Nodes;
            Assert.AreEqual(2, nodes.Count);
            var originalKeys = nodes.Select(n => n.StableKey).ToArray();
            var summary = MarkerDenseAreaSummaryTracker.BuildScreenNode(long.MinValue + 1024, nodes).PresentationCluster;
            Assert.AreEqual(2, summary.TotalCount); Assert.AreEqual(1, summary.TemporaryPhysicalMemberCount);
            CollectionAssert.AreEquivalent(new[] {1L, 2L}, summary.MemberStableKeys.ToArray());
            CollectionAssert.AreEquivalent(new[] {MarkerSemanticCategory.Hex, MarkerSemanticCategory.Page}, summary.Composition.Select(c => c.Category).ToArray());
            CollectionAssert.AreEquivalent(new[] {"Проклятие", "Страница"}, summary.ItemRows.Select(r => r.LocalizedName).ToArray());
            CollectionAssert.AreEqual(originalKeys, nodes.Select(n => n.StableKey).ToArray());
            Assert.AreEqual(2, physical.Count);
            Assert.ThrowsException<ArgumentException>(() => MarkerDenseAreaSummaryTracker.BuildScreenNode(100, new[] {nodes[0], nodes[0]}));
        }

        [TestMethod]
        public void FocusRevealsOnlyOneGroupAndCollapsedSettingsKeepCounts()
        {
            var original = new MarkerPresentationSettings(MarkerPresentationMode.Detailed, true, 1f, 12, true, true, false,
                MarkerCompactMixedStyle.CategoryDiamonds, MarkerCategorySortOrder.HighToLow);
            var lod = new MarkerAdaptiveLodTracker();
            var candidates = new[] {new MarkerExpansionCandidate(1, 3, 30, 10), new MarkerExpansionCandidate(2, 3, 40, 60)};
            Assert.IsNull(lod.Update(candidates, 0)); Assert.AreEqual(1L, lod.Update(candidates, 0.13));
            var collapsed = MarkerScreenDeclutterTracker.BuildSettings(original, true, false);
            var focused = MarkerScreenDeclutterTracker.BuildSettings(original, true, true);
            Assert.AreEqual(MarkerPresentationMode.Compact, collapsed.Mode); Assert.IsTrue(collapsed.CompactShowCount);
            Assert.IsFalse(collapsed.ShowDistance); Assert.AreEqual(MarkerPresentationMode.Detailed, focused.Mode);
            Assert.AreEqual(4, focused.DetailRows); Assert.IsTrue(focused.ShowDistance);
            Assert.AreEqual(12, MarkerScreenDeclutterTracker.BuildSettings(original, false, false).DetailRows);
        }

        [TestMethod]
        public void TrialGateOnlyAffectsMarkerCreationAndRenderingAndRooControlsAreCanonical()
        {
            var client = Raw(C2HistoricalFreezeReconciliation.ClientPresentationPath);
            StringAssert.Contains(client, "SceneInfo.instance.sceneDef");
            StringAssert.Contains(client, "var showPersonalMarkers = _config.PersonalMarkersEnabled.Value && !MarkersSuppressedInCurrentScene();");
            StringAssert.Contains(client, "ApplyLocalCommandPresentation(picker, commandPresentation);");
            StringAssert.Contains(C2HistoricalFreezeReconciliation.MethodBody(client, "public void RenderFrame("), "!MarkersSuppressedInCurrentScene()");
            var roo = Raw("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(roo, "CurrentMarkerOptionCount = 30;");
            foreach (var key in new[] {"HideMarkersInTrials", "AutomaticMarkerDeclutter", "MaxVisibleMarkerCards"})
            {
                Assert.AreEqual(1, Regex.Matches(roo, "AddLocalPlan\\([^\\n]+config\\." + key + "[,)]").Count);
                Assert.AreEqual(1, Regex.Matches(roo, "TryMovePlan\\(localPlans, plans, \"" + key + "\"").Count);
                StringAssert.Contains(Raw("src/ItemShareFix.Plugin/PluginConfig.cs"), "BindPresentationInvalidation(" + key + ");");
                var localization = Raw("src/ItemShareFix.Plugin/MarkerRiskOfOptionsLocalization.cs");
                var entries = Regex.Matches(localization, "\\[\"" + key + "\"\\] = A\\(([^\\n]+)\\)");
                Assert.AreEqual(2, entries.Count);
                foreach (Match entry in entries) Assert.AreEqual(11, Regex.Matches(entry.Groups[1].Value, "\"[^\"]*\"").Count);
            }
        }

        [TestMethod]
        public void ActualRendererUsesBudgetedNodesAndMemberFocusBeforeCreatingViews()
        {
            var renderer = Raw(C2HistoricalFreezeReconciliation.NativeHudMarkerRendererPath);
            var render = C2HistoricalFreezeReconciliation.MethodBody(renderer, "public void Render(");
            var group = render.IndexOf("BuildScreenPresentationNodes(camera, semanticDue)", StringComparison.Ordinal);
            var focus = render.IndexOf("BuildExpansionCandidates(camera)", StringComparison.Ordinal);
            var cards = render.IndexOf("var nodes = _worldPresentationNodes;", StringComparison.Ordinal);
            Assert.IsTrue(group >= 0 && focus > group && cards > focus);
            var frame = C2HistoricalFreezeReconciliation.MethodBody(renderer, "private bool TryBuildCurrentClusterFrame(");
            StringAssert.Contains(frame, "MarkerScreenDeclutterTracker.BuildSettings(_presentationSettings, _screenTracker.Active, expanded)");
            var expansion = C2HistoricalFreezeReconciliation.MethodBody(renderer, "private void BuildExpansionCandidates(");
            StringAssert.Contains(expansion, "cluster.MemberStableKeys");
            StringAssert.Contains(expansion, "member.WorldPosition");
        }

        [TestMethod]
        public void ReviewedDeclutterDeltaRejectsUnrelatedAndWithinDeltaChanges()
        {
            const string path = "src/ItemShareFix.Plugin/ClientPresentation.cs";
            var current = Raw(path);
            Assert.AreNotEqual(current, MarkerDeclutterReconciliation.Normalize(path, current));
            Assert.ThrowsException<AssertFailedException>(() => MarkerDeclutterReconciliation.Normalize(path, current + "// drift\n"));
            Assert.ThrowsException<AssertFailedException>(() => MarkerDeclutterReconciliation.Normalize(path,
                current.Replace("scene?.nameToken", "null", StringComparison.Ordinal)));
        }
    }
}
