using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection13PresentationColdPrewarmTests
    {
        private static DirectoryInfo RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("Repository root not found.");
            return dir;
        }

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(RepositoryRoot().FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static string MethodBody(string source, string declaration)
            => C2HistoricalFreezeReconciliation.MethodBody(source, declaration);

        [TestMethod]
        public void ISF_V111_C2B_C13_01_PrimerRunsOnceFromAwakeBeforeUpstreamAndActivation()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            var awake = MethodBody(plugin, "private void Awake()");
            const string call = "PresentationColdPrimer.PrimeCommandPresentationDuringInitialization(Logger);";
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(plugin, call));
            var primer = awake.IndexOf(call, StringComparison.Ordinal);
            var upstream = awake.IndexOf("var upstream = new UpstreamBridge(", StringComparison.Ordinal);
            var active = awake.IndexOf("_active = true;", StringComparison.Ordinal);
            Assert.IsTrue(primer >= 0 && upstream > primer && active > upstream);
            Assert.IsFalse(MethodBody(plugin, "private void FixedUpdate()").Contains(call, StringComparison.Ordinal));
            Assert.IsFalse(MethodBody(plugin, "private void Update()").Contains(call, StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_02_PrimerWarmsExactPurePresentationHelpersAndMetadata()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            StringAssert.Contains(primer, "CommandShareabilityPolicy.Evaluate(new bool?[] { true, false })");
            StringAssert.Contains(primer, "MarkerClassPolicy.ResolveCommandClassForReadablePresentation(");
            StringAssert.Contains(primer, "MarkerClassPolicy.LocalizedReadableClassLabel(MarkerClassKind.Tier1, MarkerLanguage.English)");
            StringAssert.Contains(primer, "MarkerClassPolicy.DiagnosticClassName(MarkerClassKind.Tier1)");
            StringAssert.Contains(primer, "new MarkerRuntimeMetadata(");
            StringAssert.Contains(primer, "Color.white");
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_03_PrimerContainsNoUpstreamOrItemShareInvocation()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            Assert.IsFalse(primer.Contains("_upstream", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("UpstreamBridge", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("TryIsShareable", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("IsShareable(", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("MethodInfo.Invoke", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("CreateDelegate", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_04_PrimerContainsNoGameCatalogOrPickupResolution()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            Assert.IsFalse(primer.Contains("PickupCatalog", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("ItemCatalog", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("EquipmentCatalog", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("PickupDef", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("ResolveCommandMarkerMetadata", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_05_PrimerContainsNoLiveUnityDiscoveryOrRendering()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            Assert.IsFalse(primer.Contains("GameObject", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("Resources.", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("FindObjectsOfTypeAll", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("NativeHudMarkerRenderer", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("TMP_", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("RenderFrame", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_06_PrimerDoesNotUseRuntimeHelpersPrepareMethod()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            Assert.IsFalse(primer.Contains("RuntimeHelpers", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("PrepareMethod", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_07_PrimerFailureIsBoundedLoggedAndNonFatal()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/PresentationColdPrimer.cs"), "public static bool PrimeCommandPresentationDuringInitialization(");
            StringAssert.Contains(primer, "catch (Exception ex)");
            StringAssert.Contains(primer, "result = \"failed:\" + ex.GetType().Name;");
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(primer, "ISF_COMMAND_PRESENTATION_PRIMER elapsed_us="));
            Assert.AreEqual(1, C2HistoricalFreezeReconciliation.CountExact(primer, "log.LogWarning(message);"));
            StringAssert.Contains(primer, "return success;");
            Assert.IsFalse(primer.Contains("throw;", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_08_CommandOptionResolutionInstrumentationRemainsExactCorrection12Bytes()
        {
            Assert.AreEqual(
                C2HistoricalFreezeReconciliation.ClientPresentationC12Sha256,
                C2HistoricalFreezeReconciliation.SourceSha256(C2HistoricalFreezeReconciliation.ClientPresentationPath));
            var client = ReadSource(C2HistoricalFreezeReconciliation.ClientPresentationPath);
            StringAssert.Contains(client, "ISF_COMMAND_OPTION_PROFILE");
            StringAssert.Contains(client, "presentation_finalization_us=");
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_09_Correction12TypedDelegateAndReflectionFallbackRemainExactBytes()
        {
            Assert.AreEqual(
                C2HistoricalFreezeReconciliation.UpstreamBridgeC12Sha256,
                C2HistoricalFreezeReconciliation.SourceSha256(C2HistoricalFreezeReconciliation.UpstreamBridgePath));
            var upstream = ReadSource(C2HistoricalFreezeReconciliation.UpstreamBridgePath);
            StringAssert.Contains(upstream, "_isShareableDelegate = TryBindIsShareableDelegate(_isShareableMethod);");
            StringAssert.Contains(upstream, "var result = _isShareableMethod.Invoke(null, new object[] { def });");
        }

        [TestMethod]
        public void ISF_V111_C2B_C13_10_CommandSemanticPoliciesRemainUnchanged()
        {
            C2HistoricalFreezeReconciliation.AssertCorrection12CorePoliciesCurrentAndCorrection11Normalized();
            Assert.AreEqual(CommandShareabilityState.Mixed, CommandShareabilityPolicy.Evaluate(new bool?[] { true, false }).State);
            var decision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(
                new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier1 },
                MarkerLanguage.English);
            Assert.AreEqual(MarkerClassKind.Tier1, decision.Kind);
            Assert.AreEqual("White", decision.Label);
            Assert.IsTrue(decision.ExactClass);
        }
    }
}
