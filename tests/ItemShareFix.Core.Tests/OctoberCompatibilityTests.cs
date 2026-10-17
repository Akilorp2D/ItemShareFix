using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public class OctoberCompatibilityTests
    {
        private static string Current(string path) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));

        [TestMethod]
        public void ProgressionOnlyPickerHasNoMarkerEvenWhenUpstreamSaysShareable()
        {
            var decision = CommandShareabilityPolicy.Evaluate(new bool?[] { true }, new[] { true });
            Assert.AreEqual(CommandShareabilityState.AllUnshareable, decision.State);
            Assert.IsFalse(decision.MarkerEligible);
        }

        [TestMethod]
        public void ExcludedOptionsDoNotVetoOrdinaryBossInEitherOrder()
        {
            foreach (var excluded in new[] { new[] { true, false }, new[] { false, true } })
                Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true, true }, excluded).MarkerEligible);
        }

        [TestMethod]
        public void UnknownAndUpstreamUnshareableOptionsStillFailClosed()
        {
            Assert.AreEqual(CommandShareabilityState.Unresolved,
                CommandShareabilityPolicy.Evaluate(new bool?[] { true, null }, new[] { true, false }).State);
            Assert.IsFalse(CommandShareabilityPolicy.Evaluate(new bool?[] { true, false }, new[] { true, false }).MarkerEligible);
            Assert.AreEqual(CommandShareabilityState.Mixed,
                CommandShareabilityPolicy.Evaluate(new bool?[] { true, true, false }, new[] { true, false, false }).State);
            Assert.AreEqual(CommandShareabilityState.Unresolved,
                CommandShareabilityPolicy.Evaluate(Array.Empty<bool?>(), Array.Empty<bool>()).State);
            Assert.AreEqual(CommandShareabilityState.Unresolved,
                CommandShareabilityPolicy.Evaluate(new bool?[] { true }, Array.Empty<bool>()).State);
        }

        [TestMethod]
        public void OrdinaryPickerPolicyRemainsUnchanged()
        {
            Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true }, new[] { false }).MarkerEligible);
            Assert.IsFalse(CommandShareabilityPolicy.Evaluate(new bool?[] { false }, new[] { false }).MarkerEligible);
            Assert.IsFalse(CommandShareabilityPolicy.Evaluate(new bool?[] { true, false }, new[] { false, false }).MarkerEligible);
        }

        [TestMethod]
        public void SelectionExclusionPrecedesTemporaryAndGrantAndCallsOriginalOnce()
        {
            var source = Current("src/ItemShareFix.Plugin/RuntimePatches.cs");
            var body = C2HistoricalFreezeReconciliation.MethodBody(source, "private static bool ItemShareCommandSelectionPrefix(");
            var exclusion = body.IndexOf("KnownProgressionPickupCompatibility.IsExcluded", StringComparison.Ordinal);
            var temporary = body.IndexOf("var isTemporary", StringComparison.Ordinal);
            Assert.IsTrue(exclusion >= 0 && temporary > exclusion);
            var block = body.Substring(exclusion, temporary - exclusion);
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(block, "InvokeSuppliedOriginal(__args[0], picker, choiceIndex);"));
            StringAssert.Contains(block, "__state.Bypassed = true;");
            StringAssert.Contains(block, "return false;");
            Assert.IsTrue(body.IndexOf("if (!option.available) return true;", StringComparison.Ordinal) < exclusion);
            var postfix = C2HistoricalFreezeReconciliation.MethodBody(source, "private static void ItemShareCommandSelectionPostfix(");
            StringAssert.Contains(postfix, "__state.Bypassed || _server == null) return;");
        }

        [TestMethod]
        public void MarkerExclusionRequiresExactSourceAndCannotPolluteClassOrUpstreamCalls()
        {
            var body = C2HistoricalFreezeReconciliation.MethodBody(Current("src/ItemShareFix.Plugin/ClientPresentation.cs"), "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            var exclusion = body.IndexOf("if (exactSource && KnownProgressionPickupCompatibility.IsExcluded(def))", StringComparison.Ordinal);
            var classification = body.IndexOf("ResolvePickupMarkerMetadata(def)", StringComparison.Ordinal);
            var upstream = body.IndexOf("_upstream.TryIsShareable", StringComparison.Ordinal);
            Assert.IsTrue(exclusion >= 0 && classification > exclusion && upstream > classification);
            StringAssert.Contains(body.Substring(exclusion, classification - exclusion), "continue;");
            StringAssert.Contains(body, "Array.Resize(ref resolvedClasses, includedClassCount);");
            StringAssert.Contains(body, "Array.Resize(ref resolvedColors, includedClassCount);");
        }

        [TestMethod]
        public void RemoteOperationUsesExactPresetAndPreservesOwnershipGate()
        {
            var source = Current("src/ItemShareFix.Plugin/ParticipantRuntime.cs");
            StringAssert.Contains(source, "config.DetermineCurrentPreset() == BodyHandlingPresetIndex.RemoteOperation");
            StringAssert.Contains(source, "ReferenceEquals(controller.master, master)");
            StringAssert.Contains(source, "master.hasBodyHandlingConfig && config != null");
            Assert.IsFalse(source.Contains("master.GetInRemoteOp()", StringComparison.Ordinal));
        }

        [TestMethod]
        public void HistoricalNormalizationRejectsUnrelatedAndWithinDeltaDrift()
        {
            const string path = "src/ItemShareFix.Plugin/RuntimePatches.cs";
            var source = Current(path);
            Assert.ThrowsException<AssertFailedException>(() => OctoberCompatibilityReconciliation.Normalize(path, source + "// drift"));
            Assert.ThrowsException<AssertFailedException>(() => OctoberCompatibilityReconciliation.Normalize(path, source.Replace("__state.Bypassed = true;", "__state.Bypassed = false;", StringComparison.Ordinal)));
            Assert.AreEqual(C2HistoricalFreezeReconciliation.RuntimePatchesC8Sha256,
                C2HistoricalFreezeReconciliation.TextSha256(OctoberCompatibilityReconciliation.Normalize(path, source)));
        }

        [TestMethod]
        public void CatalogDiagnosticsRunOnceAfterLoadAndNeverFromFrameLoops()
        {
            var source = Current("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            var catalog = C2HistoricalFreezeReconciliation.MethodBody(source, "private void LogDlcPickupCatalog(");
            StringAssert.Contains(catalog, "if (_dlcCatalogLogged || !_active || _config == null) return;");
            StringAssert.Contains(catalog, "_dlcCatalogLogged = true;");
            StringAssert.Contains(catalog, "if (!_config.DiagnosticInfoEnabled) return;");
            StringAssert.Contains(source, "RoR2Application.onLoad += LogDlcPickupCatalog;");
            foreach (var method in new[] { "private void Update(", "private void FixedUpdate(" })
                Assert.IsFalse(C2HistoricalFreezeReconciliation.MethodBody(source, method).Contains("LogDlcPickupCatalog", StringComparison.Ordinal));
        }
    }
}
