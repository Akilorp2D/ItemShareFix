using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection8RuntimeHygieneTests
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

        private static int CountExact(string source, string value)
            => source.Split(new[] { value }, StringSplitOptions.None).Length - 1;

        [TestMethod]
        public void ISF_V111_C2B_C8_01_CommandSnapshotEnumeratesPickerOptionsExactlyOnce()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshot = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            Assert.AreEqual(1, CountExact(snapshot, "foreach (var boxedOption in options)"));
            StringAssert.Contains(snapshot, "_pickerOptionsField.GetValue(picker) is not IEnumerable options");
            StringAssert.Contains(snapshot, "GetCachedCommandOptionAccessor(boxedOption.GetType())");
            StringAssert.Contains(upstream, "CommandOptionAccessorCache");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_02_ClientSweepBuildsOneSnapshotAndReusesLifetimeAndMetadata()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var sweep = MethodBody(client, "private void Sweep(");
            Assert.AreEqual(1, CountExact(sweep, "_upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions)"));
            Assert.AreEqual(0, CountExact(sweep, "_upstream.TryGetCommandChoicePickupIndexes("));
            Assert.AreEqual(0, CountExact(sweep, "_upstream.TryGetCommandChoiceLifetime("));
            StringAssert.Contains(sweep, "ResolveCommandMarkerMetadata(\n                            commandOptions,");
            StringAssert.Contains(sweep, "commandLifetime = commandOptions.Lifetime;");
            StringAssert.Contains(sweep, "exactLifetimeOptionCount = commandOptions.ExactNestedAvailableOptionCount;");
            StringAssert.Contains(sweep, "unresolvedLifetimeOptionCount = commandOptions.UnresolvedAvailableOptionCount;");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_03_CommandSourceNestedAuthorityFallbackAndDisagreementSemanticsRemainExact()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshot = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            var pickup = MethodBody(upstream, "private static bool TryReadPickupIndex(");
            StringAssert.Contains(snapshot, "nestedPickupObject is UniquePickup exactNestedPickup");
            StringAssert.Contains(pickup, "accessor.DirectPickupIndex");
            StringAssert.Contains(pickup, "CommandOptionSourcePolicy.Resolve(");
            StringAssert.Contains(pickup, "disagreement = decision.Disagreement;");
            StringAssert.Contains(snapshot, "sourceDisagreement |= disagreement;");
            Assert.IsFalse(snapshot.Contains("Language.", StringComparison.Ordinal));
            Assert.IsFalse(snapshot.Contains("nameToken", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_04_CommandLifetimeSemanticsAreComputedFromSameSnapshotPass()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshot = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            StringAssert.Contains(snapshot, "if (exactNestedPickup.isTempItem) sawTemporary = true;");
            StringAssert.Contains(snapshot, "else sawPermanent = true;");
            StringAssert.Contains(snapshot, "unresolvedAvailableOptionCount++;");
            StringAssert.Contains(snapshot, "availableOptionCount == 0 || unresolvedAvailableOptionCount > 0");
            StringAssert.Contains(snapshot, "? MarkerLifetimeKind.Unknown");
            StringAssert.Contains(snapshot, "MarkerLifetimePolicy.FromExactOptionKinds(sawTemporary, sawPermanent)");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_05_CommandShareabilityAndClassResolutionReuseSnapshotPickupIndexes()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            const string metadataDeclaration = "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(";
            var declarationIndex = client.IndexOf(metadataDeclaration, StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0);
            var declarationBodyStart = client.IndexOf('{', declarationIndex);
            var snapshotParameter = client.IndexOf("UpstreamBridge.CommandChoiceSnapshot snapshot", declarationIndex, StringComparison.Ordinal);
            Assert.IsTrue(declarationBodyStart > declarationIndex && snapshotParameter > declarationIndex && snapshotParameter < declarationBodyStart);

            var metadata = MethodBody(client, metadataDeclaration);
            StringAssert.Contains(metadata, "var pickupIndexes = snapshot.PickupIndexes;");
            StringAssert.Contains(metadata, "var exactSource = snapshot.ExactSource;");
            StringAssert.Contains(metadata, "_upstream.TryIsShareable(def, out var isShareable)");
            StringAssert.Contains(metadata, "CommandShareabilityPolicy.Evaluate(upstreamShareability)");
            StringAssert.Contains(metadata, "MarkerClassPolicy.ResolveCommandClassForReadablePresentation");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_06_InteractiveWarmupAndLiveRenderContainNoGlobalTypographyDiscovery()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var warmup = MethodBody(renderer, "public bool WarmColdPathStep(");
            var render = MethodBody(renderer, "public void Render(");
            Assert.IsFalse(warmup.Contains("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            StringAssert.Contains(warmup, "ResolveFallbackTypographyWithoutGlobalDiscovery();");
            Assert.IsFalse(render.Contains("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal));
            Assert.IsFalse(render.Contains("ResolveNativeTypography();", StringComparison.Ordinal));
            StringAssert.Contains(render, "ResolveFallbackTypographyWithoutGlobalDiscovery();");
            StringAssert.Contains(renderer, "public void PrimeNativeTypographyDuringInitialization()");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_07_HudInteractiveWarmupIsScanFreeAndInitializationOwnsGlobalDiscovery()
        {
            var probe = ReadSource("src/ItemShareFix.Plugin/LocalHudPresentationProbe.cs");
            var warmup = MethodBody(probe, "public bool WarmColdPathStep(bool refreshIfDue)");
            var init = MethodBody(probe, "public void PrimeGlobalDiscoveryDuringInitialization(");
            Assert.IsFalse(warmup.Contains("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("DiscoverBlockingModalType", StringComparison.Ordinal));
            Assert.IsFalse(warmup.Contains("DiscoverMessageHudType", StringComparison.Ordinal));
            StringAssert.Contains(init, "DiscoverBlockingModalType");
            StringAssert.Contains(init, "DiscoverMessageHudType");
            StringAssert.Contains(init, "DiscoverMessageHudFallback");
            StringAssert.Contains(probe, "public void ObserveHudLifecycle(Component? hud)");
            var modalQuery = MethodBody(probe, "public bool TryGetBlockingModal(out string reason)");
            var messageQuery = MethodBody(probe, "public bool TryGetVisibleMessageHudRect(out MarkerHudRect rect)");
            Assert.IsFalse(modalQuery.Contains("RefreshBlockingModalCandidates", StringComparison.Ordinal));
            Assert.IsFalse(messageQuery.Contains("RefreshMessageHudCandidates", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_08_FieldFixedMarkerOwnershipGuardsRemainIntact()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.IsFalse(renderer.Contains("_firstMarkerViewPool", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("PrimeFirstMarkerViewPool", StringComparison.Ordinal));
            Assert.IsFalse(renderer.Contains("_nativeFontMaterial", StringComparison.Ordinal));
            Assert.IsFalse(MethodBody(renderer, "private void ResolveNativeTypography(").Contains("fontSharedMaterial", StringComparison.Ordinal));
            Assert.IsFalse(MethodBody(renderer, "private void EnsureTypography(").Contains("fontSharedMaterial", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_09_TmpTeardownDeactivatesOwnedRootsBeforeDestroyAndAddsNonSuppressingOwnershipEvidence()
        {
            var renderer = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            var remove = MethodBody(renderer, "private void RemoveView(");
            var active = remove.IndexOf("view.Root.SetActive(false);", StringComparison.Ordinal);
            var destroy = remove.IndexOf("UnityEngine.Object.Destroy(view.Root);", StringComparison.Ordinal);
            Assert.IsTrue(active >= 0 && destroy > active);
            StringAssert.Contains(renderer, "ISF_TMP_TEARDOWN_OWNERSHIP owner=itemsharefix");
            var observe = MethodBody(renderer, "public void ObserveTmpSubMeshOnDisable(");
            StringAssert.Contains(observe, "subMesh.transform.IsChildOf(_canvasObject.transform)");
            StringAssert.Contains(observe, "ISF_TMP_SUBMESH_ONDISABLE owner=itemsharefix");
            Assert.IsFalse(observe.Contains("throw;", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_10_TmpLifecycleProbeIsExactDeclaredAndDoesNotSuppressOnDisable()
        {
            var patches = ReadSource("src/ItemShareFix.Plugin/RuntimePatches.cs");
            var install = MethodBody(patches, "private static void InstallTmpSubMeshLifecycleDiagnosticPatch(");
            StringAssert.Contains(install, "DeclaredParameterless(typeof(TMP_SubMeshUI), \"OnDisable\")");
            StringAssert.Contains(install, "prefix: nameof(TmpSubMeshOnDisableDiagnosticPrefix)");
            StringAssert.Contains(patches,
                "private static void TmpSubMeshOnDisableDiagnosticPrefix(TMP_SubMeshUI __instance)\n"
                + "            => _presentation?.OnTmpSubMeshOnDisable(__instance);");
            Assert.IsFalse(install.Contains("return false", StringComparison.Ordinal));
            Assert.IsFalse(install.Contains("catch", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_11_DisconnectHookCanOnlyBeExactDeclaredOrAbsent()
        {
            var guard = ReadSource("src/ItemShareFix.Plugin/CompatibilityGuard.cs");
            var find = MethodBody(guard, "private static MethodInfo? FindDisconnectMethod(");
            StringAssert.Contains(find, "BindingFlags.DeclaredOnly");
            StringAssert.Contains(find, "method.DeclaringType == type");
            StringAssert.Contains(find, "return null;");
            var probe = MethodBody(guard, "public static CompatibilityResult Probe(");
            StringAssert.Contains(probe, "<none-declared>; authoritative PlayerCharacterMasterController.instances absence sweep remains the cleanup authority");
            var patches = ReadSource("src/ItemShareFix.Plugin/RuntimePatches.cs");
            StringAssert.Contains(patches, "if (compatibility.DisconnectMethod != null)");
            StringAssert.Contains(patches, "Patch(harmony, compatibility.DisconnectMethod, prefix: nameof(NetworkDestroyObservationPrefix));");
        }

        [TestMethod]
        public void ISF_V111_C2B_C8_12_HistoricalC8DeltasNormalizeFailClosedAndCompatibilityTableRemainsTwelve()
        {
            C2HistoricalFreezeReconciliation.AssertCorrection8ResidualFilesCurrentAndCorrection7Normalized();
            C2HistoricalFreezeReconciliation.AssertAllHistoricalC1FilesCurrentAndNormalized();
            var compatibility = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.AreEqual(12, CountExact(compatibility, "new ExactItemIdentity("));
        }
    }
}
