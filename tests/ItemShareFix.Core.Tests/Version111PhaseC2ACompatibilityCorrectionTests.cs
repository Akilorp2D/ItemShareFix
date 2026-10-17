using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2ACompatibilityCorrectionTests
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
        public void ISF_V111_C2A_C1_01_ExactCompatibilityTableContainsOnlyProvenBelmontAndHunkProgressionIdentities()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            foreach (var identity in new[]
            {
                "new ExactItemIdentity(\"BelmontKeyFragment\", \"ROB_BELMONT_KEYFRAGMENT_NAME\")",
                "new ExactItemIdentity(\"GVirusSample\", \"ROB_HUNK_G_VIRUS_SAMPLE_NAME\")",
                "new ExactItemIdentity(\"TVirusSample\", \"ROB_HUNK_T_VIRUS_SAMPLE_NAME\")",
                "new ExactItemIdentity(\"CVirusSample\", \"ROB_HUNK_C_VIRUS_SAMPLE_NAME\")",
                "new ExactItemIdentity(\"UroborosSample\", \"ROB_HUNK_URO_SAMPLE_NAME\")",
                "new ExactItemIdentity(\"PlagasSample\", \"ROB_HUNK_PLAGAS_SAMPLE_NAME\")",
                "new ExactItemIdentity(\"SpadeKeycard\", \"ROB_HUNK_SPADE_KEYCARD_NAME\")",
                "new ExactItemIdentity(\"ClubKeycard\", \"ROB_HUNK_CLUB_KEYCARD_NAME\")",
                "new ExactItemIdentity(\"HeartKeycard\", \"ROB_HUNK_HEART_KEYCARD_NAME\")",
                "new ExactItemIdentity(\"DiamondKeycard\", \"ROB_HUNK_DIAMOND_KEYCARD_NAME\")",
                "new ExactItemIdentity(\"GoldKeycard\", \"ROB_HUNK_GOLD_KEYCARD_NAME\")",
                "new ExactItemIdentity(\"MasterKeycard\", \"ROB_HUNK_MASTER_KEYCARD_NAME\")",
            })
                StringAssert.Contains(source, identity);

            Assert.AreEqual(12, source.Split(new[] { "new ExactItemIdentity(" }, StringSplitOptions.None).Length - 1);
            Assert.IsFalse(source.Contains("new ExactItemIdentity(\"BelmontKey\",", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("new ExactItemIdentity(\"Plagas\", \"ROB_HUNK_PLAGAS_NAME\")", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Wristband", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_V111_C2A_C2_01_PlagasSampleExactExclusionDoesNotConflateHiddenPlagas()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");

            // Exact HUNK 1.5.1 evidence: PlagasSample is non-hidden, participates in the
            // provider pickup-handling chain, and is spawned as a world pickup on body death.
            StringAssert.Contains(source,
                "new ExactItemIdentity(\"PlagasSample\", \"ROB_HUNK_PLAGAS_SAMPLE_NAME\")");

            // Separate provider-internal Plagas remains hidden and is not a proven world pickup.
            Assert.IsFalse(source.Contains(
                "new ExactItemIdentity(\"Plagas\", \"ROB_HUNK_PLAGAS_NAME\")",
                StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_02_MatcherRequiresExactInternalNameAndNameTokenWithoutTierOrDisplayFallback()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            var matcher = MethodBody(source, "private static bool IsExcluded(ItemDef itemDef)");
            StringAssert.Contains(matcher, "string.Equals(name, identity.Name, StringComparison.Ordinal)");
            StringAssert.Contains(matcher, "string.Equals(nameToken, identity.NameToken, StringComparison.Ordinal)");
            StringAssert.Contains(matcher, "&&");
            Assert.IsFalse(source.Contains("Language.GetString", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ItemTier", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Contains(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("StartsWith(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_03_ItemShareAttemptGrantBypassesPreClaimForKnownProgressionPickup()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/RuntimePatches.cs");
            var method = MethodBody(source, "private static bool ItemShareAttemptGrantPrefix(");
            StringAssert.Contains(method, "_server.KnownProgressionPickupExclusionsEnabled");
            var compatibility = method.IndexOf("KnownProgressionPickupCompatibility.IsExcluded(self)", StringComparison.Ordinal);
            var temporary = method.IndexOf("var pickup = self.pickup;", StringComparison.Ordinal);
            Assert.IsTrue(compatibility >= 0);
            Assert.IsTrue(temporary > compatibility);
            StringAssert.Contains(method, "InvokeSuppliedOriginal(__args[0], self, __args[2]);");
            var afterCompatibility = method.Substring(compatibility, temporary - compatibility);
            StringAssert.Contains(afterCompatibility, "return false;");
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_04_ServerLedgerDefenseRejectsKnownProgressionBeforeUpstreamShareabilityAndClaimCreation()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ServerCoordinator.cs");
            var method = MethodBody(source, "public void BeginDistribution(");
            var compatibility = method.IndexOf("KnownProgressionPickupCompatibility.IsExcluded(pickup)", StringComparison.Ordinal);
            var shareability = method.IndexOf("_upstream.IsShareable(pickup)", StringComparison.Ordinal);
            var push = method.IndexOf("_distribution.Push(context);", StringComparison.Ordinal);
            var claims = method.IndexOf("EnsureClaims(context);", StringComparison.Ordinal);
            Assert.IsTrue(compatibility >= 0);
            Assert.IsTrue(shareability > compatibility);
            Assert.IsTrue(push > compatibility);
            Assert.IsTrue(claims > compatibility);
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_05_ClientPresentationRejectsKnownProgressionFromVisibilityInteractionAndMarkerOwnership()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(MethodBody(source, "public bool TryEvaluateLocalCollected("),
                "KnownProgressionPickupCompatibility.IsExcluded(pickup)");
            StringAssert.Contains(MethodBody(source, "public bool ShouldSuppressLocalPickupInteraction("),
                "KnownProgressionPickupCompatibility.IsExcluded(pickup)");

            var sweep = MethodBody(source, "private void Sweep(");
            var compatibility = sweep.IndexOf("KnownProgressionPickupCompatibility.IsExcluded(pickup)", StringComparison.Ordinal);
            var shareability = sweep.IndexOf("_upstream.IsShareable(pickup)", StringComparison.Ordinal);
            Assert.IsTrue(compatibility >= 0);
            Assert.IsTrue(shareability > compatibility);
            StringAssert.Contains(sweep, "_markerRegistry.Remove(PersonalMarkerKind.OrdinaryPickup, instanceId);");
            StringAssert.Contains(sweep, "ReleaseGate(instanceId);");
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_06_OrdinaryBossSharingHasNoBlanketSuppressionRule()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.IsFalse(source.Contains("Boss", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ItemTier", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("TierDef", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("PickupColor", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_07_CompatibilityHasNoHardProviderAssemblyDependency()
        {
            var project = ReadSource("src/ItemShareFix.Plugin/ItemShareFix.Plugin.csproj");
            Assert.IsFalse(project.Contains("BelmontMod", StringComparison.Ordinal));
            Assert.IsFalse(project.Contains("HunkMod", StringComparison.Ordinal));
            Assert.IsFalse(project.Contains("rob-Belmont", StringComparison.Ordinal));
            Assert.IsFalse(project.Contains("rob-HUNK", StringComparison.Ordinal));

            var source = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.IsFalse(source.Contains("Assembly.Load", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Type.GetType", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Chainloader", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2A_C1_08_C2HitchDiagnosticContractRemainsPresentAndCompatibilityDoesNotClaimAHitchFix()
        {
            var diagnostic = ReadSource("src/ItemShareFix.Plugin/C2HitchDiagnostics.cs");
            StringAssert.Contains(diagnostic, "ISF_C2_HITCH_DIAG");
            StringAssert.Contains(diagnostic, "private const int MaxRecords = 64;");

            var compatibility = ReadSource("src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs");
            Assert.IsFalse(compatibility.Contains("hitch", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(compatibility.Contains("Stopwatch", StringComparison.Ordinal));
            Assert.IsFalse(compatibility.Contains("GC.CollectionCount", StringComparison.Ordinal));
        }
    }
}
