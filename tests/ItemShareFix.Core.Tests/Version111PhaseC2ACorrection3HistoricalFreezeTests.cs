using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2ACorrection3HistoricalFreezeTests
    {
        private static string ReadSource(string relativePath)
            => C2HistoricalFreezeReconciliation.ReadSource(relativePath);

        [TestMethod]
        public void ISF_V111_C2A_C3_01_ExactlyThreeHistoricalC1ProductionFilesAreAuthorizedToDiffer()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    C2HistoricalFreezeReconciliation.RuntimePatchesPath,
                    C2HistoricalFreezeReconciliation.ServerCoordinatorPath,
                    C2HistoricalFreezeReconciliation.ClientPresentationPath,
                },
                C2HistoricalFreezeReconciliation.HistoricalC1FilesAuthorizedToDiffer);
            Assert.AreEqual(3, C2HistoricalFreezeReconciliation.HistoricalC1FilesAuthorizedToDiffer.Length);
        }

        [TestMethod]
        public void ISF_V111_C2A_C3_02_EveryAuthorizedNormalizationSnippetOccursExactlyOnceAndFailsClosed()
        {
            var runtime = ReadSource(C2HistoricalFreezeReconciliation.RuntimePatchesPath);
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(runtime, C2HistoricalFreezeReconciliation.RuntimePatchesCompatibilityBlock));

            var server = ReadSource(C2HistoricalFreezeReconciliation.ServerCoordinatorPath);
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(server, C2HistoricalFreezeReconciliation.ServerCoordinatorCompatibilityProperty));
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(server, C2HistoricalFreezeReconciliation.ServerCoordinatorC2ShareGuard));

            var client = ReadSource(C2HistoricalFreezeReconciliation.ClientPresentationPath);
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(client, C2HistoricalFreezeReconciliation.ClientTryEvaluateCompatibilityGuard));
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(client, C2HistoricalFreezeReconciliation.ClientC2SuppressGuard));
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(client, C2HistoricalFreezeReconciliation.ClientSweepCompatibilityBlock));

            Assert.ThrowsException<AssertFailedException>(() =>
                C2HistoricalFreezeReconciliation.ReplaceExactOnce(runtime + C2HistoricalFreezeReconciliation.RuntimePatchesCompatibilityBlock,
                    C2HistoricalFreezeReconciliation.RuntimePatchesCompatibilityBlock,
                    string.Empty,
                    "deliberate duplicate fail-closed probe"));
        }

        [TestMethod]
        public void ISF_V111_C2A_C3_03_CurrentC2FilesNormalizeExactlyToHistoricalC1Hashes()
            => C2HistoricalFreezeReconciliation.AssertAllHistoricalC1FilesCurrentAndNormalized();

        [TestMethod]
        public void ISF_V111_C2A_C3_04_CurrentCompatibilityTableIsExactlyTwelveAndHiddenPlagasRemainsAbsent()
        {
            var source = ReadSource(C2HistoricalFreezeReconciliation.CompatibilityPath);
            Assert.AreEqual(C2HistoricalFreezeReconciliation.CompatibilityC2Sha256,
                C2HistoricalFreezeReconciliation.SourceSha256(C2HistoricalFreezeReconciliation.CompatibilityPath));
            Assert.AreEqual(12, source.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(source, "new ExactItemIdentity(\"PlagasSample\", \"ROB_HUNK_PLAGAS_SAMPLE_NAME\")");
            Assert.IsFalse(source.Contains("new ExactItemIdentity(\"Plagas\", \"ROB_HUNK_PLAGAS_NAME\")", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_C3_05_C1OptimizationContractTestsRemainPresent()
        {
            var source = ReadSource("tests/ItemShareFix.Core.Tests/Version111PerformanceOptimizationContractTests.cs");
            for (var i = 1; i <= 9; i++)
                StringAssert.Contains(source, "public void ISF_V111_PERF_0" + i + "_");
        }

        [TestMethod]
        public void ISF_V111_C2A_C3_06_C2HitchDiagnosticContractTestsRemainPresent()
        {
            var source = ReadSource("tests/ItemShareFix.Core.Tests/Version111PhaseC2ADiagnosticContractTests.cs");
            for (var i = 1; i <= 8; i++)
                StringAssert.Contains(source, "public void ISF_V111_C2A_0" + i + "_");
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs"), "ISF_C2_HITCH_DIAG");
        }

        [TestMethod]
        public void ISF_V111_C2A_C3_07_VisibilityMethodsUseTwoSidedCurrentAndNormalizedProof()
            => C2HistoricalFreezeReconciliation.AssertVisibilityMethodsCurrentAndC1Normalized();
    }
}
