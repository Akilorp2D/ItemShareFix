using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection3RenderPipelineReductionTests
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
        public void ISF_V111_C2B_C3_01_ColdJitPreparationRunsOnlyAfterCanvasAndTypographyAndNeverRendersMarkers()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var warmup = MethodBody(renderer, "public bool WarmColdPathStep(");
            Assert.IsTrue(warmup.IndexOf("EnsureCanvas();", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(warmup.IndexOf("ResolveFallbackTypographyWithoutGlobalDiscovery();", StringComparison.Ordinal) > warmup.IndexOf("EnsureCanvas();", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("AdvanceFirstMarkerJitWarmup", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("RuntimeHelpers.PrepareMethod", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("Render(", StringComparison.Ordinal));

            var primer = MethodBody(renderer, "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "isolatedColdPrimer: true");
            StringAssert.Contains(primer, "scratch.Render(");
            Assert.IsFalse(primer.Contains("PrimeFirstMarkerViewPool", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_firstMarkerViewPool", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_02_ColdJitPreparationIsOneMethodPerIdleFrameAndBoundedToEighteenStages()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("_coldJitPrepareStage", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("PrepareColdMethod(", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("RuntimeHelpers.PrepareMethod", StringComparison.Ordinal));
            var primer = MethodBody(renderer, "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "if (_loadingNestedPrimerAttempted) return _loadingNestedPrimerCompleted;");
            Assert.AreEqual(2, primer.Split(new[] { "scratch.Render(" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_03_RuntimePreparationCompilesMethodBodiesWithoutExecutingSyntheticMarkerState()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var primer = MethodBody(renderer, "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "new NativeHudMarkerRenderer(_log, new MarkerRuntimePerformanceCounters(), isolatedColdPrimer: true)");
            StringAssert.Contains(primer, "camera.enabled = false;");
            StringAssert.Contains(primer, "scratch?.Dispose()");
            StringAssert.Contains(primer, "UnityEngine.Object.Destroy(cameraObject)");
            Assert.IsFalse(primer.Contains("GenericPickupController", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("Inventory", StringComparison.Ordinal));
            Assert.IsFalse(primer.Contains("Network", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_04_FirstSemanticBuildHasRequiredFirstOnlySubphaseTiming()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var render = MethodBody(renderer, "public void Render(");
            foreach (var phase in new[]
            {
                "SemanticSignatureIndex", "SemanticBuildWorldMembers", "SemanticTrackerUpdate",
                "SemanticDenseTrackerUpdate", "SemanticClassification", "SemanticExpansionLod"
            }) StringAssert.Contains(render, "C2HitchPhase." + phase);
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_05_FirstLayoutSolveSeparatesFramePlanCandidateSingleSolveAndDirectionalAggregation()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var render = MethodBody(renderer, "public void Render(");
            StringAssert.Contains(render, "C2HitchPhase.LayoutClusterFrameBuild");
            StringAssert.Contains(render, "C2HitchPhase.LayoutPlacementCandidateBuild");
            StringAssert.Contains(render, "C2HitchPhase.LayoutDirectionalAggregation");
            StringAssert.Contains(MethodBody(renderer, "private MarkerClusterPresentationPlan GetOrBuildPresentationPlan("), "C2HitchPhase.PresentationPlanBuild");
            StringAssert.Contains(MethodBody(renderer, "private void ResolveCurrentPlacements("), "C2HitchPhase.LayoutSingleMarkerPlacement");
            StringAssert.Contains(MethodBody(renderer, "private MarkerView GetOrCreateView("), "C2HitchPhase.GetOrCreateView");
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_06_FirstApplySeparatesTextStyleGeometryAndActivation()
        {
            var apply = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "private void ApplyView(");
            var text = apply.IndexOf("C2HitchPhase.ApplyTextStyle", StringComparison.Ordinal);
            var geometry = apply.IndexOf("C2HitchPhase.ApplyGeometryLayout", StringComparison.Ordinal);
            var activation = apply.IndexOf("C2HitchPhase.ApplyActivation", StringComparison.Ordinal);
            Assert.IsTrue(text >= 0 && geometry > text && activation > geometry);
            Assert.IsTrue(apply.IndexOf("view.Label.text = renderedText", StringComparison.Ordinal) > text);
            Assert.IsTrue(apply.IndexOf("view.Rect.sizeDelta", StringComparison.Ordinal) > geometry);
            Assert.IsTrue(apply.IndexOf("view.Root.SetActive(true)", StringComparison.Ordinal) > activation);
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_07_DiagnosticsRemainStrictlyBoundedWhileCapturingNewColdSubphases()
        {
            var diagnostic = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(diagnostic, "private const int MaxRecords = 64;");
            StringAssert.Contains(diagnostic, "if (phase >= C2HitchPhase.SemanticSignatureIndex && phase <= C2HitchPhase.ApplyActivation) return 1;");
            StringAssert.Contains(diagnostic, "if (phase == C2HitchPhase.LoadingNestedPrimer) return 1;");
            foreach (var phase in new[]
            {
                "SEMANTIC_SIGNATURE_INDEX", "SEMANTIC_TRACKER_UPDATE", "SEMANTIC_DENSE_TRACKER_UPDATE",
                "LAYOUT_CLUSTER_FRAME_BUILD", "LAYOUT_SINGLE_MARKER_PLACEMENT", "PRESENTATION_PLAN_BUILD",
                "APPLY_TEXT_STYLE", "APPLY_GEOMETRY_LAYOUT", "APPLY_ACTIVATION", "LOADING_NESTED_PRIMER"
            }) StringAssert.Contains(diagnostic, phase);
            Assert.IsFalse(diagnostic.Contains("COLD_JIT_PREPARE", StringComparison.Ordinal));
            Assert.IsFalse(diagnostic.Contains("GC.GetTotalMemory", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_08_FieldFixedTypographyOwnershipRemainsPreserved()
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
        public void ISF_V111_C2B_C3_09_CompatibilityAndGameplayNetworkConfigOwnersRemainFrozen()
        {
            var compatibility = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.AreEqual(12, compatibility.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual("1871A19B8FFCADEC1D1097E383D6B20399BA5DECB24DC7B1455BC84F1E0A2F77", Sha256("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", Sha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
            Assert.AreEqual("C058FA7864C57BE861E5F3ED49A918D68D1B76285DCB9B5E2BD79E1031F181F1", Sha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            C2HistoricalFreezeReconciliation.AssertRuntimePatchesCurrentAndC1Normalized();
        }

        [TestMethod]
        public void ISF_V111_C2B_C3_10_NoProductionPathClaimsTheHitchFixedBeforeOperatorRetest()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs")
                + ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs")
                + ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            Assert.IsFalse(source.Contains("FIRST_PICKUP_HITCH_FIX=PASS", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("FIRST_MARKER_HITCH=FIXED", StringComparison.Ordinal));
        }
    }
}
