using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BFirstMarkerHitchTests
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
            => File.ReadAllText(Path.Combine(RepositoryRoot().FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static string Sha256(string relativePath)
        {
            return C2HistoricalFreezeReconciliation.SourceSha256(relativePath);
        }

        private static string MethodBody(string source, string declaration)
        {
            var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0, "Declaration missing: " + declaration);
            var openBrace = source.IndexOf('{', declarationIndex);
            Assert.IsTrue(openBrace >= 0, "Method body missing: " + declaration);
            var depth = 0;
            for (var i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }
            Assert.Fail("Unbalanced method body: " + declaration);
            return string.Empty;
        }

        [TestMethod]
        public void ISF_V111_C2B_01_HudColdDiscoveryIsStagedAtOneGlobalTypePerWarmupCall()
        {
            var source = ReadSource(C2HistoricalFreezeReconciliation.LocalHudPresentationProbeC7FixturePath);
            Assert.AreEqual("EAF34078B50C73E8EF3FAD487481D5EC0F0EB1E26B039D7DBEF276DD6C93DD1E", C2HistoricalFreezeReconciliation.TextSha256(source), "Exact Correction7 HUD fixture drift.");
            var warmup = MethodBody(source, "public bool WarmColdPathStep(bool refreshIfDue)");
            StringAssert.Contains(warmup, "DiscoverBlockingModalType(_blockingModalTypes[_coldWarmupBlockingTypeIndex++]);");
            StringAssert.Contains(warmup, "DiscoverMessageHudType(_messageHudTypes[_coldWarmupMessageTypeIndex++]);");
            StringAssert.Contains(warmup, "DiscoverMessageHudFallback();");
            StringAssert.Contains(warmup, "return false;");
            Assert.IsFalse(warmup.Contains("for (var i = 0; i < _blockingModalTypes.Length", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("for (var i = 0; i < _messageHudTypes.Length", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_02_FirstMarkerCannotEnterMeasuredRenderUntilColdWarmupCompletes()
        {
            var render = MethodBody(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "public void RenderFrame(");
            var gate = render.IndexOf("if (!AdvanceMarkerColdPathWarmup(allowRefresh: false)) return;", StringComparison.Ordinal);
            var firstMarker = render.IndexOf("C2HitchDiagnostics.MarkFirstMarkerFrame();", StringComparison.Ordinal);
            Assert.IsTrue(gate >= 0 && firstMarker > gate, "Cold-path gate must precede first-marker capture/render.");
            StringAssert.Contains(render, "AdvanceMarkerColdPathWarmup(allowRefresh: true);");
        }

        [TestMethod]
        public void ISF_V111_C2B_03_NativeColdWarmupStagesCanvasThenTypographyWithoutRenderingMarkers()
        {
            var rawBody = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public bool WarmColdPathStep(");
            var body = string.Join("\n", Array.ConvertAll(rawBody.Split('\n'), line =>
            {
                var comment = line.IndexOf("//", StringComparison.Ordinal);
                return comment >= 0 ? line.Substring(0, comment) : line;
            }));
            var canvas = body.IndexOf("EnsureCanvas();", StringComparison.Ordinal);
            var typography = body.IndexOf("ResolveFallbackTypographyWithoutGlobalDiscovery();", StringComparison.Ordinal);
            Assert.IsTrue(canvas >= 0 && typography > canvas);
            Assert.IsFalse(body.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("fontSharedMaterial", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Render(", StringComparison.Ordinal));
            StringAssert.Contains(body, "return false;");
            StringAssert.Contains(body, "return true;");
        }

        [TestMethod]
        public void ISF_V111_C2B_04_BoundedDiagnosticsCoverNewClientNativeAndPrewarmSubphases()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(source, "private const int MaxRecords = 64;");
            StringAssert.Contains(source, "new int[(int)C2HitchPhase.Count]");
            foreach (var phase in new[]
            {
                "ClientHudProbe", "ClientCandidateBuild", "ClientRendererCall", "NativeSemanticBuild",
                "NativeLayoutSolve", "NativeApplyViews", "NativePostRenderCleanup", "ColdWarmupHudProbe", "ColdWarmupNative"
            }) StringAssert.Contains(source, phase);
            Assert.IsFalse(source.Contains("GC.GetTotalMemory", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_05_ClientRenderFrameSeparatesHudCandidateAndRendererTiming()
        {
            var render = MethodBody(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "public void RenderFrame(");
            StringAssert.Contains(render, "C2HitchPhase.ClientHudProbe");
            StringAssert.Contains(render, "C2HitchPhase.ClientCandidateBuild");
            StringAssert.Contains(render, "C2HitchPhase.ClientRendererCall");
            StringAssert.Contains(render, "_localHudProbe.TryGetBlockingModal");
            StringAssert.Contains(render, "_localHudProbe.TryGetVisibleMessageHudRect");
            StringAssert.Contains(render, "_hudRenderer.Render(");
        }

        [TestMethod]
        public void ISF_V111_C2B_06_NativeRendererSeparatesSemanticLayoutApplyAndCleanupTiming()
        {
            var render = MethodBody(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "public void Render(");
            StringAssert.Contains(render, "C2HitchPhase.NativeSemanticBuild");
            StringAssert.Contains(render, "C2HitchPhase.NativeLayoutSolve");
            StringAssert.Contains(render, "C2HitchPhase.NativeApplyViews");
            StringAssert.Contains(render, "C2HitchPhase.NativePostRenderCleanup");
            StringAssert.Contains(render, "BuildWorldMembers(language);");
            StringAssert.Contains(render, "ResolveCurrentPlacements(structuralInvalidated, projectionInvalidated);");
            StringAssert.Contains(render, "ApplyView(");
            StringAssert.Contains(render, "RemoveView(_staleKeys[i]);");
        }

        [TestMethod]
        public void ISF_V111_C2B_07_CompatibilityTableRemainsExactlyTwelve()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.AreEqual(12, source.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(source, "new ExactItemIdentity(\"PlagasSample\", \"ROB_HUNK_PLAGAS_SAMPLE_NAME\")");
            Assert.IsFalse(source.Contains("new ExactItemIdentity(\"Plagas\", \"ROB_HUNK_PLAGAS_NAME\")", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_08_C1OptimizationContractsRemainPresent()
        {
            var source = ReadSource("tests/ItemShareFix.Core.Tests/Version111PerformanceOptimizationContractTests.cs");
            for (var i = 1; i <= 9; i++) StringAssert.Contains(source, "public void ISF_V111_PERF_0" + i + "_");
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/ParticipantRuntime.cs"), "private static readonly Dictionary<Type, Dictionary<string, MemberInfo?>> MemberInfoCache");
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"), "StringComparison.OrdinalIgnoreCase");
        }

        [TestMethod]
        public void ISF_V111_C2B_09_GameplayNetworkAndCompatibilityOwnersAreByteFrozenFromC2A()
        {
            C2HistoricalFreezeReconciliation.AssertRuntimePatchesCurrentAndC1Normalized();
            Assert.AreEqual(C2HistoricalFreezeReconciliation.ServerCoordinatorC2Sha256, Sha256("src/ItemShareFix.Plugin/ServerCoordinator.cs"));
            Assert.AreEqual(C2HistoricalFreezeReconciliation.CompatibilityC2Sha256, Sha256("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs"));
            Assert.AreEqual("F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54", Sha256("src/ItemShareFix.Plugin/PluginConfig.cs"));
        }
    }
}
