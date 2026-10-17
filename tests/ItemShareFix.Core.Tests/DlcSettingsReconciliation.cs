using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    internal static class DlcSettingsReconciliation
    {
        private static readonly Lazy<OctoberCompatibilityReconciliation.Delta[]> Deltas = new Lazy<OctoberCompatibilityReconciliation.Delta[]>(() =>
            JsonSerializer.Deserialize<OctoberCompatibilityReconciliation.Delta[]>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/DlcSettingsDeltas.json")))!);

        internal static string Normalize(string path, string source)
        {
            source = LiveSharingReconciliation.Normalize(path, source);
            foreach (var delta in Deltas.Value)
            {
                if (delta.Path != path) continue;
                Assert.AreEqual(delta.CurrentSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "DLC settings current source drift: " + path);
                foreach (var seam in delta.Seams)
                    source = C2HistoricalFreezeReconciliation.ReplaceExactOnce(source, seam.Current, seam.Historical, "DLC settings: " + path);
                Assert.AreEqual(delta.HistoricalSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "DLC settings parent drift: " + path);
                return source;
            }
            return source;
        }

        internal static void RemoveReviewedAddedFiles(ICollection<string> paths)
        {
            AuditFixReconciliation.RemoveReviewedAddedFiles(paths);
            var added = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/DlcSettingsAddedFiles.json")))!;
            foreach (var file in added)
            {
                Assert.IsTrue(paths.Contains(file.Key), "Reviewed DLC policy source missing: " + file.Key);
                Assert.AreEqual(file.Value, C2HistoricalFreezeReconciliation.SourceSha256(file.Key), "Reviewed DLC policy drift: " + file.Key);
                Assert.IsTrue(paths.Remove(file.Key));
            }
        }
    }
}
