using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection1RuntimeRegressionTests
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
        public void ISF_V111_C2B_C1_01_MarkerBearingHudProbeIsCacheOnly()
        {
            var render = MethodBody(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "public void RenderFrame(");
            StringAssert.Contains(render, "TryGetBlockingModalCached(out modalReason)");
            StringAssert.Contains(render, "TryGetVisibleMessageHudRectCached(out messageHudRect)");
            Assert.IsFalse(render.Contains("TryGetBlockingModal(out modalReason)", StringComparison.Ordinal));
            Assert.IsFalse(render.Contains("TryGetVisibleMessageHudRect(out messageHudRect)", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C1_02_CachedHudQueriesNeverRunGlobalDiscovery()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/LocalHudPresentationProbe.cs");
            var modal = MethodBody(source, "public bool TryGetBlockingModalCached(");
            var message = MethodBody(source, "public bool TryGetVisibleMessageHudRectCached(");
            Assert.IsFalse(modal.Contains("RefreshBlockingModalCandidates", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("RefreshMessageHudCandidates", StringComparison.Ordinal));
            Assert.IsFalse(modal.Contains("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal));
            StringAssert.Contains(modal, "PruneBlockingModalCandidates();");
            StringAssert.Contains(message, "PruneMessageHudCandidates();");
        }

        [TestMethod]
        public void ISF_V111_C2B_C1_03_IdleWarmupKeepsTypographyScanOffMarkerFrameWithoutBorrowingMaterial()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var warmup = MethodBody(renderer, "public bool WarmColdPathStep(");
            StringAssert.Contains(warmup, "EnsureCanvas();");
            StringAssert.Contains(warmup, "ResolveFallbackTypographyWithoutGlobalDiscovery();");
            Assert.IsFalse(warmup.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_nativeFontMaterial", StringComparison.Ordinal));
            var render = MethodBody(renderer, "public void Render(");
            StringAssert.Contains(render, "if (!_typographyResolved) ResolveFallbackTypographyWithoutGlobalDiscovery();");
            Assert.IsFalse(render.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            StringAssert.Contains(renderer, "PrimeNativeTypographyDuringInitialization()");
        }

        [TestMethod]
        public void ISF_V111_C2B_C1_04_TypographyDoesNotBorrowLiveSharedMaterial()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("_nativeFontMaterial", StringComparison.Ordinal));
            var typography = MethodBody(renderer, "private void EnsureTypography(");
            StringAssert.Contains(typography, "text.font = _nativeFont");
            Assert.IsFalse(typography.Contains("fontSharedMaterial", StringComparison.Ordinal));
            var resolver = MethodBody(renderer, "private void ResolveNativeTypography(");
            Assert.IsFalse(resolver.Contains("fontSharedMaterial", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C1_05_FirstPreferredTmpMeasurementIsDeferredOneFrame()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            StringAssert.Contains(renderer, "public bool InitialPreferredMeasurementDeferred { get; set; }");
            var measure = MethodBody(renderer, "private VisualMeasurement GetOrMeasureVisualFootprint(");
            var defer = measure.IndexOf("if (!view.InitialPreferredMeasurementDeferred)", StringComparison.Ordinal);
            var preferred = measure.IndexOf("view.Label.GetPreferredValues(renderedText)", StringComparison.Ordinal);
            Assert.IsTrue(defer >= 0 && preferred > defer, "Cold TMP preferred measurement must occur only after the one-frame conservative fallback.");
            StringAssert.Contains(measure, "MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(");
        }

        [TestMethod]
        public void ISF_V111_C2B_C1_06_CompatibilityTableRemainsExactlyTwelve()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.AreEqual(12, source.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
        }
    }
}
