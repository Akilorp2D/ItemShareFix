using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    internal static class LiveSharingReconciliation
    {
        private static readonly Lazy<OctoberCompatibilityReconciliation.Delta[]> Deltas = new Lazy<OctoberCompatibilityReconciliation.Delta[]>(() =>
            JsonSerializer.Deserialize<OctoberCompatibilityReconciliation.Delta[]>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/LiveSharingDeltas.json")))!);

        internal static string Normalize(string path, string source)
        {
            source = AuditFixReconciliation.Normalize(path, source);
            foreach (var delta in Deltas.Value)
            {
                if (delta.Path != path) continue;
                Assert.AreEqual(delta.CurrentSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "Live sharing current drift: " + path);
                foreach (var seam in delta.Seams)
                    source = C2HistoricalFreezeReconciliation.ReplaceExactOnce(source, seam.Current, seam.Historical, "Live sharing: " + path);
                Assert.AreEqual(delta.HistoricalSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "Live sharing parent drift: " + path);
                return source;
            }
            return source;
        }
    }
}
