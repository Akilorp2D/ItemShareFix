using System;
using System.IO;
using System.Security.Cryptography;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class C21A1TemporarySharingPolicyTests
    {
        private static string ReadSource(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            Assert.Fail(relativePath + " was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }

        private static string Plugin(string fileName) => ReadSource("src/ItemShareFix.Plugin/" + fileName);
        private static string Core(string fileName) => ReadSource("src/ItemShareFix.Core/" + fileName);

        private static string SourceSha256(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    using var sha = SHA256.Create();
                    return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(candidate)));
                }
                directory = directory.Parent;
            }
            Assert.Fail(relativePath + " was not found while calculating SHA-256.");
            return string.Empty;
        }

        [TestMethod]
        public void ISF_R1_C21_A1_01_CanonicalGeneralShareTemporaryItemsConfig()
        {
            var source = Plugin("PluginConfig.cs");
            StringAssert.Contains(source, "ShareTemporaryItems = config.Bind(\"General\", \"ShareTemporaryItems\", false,");
            StringAssert.Contains(source, "public ConfigEntry<bool> ShareTemporaryItems { get; }");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_02_ShareTemporaryItemsDefaultIsTrue()
            => StringAssert.Contains(Plugin("PluginConfig.cs"), "config.Bind(\"General\", \"ShareTemporaryItems\", false,");

        [TestMethod]
        public void ISF_R1_C21_A1_03_ShareTemporaryItemsHasExactlyOneConfigBind()
        {
            var source = Plugin("PluginConfig.cs");
            Assert.AreEqual(1, source.Split(new[] { "config.Bind(\"General\", \"ShareTemporaryItems\"" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_R1_C21_A1_04_RiskOfOptionsUsesCanonicalShareTemporaryConfigEntry()
        {
            var source = Plugin("OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "config.ShareTemporaryItems, log");
            Assert.IsFalse(source.Contains("ShareTemporaryItemsMirror"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_05_RiskOfOptionsShareTemporaryIsCheckBoxOption()
            => StringAssert.Contains(Plugin("OptionalRiskOfOptionsIntegration.cs"), "\"RiskOfOptions.Options.CheckBoxOption\", config.ShareTemporaryItems, log");

        [TestMethod]
        public void ISF_R1_C21_A1_06_RiskOfOptionsVisibleOptionCountIsTwentySeven()
        {
            var source = Plugin("OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "public const int CurrentMarkerOptionCount = 27;");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_07_EnglishShareTemporaryLabelIsHumanReadable()
            => StringAssert.Contains(Plugin("MarkerRiskOfOptionsLocalization.cs"), "[\"ShareTemporaryItems\"] = A(\"Share temporary items\"");

        [TestMethod]
        public void ISF_R1_C21_A1_08_RussianShareTemporaryTextDataIsPresent()
        {
            var source = Plugin("MarkerRiskOfOptionsLocalization.cs");
            StringAssert.Contains(source, "Раздавать временные предметы");
            StringAssert.Contains(source, "ванильное поведение");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_09_ShareTemporaryIsIndependentFromPersonalMarkersEnabled()
        {
            var config = Plugin("PluginConfig.cs");
            StringAssert.Contains(config, "BindPresentationInvalidation(ShareTemporaryItems)");
            Assert.IsFalse(config.Contains("ShareTemporaryItems = PersonalMarkersEnabled"));
            Assert.IsFalse(config.Contains("PersonalMarkersEnabled = ShareTemporaryItems"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_10_ShareTemporaryIsIndependentFromMasterEnabled()
        {
            var config = Plugin("PluginConfig.cs");
            Assert.IsFalse(config.Contains("ShareTemporaryItems = Enabled"));
            Assert.IsFalse(config.Contains("Enabled = ShareTemporaryItems"));
            StringAssert.Contains(config, "Enabled = config.Bind(\"General\", \"Enabled\", true,");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_11_NoItemShareTierConfigEntryIsModifiedByC21Policy()
        {
            var runtime = Plugin("RuntimePatches.cs") + Plugin("ServerCoordinator.cs") + Core("TemporarySharingPolicy.cs");
            Assert.IsFalse(runtime.Contains("_shareTier"));
            Assert.IsFalse(runtime.Contains("ShareTier"));
            Assert.IsFalse(runtime.Contains("ItemTier"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_12_RuntimeUsesExactUniquePickupIsTempItemPredicate()
        {
            var runtime = Plugin("RuntimePatches.cs");
            StringAssert.Contains(runtime, "var pickup = self.pickup;");
            StringAssert.Contains(runtime, "var isTemporary = pickup.isTempItem;");
            StringAssert.Contains(runtime, "var selectedPickup = option.pickup;");
            StringAssert.Contains(runtime, "selectedPickup.isTempItem");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_13_NoTemporaryNamePrefabIndexOrTierHeuristicIntroduced()
        {
            var runtime = Plugin("RuntimePatches.cs") + Core("TemporarySharingPolicy.cs");
            Assert.IsFalse(runtime.Contains("pickupIndex.ToString"));
            Assert.IsFalse(runtime.Contains("prefab"));
            Assert.IsFalse(runtime.Contains("name.Contains"));
            Assert.IsFalse(runtime.Contains("ItemTier"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_14_OrdinaryTemporaryOffSelectsVanillaBypass()
            => Assert.IsTrue(TemporarySharingPolicy.ShouldUseVanillaBypass(true, false, false));

        [TestMethod]
        public void ISF_R1_C21_A1_15_OrdinaryPermanentOffStaysItemShare()
            => Assert.IsFalse(TemporarySharingPolicy.ShouldUseVanillaBypass(false, false, false));

        [TestMethod]
        public void ISF_R1_C21_A1_16_OrdinaryTemporaryOnStaysItemShare()
            => Assert.IsFalse(TemporarySharingPolicy.ShouldUseVanillaBypass(true, true, false));

        [TestMethod]
        public void ISF_R1_C21_A1_17_OrdinaryOffBypassPrecedesItemShareDistributionEntry()
        {
            var runtime = Plugin("RuntimePatches.cs");
            var attemptPatch = runtime.IndexOf("OnAttemptGrant\", 3, typeof(void), isStatic: true), prefix: nameof(ItemShareAttemptGrantPrefix)", StringComparison.Ordinal);
            var grantIndividualPatch = runtime.IndexOf("GrantIndividual\", 5, typeof(void)", StringComparison.Ordinal);
            var beginDistribution = runtime.IndexOf("_server.BeginDistribution(__args, instant: false);", StringComparison.Ordinal);
            Assert.IsTrue(attemptPatch >= 0 && grantIndividualPatch > attemptPatch && beginDistribution > attemptPatch);
        }

        [TestMethod]
        public void ISF_R1_C21_A1_18_OrdinaryOffInvokesSuppliedOriginalDelegate()
        {
            var runtime = Plugin("RuntimePatches.cs");
            StringAssert.Contains(runtime, "InvokeSuppliedOriginal(__args[0], self, __args[2]);");
            StringAssert.Contains(runtime, "candidate is not Delegate original");
            StringAssert.Contains(runtime, "original.DynamicInvoke(arguments);");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_19_CommandTemporaryOffBypassesBeforeChoicesSemantics()
        {
            var runtime = Plugin("RuntimePatches.cs");
            StringAssert.Contains(runtime, "InvokeSuppliedOriginal(__args[0], picker, choiceIndex);");
            StringAssert.Contains(runtime, "ContainsInstanceId(_itemShareChoicesField, instanceId)");
            StringAssert.Contains(runtime, "\"vanilla-bypass\", \"prechoices\"");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_20_CommandTemporaryOnStaysItemShare()
            => Assert.IsFalse(TemporarySharingPolicy.ShouldUseVanillaBypass(true, true, false));

        [TestMethod]
        public void ISF_R1_C21_A1_21_CommandPermanentOffStaysItemShare()
            => Assert.IsFalse(TemporarySharingPolicy.ShouldUseVanillaBypass(false, false, false));

        [TestMethod]
        public void ISF_R1_C21_A1_22_CommandBypassSuppressesItemShareFixCompletionPostfix()
        {
            var runtime = Plugin("RuntimePatches.cs");
            StringAssert.Contains(runtime, "__state = true;");
            StringAssert.Contains(runtime, "if (__state || _server == null) return;");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_23_OffPathSkipsItemShareFixDistributionAndDeferredCreation()
        {
            var runtime = Plugin("RuntimePatches.cs");
            var ordinaryBypass = runtime.IndexOf("InvokeSuppliedOriginal(__args[0], self, __args[2]);", StringComparison.Ordinal);
            var ordinarySkip = runtime.IndexOf("return false;", ordinaryBypass, StringComparison.Ordinal);
            var beginDistribution = runtime.IndexOf("_server.BeginDistribution", StringComparison.Ordinal);
            Assert.IsTrue(ordinaryBypass >= 0 && ordinarySkip > ordinaryBypass && beginDistribution > ordinarySkip);
        }

        [TestMethod]
        public void ISF_R1_C21_A1_24_DeferredPayloadStillStoresFullBoxedUniquePickup()
        {
            var server = Plugin("ServerCoordinator.cs");
            StringAssert.Contains(server, "public object? BoxedUniquePickup;");
            StringAssert.Contains(server, "BoxedUniquePickup = pickup.pickup,");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_25_DeferredReplayStillUsesItemShareGiveDirectPath()
        {
            var upstream = Plugin("UpstreamBridge.cs");
            StringAssert.Contains(upstream, "public bool GiveDeferred(Inventory inventory, PickupDef pickupDef, object boxedUniquePickup)");
            StringAssert.Contains(upstream, "_giveDirectMethod.Invoke(null, new[] { (object)inventory, pickupDef, boxedUniquePickup })");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_26_NoGiveItemPermanentIntroducedForTemporaryPolicy()
        {
            var c21 = Plugin("RuntimePatches.cs") + Plugin("ServerCoordinator.cs") + Core("TemporarySharingPolicy.cs");
            Assert.IsFalse(c21.Contains("GiveItemPermanent"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_27_NoSyntheticGrantContextIntroducedInC21Bypass()
        {
            var runtime = Plugin("RuntimePatches.cs") + Core("TemporarySharingPolicy.cs");
            Assert.IsFalse(runtime.Contains("GrantContext"));
            Assert.IsFalse(runtime.Contains("attemptGrant"));
        }

        [TestMethod]
        public void ISF_R1_C21_A1_28_MarkerPresentationFilesRemainParentByteIdentical()
        {
            var expectedProductionHashes = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["src/ItemShareFix.Core/BlockingModalLifecyclePolicy.cs"] = "E592D5E521EBF075D580AB592546130EA18D64E818B63A6BE102AACC06FBE57C",
                ["src/ItemShareFix.Core/ClaimLedger.cs"] = "DE8EAF1D27B156E85357C6FE5B31DF95D7A80370DD397E3C0ABD3482D0CD43A9",
                ["src/ItemShareFix.Core/CommandOptionSourcePolicy.cs"] = "D6981FAAC4EDDC59F34B7F37FEF8BF68ED10F9E71EEF2401B9B61865181FBD0D",
                ["src/ItemShareFix.Core/CommandShareabilityPolicy.cs"] = "E145FDDC09FD78CCC7EB2ED2BE5097831D29EE9D03C25F394E101E6EDB170A5E",
                ["src/ItemShareFix.Core/DisconnectConfirmationPolicy.cs"] = "B567FC3906BD0BE4FC917CD245E9CFA6F92EAEA5E4F62161F2EC551E12ED7217",
                ["src/ItemShareFix.Core/GenerationProbeGate.cs"] = "D8FB27191427D2779098DDE290151676BEE78B5149A538F192C478DF75DAEF98",
                ["src/ItemShareFix.Core/ImmediateGivePolicy.cs"] = "7D87451897EA90FF8220DA70CE49975F3EEBC70335B5BA639B4D3AED40F87D75",
                ["src/ItemShareFix.Core/ItemShareActiveGatePolicy.cs"] = "80AB87F77E0105C9B14A367FD07771D44E4D4AA7BBADC20C17F9ED37263C519A",
                ["src/ItemShareFix.Core/ItemShareFix.Core.csproj"] = "61B633AAD363DDDD276052B19718DA7E857726BFAD017C72947C0CF139738F0A",
                ["src/ItemShareFix.Core/LocalCommandPresentationPolicy.cs"] = "8368A8296193FD2A677F4D39AEE809C12BF6539C0BEC9222067136A53FF5A13E",
                ["src/ItemShareFix.Core/LocalParticipantPresentationPolicy.cs"] = "EB383CC66A7890AAF1A508A02E8C2C1D517F96E0275C8C34E18F9DF19BF0187B",
                ["src/ItemShareFix.Core/LocalPickupSuppressionPolicy.cs"] = "47E99425B7E39422D5CF1304E31853698CE903CD3E8FDB24BF8B8231E3A948BA",
                ["src/ItemShareFix.Core/MarkerAdaptiveLodPolicy.cs"] = "4A02358631C8D0CE2031B6A5C88BF6050D657DB829D2AD058293A52D283AA155",
                ["src/ItemShareFix.Core/MarkerCategorySummaryPolicy.cs"] = "90803265938F5DF2DA885E2FF6975F5B178470175F3DF9D1A171609A1F65C182",
                ["src/ItemShareFix.Core/MarkerClassPolicy.cs"] = "A747046392D219F18246B84470D018E6B7106502CB4A85B3CA44CC54935E823D",
                ["src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs"] = "F3C606253F9381AE2C30DB338D29A0C9DFE9E374DBFE9CE2C5E12CB8713E3B10",
                ["src/ItemShareFix.Core/MarkerDenseAreaSummaryPolicy.cs"] = "2B20C88C7D5D37DC5F342023B90955ACC3661827A9523DAA6B02161666F0D4CE",
                ["src/ItemShareFix.Core/MarkerDensityPolicy.cs"] = "3407CC095FA57F2D17B3579D38DB4C9EC7453EFB25C53C6052B15B95A2414850",
                ["src/ItemShareFix.Core/MarkerDirectionalAggregationPolicy.cs"] = "08D16F0AE627840A398F1A1FBAD0B888D518323805D2EEDEDE59A7173A5D60E4",
                ["src/ItemShareFix.Core/MarkerFramePipelinePolicy.cs"] = "858460E829D8FC075CD70CB338F389BA031534CB291A70F7F2CA6CA39441DD06",
                ["src/ItemShareFix.Core/MarkerHudNavigationPolicy.cs"] = "437E1B4DB64F397A0571213A2236889A64C6C37D54DE7D61A0AE57B666BBFB50",
                ["src/ItemShareFix.Core/MarkerLifetimePolicy.cs"] = "565B016B003AC02EA911C6712D223AF56C7ECEC7FA1904A23063E11F08C0103A",
                ["src/ItemShareFix.Core/MarkerPlacementStabilityPolicy.cs"] = "D5A93D949049326F011D0AD039261FB4D2E855BA16FCC53EC26D2CA0C32612A8",
                ["src/ItemShareFix.Core/MarkerPresentationPolicy.cs"] = "3D94205F9D9663C6AEBAF2E903F6812A582DF21A74A13533293130FC5B80A8DC",
                ["src/ItemShareFix.Core/MarkerProjectionRelativePlacementPolicy.cs"] = "1700F68F1EF9342EB4A39590A76C388B34DA390EAD0F4CE22DF4B86453CD7537",
                ["src/ItemShareFix.Core/MarkerRuntimeHotPathPolicy.cs"] = "9658CCB0CCD7135FC5B65678CB2FABCC0B56FA7309DD417EE9A29D433A0D8959",
                ["src/ItemShareFix.Core/MarkerTextLocalization.cs"] = "2C6E499A314DB56D16F0ACE7AE1410D0CB5FCB1C0DC5392193F3FA80B9F00BF1",
                ["src/ItemShareFix.Core/MarkerWorldClusterPolicy.cs"] = "B6D274B0B49864D06201E73701C097DE810E053E0C1CA85EB7F443CE4AE55CFB",
                ["src/ItemShareFix.Core/Model.cs"] = "16FBF6E0C6F1C25C60BD5FE73B7833746DE448E7498D703675D4F6AC0A143C44",
                ["src/ItemShareFix.Core/ProjectionPolicy.cs"] = "E0948F079F3F670F4887964B13C80FECF33C3761E8D09B845C38BFD83EC4AA6F",
                ["src/ItemShareFix.Core/RemoteOperationSignalPolicy.cs"] = "D4C33552BA840C0D0EC87BC742BA96829E1AEC62B68749E8A7DB4C2B07046936",
                ["src/ItemShareFix.Core/TemporarySharingPolicy.cs"] = "E9540487B1901827E3012AE5D7BA3A2C9C4C110EA97010245D4E54D236A85145",
                ["src/ItemShareFix.Plugin/ClientPresentation.cs"] = "29067DD6154199D4E9EA213FC3A8835CAFC73420FB4E88E6E9E17D4D38B57D79",
                ["src/ItemShareFix.Plugin/CompatibilityGuard.cs"] = "C2B71BED8EEE689DAD4AE54EE0880527DBBC7A8F9F1C2ED8F2968BEE21F8DDB1",
                ["src/ItemShareFix.Plugin/ItemShareFix.Plugin.csproj"] = "B4A688418740D84D01FA23FB3D8AE410B4B965689C7FFD46F19043C81860781D",
                ["src/ItemShareFix.Plugin/ItemShareFixPlugin.cs"] = "85F1019CAF871DA0C7E466D23994030C6C2F138CCEF2E4398A6B735081DF010B",
                ["src/ItemShareFix.Plugin/LocalHudPresentationProbe.cs"] = "71F8A4B66532A542D153870770F9E9B40CA7EB8DBE387A282E9C066DEB7D28FE",
                ["src/ItemShareFix.Plugin/MarkerRiskOfOptionsLocalization.cs"] = "EBB72984DD1888AACA01712EE98FCE639A656F0E7AAF4074A06370A14434E043",
                ["src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs"] = "C3263B539FED87B3356FCFA8A80DA525707E95583BCA201A6B4CEAC9DD463AB1",
                ["src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs"] = "269649CB98EF42783EAA90E236AEEAACE1FD1F2FA16BF33AEF8C2D536ACC5DB2",
                ["src/ItemShareFix.Plugin/ParticipantRuntime.cs"] = "4570B1E44C5A02AC8F71296D5A40477C6503D45D64B4786162BE15047C69F9BE",
                ["src/ItemShareFix.Plugin/PluginConfig.cs"] = "F2C84981657F460B6DAC42644176023E66A8E0F2FD0B724B447897F0C2BCEF54",
                ["src/ItemShareFix.Plugin/RuntimePatches.cs"] = "AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506",
                ["src/ItemShareFix.Plugin/ServerCoordinator.cs"] = "3EC43F2BFA9354D05F60F85164581C0E39C2AFAD4067049D5339ECF5E608BA23",
                ["src/ItemShareFix.Plugin/UpstreamBridge.cs"] = "43935D44596A14C91FAAF276357DD7CBBF5B226330835BE6BCF265B9531AD4A9",
            };

            Assert.AreEqual(45, expectedProductionHashes.Count, "A2 release production freeze map must contain exactly 45 paths.");

            foreach (var pair in expectedProductionHashes)
                Assert.AreEqual(pair.Value, SourceSha256(pair.Key), "A2 release production hash drift: " + pair.Key);

            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "ItemShareFix.sln"))) root = root.Parent;
            Assert.IsNotNull(root);

            var actualProductionPaths = new System.Collections.Generic.List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root.FullName, "src"), "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(root.FullName, file).Replace('\\', '/');
                var relativeDirectory = (Path.GetDirectoryName(relativePath) ?? string.Empty).Replace('\\', '/');
                var normalizedDirectory = "/" + relativeDirectory + "/";
                if (normalizedDirectory.IndexOf("/bin/", StringComparison.Ordinal) >= 0
                    || normalizedDirectory.IndexOf("/obj/", StringComparison.Ordinal) >= 0)
                    continue;

                actualProductionPaths.Add(relativePath);
            }

            var expectedProductionPaths = new System.Collections.Generic.List<string>(expectedProductionHashes.Keys);
            actualProductionPaths.Sort(StringComparer.Ordinal);
            expectedProductionPaths.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(expectedProductionPaths, actualProductionPaths, "A2 release production path set drift.");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_29_VisibilityRepairPathRemainsParentByteIdentical()
        {
            var source = Plugin("ClientPresentation.cs");

            string MethodBodySha256(string declaration)
            {
                var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
                Assert.IsTrue(declarationIndex >= 0, "Visibility-repair declaration missing: " + declaration);
                var openBrace = source.IndexOf('{', declarationIndex);
                Assert.IsTrue(openBrace >= 0, "Visibility-repair body missing: " + declaration);
                var depth = 0;
                var closeBrace = -1;
                for (var i = openBrace; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            closeBrace = i;
                            break;
                        }
                    }
                }
                Assert.IsTrue(closeBrace >= openBrace, "Visibility-repair body is unbalanced: " + declaration);
                var body = source.Substring(openBrace, closeBrace - openBrace + 1);
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body)));
            }

            Assert.AreEqual("C19974B057E496BFE1BB82563855E7699DB201831EAF63D0D575EFB7C239DD75", MethodBodySha256("public bool TryEvaluateLocalCollected("));
            Assert.AreEqual("1EF102C63F2C82950D754A249A8A5B4DB3A42E104C8AD9BDBE1683FF26AC1055", MethodBodySha256("public bool ShouldSuppressLocalPickupInteraction("));
            Assert.AreEqual("7CD2DA3B2C1BD5695A74EAA206DD0898DC9C0E89FBAC01C44F61DDFF5972883E", MethodBodySha256("public void OnUpstreamVisibilityApplied("));
            Assert.AreEqual("149C1A7ACA082B527E42414D698AC5CE346B46D5B06EFCAFFBDA7609B3B4A5F0", MethodBodySha256("private void NormalizeUpstreamForGate("));
            Assert.AreEqual("C3CA641AE9BA1E1A9A0E0175E87C1023635D09D398D25B576175D78C22476DC3", MethodBodySha256("private void ReleaseGate("));

            StringAssert.Contains(source, "PersonalPickupVisibilityRepairEnabled.Value");
            StringAssert.Contains(source, "collectedByAllLocalParticipants = localMasters.All(master => _upstream.HasCollected(pickup, master));");
            StringAssert.Contains(source, "LocalPickupSuppressionPolicy.ShouldSuppressInteractor(");
            StringAssert.Contains(source, "TryEvaluateLocalCollected(pickup, out var collectedByAllLocalParticipants)");
            StringAssert.Contains(source, "NormalizeUpstreamForGate(pickup);");
            StringAssert.Contains(source, "gate.ApplyHidden();");
        }

        [TestMethod]
        public void ISF_R1_C21_A1_30_CompatibilityGuardFailsClosedOnExactHookDelegateShape()
        {
            var guard = Plugin("CompatibilityGuard.cs");
            StringAssert.Contains(guard, "HasExactOriginalDelegateHookShape(onAttemptGrant, typeof(RoR2.GenericPickupController), typeof(RoR2.CharacterBody))");
            StringAssert.Contains(guard, "HasExactOriginalDelegateHookShape(onPickupSelected, typeof(RoR2.PickupPickerController), typeof(int))");
            StringAssert.Contains(guard, "RoR2 UniquePickup.isTempItem : bool exact property missing.");
        }
    }
}
