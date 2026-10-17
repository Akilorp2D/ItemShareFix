using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    internal static class AuditFixReconciliation
    {
        private static readonly Lazy<OctoberCompatibilityReconciliation.Delta[]> Deltas = new Lazy<OctoberCompatibilityReconciliation.Delta[]>(() =>
            JsonSerializer.Deserialize<OctoberCompatibilityReconciliation.Delta[]>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/AuditFixDeltas.json")))!);
        internal static string Normalize(string path, string source)
        {
            source = MarkerLocaleReconciliation.Normalize(path, source);
            foreach (var delta in Deltas.Value)
            {
                if (delta.Path != path) continue;
                Assert.AreEqual(delta.CurrentSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "Audit current drift: " + path);
                foreach (var seam in delta.Seams)
                    source = C2HistoricalFreezeReconciliation.ReplaceExactOnce(source, seam.Current, seam.Historical, "Audit: " + path);
                Assert.AreEqual(delta.HistoricalSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "Audit parent drift: " + path);
                return source;
            }
            return source;
        }
        internal static void RemoveReviewedAddedFiles(ICollection<string> paths)
        {
            MarkerDeclutterReconciliation.RemoveReviewedAddedFiles(paths);
            var added = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/AuditFixAddedFiles.json")))!;
            foreach (var file in added)
            {
                Assert.IsTrue(paths.Contains(file.Key), "Audit added source missing: " + file.Key);
                var source = File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(file.Key));
                Assert.AreEqual(file.Value, C2HistoricalFreezeReconciliation.TextSha256(source), "Audit added source drift: " + file.Key);
                Assert.IsTrue(paths.Remove(file.Key));
            }
        }
    }
}
