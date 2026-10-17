using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PerformanceOptimizationContractTests
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
        public void ISF_V111_PERF_01_CandidateVersionIsOnePointOnePointOneAndPhaseADiagnosticsAreAbsent()
        {
            StringAssert.Contains(ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs"), "public const string PluginVersion = \"1.1.1\";");
            StringAssert.Contains(ReadSource("Directory.Build.props"), "<Version>1.1.1</Version>");
            StringAssert.Contains(ReadSource("manifest.json"), "\"version_number\": \"1.1.1\"");

            var root = RepositoryRoot().FullName;
            foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                Assert.IsFalse(source.Contains("ISF_PERF_DIAG", StringComparison.Ordinal), "Phase A diagnostic token leaked into production: " + file);
                Assert.IsFalse(source.Contains("FirstPickupPerfPhase", StringComparison.Ordinal), "Phase A diagnostic phase leaked into production: " + file);
                Assert.IsFalse(source.Contains("FirstPickupPerformanceDiagnostics", StringComparison.Ordinal), "Phase A diagnostic helper leaked into production: " + file);
            }
        }

        [TestMethod]
        public void ISF_V111_PERF_02_ParticipantReflectionMetadataIsCachedByRuntimeType()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ParticipantRuntime.cs");
            StringAssert.Contains(source, "MemberInfoCache");
            StringAssert.Contains(source, "ReadableMemberCache");
            StringAssert.Contains(source, "GetReadableMembers(owner.GetType())");
            StringAssert.Contains(source, "PlayerSlotPreferredNames");
            Assert.IsFalse(source.Contains(".FirstOrDefault(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".Where(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".Select(", StringComparison.Ordinal));

            var cachedLookup = MethodBody(source, "private static MemberInfo? GetMemberInfo(");
            StringAssert.Contains(cachedLookup, "MemberInfoCache.TryGetValue(type");
            StringAssert.Contains(cachedLookup, "byName.TryGetValue(name, out var cached)");
            StringAssert.Contains(cachedLookup, "byName[name] = resolved;");

            var readableLookup = MethodBody(source, "private static MemberInfo[] GetReadableMembers(");
            StringAssert.Contains(readableLookup, "ReadableMemberCache.TryGetValue(type");
            StringAssert.Contains(readableLookup, "ReadableMemberCache[type] = cached;");
        }

        [TestMethod]
        public void ISF_V111_PERF_03_LocalParticipantEnumerationCanFillCallerOwnedBufferWithoutPerSweepListAllocation()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ParticipantRuntime.cs");
            var fill = MethodBody(source, "public static void FillLocalMasters(");
            StringAssert.Contains(fill, "result.Clear();");
            StringAssert.Contains(fill, "if (LocalUsersMember == null) return;");
            StringAssert.Contains(fill, "if (master != null && !result.Contains(master)) result.Add(master);");
            StringAssert.Contains(source, "private static readonly MemberInfo? LocalUsersMember = ResolveLocalUsersMember();");

            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var snapshot = MethodBody(client, "private CharacterMaster[] GetLocalMastersSnapshot(");
            StringAssert.Contains(snapshot, "LocalParticipantResolver.FillLocalMasters(_localMasterBuffer);");
            Assert.IsFalse(snapshot.Contains(".Where(", StringComparison.Ordinal));
            Assert.IsFalse(snapshot.Contains(".ToArray(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_PERF_04_UpstreamRecurringReflectionShapesAreCachedWithoutRemovingFailClosedLookup()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            StringAssert.Contains(source, "ReadMemberCache");
            StringAssert.Contains(source, "CommandOptionAccessorCache");
            StringAssert.Contains(source, "ClaimAddMethodCache");
            StringAssert.Contains(source, "ConfigValuePropertyCache");
            StringAssert.Contains(source, "GetCachedCommandOptionAccessor(boxedOption.GetType())");
            StringAssert.Contains(source, "GetCachedReadableMember(typeof(UniquePickup), \"pickupIndex\")");
            StringAssert.Contains(source, "GetCachedClaimAddMethod(claimSet.GetType())");
            StringAssert.Contains(source, "GetCachedConfigValueProperty(entry.GetType())?.GetValue(entry)");

            var optionAccessor = MethodBody(source, "private static CommandOptionAccessor GetCachedCommandOptionAccessor(");
            StringAssert.Contains(optionAccessor, "lock (ReflectionCacheGate)");
            StringAssert.Contains(optionAccessor, "CommandOptionAccessorCache.TryGetValue(type, out var cached)");
            StringAssert.Contains(optionAccessor, "GetCachedReadableMemberLocked(type, \"available\")");
            StringAssert.Contains(optionAccessor, "GetCachedReadableMemberLocked(type, \"pickup\")");
            StringAssert.Contains(optionAccessor, "GetCachedReadableMemberLocked(type, \"pickupIndex\")");
            StringAssert.Contains(optionAccessor, "CommandOptionAccessorCache[type] = cached;");

            var lookup = MethodBody(source, "private static MemberInfo? GetCachedReadableMember(");
            StringAssert.Contains(lookup, "lock (ReflectionCacheGate)");
            StringAssert.Contains(lookup, "GetCachedReadableMemberLocked(type, name)");
            var lockedLookup = MethodBody(source, "private static MemberInfo? GetCachedReadableMemberLocked(");
            StringAssert.Contains(lockedLookup, "type.GetField(name");
            StringAssert.Contains(lockedLookup, "type.GetProperty(name");
            StringAssert.Contains(lockedLookup, "property.GetIndexParameters().Length == 0");
            StringAssert.Contains(lockedLookup, "byName[name] = resolved;");
        }

        [TestMethod]
        public void ISF_V111_PERF_05_ClientSweepReusesOwnedCollectionsAndParticipantStateBuffers()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(source, "private readonly List<PersonalPickupMarker> _nextOrdinaryMarkers");
            StringAssert.Contains(source, "private readonly List<PersonalCommandMarker> _nextCommandMarkers");
            StringAssert.Contains(source, "private readonly HashSet<int> _seenPickups");
            StringAssert.Contains(source, "private readonly HashSet<int> _previousOrdinaryIds");
            StringAssert.Contains(source, "private readonly HashSet<int> _seenCommandCandidateIds");
            StringAssert.Contains(source, "private readonly HashSet<int> _previousCommandIds");
            StringAssert.Contains(source, "private readonly List<int> _staleInstanceIds");
            StringAssert.Contains(source, "private ParticipantState[] _localStateBuffer = Array.Empty<ParticipantState>();");
            StringAssert.Contains(source, "private bool[] _collectedBuffer = Array.Empty<bool>();");
            StringAssert.Contains(source, "private bool?[] _localPickedStateBuffer = Array.Empty<bool?>();");

            var sweep = MethodBody(source, "private void Sweep(");
            StringAssert.Contains(sweep, "_nextOrdinaryMarkers.Clear();");
            StringAssert.Contains(sweep, "_seenPickups.Clear();");
            StringAssert.Contains(sweep, "_previousOrdinaryIds.Clear();");
            StringAssert.Contains(sweep, "_nextCommandMarkers.Clear();");
            StringAssert.Contains(sweep, "_seenCommandCandidateIds.Clear();");
            StringAssert.Contains(sweep, "_previousCommandIds.Clear();");
            Assert.IsFalse(sweep.Contains("new List<PersonalPickupMarker>", StringComparison.Ordinal));
            Assert.IsFalse(sweep.Contains("new List<PersonalCommandMarker>", StringComparison.Ordinal));
            Assert.IsFalse(sweep.Contains("new HashSet<int>", StringComparison.Ordinal));
            Assert.IsFalse(sweep.Contains("localMasters.Select(", StringComparison.Ordinal));
            Assert.IsFalse(sweep.Contains(".Where(x => !", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_PERF_06_ReusedParticipantArraysAreExactLengthAndOverwrittenBeforePolicyUse()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var ensure = MethodBody(source, "private void EnsureParticipantBuffers(");
            StringAssert.Contains(ensure, "if (_localStateBuffer.Length != count) _localStateBuffer = new ParticipantState[count];");
            StringAssert.Contains(ensure, "if (_collectedBuffer.Length != count) _collectedBuffer = new bool[count];");
            StringAssert.Contains(ensure, "if (_localPickedStateBuffer.Length != count) _localPickedStateBuffer = new bool?[count];");

            var sweep = MethodBody(source, "private void Sweep(");
            StringAssert.Contains(sweep, "localStates[i] = LocalParticipantResolver.ClassifyLocal(localMasters[i]);");
            StringAssert.Contains(sweep, "collected[i] = hasCollected;");
            StringAssert.Contains(sweep, "localPickedStates[i] = picked;");
            StringAssert.Contains(sweep, "localPickedStates[i] = null;");
        }

        [TestMethod]
        public void ISF_V111_PERF_07_DiagnosticLoggingDisabledShortCircuitsExpensiveMarkerFormattingPaths()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            foreach (var declaration in new[]
            {
                "private void OnMarkerRendered(",
                "private void LogOrdinaryCleanup(",
                "private void LogCommandCleanup("
            })
            {
                var body = MethodBody(source, declaration);
                var guard = body.IndexOf("if (!_config.DiagnosticLogging.Value) return;", StringComparison.Ordinal);
                Assert.IsTrue(guard >= 0, "Diagnostic fast-path guard missing: " + declaration);
                var firstLog = body.IndexOf("LogInfo(", StringComparison.Ordinal);
                Assert.IsTrue(firstLog < 0 || guard < firstLog, "Diagnostic formatting/log call occurs before disabled-path guard: " + declaration);
            }
        }

        [TestMethod]
        public void ISF_V111_PERF_08_GlobalTypographyDiscoveryRemainsBoundedAndScanLoopAvoidsLowercaseAllocations()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs");
            Assert.AreEqual(1, source.Split(new[] { "Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(source, "public void PrimeNativeTypographyDuringInitialization()");
            StringAssert.Contains(source, "if (!_typographyResolved) ResolveFallbackTypographyWithoutGlobalDiscovery();");
            var resolver = MethodBody(source, "private void ResolveNativeTypography(");
            StringAssert.Contains(resolver, "_typographyResolved = true;");
            StringAssert.Contains(resolver, "Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()");
            Assert.IsFalse(resolver.Contains("ToLowerInvariant()", StringComparison.Ordinal));
            StringAssert.Contains(resolver, "StringComparison.OrdinalIgnoreCase");
        }

        [TestMethod]
        public void ISF_V111_PERF_09_CriticalGameplayNetworkAndConfigurationOwnersRemainOutsideC1ImplementationDelta()
        {
            var root = RepositoryRoot().FullName;
            var unchanged = new[]
            {
                "src/ItemShareFix.Plugin/ServerCoordinator.cs",
                "src/ItemShareFix.Plugin/RuntimePatches.cs",
                "src/ItemShareFix.Plugin/PluginConfig.cs",
                "src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs",
                "src/ItemShareFix.Plugin/MarkerRiskOfOptionsLocalization.cs",
                "src/ItemShareFix.Core/TemporarySharingPolicy.cs"
            };
            var forbidden = new[] { "ISF_PERF_DIAG", "1.1.1 Phase A" };
            foreach (var relative in unchanged)
            {
                var source = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                foreach (var token in forbidden)
                    Assert.IsFalse(source.Contains(token, StringComparison.Ordinal), relative + " contains forbidden diagnostic token " + token);
            }
        }
    }
}
