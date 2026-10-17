using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2ADiagnosticContractTests
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
        public void ISF_V111_C2A_01_HitchRecorderIsStrictlyBoundedAndDoesNotSampleTotalManagedMemory()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(source, "private const int MaxRecords = 64;");
            StringAssert.Contains(source, "private static readonly Sample[] Records = new Sample[MaxRecords];");
            StringAssert.Contains(source, "private static readonly int[] PhaseCounts = new int[(int)C2HitchPhase.Count];");
            StringAssert.Contains(source, "PhaseCounts[phaseIndex] >= MaxSamples(phase) || _recordCount >= MaxRecords");
            Assert.IsFalse(source.Contains("GC.GetTotalMemory", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("new List<", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_02_DisabledDiagnosticFastPathPrecedesStopwatchGcAndFormattingWork()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            var begin = MethodBody(source, "public static C2HitchToken Begin(");
            const string disabledOrSuppressedFastPath = "if (!_enabled || _captureSuppressed) return default;";
            var disabled = begin.IndexOf(disabledOrSuppressedFastPath, StringComparison.Ordinal);
            Assert.IsTrue(disabled >= 0, "The additive disabled/capture-suppressed fast path must remain exact.");
            Assert.IsTrue(disabled < begin.IndexOf("Stopwatch.GetTimestamp()", StringComparison.Ordinal));
            Assert.IsTrue(disabled < begin.IndexOf("GC.CollectionCount(0)", StringComparison.Ordinal));

            var flush = MethodBody(source, "public static void FlushIfReady(");
            var flushDisabled = flush.IndexOf("if (!_enabled || _flushedCount >= _recordCount) return;", StringComparison.Ordinal);
            Assert.IsTrue(flushDisabled >= 0);
            Assert.IsTrue(flushDisabled < flush.IndexOf("elapsedUs", StringComparison.Ordinal));
            Assert.IsTrue(flushDisabled < flush.IndexOf("ISF_C2_HITCH_DIAG", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_03_DiagnosticClassifiesFirstAndSubsequentSamplesAndDefersFlushPastCapturedFrame()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(source, "+ \" sample=\" + (sample.Ordinal == 1 ? \"first\" : \"subsequent\")");
            StringAssert.Contains(source, "if (Time.frameCount <= _lastCaptureFrame + 2 || Time.unscaledTime < _nextFlushAt) return;");
            StringAssert.Contains(source, "private const float FlushDelaySeconds = 0.75f;");
            StringAssert.Contains(source, "first_marker_frame=");
            StringAssert.Contains(source, "same_first_marker_frame=");
        }

        [TestMethod]
        public void ISF_V111_C2A_04_ClientInstrumentationCoversSweepPickupPresentAndFirstMarkerBearingRenderFrame()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var sweep = MethodBody(source, "private void Sweep(");
            StringAssert.Contains(sweep, "C2HitchPhase.ClientSweep");
            StringAssert.Contains(sweep, "C2HitchPhase.ClientSweepPickupPresent");
            StringAssert.Contains(sweep, "pickupCount: pickupControllers.Count");
            StringAssert.Contains(sweep, "foreach (var pickup in pickupControllers)");

            var render = MethodBody(source, "public void RenderFrame(");
            StringAssert.Contains(render, "C2HitchDiagnostics.MarkFirstMarkerFrame();");
            StringAssert.Contains(render, "C2HitchPhase.ClientRenderFrameMarkerBearing");
            StringAssert.Contains(render, "C2HitchDiagnostics.FlushIfReady(_log);");
        }

        [TestMethod]
        public void ISF_V111_C2A_05_NativeMarkerInstrumentationCoversOneTimeUiAndFirstMeasurementWithoutUndoingC1TypographyOptimization()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(MethodBody(source, "public void Render("), "C2HitchPhase.NativeMarkerRender");
            StringAssert.Contains(MethodBody(source, "private void EnsureCanvas("), "C2HitchPhase.EnsureCanvas");
            StringAssert.Contains(MethodBody(source, "private MarkerView GetOrCreateView("), "C2HitchPhase.GetOrCreateView");
            StringAssert.Contains(MethodBody(source, "private VisualMeasurement GetOrMeasureVisualFootprint("), "C2HitchPhase.MeasureVisualFootprint");
            var typography = MethodBody(source, "private void ResolveNativeTypography(");
            StringAssert.Contains(typography, "C2HitchPhase.ResolveNativeTypography");
            StringAssert.Contains(typography, "StringComparison.OrdinalIgnoreCase");
            Assert.IsFalse(typography.Contains("ToLowerInvariant()", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_06_ServerInstrumentationSeparatesDistributionParticipantSweepFromPeriodicSweep()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ServerCoordinator.cs");
            StringAssert.Contains(source, "private int _c2DistributionSweepDepth;");
            StringAssert.Contains(MethodBody(source, "public void BeginDistribution("), "C2HitchPhase.ServerBeginDistribution");
            var ensure = MethodBody(source, "private void EnsureClaims(");
            StringAssert.Contains(ensure, "C2HitchPhase.ServerEnsureClaims");
            StringAssert.Contains(ensure, "_c2DistributionSweepDepth++;");
            StringAssert.Contains(ensure, "_c2DistributionSweepDepth--;");
            var sweep = MethodBody(source, "private void SweepParticipants(");
            StringAssert.Contains(sweep, "C2HitchPhase.ServerSweepParticipantsDistribution");
            StringAssert.Contains(sweep, "C2HitchPhase.ServerSweepParticipantsPeriodic");
            StringAssert.Contains(MethodBody(source, "public void Tick("), "C2HitchDiagnostics.FlushIfReady(_log);");
        }

        [TestMethod]
        public void ISF_V111_C2A_07_DiagnosticLoggingDoesNotGateProductionBehaviorPaths()
        {
            var diagnostic = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(diagnostic, "public static void SetEnabled(bool enabled) => _enabled = enabled;");
            Assert.IsFalse(diagnostic.Contains("PluginConfig", StringComparison.Ordinal));
            Assert.IsFalse(diagnostic.Contains("ConfigEntry", StringComparison.Ordinal));
            Assert.IsFalse(diagnostic.Contains("ClaimLedger", StringComparison.Ordinal));
            Assert.IsFalse(diagnostic.Contains("MarkerPresentationPolicy", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_08_BlockedCompatibilityDiscoveryDoesNotLeakGuessedDisplayNameMatchingIntoProduction()
        {
            var root = RepositoryRoot().FullName;
            foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                Assert.IsFalse(source.Contains("Key Fragment", StringComparison.OrdinalIgnoreCase), "Guessed Belmont display-name identity leaked into production: " + file);
                Assert.IsFalse(source.Contains("G-Virus Sample", StringComparison.OrdinalIgnoreCase), "Guessed HUNK identity leaked into production: " + file);
                Assert.IsFalse(source.Contains("T-Virus Sample", StringComparison.OrdinalIgnoreCase), "Guessed HUNK identity leaked into production: " + file);
                Assert.IsFalse(source.Contains("C-Virus Sample", StringComparison.OrdinalIgnoreCase), "Guessed HUNK identity leaked into production: " + file);
                Assert.IsFalse(source.Contains("Uroboros Sample", StringComparison.OrdinalIgnoreCase), "Guessed HUNK identity leaked into production: " + file);
            }
        }
    }
}
