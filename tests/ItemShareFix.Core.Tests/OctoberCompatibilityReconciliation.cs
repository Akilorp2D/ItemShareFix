using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    // Historical freeze assertions still check their original hashes. Only the
    // six reviewed October deltas may be reversed; drift anywhere else fails.
    internal static class OctoberCompatibilityReconciliation
    {
        public sealed class Seam
        {
            public string Current { get; set; } = string.Empty;
            public string Historical { get; set; } = string.Empty;
        }

        public sealed class Delta
        {
            public string Path { get; set; } = string.Empty;
            public string CurrentSha256 { get; set; } = string.Empty;
            public string HistoricalSha256 { get; set; } = string.Empty;
            public Seam[] Seams { get; set; } = Array.Empty<Seam>();
        }

        private static readonly Lazy<Delta[]> Deltas = new Lazy<Delta[]>(() =>
            JsonSerializer.Deserialize<Delta[]>(File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(
                "tests/ItemShareFix.Core.Tests/Fixtures/OctoberCompatibilityDeltas.json")))!);

        internal static string Normalize(string path, string source)
        {
            source = DlcSettingsReconciliation.Normalize(path, source);
            foreach (var delta in Deltas.Value)
            {
                if (!string.Equals(delta.Path, path, StringComparison.Ordinal)) continue;
                Assert.AreEqual(delta.CurrentSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "October current source drift: " + path);
                foreach (var seam in delta.Seams)
                    source = C2HistoricalFreezeReconciliation.ReplaceExactOnce(source, seam.Current, seam.Historical, "October: " + path);
                Assert.AreEqual(delta.HistoricalSha256, C2HistoricalFreezeReconciliation.TextSha256(source), "October parent source drift: " + path);
                return source;
            }
            return source;
        }
    }
}
