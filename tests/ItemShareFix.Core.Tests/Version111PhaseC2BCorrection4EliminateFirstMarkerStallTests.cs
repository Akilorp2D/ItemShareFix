using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection4EliminateFirstMarkerStallTests
    {
        private static DirectoryInfo RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ItemShareFix.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory);
            return directory!;
        }

        private static string ReadSource(string relativePath)
            => AuditFixReconciliation.Normalize(relativePath, File.ReadAllText(Path.Combine(RepositoryRoot().FullName, relativePath.Replace('/', Path.DirectorySeparatorChar))));

        private static string MethodBody(string source, string declaration)
        {
            var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0, "Declaration missing: " + declaration);
            var openBrace = source.IndexOf('{', declarationIndex);
            Assert.IsTrue(openBrace >= 0, "Method body missing: " + declaration);
            var depth = 0;
            var inString = false;
            var inChar = false;
            var escaped = false;
            for (var i = openBrace; i < source.Length; i++)
            {
                var c = source[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }
            Assert.Fail("Unbalanced method body: " + declaration);
            return string.Empty;
        }

        private static string Sha256(string relativePath)
        {
            return C2HistoricalFreezeReconciliation.SourceSha256(relativePath);
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_01_PrepareMethodReleaseStrategyIsRemoved()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("RuntimeHelpers.PrepareMethod", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("PrepareColdMethod(", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("AdvanceFirstMarkerJitWarmup", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_coldJitPrepareStage", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_02_ExactNestedPrimerRunsDuringCoordinatorInitialization()
        {
            var client = MethodBody(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "public ClientPresentationCoordinator(");
            var diagnosticEnable = client.IndexOf("C2HitchDiagnostics.SetEnabled(_config.DiagnosticLogging.Value);", StringComparison.Ordinal);
            var rendererCreate = client.IndexOf("new NativeHudMarkerRenderer(log, _performance);", StringComparison.Ordinal);
            var prime = client.IndexOf("_hudRenderer.PrimeNestedColdPathDuringInitialization();", StringComparison.Ordinal);
            Assert.IsTrue(diagnosticEnable >= 0 && rendererCreate > diagnosticEnable && prime > rendererCreate);
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_03_PrimerUsesIsolatedScratchRendererAndRealRenderPipeline()
        {
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "isolatedColdPrimer: true");
            StringAssert.Contains(primer, "scratch.Render(");
            Assert.AreEqual(2, primer.Split(new[] { "scratch.Render(" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(primer, "scratch?.Dispose()");
            Assert.IsFalse(primer.Contains("GenericPickupController", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("Inventory", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_04_PrimerScratchCanvasIsNeverVisible()
        {
            var ensure = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "private void EnsureCanvas(");
            StringAssert.Contains(ensure, "_canvas.enabled = !_presentationSuppressed && !_isolatedColdPrimer;");
            StringAssert.Contains(ensure, "if (_isolatedColdPrimer) _canvasObject.SetActive(false);");
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "camera.enabled = false;");
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_05_FirstLiveMarkerReusesInactiveOwnedPooledView()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("PrimeFirstMarkerViewPool", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_firstMarkerViewPool", StringComparison.Ordinal));
            var primer = MethodBody(renderer, "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "isolatedColdPrimer: true");
            StringAssert.Contains(primer, "scratch.Render(");
            StringAssert.Contains(primer, "scratch?.Dispose()");
            var get = MethodBody(renderer, "private MarkerView GetOrCreateView(");
            Assert.IsFalse(get.Contains("pooled", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_06_PrimerDoesNotBorrowFontSharedMaterialOrChangeTypographyOwnership()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("_nativeFontMaterial", StringComparison.Ordinal));
            var ensure = MethodBody(renderer, "private void EnsureTypography(");
            StringAssert.Contains(ensure, "text.font = _nativeFont");
            Assert.IsFalse(ensure.Contains("fontSharedMaterial", StringComparison.Ordinal));
            var resolver = MethodBody(renderer, "private void ResolveNativeTypography(");
            Assert.IsFalse(resolver.Contains("fontSharedMaterial", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_07_PrimerDiagnosticsAreBoundedAndDoNotConsumeLiveFirstOnlySamples()
        {
            var diagnostic = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(diagnostic, "private const int MaxRecords = 64;");
            StringAssert.Contains(diagnostic, "if (phase == C2HitchPhase.LoadingNestedPrimer) return 1;");
            StringAssert.Contains(diagnostic, "if (!_enabled || _captureSuppressed) return default;");
            var primer = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public bool PrimeNestedColdPathDuringInitialization(");
            Assert.IsTrue(primer.IndexOf("C2HitchDiagnostics.SetCaptureSuppressed(true);", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(primer.IndexOf("C2HitchDiagnostics.SetCaptureSuppressed(false);", StringComparison.Ordinal) > primer.IndexOf("scratch.Render(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_08_GameplayNetworkConfigAndCompatibilityOwnersRemainFrozen()
        {
            var compatibility = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.AreEqual(12, compatibility.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual("1871A19B8FFCADEC1D1097E383D6B20399BA5DECB24DC7B1455BC84F1E0A2F77", Sha256("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", Sha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
            Assert.AreEqual("C058FA7864C57BE861E5F3ED49A918D68D1B76285DCB9B5E2BD79E1031F181F1", Sha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            C2HistoricalFreezeReconciliation.AssertRuntimePatchesCurrentAndC1Normalized();
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_09_WarmColdPathNoLongerRunsMonolithicNestedPrimerInInteractiveFrames()
        {
            var warmup = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public bool WarmColdPathStep(");
            Assert.IsFalse(warmup.Contains("PrimeNestedColdPathDuringInitialization", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("scratch.Render", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("PrepareMethod", StringComparison.Ordinal));
            StringAssert.Contains(warmup, "EnsureCanvas();");
            StringAssert.Contains(warmup, "ResolveFallbackTypographyWithoutGlobalDiscovery();");
            Assert.IsFalse(warmup.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C4_10_NoProductionPathClaimsRuntimeHitchPassBeforeOperatorRetest()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs")
                + ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs")
                + ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            Assert.IsFalse(source.Contains("FIRST_PICKUP_HITCH_FIX=PASS", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("FIRST_MARKER_HITCH=FIXED", StringComparison.Ordinal));
        }
    }
}
