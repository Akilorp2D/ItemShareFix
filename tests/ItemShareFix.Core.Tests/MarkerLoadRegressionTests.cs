using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class MarkerLoadRegressionTests
    {
        private sealed class CountingZones : IReadOnlyList<MarkerHudExclusionZone>
        {
            public int Reads;
            public int Count => 1;
            public MarkerHudExclusionZone this[int index]
            {
                get { Reads++; return new MarkerHudExclusionZone("covered", 0, 1920, 0, 1080); }
            }
            public IEnumerator<MarkerHudExclusionZone> GetEnumerator() { yield return this[0]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static MarkerMeasurementCacheKey Typography(int field = -1)
            => new MarkerMeasurementCacheKey(field == 0 ? "changed" : "item", field == 1 ? 2 : 1,
                field == 2 ? 3 : 2, field == 3 ? 25 : 24, field == 4 ? 2560 : 1920,
                field == 5 ? 1440 : 1080, field == 6 ? 2 : 1);

        [TestMethod]
        public void WarmHudCollisionChecksDoNotAllocatePerProbe()
        {
            var rect = new MarkerHudRect(800, 500, 100, 40);
            var zones = new[] { new MarkerHudExclusionZone("message", 200, 300, 400, 500) };
            MarkerHudNavigationPolicy.IntersectsReservedHud(rect, 1920, 1080);
            MarkerHudNavigationPolicy.IntersectsDynamicHud(rect, zones, 1920, 1080);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var collisions = 0;
            for (var i = 0; i < 2000; i++)
            {
                if (MarkerHudNavigationPolicy.IntersectsReservedHud(rect, 1920, 1080)) collisions++;
                if (MarkerHudNavigationPolicy.IntersectsDynamicHud(rect, zones, 1920, 1080)) collisions++;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, collisions);
            Assert.IsTrue(allocated < 4096, "Repeated HUD probes allocated " + allocated + " bytes.");
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsReservedHud(new MarkerHudRect(100, 100, 30, 30), 1920, 1080));
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsDynamicHud(new MarkerHudRect(250, 450, 30, 30), zones, 1920, 1080));
        }

        [TestMethod]
        public void SaturatedHudSearchHasBoundedZoneReads()
        {
            var zones = new CountingZones();
            var candidate = new MarkerHudPlacementCandidate(12,
                new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 800, 500, 0, 0, 0),
                new MarkerHudVisualFootprint(200, 80, 20, 150, 8, 8));
            var placement = MarkerHudNavigationPolicy.ResolveSinglePlacement(candidate, 1920, 1080, zones);
            Assert.AreEqual(12L, placement.StableKey);
            Assert.AreEqual(800f, placement.X);
            Assert.AreEqual(500f, placement.Y);
            Assert.AreEqual(200f, placement.FinalRect.Width);
            Assert.AreEqual(80f, placement.FinalRect.Height);
            Assert.IsTrue(zones.Reads < 768, "Saturated viewport must not trigger a full-screen grid scan: " + zones.Reads);
        }

        [TestMethod]
        public void ReservedHudCacheRefreshesWhenResolutionChanges()
        {
            var rect = new MarkerHudRect(800, 100, 20, 20);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(rect, 1920, 1080));
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsReservedHud(rect, 3840, 2160));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(rect, 1920, 1080));
        }

        [TestMethod]
        public void RenderedTextCacheReusesExactContextAndClearsOldColorText()
        {
            var cache = new MarkerRenderedTextCache();
            var plan = new object();
            var cluster = new object();
            Assert.IsFalse(cache.TryGet(plan, cluster, Typography(), 300, out _));
            cache.Store(plan, cluster, Typography(), 300, "<color=#ABCDEF>Предмет ×2</color>");
            Assert.IsTrue(cache.TryGet(plan, cluster, Typography(), 300, out var text));
            Assert.AreEqual("<color=#ABCDEF>Предмет ×2</color>", text);
            Assert.IsFalse(cache.TryGet(new object(), cluster, Typography(), 300, out _));
            Assert.IsFalse(cache.TryGet(plan, new object(), Typography(), 300, out _));
            Assert.IsFalse(cache.TryGet(plan, cluster, Typography(), 301, out _));
            cache.Clear();
            Assert.IsFalse(cache.TryGet(plan, cluster, Typography(), 300, out _));
            cache.Store(plan, cluster, Typography(), 300, "новый цвет");
            Assert.IsTrue(cache.TryGet(plan, cluster, Typography(), 300, out text));
            Assert.AreEqual("новый цвет", text);
        }

        [TestMethod]
        [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
        public void RenderedTextCacheRejectsEveryChangedTypographyDimension(int field)
        {
            var cache = new MarkerRenderedTextCache();
            var plan = new object();
            var cluster = new object();
            cache.Store(plan, cluster, Typography(), 300, "old");
            Assert.IsFalse(cache.TryGet(plan, cluster, Typography(field), 300, out _));
        }

        [TestMethod]
        public void RendererChecksTextCacheBeforeAnyWidthProbeAndClearsItWithSettings()
        {
            var source = File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(C2HistoricalFreezeReconciliation.NativeHudMarkerRendererPath));
            var method = C2HistoricalFreezeReconciliation.MethodBody(source, "private VisualMeasurement GetOrMeasureVisualFootprint(");
            var guard = method.IndexOf("if (!view.RenderedTextCache.TryGet", StringComparison.Ordinal);
            var build = method.IndexOf("renderedText = BuildRenderedText(", StringComparison.Ordinal);
            var store = method.IndexOf("view.RenderedTextCache.Store(", StringComparison.Ordinal);
            Assert.IsTrue(guard >= 0 && build > guard && store > build);
            var invalidation = C2HistoricalFreezeReconciliation.MethodBody(source, "public void InvalidatePresentationSettings(");
            StringAssert.Contains(invalidation, "view.RenderedTextCache.Clear();");
        }

        [TestMethod]
        public void HistoricalMarkerLoadDeltaRejectsUnreviewedSourceChanges()
        {
            const string path = "src/ItemShareFix.Core/MarkerHudNavigationPolicy.cs";
            var source = File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));
            var baseline = MarkerLoadReconciliation.Normalize(path, source);
            Assert.AreNotEqual(source, baseline);
            Assert.ThrowsException<AssertFailedException>(() => MarkerLoadReconciliation.Normalize(path, source + "// unrelated drift\n"));
            Assert.ThrowsException<AssertFailedException>(() => MarkerLoadReconciliation.Normalize(path,
                source.Replace("MaxHardHudEscapeGridCandidates = 256", "MaxHardHudEscapeGridCandidates = 257", StringComparison.Ordinal)));
        }
    }
}
