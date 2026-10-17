using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection6MarkerRegressionAndResidualHitchTests
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
        {
            var start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing declaration: " + declaration);
            var open = source.IndexOf('{', start);
            Assert.IsTrue(open >= 0, "Missing opening brace: " + declaration);
            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            Assert.Fail("Unbalanced method body: " + declaration);
            return string.Empty;
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_01_LiveFirstMarkerViewPoolIsCompletelyRemoved()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("PrimeFirstMarkerViewPool", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_firstMarkerViewPool", StringComparison.Ordinal));
            var get = MethodBody(renderer, "private MarkerView GetOrCreateView(");
            Assert.IsFalse(get.Contains("pooled", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_02_PrimerUsesOnlyIsolatedScratchRendererAndDisposesIt()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var primer = MethodBody(renderer, "public bool PrimeNestedColdPathDuringInitialization(");
            StringAssert.Contains(primer, "isolatedColdPrimer: true");
            Assert.AreEqual(2, primer.Split(new[] { "scratch.Render(" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(primer, "scratch?.Dispose()");
            StringAssert.Contains(primer, "UnityEngine.Object.Destroy(cameraObject)");
            Assert.IsFalse(primer.Contains("GetOrCreateView(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_03_CaptureSuppressionAlsoProtectsFirstMarkerFrameIdentity()
        {
            var diagnostics = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            var mark = MethodBody(diagnostics, "public static void MarkFirstMarkerFrame(");
            StringAssert.Contains(mark, "!_enabled || _captureSuppressed || _firstMarkerFrame >= 0");
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_04_BorrowedFontSharedMaterialRemainsAbsent()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("_nativeFontMaterial", StringComparison.Ordinal));
            Assert.IsFalse(MethodBody(renderer, "private void ResolveNativeTypography(").Contains("fontSharedMaterial", StringComparison.Ordinal));
            Assert.IsFalse(MethodBody(renderer, "private void EnsureTypography(").Contains("fontSharedMaterial", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_05_CommandPickerActivationSubphasesAreExplicitAndBounded()
        {
            var diagnostics = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            foreach (var phase in new[]
            {
                "CommandPickerDiscovery", "CommandOptionResolution", "CommandMarkerRegistryUpdate",
                "CommandRenderInputConstruction", "FirstMarkerActivationEndToEnd"
            }) StringAssert.Contains(diagnostics, "C2HitchPhase." + phase);
            StringAssert.Contains(diagnostics, "private const int MaxRecords = 64;");
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_06_CommandPickerDiscoveryAndOptionResolutionAreMeasuredOutsideRenderer()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var sweep = MethodBody(client, "private void Sweep(");
            StringAssert.Contains(sweep, "C2HitchPhase.CommandPickerDiscovery");
            StringAssert.Contains(sweep, "InstanceTracker.GetInstancesList<PickupPickerController>()");
            StringAssert.Contains(sweep, "C2HitchPhase.CommandOptionResolution");
            StringAssert.Contains(sweep, "_upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions)");
            StringAssert.Contains(sweep, "ResolveCommandMarkerMetadata(");
            StringAssert.Contains(sweep, "commandOptions,");
            StringAssert.Contains(sweep, "commandLifetime = commandOptions.Lifetime;");

            var optionTiming = sweep.IndexOf("C2HitchPhase.CommandOptionResolution", StringComparison.Ordinal);
            var snapshotBuild = sweep.IndexOf("_upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions)", optionTiming, StringComparison.Ordinal);
            var metadataCall = sweep.IndexOf("ResolveCommandMarkerMetadata(", snapshotBuild, StringComparison.Ordinal);
            var snapshotArgument = sweep.IndexOf("commandOptions,", metadataCall, StringComparison.Ordinal);
            var lifetimeReuse = sweep.IndexOf("commandLifetime = commandOptions.Lifetime;", snapshotArgument, StringComparison.Ordinal);
            var eligibility = sweep.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(commandLifetime, _config.ShareTemporaryItems.Value)", lifetimeReuse, StringComparison.Ordinal);
            Assert.IsTrue(optionTiming >= 0 && snapshotBuild > optionTiming);
            Assert.IsTrue(metadataCall > snapshotBuild && snapshotArgument > metadataCall);
            Assert.IsTrue(lifetimeReuse > snapshotArgument && eligibility > lifetimeReuse);
            Assert.IsFalse(sweep.Contains("_upstream.TryGetCommandChoiceLifetime(", StringComparison.Ordinal));
            Assert.IsFalse(sweep.Contains("_upstream.TryGetCommandChoicePickupIndexes(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_07_RegistryAndCommandRenderInputConstructionAreMeasured()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var sweep = MethodBody(client, "private void Sweep(");
            StringAssert.Contains(sweep, "C2HitchPhase.CommandMarkerRegistryUpdate");
            StringAssert.Contains(sweep, "_markerRegistry.MarkPending(PersonalMarkerKind.CommandPicker");
            var render = MethodBody(client, "public void RenderFrame(");
            StringAssert.Contains(render, "C2HitchPhase.CommandRenderInputConstruction");
            StringAssert.Contains(render, "PersonalMarkerKind.CommandPicker");
        }

        [TestMethod]
        public void ISF_V111_C2B_C6_08_EndToEndActivationTimingEndsAfterRendererCall()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var render = MethodBody(client, "public void RenderFrame(");
            var rendererCall = render.IndexOf("_hudRenderer.Render(", StringComparison.Ordinal);
            var endToEnd = render.IndexOf("C2HitchDiagnostics.End(\n                        _firstMarkerActivationTiming", StringComparison.Ordinal);
            Assert.IsTrue(rendererCall >= 0 && endToEnd > rendererCall);
            StringAssert.Contains(client, "C2HitchPhase.FirstMarkerActivationEndToEnd");
        }
    }
}
