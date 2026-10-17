using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection11CommandOptionInstrumentationTests
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
        public void ISF_V111_C2B_C11_01_CommandProfileIsBoundedDedicatedAndGloballyIndependent()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(client, "internal static class CommandOptionResolutionDiagnostics");
            StringAssert.Contains(client, "private const int MaxSamples = 5;");
            StringAssert.Contains(client, "private static readonly Sample[] Records = new Sample[MaxSamples];");
            StringAssert.Contains(client, "if (!_enabled || _active.Active || _recordCount >= MaxSamples) return;");
            StringAssert.Contains(client, "ISF_COMMAND_OPTION_PROFILE");
            Assert.AreEqual(0, CountExact(MethodBody(client, "internal static void Begin(int pickerInstanceId)"), "C2HitchDiagnostics"));
            Assert.AreEqual(0, CountExact(MethodBody(client, "internal static void Complete()"), "C2HitchDiagnostics"));
        }

        [TestMethod]
        public void ISF_V111_C2B_C11_02_ProfileSchemaContainsAllRequiredTimingAndContextFields()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var flush = MethodBody(client, "internal static void FlushIfReady(ManualLogSource log)");
            foreach (var token in new[]
            {
                "sample=", "ordinal=", "frame=", "picker_instance_id=", "elapsed_us=", "snapshot_us=",
                "snapshot_preflight_us=", "option_enumeration_member_resolution_us=", "pickup_index_source_resolution_us=",
                "dedup_finalization_us=", "metadata_us=", "pickup_metadata_catalog_classification_us=",
                "upstream_shareability_bridge_us=", "presentation_finalization_us=", "options_visited=",
                "available_options=", "resolved_pickup_indexes=", "exact_nested_available=", "unresolved_available=",
                "duplicate_pickups=", "upstream_shareability_calls=", "gc0_delta=", "gc1_delta=", "gc2_delta="
            }) StringAssert.Contains(flush, token);
        }

        [TestMethod]
        public void ISF_V111_C2B_C11_03_NoPerOptionDiagnosticLoggingOrFormattingWasIntroduced()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshot = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var metadata = MethodBody(client, "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            Assert.IsFalse(snapshot.Contains("ISF_COMMAND_OPTION_PROFILE", StringComparison.Ordinal));
            Assert.IsFalse(metadata.Contains("ISF_COMMAND_OPTION_PROFILE", StringComparison.Ordinal));
            Assert.IsFalse(snapshot.Contains("ToString(CultureInfo", StringComparison.Ordinal));
            Assert.IsFalse(metadata.Contains("ToString(CultureInfo", StringComparison.Ordinal));
            Assert.AreEqual(1, CountExact(client, "[ItemShareFix] ISF_COMMAND_OPTION_PROFILE"));
        }

        [TestMethod]
        public void ISF_V111_C2B_C11_04_SnapshotProfilingSeparatesPreflightEnumerationSourceAndDedupFinalization()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshot = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            StringAssert.Contains(snapshot, "var profilePreflightStarted = profileSnapshotStarted;");
            StringAssert.Contains(snapshot, "var uniquePickupIndexMember = GetCachedReadableMember(typeof(UniquePickup), \"pickupIndex\");");
            StringAssert.Contains(snapshot, "var profileLoopTicks = profileLoopCompleted - profileLoopStarted;");
            StringAssert.Contains(snapshot, "profileOptionEnumerationMemberResolutionTicks = profileLoopTicks");
            StringAssert.Contains(snapshot, "var profileSourceStarted = profileActive ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;");
            StringAssert.Contains(snapshot, "profilePickupIndexSourceResolutionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - profileSourceStarted;");
            StringAssert.Contains(snapshot, "var profileDedupStarted = profileActive ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;");
            StringAssert.Contains(snapshot, "profileDedupFinalizationTicks += profileCompleted - profileFinalizationStarted;");
            StringAssert.Contains(snapshot, "profileDuplicatePickupCount++;");
            StringAssert.Contains(snapshot, "CommandOptionResolutionDiagnostics.RecordSnapshot(");
        }

        [TestMethod]
        public void ISF_V111_C2B_C11_05_MetadataProfilingSeparatesCatalogShareabilityAndPresentationFinalization()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var metadata = MethodBody(client, "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            var catalog = metadata.IndexOf("PickupCatalog.GetPickupDef(pickupIndex)", StringComparison.Ordinal);
            var metadataResolve = metadata.IndexOf("ResolvePickupMarkerMetadata(def)", catalog, StringComparison.Ordinal);
            var shareability = metadata.IndexOf("_upstream.TryIsShareable(def, out var isShareable)", metadataResolve, StringComparison.Ordinal);
            var policy = metadata.IndexOf("CommandShareabilityPolicy.Evaluate(upstreamShareability)", shareability, StringComparison.Ordinal);
            var presentation = metadata.IndexOf("MarkerClassPolicy.ResolveCommandClassForReadablePresentation", policy, StringComparison.Ordinal);
            Assert.IsTrue(catalog >= 0 && metadataResolve > catalog && shareability > metadataResolve && policy > shareability && presentation > policy);
            StringAssert.Contains(metadata, "profileUpstreamShareabilityCalls++;");
            StringAssert.Contains(metadata, "CommandOptionResolutionDiagnostics.RecordMetadata(");
        }

        [TestMethod]
        public void ISF_V111_C2B_C11_06_CommandSemanticOrderAndSingleSnapshotInvariantRemainFrozen()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var sweep = MethodBody(client, "private void Sweep(");
            var snapshot = sweep.IndexOf("_upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions)", StringComparison.Ordinal);
            var metadata = sweep.IndexOf("ResolveCommandMarkerMetadata(\n                            commandOptions,", snapshot, StringComparison.Ordinal);
            var lifetime = sweep.IndexOf("commandLifetime = commandOptions.Lifetime;", metadata, StringComparison.Ordinal);
            var eligibility = sweep.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(commandLifetime, _config.ShareTemporaryItems.Value)", lifetime, StringComparison.Ordinal);
            Assert.AreEqual(1, CountExact(sweep, "_upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions)"));
            Assert.IsTrue(snapshot >= 0 && metadata > snapshot && lifetime > metadata && eligibility > lifetime);

            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var snapshotBody = MethodBody(upstream, "public bool TryBuildCommandChoiceSnapshot(");
            var pickupSource = MethodBody(upstream, "private static bool TryReadPickupIndex(");
            StringAssert.Contains(snapshotBody, "nestedPickupObject is UniquePickup exactNestedPickup");
            StringAssert.Contains(pickupSource, "CommandOptionSourcePolicy.Resolve(");
            StringAssert.Contains(snapshotBody, "availableOptionCount == 0 || unresolvedAvailableOptionCount > 0");
            StringAssert.Contains(snapshotBody, "exactSource: result.Count > 0 && sawNested && !sawFallback");
        }
    }
}
