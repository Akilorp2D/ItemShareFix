using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    // Reverse only the reviewed release metadata delta before historical checks.
    internal static class Version120Reconciliation
    {
        private static readonly Lazy<OctoberCompatibilityReconciliation.Delta[]> Deltas = new(() =>
            JsonSerializer.Deserialize<OctoberCompatibilityReconciliation.Delta[]>(File.ReadAllText(
                C2HistoricalFreezeReconciliation.ResolvePath("tests/ItemShareFix.Core.Tests/Fixtures/Version120Deltas.json")))!);

        internal static string Normalize(string path, string source)
        {
            foreach (var delta in Deltas.Value)
            {
                if (!string.Equals(delta.Path, path, StringComparison.Ordinal)) continue;
                Assert.AreEqual(delta.CurrentSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "1.2.0 metadata drift: " + path);
                foreach (var seam in delta.Seams)
                    source = C2HistoricalFreezeReconciliation.ReplaceExactOnce(source, seam.Current, seam.Historical, "1.2.0 metadata: " + path);
                Assert.AreEqual(delta.HistoricalSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "1.2.0 parent drift: " + path);
                return source;
            }
            return source;
        }
    }
}
