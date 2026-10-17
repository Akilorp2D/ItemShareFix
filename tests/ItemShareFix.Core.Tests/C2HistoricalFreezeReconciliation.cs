using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    internal static class C2HistoricalFreezeReconciliation
    {
        internal const string RuntimePatchesPath = "src/ItemShareFix.Plugin/RuntimePatches.cs";
        internal const string ServerCoordinatorPath = "src/ItemShareFix.Plugin/ServerCoordinator.cs";
        internal const string ClientPresentationPath = "src/ItemShareFix.Plugin/ClientPresentation.cs";
        internal const string C2HitchDiagnosticsPath = "src/ItemShareFix.Plugin/C2HitchDiagnostics.cs";
        internal const string NativeHudMarkerRendererPath = "src/ItemShareFix.Plugin/NativeHudMarkerRenderer.cs";
        internal const string CompatibilityPath = "src/ItemShareFix.Plugin/KnownProgressionPickupCompatibility.cs";
        internal const string UpstreamBridgePath = "src/ItemShareFix.Plugin/UpstreamBridge.cs";
        internal const string LocalHudPresentationProbePath = "src/ItemShareFix.Plugin/LocalHudPresentationProbe.cs";
        internal const string CompatibilityGuardPath = "src/ItemShareFix.Plugin/CompatibilityGuard.cs";

        internal const string RuntimePatchesC1Sha256 = "AD4D04D530B3D7F14CE84D03C8D67ACACBA29D6E09E835608A49B9D262D6A506";
        internal const string RuntimePatchesC2Sha256 = "C3501618C5E31C997183E1822C0D38B2357B3427785933CAFAB1CC77303A1F8E";
        internal const string ServerCoordinatorC1Sha256 = "7EE99CD39351FB10699A99D13ED79946F3056C0579A6FC4CB5163A3E2365284B";
        internal const string ServerCoordinatorC2Sha256 = "C058FA7864C57BE861E5F3ED49A918D68D1B76285DCB9B5E2BD79E1031F181F1";
        internal const string ClientPresentationC1Sha256 = "A4AF3FBB564ECA8855676547370DF833FFC15EAA01853D7337DFEAA85DF31A2A";
        internal const string ClientPresentationC2ABeforeC2BSha256 = "A248EA2C9A4AEFB655E5ED6634A69EA11A55AA7B30819D0940027B502C821960";
        internal const string ClientPresentationC2Sha256 = "1636A0121CBB6FEB94ACB50EFF9A5C02D250D760C346EB12FBFC8DF99EB3C03B";
        internal const string ClientPresentationC4Sha256 = "CCF72310531EE1D6942040B14401099F54CDA0639B683A4FECB7C91A85D3069E";
        internal const string ClientPresentationC6Sha256 = "42FD596E808AF548AAD019E7EE192B38A5FF9F7859BEFF26D66B7F13EAC5B44C";
        internal const string C2HitchDiagnosticsC3Sha256 = "3322C9639EEE3555966939EBB47391187C61607BD859234FE518A29974C7106E";
        internal const string C2HitchDiagnosticsC4Sha256 = "6988C28B78C926487C55FF20090175A74CFD73B53A8ECD26A66D9DF3FA84F3CA";
        internal const string C2HitchDiagnosticsC6Sha256 = "02D6B1981015031F5BD14C0F665B3A73F9104A5003BEAB64D92F89D942FFA11E";
        internal const string NativeHudMarkerRendererC3Sha256 = "5B81168C03C685B57F257B91F00DBDFD18DADD5649674AFFDC9BFE8B56D9343A";
        internal const string NativeHudMarkerRendererC4Sha256 = "8BE2B34726F8DA777362066BBA6861F5FC0FCEFEED4E794363BCFCA9649D5748";
        internal const string NativeHudMarkerRendererC6Sha256 = "4C11F1A775141CE09C578EC3DA577E25A19463F1739758CCEE60417161B329D3";
        internal const string RuntimePatchesC8Sha256 = "9D62AE98B55D320F5059749DEE326B6C0A30A1288FD13B7B573579331AB786E5";
        internal const string ClientPresentationC8Sha256 = "F66C6116134B24D77FBC84A3091F642C825188DD3C9BEE6974D85283F8BF4568";
        internal const string ClientPresentationC11Sha256 = "E300FB8CE8B7319DF9385D0F40FFABA2CA963BE9A15642E2459C9EA836C4A95A";
        internal const string ClientPresentationC12Sha256 = "312D7DC7C2F796114867A363039EF47D56BB1D51B5BE0B1447CA6A672024B35F";
        internal const string NativeHudMarkerRendererC8Sha256 = "61EE416E4EDC907F39336330799DC891B0A1D175ADE0005291EECA18B1B09F7D";
        internal const string UpstreamBridgeC8Sha256 = "82A54135D2DC34CF600688A264B76A4C97695F94D52B2606C477EA1AA65B25F8";
        internal const string UpstreamBridgeC11Sha256 = "118001564AD4FAB41BD7DBB373E94996DDA9057AFE9DB2EEF22C4FEB46F16668";
        internal const string UpstreamBridgeC12Sha256 = "1A8BEA0482C0299B55C7FC739C832CFF5F97D38C9781B5EB47B417485CE55CE0";
        internal const string CommandShareabilityPolicyC11Sha256 = "E145FDDC09FD78CCC7EB2ED2BE5097831D29EE9D03C25F394E101E6EDB170A5E";
        internal const string CommandShareabilityPolicyC12Sha256 = "633B47FB30B397A2F725ED9F0B7C2F9734983B0311278EFCF0BCC019453200AC";
        internal const string MarkerClassPolicyC11Sha256 = "A747046392D219F18246B84470D018E6B7106502CB4A85B3CA44CC54935E823D";
        internal const string MarkerClassPolicyC12Sha256 = "87CFB677A5F96254234AA9E7311814CB34E2C70CBDCC9BB5DC7620511A3F32F0";
        internal const string LocalHudPresentationProbeC8Sha256 = "AFAD5B03DF47D568C597CAB98D2E2268883B6447636E05F6CD471EA3375DB17C";
        internal const string LocalHudPresentationProbeC9Sha256 = "5AA913E5799C2059C33C0DD61196EEA24D6B211A90A2848468198B075DDC997D";
        internal const string CompatibilityGuardC8Sha256 = "CBACEEB9E716C9715E394F4D43251D747F80D6C9161A601905DCC168CFFA3914";
        internal const string ClientPresentationC5FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/ClientPresentation.Correction5.txt";
        internal const string C2HitchDiagnosticsC5FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/C2HitchDiagnostics.Correction5.txt";
        internal const string NativeHudMarkerRendererC5FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/NativeHudMarkerRenderer.Correction5.txt";
        internal const string NativeHudMarkerRendererC3FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/NativeHudMarkerRenderer.Correction3.txt";
        internal const string RuntimePatchesC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/RuntimePatches.Correction7.txt";
        internal const string ClientPresentationC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/ClientPresentation.Correction7.txt";
        internal const string ClientPresentationC10FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/ClientPresentation.Correction10.txt";
        internal const string ClientPresentationC11FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/ClientPresentation.Correction11.txt";
        internal const string CommandShareabilityPolicyC11FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/CommandShareabilityPolicy.Correction11.txt";
        internal const string MarkerClassPolicyC11FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/MarkerClassPolicy.Correction11.txt";
        internal const string NativeHudMarkerRendererC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/NativeHudMarkerRenderer.Correction7.txt";
        internal const string UpstreamBridgeC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/UpstreamBridge.Correction7.txt";
        internal const string UpstreamBridgeC10FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/UpstreamBridge.Correction10.txt";
        internal const string UpstreamBridgeC11FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/UpstreamBridge.Correction11.txt";
        internal const string LocalHudPresentationProbeC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/LocalHudPresentationProbe.Correction7.txt";
        internal const string CompatibilityGuardC7FixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/CompatibilityGuard.Correction7.txt";
        internal const string ClientPresentationC2ARenderFrameFixturePath = "tests/ItemShareFix.Core.Tests/Fixtures/ClientPresentation.RenderFrame.C2A_Correction4.txt";
        internal const string ClientPresentationC2ARenderFrameFixtureSha256 = "950CFCD8B2BC343039F0B94F32DCDCB82CD641A3B137FBEC4041B2485C8048C5";
        internal const string CompatibilityC2Sha256 = "1871A19B8FFCADEC1D1097E383D6B20399BA5DECB24DC7B1455BC84F1E0A2F77";

        internal const string TryEvaluateLocalCollectedC1BodySha256 = "C19974B057E496BFE1BB82563855E7699DB201831EAF63D0D575EFB7C239DD75";
        internal const string TryEvaluateLocalCollectedC2BodySha256 = "4E97A614E540A5899C7E125B1BB18BEF1F949C6F19277A5F9CA417506DE3AB9B";
        internal const string ShouldSuppressLocalPickupInteractionC1BodySha256 = "1EF102C63F2C82950D754A249A8A5B4DB3A42E104C8AD9BDBE1683FF26AC1055";
        internal const string ShouldSuppressLocalPickupInteractionC2BodySha256 = "F024AF450CF82C68983840C79E745E1C636B9652F5AE40A94C7A7E492A89E9F2";

        internal const string RuntimePatchesCompatibilityBlock =
            "            // Exact provider progression pickups must never enter ItemShare's claim/distribution path.\n" +
            "            // Invoke the supplied original pickup grant directly so provider/vanilla collection semantics\n" +
            "            // remain authoritative while bypassing ItemShare before it can claim this world pickup.\n" +
            "            if (_server.KnownProgressionPickupExclusionsEnabled\n" +
            "                && KnownProgressionPickupCompatibility.IsExcluded(self))\n" +
            "            {\n" +
            "                InvokeSuppliedOriginal(__args[0], self, __args[2]);\n" +
            "                return false;\n" +
            "            }\n" +
            "\n";

        internal const string ServerCoordinatorCompatibilityProperty =
            "        public bool KnownProgressionPickupExclusionsEnabled => _config.Enabled.Value;\n";

        internal const string ServerCoordinatorC2ShareGuard =
            "            if (pickup == null\n" +
            "                || KnownProgressionPickupCompatibility.IsExcluded(pickup)\n" +
            "                || !_upstream.IsShareable(pickup)) return;\n";

        internal const string ServerCoordinatorC1ShareGuard =
            "            if (pickup == null || !_upstream.IsShareable(pickup)) return;\n";

        internal const string ClientTryEvaluateCompatibilityGuard =
            "                || KnownProgressionPickupCompatibility.IsExcluded(pickup)\n";

        internal const string ClientC2SuppressGuard =
            "                || interactor == null\n" +
            "                || KnownProgressionPickupCompatibility.IsExcluded(pickup)) return false;\n";

        internal const string ClientC1SuppressGuard =
            "                || interactor == null) return false;\n";

        internal const string ClientSweepCompatibilityBlock =
            "                if (KnownProgressionPickupCompatibility.IsExcluded(pickup))\n" +
            "                {\n" +
            "                    ReleaseGate(instanceId);\n" +
            "                    _markerRegistry.Remove(PersonalMarkerKind.OrdinaryPickup, instanceId);\n" +
            "                    _ordinaryRenderLogged.Remove(instanceId);\n" +
            "                    continue;\n" +
            "                }\n";

        internal const string ClientC4DiagnosticEnableLine =
            "            _log = log;\n" +
            "            C2HitchDiagnostics.SetEnabled(_config.DiagnosticLogging.Value);\n" +
            "            _localHudProbe = new LocalHudPresentationProbe(_performance);\n";
        internal const string ClientC3DiagnosticEnableContext =
            "            _log = log;\n" +
            "            _localHudProbe = new LocalHudPresentationProbe(_performance);\n";

        internal const string ClientC4NestedPrimerLine =
            "            _hudRenderer.PrimeNestedColdPathDuringInitialization();\n";

        internal const string DiagnosticsC4PhaseName = "        LoadingNestedPrimer = 35,\n";
        internal const string DiagnosticsC3PhaseName = "        ColdJitPrepare = 35,\n";
        internal const string DiagnosticsC4CaptureField = "        private static bool _captureSuppressed;\n";
        internal const string DiagnosticsC4CaptureSetter =
            "        public static void SetCaptureSuppressed(bool suppressed) => _captureSuppressed = suppressed;\n\n";
        internal const string DiagnosticsC4FastPath = "            if (!_enabled || _captureSuppressed) return default;\n";
        internal const string DiagnosticsC3FastPath = "            if (!_enabled) return default;\n";
        internal const string DiagnosticsC4SampleLimit =
            "            if (phase == C2HitchPhase.LoadingNestedPrimer) return 1;\n";
        internal const string DiagnosticsC3SampleLimit =
            "            if (phase == C2HitchPhase.ColdJitPrepare) return 18;\n";
        internal const string DiagnosticsC4Token =
            "                C2HitchPhase.LoadingNestedPrimer => \"LOADING_NESTED_PRIMER\",\n";
        internal const string DiagnosticsC3Token =
            "                C2HitchPhase.ColdJitPrepare => \"COLD_JIT_PREPARE\",\n";
        internal const string DiagnosticsC4Location =
            "                C2HitchPhase.LoadingNestedPrimer => \"NativeHudMarkerRenderer.PrimeNestedColdPathDuringInitialization\",\n";
        internal const string DiagnosticsC3Location =
            "                C2HitchPhase.ColdJitPrepare => \"NativeHudMarkerRenderer.AdvanceFirstMarkerJitWarmup\",\n";
        internal const string DiagnosticsC4Criticality =
            "                : phase == C2HitchPhase.LoadingNestedPrimer ? \"plugin-initialization-noninteractive\"\n" +
            "                : phase == C2HitchPhase.ColdWarmupHudProbe || phase == C2HitchPhase.ColdWarmupNative ? \"cold-prewarm\"\n";
        internal const string DiagnosticsC3Criticality =
            "                : phase == C2HitchPhase.ColdWarmupHudProbe || phase == C2HitchPhase.ColdWarmupNative || phase == C2HitchPhase.ColdJitPrepare ? \"cold-prewarm\"\n";

        internal static readonly string[] HistoricalC1FilesAuthorizedToDiffer =
        {
            RuntimePatchesPath,
            ServerCoordinatorPath,
            ClientPresentationPath,
        };

        internal static string ReadSource(string relativePath)
            => OctoberCompatibilityReconciliation.Normalize(relativePath, File.ReadAllText(ResolvePath(relativePath)));

        internal static string SourceSha256(string relativePath)
        {
            return TextSha256(ReadSource(relativePath));
        }

        internal static string TextSha256(string text)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        internal static int CountExact(string source, string exact)
        {
            Assert.IsFalse(string.IsNullOrEmpty(exact), "Exact normalization snippet must not be empty.");
            var count = 0;
            var offset = 0;
            while (true)
            {
                var index = source.IndexOf(exact, offset, StringComparison.Ordinal);
                if (index < 0) return count;
                count++;
                offset = index + exact.Length;
            }
        }

        internal static string ReplaceExactOnce(string source, string currentExact, string historicalExact, string label)
        {
            var count = CountExact(source, currentExact);
            Assert.AreEqual(1, count, "Authorized C2 normalization snippet count must be exactly one: " + label);
            var index = source.IndexOf(currentExact, StringComparison.Ordinal);
            return source.Substring(0, index) + historicalExact + source.Substring(index + currentExact.Length);
        }

        internal static string RemoveMethodExactOnce(string source, string declaration, string label)
        {
            var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0, "Authorized method missing: " + label);
            Assert.AreEqual(declarationIndex, source.LastIndexOf(declaration, StringComparison.Ordinal), "Authorized method declaration must occur exactly once: " + label);
            var lineStart = source.LastIndexOf('\n', declarationIndex);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var openBrace = source.IndexOf('{', declarationIndex);
            Assert.IsTrue(openBrace >= 0, "Authorized method body missing: " + label);
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
            Assert.IsTrue(closeBrace >= 0, "Authorized method body is unbalanced: " + label);
            var end = closeBrace + 1;
            if (end < source.Length && source[end] == '\r') end++;
            if (end < source.Length && source[end] == '\n') end++;
            if (end < source.Length && source[end] == '\r') end++;
            if (end < source.Length && source[end] == '\n') end++;
            return source.Substring(0, lineStart) + source.Substring(end);
        }

        private readonly struct RequiredCorrection11Seam
        {
            internal RequiredCorrection11Seam(string exact, int expectedCount)
            {
                Exact = exact;
                ExpectedCount = expectedCount;
            }

            internal string Exact { get; }
            internal int ExpectedCount { get; }
        }

        private static RequiredCorrection11Seam RequireCorrection11Seam(string exact, int expectedCount = 1)
            => new RequiredCorrection11Seam(exact, expectedCount);

        private static string NormalizeCorrection11FileToCorrection8Parent(
            string source,
            string currentSha256,
            string correction10FixturePath,
            string correction8Sha256,
            string label,
            params RequiredCorrection11Seam[] requiredCurrentSeams)
        {
            Assert.AreEqual(currentSha256, TextSha256(source), "Current Correction11 hash drift: " + label);
            for (var i = 0; i < requiredCurrentSeams.Length; i++)
            {
                var seam = requiredCurrentSeams[i];
                Assert.IsTrue(seam.ExpectedCount > 0, "Correction11 required instrumentation seam expected count must be positive: " + label + " / " + seam.Exact);
                Assert.AreEqual(
                    seam.ExpectedCount,
                    CountExact(source, seam.Exact),
                    "Correction11 required instrumentation seam drift: " + label + " / " + seam.Exact);
            }

            var parent = File.ReadAllText(ResolvePath(correction10FixturePath));
            Assert.AreEqual(correction8Sha256, TextSha256(parent), "Correction10 parent fixture drift: " + label);
            return parent;
        }

        private static string NormalizeCorrection12FileToCorrection11Parent(
            string source,
            string currentSha256,
            string correction11FixturePath,
            string correction11Sha256,
            string label)
        {
            Assert.AreEqual(currentSha256, TextSha256(source), "Current Correction12 hash drift: " + label);
            var parent = File.ReadAllText(ResolvePath(correction11FixturePath));
            Assert.AreEqual(correction11Sha256, TextSha256(parent), "Correction11 parent fixture drift: " + label);
            return parent;
        }

        internal static string NormalizeClientPresentationC12ToC11(string source)
            => NormalizeCorrection12FileToCorrection11Parent(
                source,
                ClientPresentationC12Sha256,
                ClientPresentationC11FixturePath,
                ClientPresentationC11Sha256,
                "ClientPresentation");

        internal static string NormalizeUpstreamBridgeC12ToC11(string source)
            => NormalizeCorrection12FileToCorrection11Parent(
                source,
                UpstreamBridgeC12Sha256,
                UpstreamBridgeC11FixturePath,
                UpstreamBridgeC11Sha256,
                "UpstreamBridge");

        internal static string NormalizeCommandShareabilityPolicyC12ToC11(string source)
            => NormalizeCorrection12FileToCorrection11Parent(
                source,
                CommandShareabilityPolicyC12Sha256,
                CommandShareabilityPolicyC11FixturePath,
                CommandShareabilityPolicyC11Sha256,
                "CommandShareabilityPolicy");

        internal static string NormalizeMarkerClassPolicyC12ToC11(string source)
            => NormalizeCorrection12FileToCorrection11Parent(
                source,
                MarkerClassPolicyC12Sha256,
                MarkerClassPolicyC11FixturePath,
                MarkerClassPolicyC11Sha256,
                "MarkerClassPolicy");

        internal static void AssertCorrection12CorePoliciesCurrentAndCorrection11Normalized()
        {
            Assert.AreEqual(CommandShareabilityPolicyC11Sha256, TextSha256(NormalizeCommandShareabilityPolicyC12ToC11(ReadSource("src/ItemShareFix.Core/CommandShareabilityPolicy.cs"))));
            Assert.AreEqual(MarkerClassPolicyC11Sha256, TextSha256(NormalizeMarkerClassPolicyC12ToC11(ReadSource("src/ItemShareFix.Core/MarkerClassPolicy.cs"))));
        }

        internal static string NormalizeClientPresentationC11ToC8(string source)
            => NormalizeCorrection11FileToCorrection8Parent(
                source,
                ClientPresentationC11Sha256,
                ClientPresentationC10FixturePath,
                ClientPresentationC8Sha256,
                "ClientPresentation",
                RequireCorrection11Seam("    internal static class CommandOptionResolutionDiagnostics\n"),
                RequireCorrection11Seam("                    CommandOptionResolutionDiagnostics.Begin(instanceId);\n"),
                RequireCorrection11Seam("                    + \" sample=\" + (sample.Ordinal == 1 ? \"first\" : \"subsequent\")\n"),
                RequireCorrection11Seam("                    CommandOptionResolutionDiagnostics.RecordMetadata(\n", 2));

        internal static string NormalizeUpstreamBridgeC11ToC8(string source)
            => NormalizeCorrection11FileToCorrection8Parent(
                source,
                UpstreamBridgeC11Sha256,
                UpstreamBridgeC10FixturePath,
                UpstreamBridgeC8Sha256,
                "UpstreamBridge",
                RequireCorrection11Seam("            var profileActive = CommandOptionResolutionDiagnostics.CaptureActive;\n"),
                RequireCorrection11Seam("                    CommandOptionResolutionDiagnostics.RecordSnapshot(\n", 4),
                RequireCorrection11Seam("                    if (profileActive) profilePickupIndexSourceResolutionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - profileSourceStarted;\n"));

        private static string NormalizeCorrection8FileToCorrection7(
            string source,
            string currentSha256,
            string correction7FixturePath,
            string correction7Sha256,
            string label,
            params string[] requiredCurrentSeams)
        {
            Assert.AreEqual(currentSha256, TextSha256(source), "Current Correction8 hash drift: " + label);
            for (var i = 0; i < requiredCurrentSeams.Length; i++)
                Assert.AreEqual(1, CountExact(source, requiredCurrentSeams[i]), "Correction8 required seam drift: " + label + " / " + requiredCurrentSeams[i]);

            var historical = File.ReadAllText(ResolvePath(correction7FixturePath));
            Assert.AreEqual(correction7Sha256, TextSha256(historical), "Correction7 fixture drift: " + label);
            return historical;
        }

        internal static string NormalizeRuntimePatchesC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                RuntimePatchesC8Sha256,
                RuntimePatchesC7FixturePath,
                RuntimePatchesC2Sha256,
                "RuntimePatches",
                "            InstallHudLifecyclePatches(harmony);\n",
                "            InstallTmpSubMeshLifecycleDiagnosticPatch(harmony);\n",
                "BindingFlags.DeclaredOnly");

        internal static string NormalizeClientPresentationC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                ClientPresentationC8Sha256,
                ClientPresentationC7FixturePath,
                ClientPresentationC6Sha256,
                "ClientPresentation",
                "            _localHudProbe.PrimeGlobalDiscoveryDuringInitialization();\n",
                "            _hudRenderer.PrimeNativeTypographyDuringInitialization();\n",
                "                        _upstream.TryBuildCommandChoiceSnapshot(picker, out var commandOptions);\n");

        internal static string NormalizeNativeHudMarkerRendererC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                NativeHudMarkerRendererC8Sha256,
                NativeHudMarkerRendererC7FixturePath,
                NativeHudMarkerRendererC6Sha256,
                "NativeHudMarkerRenderer",
                "        public void PrimeNativeTypographyDuringInitialization()\n",
                "        public void ObserveTmpSubMeshOnDisable(TMP_SubMeshUI subMesh)\n",
                "                if (view.Root.activeSelf) view.Root.SetActive(false);\n");

        internal static string NormalizeUpstreamBridgeC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                UpstreamBridgeC8Sha256,
                UpstreamBridgeC7FixturePath,
                "B58340156EDFCD53B074DD153AD90D515BC3569E6A82F0D37E695B9E9AE98E5B",
                "UpstreamBridge",
                "        public bool TryBuildCommandChoiceSnapshot(\n",
                "                foreach (var boxedOption in options)\n",
                "        private static CommandOptionAccessor GetCachedCommandOptionAccessor(Type type)\n");

        private const string LocalHudC9FieldSeam =
            "        private bool _lastPauseSignal;\n" +
            "\n" +
            "        public LocalHudPresentationProbe(MarkerRuntimePerformanceCounters performance)\n";
        private const string LocalHudC8FieldSeam =
            "        private bool _lastPauseSignal;\n" +
            "        private int _coldWarmupBlockingTypeIndex;\n" +
            "        private int _coldWarmupMessageTypeIndex;\n" +
            "        private bool _coldWarmupHudFallbackScanned;\n" +
            "        private bool _coldWarmupComplete;\n" +
            "\n" +
            "        public LocalHudPresentationProbe(MarkerRuntimePerformanceCounters performance)\n";
        private const string LocalHudC9InvalidateTail =
            "            _nextBlockingDiscovery = float.PositiveInfinity;\n" +
            "            _nextMessageDiscovery = float.PositiveInfinity;\n" +
            "        }\n" +
            "\n" +
            "        /// <summary>\n" +
            "        /// The only scene-wide discovery pass is allowed during plugin initialization, before gameplay input owns\n";
        private const string LocalHudC8InvalidateTail =
            "            _nextBlockingDiscovery = float.PositiveInfinity;\n" +
            "            _nextMessageDiscovery = float.PositiveInfinity;\n" +
            "            _coldWarmupBlockingTypeIndex = _blockingModalTypes.Length;\n" +
            "            _coldWarmupMessageTypeIndex = _messageHudTypes.Length;\n" +
            "            _coldWarmupHudFallbackScanned = true;\n" +
            "            _coldWarmupComplete = true;\n" +
            "        }\n" +
            "\n" +
            "        /// <summary>\n" +
            "        /// The only scene-wide discovery pass is allowed during plugin initialization, before gameplay input owns\n";
        private const string LocalHudC9PrimeTail =
            "            _nextBlockingDiscovery = float.PositiveInfinity;\n" +
            "            _nextMessageDiscovery = float.PositiveInfinity;\n" +
            "        }\n" +
            "\n" +
            "        /// <summary>\n" +
            "        /// Interactive warmup is intentionally scan-free. Exact lifecycle hooks seed modal/HUD candidates; marker\n";
        private const string LocalHudC8PrimeTail =
            "            _nextBlockingDiscovery = float.PositiveInfinity;\n" +
            "            _nextMessageDiscovery = float.PositiveInfinity;\n" +
            "            _coldWarmupBlockingTypeIndex = _blockingModalTypes.Length;\n" +
            "            _coldWarmupMessageTypeIndex = _messageHudTypes.Length;\n" +
            "            _coldWarmupHudFallbackScanned = true;\n" +
            "            _coldWarmupComplete = true;\n" +
            "        }\n" +
            "\n" +
            "        /// <summary>\n" +
            "        /// Interactive warmup is intentionally scan-free. Exact lifecycle hooks seed modal/HUD candidates; marker\n";

        internal static string NormalizeLocalHudPresentationProbeC9ToC8(string source)
        {
            Assert.AreEqual(LocalHudPresentationProbeC9Sha256, TextSha256(source), "Current Correction9 LocalHudPresentationProbe hash drift.");
            var normalized = ReplaceExactOnce(source, LocalHudC9FieldSeam, LocalHudC8FieldSeam, "Correction9 dead staged-warmup fields");
            normalized = ReplaceExactOnce(normalized, LocalHudC9InvalidateTail, LocalHudC8InvalidateTail, "Correction9 InvalidateLifecycle dead staged-warmup assignments");
            normalized = ReplaceExactOnce(normalized, LocalHudC9PrimeTail, LocalHudC8PrimeTail, "Correction9 PrimeGlobalDiscovery dead staged-warmup assignments");
            Assert.AreEqual(LocalHudPresentationProbeC8Sha256, TextSha256(normalized), "Correction9 LocalHudPresentationProbe normalization must reach exact Correction8 bytes.");
            return normalized;
        }

        internal static string NormalizeLocalHudPresentationProbeC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                LocalHudPresentationProbeC8Sha256,
                LocalHudPresentationProbeC7FixturePath,
                "EAF34078B50C73E8EF3FAD487481D5EC0F0EB1E26B039D7DBEF276DD6C93DD1E",
                "LocalHudPresentationProbe",
                "        public void PrimeGlobalDiscoveryDuringInitialization()\n",
                "        public void ObserveHudLifecycle(Component? hud)\n",
                "        public bool WarmColdPathStep(bool refreshIfDue)\n");

        internal static string NormalizeCompatibilityGuardC8ToC7(string source)
            => NormalizeCorrection8FileToCorrection7(
                source,
                CompatibilityGuardC8Sha256,
                CompatibilityGuardC7FixturePath,
                "C2B71BED8EEE689DAD4AE54EE0880527DBBC7A8F9F1C2ED8F2968BEE21F8DDB1",
                "CompatibilityGuard",
                "BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly",
                "<none-declared>; authoritative PlayerCharacterMasterController.instances absence sweep remains the cleanup authority");

        internal static void AssertCorrection8ResidualFilesCurrentAndCorrection7Normalized()
        {
            Assert.AreEqual(RuntimePatchesC2Sha256, TextSha256(NormalizeRuntimePatchesC8ToC7(ReadSource(RuntimePatchesPath))));
            Assert.AreEqual(ClientPresentationC6Sha256, TextSha256(NormalizeClientPresentationC8ToC7(NormalizeClientPresentationC11ToC8(NormalizeClientPresentationC12ToC11(ReadSource(ClientPresentationPath))))));
            Assert.AreEqual(NativeHudMarkerRendererC6Sha256, TextSha256(NormalizeNativeHudMarkerRendererC8ToC7(ReadSource(NativeHudMarkerRendererPath))));
            Assert.AreEqual("B58340156EDFCD53B074DD153AD90D515BC3569E6A82F0D37E695B9E9AE98E5B", TextSha256(NormalizeUpstreamBridgeC8ToC7(NormalizeUpstreamBridgeC11ToC8(NormalizeUpstreamBridgeC12ToC11(ReadSource(UpstreamBridgePath))))));
            Assert.AreEqual("EAF34078B50C73E8EF3FAD487481D5EC0F0EB1E26B039D7DBEF276DD6C93DD1E", TextSha256(NormalizeLocalHudPresentationProbeC8ToC7(NormalizeLocalHudPresentationProbeC9ToC8(ReadSource(LocalHudPresentationProbePath)))));
            Assert.AreEqual("C2B71BED8EEE689DAD4AE54EE0880527DBBC7A8F9F1C2ED8F2968BEE21F8DDB1", TextSha256(NormalizeCompatibilityGuardC8ToC7(ReadSource(CompatibilityGuardPath))));
        }

        internal static void AssertUpstreamBridgeCorrection8CurrentAndCorrection7Normalized()
        {
            var normalized = NormalizeUpstreamBridgeC8ToC7(NormalizeUpstreamBridgeC11ToC8(NormalizeUpstreamBridgeC12ToC11(ReadSource(UpstreamBridgePath))));
            Assert.AreEqual("B58340156EDFCD53B074DD153AD90D515BC3569E6A82F0D37E695B9E9AE98E5B", TextSha256(normalized));
        }

        internal static string NormalizeRuntimePatchesToC1(string source)
            => ReplaceExactOnce(source, RuntimePatchesCompatibilityBlock, string.Empty, "RuntimePatches pre-claim progression bypass");

        internal static string NormalizeServerCoordinatorToC1(string source)
        {
            var normalized = ReplaceExactOnce(source, ServerCoordinatorCompatibilityProperty, string.Empty, "ServerCoordinator exclusion enable property");
            return ReplaceExactOnce(normalized, ServerCoordinatorC2ShareGuard, ServerCoordinatorC1ShareGuard, "ServerCoordinator BeginDistribution share guard");
        }

        internal static string NormalizeClientPresentationC6ToC4(string source)
        {
            Assert.AreEqual(ClientPresentationC6Sha256, TextSha256(source), "Current Correction6 ClientPresentation hash drift.");
            var historical = File.ReadAllText(ResolvePath(ClientPresentationC5FixturePath));
            Assert.AreEqual(ClientPresentationC4Sha256, TextSha256(historical), "Correction5 ClientPresentation fixture drift.");
            return historical;
        }

        internal static string NormalizeC2HitchDiagnosticsC6ToC4(string source)
        {
            Assert.AreEqual(C2HitchDiagnosticsC6Sha256, TextSha256(source), "Current Correction6 C2HitchDiagnostics hash drift.");
            Assert.AreEqual(1, CountExact(source, "            if (!_enabled || _captureSuppressed || _firstMarkerFrame >= 0) return;\n"));
            var historical = File.ReadAllText(ResolvePath(C2HitchDiagnosticsC5FixturePath));
            Assert.AreEqual(C2HitchDiagnosticsC4Sha256, TextSha256(historical), "Correction5 C2HitchDiagnostics fixture drift.");
            return historical;
        }

        internal static string NormalizeNativeHudMarkerRendererC6ToC4(string source)
        {
            Assert.AreEqual(NativeHudMarkerRendererC6Sha256, TextSha256(source), "Current Correction6 NativeHudMarkerRenderer hash drift.");
            Assert.AreEqual(0, CountExact(source, "        private void PrimeFirstMarkerViewPool()\n"));
            Assert.AreEqual(0, CountExact(source, "_firstMarkerViewPool"));
            Assert.AreEqual(1, CountExact(source, "        public bool PrimeNestedColdPathDuringInitialization()\n"));
            var historical = File.ReadAllText(ResolvePath(NativeHudMarkerRendererC5FixturePath));
            Assert.AreEqual(NativeHudMarkerRendererC4Sha256, TextSha256(historical), "Correction5 NativeHudMarkerRenderer fixture drift.");
            return historical;
        }

        internal static string NormalizeClientPresentationC4ToC3(string source)
        {
            var normalized = ReplaceExactOnce(source, ClientC4DiagnosticEnableLine, ClientC3DiagnosticEnableContext, "ClientPresentation C4 diagnostic enable relocation");
            normalized = ReplaceExactOnce(normalized, ClientC4NestedPrimerLine, string.Empty, "ClientPresentation C4 initialization primer call");
            Assert.AreEqual(ClientPresentationC2Sha256, TextSha256(normalized), "ClientPresentation C4 normalization must reach the accepted Correction3/C2-B bytes exactly.");
            return normalized;
        }

        internal static string NormalizeC2HitchDiagnosticsC4ToC3(string source)
        {
            var normalized = ReplaceExactOnce(source, DiagnosticsC4PhaseName, DiagnosticsC3PhaseName, "C2HitchDiagnostics C4 primer phase enum");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4CaptureField, string.Empty, "C2HitchDiagnostics C4 capture-suppression field");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4CaptureSetter, string.Empty, "C2HitchDiagnostics C4 capture-suppression setter");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4FastPath, DiagnosticsC3FastPath, "C2HitchDiagnostics C4 additive disabled/suppressed fast path");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4SampleLimit, DiagnosticsC3SampleLimit, "C2HitchDiagnostics C4 primer sample bound");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4Token, DiagnosticsC3Token, "C2HitchDiagnostics C4 primer token");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4Location, DiagnosticsC3Location, "C2HitchDiagnostics C4 primer location");
            normalized = ReplaceExactOnce(normalized, DiagnosticsC4Criticality, DiagnosticsC3Criticality, "C2HitchDiagnostics C4 primer criticality");
            Assert.AreEqual(C2HitchDiagnosticsC3Sha256, TextSha256(normalized), "C2HitchDiagnostics C4 normalization must reach the accepted Correction3 bytes exactly.");
            return normalized;
        }

        internal static string NormalizeNativeHudMarkerRendererC4ToC3(string source)
        {
            Assert.AreEqual(1, CountExact(source, "        private readonly bool _isolatedColdPrimer;\n"));
            Assert.AreEqual(1, CountExact(source, "        public bool PrimeNestedColdPathDuringInitialization()\n"));
            Assert.AreEqual(1, CountExact(source, "        private void PrimeFirstMarkerViewPool()\n"));
            Assert.AreEqual(1, CountExact(source, "            if (!_canvas.enabled && !_isolatedColdPrimer) _canvas.enabled = true;\n"));
            Assert.AreEqual(1, CountExact(source, "            _canvas.enabled = !_presentationSuppressed && !_isolatedColdPrimer;\n"));
            Assert.AreEqual(1, CountExact(source, "            if (!_isolatedColdPrimer && _firstMarkerViewPool != null && _firstMarkerViewPool.Root != null)\n"));

            var historical = File.ReadAllText(ResolvePath(NativeHudMarkerRendererC3FixturePath));
            Assert.AreEqual(NativeHudMarkerRendererC3Sha256, TextSha256(historical), "Correction3 NativeHudMarkerRenderer fixture drift.");
            return historical;
        }

        internal static void AssertCorrection4PresentationFilesCurrentAndC3Normalized()
        {
            Assert.AreEqual(C2HitchDiagnosticsC6Sha256, SourceSha256(C2HitchDiagnosticsPath), "Current Correction6 C2HitchDiagnostics hash drift.");
            Assert.AreEqual(C2HitchDiagnosticsC3Sha256,
                TextSha256(NormalizeC2HitchDiagnosticsC4ToC3(NormalizeC2HitchDiagnosticsC6ToC4(ReadSource(C2HitchDiagnosticsPath)))),
                "C2HitchDiagnostics differs from Correction3 outside the exact authorized Correction6/C4 deltas.");

            Assert.AreEqual(ClientPresentationC12Sha256, SourceSha256(ClientPresentationPath), "Current Correction12 ClientPresentation hash drift.");
            Assert.AreEqual(ClientPresentationC2Sha256,
                TextSha256(NormalizeClientPresentationC4ToC3(NormalizeClientPresentationC6ToC4(NormalizeClientPresentationC8ToC7(NormalizeClientPresentationC11ToC8(NormalizeClientPresentationC12ToC11(ReadSource(ClientPresentationPath))))))),
                "ClientPresentation differs from Correction3 outside the exact authorized Correction8/Correction6/C4 deltas.");

            Assert.AreEqual(NativeHudMarkerRendererC8Sha256, SourceSha256(NativeHudMarkerRendererPath), "Current Correction8 NativeHudMarkerRenderer hash drift.");
            Assert.AreEqual(NativeHudMarkerRendererC3Sha256,
                TextSha256(NormalizeNativeHudMarkerRendererC4ToC3(NormalizeNativeHudMarkerRendererC6ToC4(NormalizeNativeHudMarkerRendererC8ToC7(ReadSource(NativeHudMarkerRendererPath))))),
                "NativeHudMarkerRenderer differs from Correction3 outside the exact authorized Correction8/Correction6/C4 primer deltas.");
        }

        internal static string NormalizeClientPresentationC2BToC2A(string source)
        {
            var currentRenderBody = MethodBody(source, "public void RenderFrame(");
            var historicalRenderBody = File.ReadAllText(ResolvePath(ClientPresentationC2ARenderFrameFixturePath));
            Assert.AreEqual(ClientPresentationC2ARenderFrameFixtureSha256, TextSha256(historicalRenderBody), "Historical C2-A RenderFrame fixture drift.");
            var normalized = ReplaceExactOnce(source, currentRenderBody, historicalRenderBody, "ClientPresentation C2-B RenderFrame instrumentation/warmup delta");
            return RemoveMethodExactOnce(normalized, "private bool AdvanceMarkerColdPathWarmup(bool allowRefresh)", "ClientPresentation C2-B cold-path warmup helper");
        }

        internal static string NormalizeClientPresentationToC1(string source)
        {
            var normalized = NormalizeClientPresentationC4ToC3(NormalizeClientPresentationC6ToC4(NormalizeClientPresentationC8ToC7(NormalizeClientPresentationC11ToC8(NormalizeClientPresentationC12ToC11(source)))));
            normalized = NormalizeClientPresentationC2BToC2A(normalized);
            Assert.AreEqual(ClientPresentationC2ABeforeC2BSha256, TextSha256(normalized), "ClientPresentation differs from C2-A outside the exact C2-B RenderFrame/warmup delta.");
            normalized = ReplaceExactOnce(normalized, ClientTryEvaluateCompatibilityGuard, string.Empty, "ClientPresentation TryEvaluateLocalCollected guard");
            normalized = ReplaceExactOnce(normalized, ClientC2SuppressGuard, ClientC1SuppressGuard, "ClientPresentation ShouldSuppressLocalPickupInteraction guard");
            return ReplaceExactOnce(normalized, ClientSweepCompatibilityBlock, string.Empty, "ClientPresentation ordinary pickup sweep exclusion block");
        }

        internal static void AssertRuntimePatchesCurrentAndC1Normalized()
        {
            Assert.AreEqual(RuntimePatchesC8Sha256, SourceSha256(RuntimePatchesPath), "Current Correction8 RuntimePatches hash drift.");
            var normalized = NormalizeRuntimePatchesToC1(NormalizeRuntimePatchesC8ToC7(ReadSource(RuntimePatchesPath)));
            Assert.AreEqual(RuntimePatchesC1Sha256, TextSha256(normalized), "RuntimePatches differs from C1 outside the exact authorized compatibility block.");
        }

        internal static void AssertServerCoordinatorCurrentAndC1Normalized()
        {
            Assert.AreEqual(ServerCoordinatorC2Sha256, SourceSha256(ServerCoordinatorPath), "Current C2 ServerCoordinator hash drift.");
            var normalized = NormalizeServerCoordinatorToC1(ReadSource(ServerCoordinatorPath));
            Assert.AreEqual(ServerCoordinatorC1Sha256, TextSha256(normalized), "ServerCoordinator differs from C1 outside the exact authorized compatibility delta.");
        }

        internal static void AssertClientPresentationCurrentAndC1Normalized()
        {
            Assert.AreEqual(ClientPresentationC12Sha256, SourceSha256(ClientPresentationPath), "Current Correction12 ClientPresentation hash drift.");
            var normalized = NormalizeClientPresentationToC1(ReadSource(ClientPresentationPath));
            Assert.AreEqual(ClientPresentationC1Sha256, TextSha256(normalized), "ClientPresentation differs from C1 outside the exact authorized compatibility delta.");
        }

        internal static void AssertAllHistoricalC1FilesCurrentAndNormalized()
        {
            AssertRuntimePatchesCurrentAndC1Normalized();
            AssertServerCoordinatorCurrentAndC1Normalized();
            AssertClientPresentationCurrentAndC1Normalized();
        }

        internal static string MethodBody(string source, string declaration)
        {
            var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0, "Method declaration missing: " + declaration);
            var openBrace = source.IndexOf('{', declarationIndex);
            Assert.IsTrue(openBrace >= 0, "Method body missing: " + declaration);
            var depth = 0;
            for (var i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Method body is unbalanced: " + declaration);
            return string.Empty;
        }

        internal static void AssertVisibilityMethodsCurrentAndC1Normalized()
        {
            var source = ReadSource(ClientPresentationPath);

            var tryEvaluate = MethodBody(source, "public bool TryEvaluateLocalCollected(");
            Assert.AreEqual(TryEvaluateLocalCollectedC2BodySha256, TextSha256(tryEvaluate), "Current C2 TryEvaluateLocalCollected body drift.");
            var normalizedTryEvaluate = ReplaceExactOnce(
                tryEvaluate,
                ClientTryEvaluateCompatibilityGuard,
                string.Empty,
                "TryEvaluateLocalCollected exact progression guard");
            Assert.AreEqual(TryEvaluateLocalCollectedC1BodySha256, TextSha256(normalizedTryEvaluate), "TryEvaluateLocalCollected differs from C1 outside the exact exclusion guard.");

            var shouldSuppress = MethodBody(source, "public bool ShouldSuppressLocalPickupInteraction(");
            Assert.AreEqual(ShouldSuppressLocalPickupInteractionC2BodySha256, TextSha256(shouldSuppress), "Current C2 ShouldSuppressLocalPickupInteraction body drift.");
            var normalizedShouldSuppress = ReplaceExactOnce(
                shouldSuppress,
                ClientC2SuppressGuard,
                ClientC1SuppressGuard,
                "ShouldSuppressLocalPickupInteraction exact progression guard");
            Assert.AreEqual(ShouldSuppressLocalPickupInteractionC1BodySha256, TextSha256(normalizedShouldSuppress), "ShouldSuppressLocalPickupInteraction differs from C1 outside the exact exclusion guard.");
        }

        internal static string ResolvePath(string relativePath)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }

            Assert.Fail(relativePath + " was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }
    }
}
