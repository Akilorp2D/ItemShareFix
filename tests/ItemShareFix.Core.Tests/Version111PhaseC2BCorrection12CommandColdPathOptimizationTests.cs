using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version111PhaseC2BCorrection12CommandColdPathOptimizationTests
    {
        private sealed class SingleUseEnumerable<T> : IEnumerable<T>
        {
            private readonly IEnumerable<T> _source;
            private bool _enumerated;

            internal SingleUseEnumerable(IEnumerable<T> source)
            {
                _source = source;
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (_enumerated) throw new InvalidOperationException("Enumerable was requested more than once.");
                _enumerated = true;
                return _source.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

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

        [TestMethod]
        public void ISF_V111_C2B_C12_01_CommandShareabilityNullAndEmptyRemainUnresolved()
        {
            Assert.AreEqual(CommandShareabilityState.Unresolved, CommandShareabilityPolicy.Evaluate(null).State);
            Assert.AreEqual(CommandShareabilityState.Unresolved, CommandShareabilityPolicy.Evaluate(Array.Empty<bool?>()).State);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_02_CommandShareabilitySingleTrueAndFalseRemainExact()
        {
            Assert.AreEqual(CommandShareabilityState.AllShareable, CommandShareabilityPolicy.Evaluate(new bool?[] { true }).State);
            Assert.AreEqual(CommandShareabilityState.AllUnshareable, CommandShareabilityPolicy.Evaluate(new bool?[] { false }).State);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_03_CommandShareabilityAllAndMixedStatesRemainExact()
        {
            Assert.AreEqual(CommandShareabilityState.AllShareable, CommandShareabilityPolicy.Evaluate(new bool?[] { true, true, true }).State);
            Assert.AreEqual(CommandShareabilityState.AllUnshareable, CommandShareabilityPolicy.Evaluate(new bool?[] { false, false, false }).State);
            Assert.AreEqual(CommandShareabilityState.Mixed, CommandShareabilityPolicy.Evaluate(new bool?[] { true, false, true }).State);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_04_CommandShareabilityAnyNullRemainsUnresolved()
        {
            Assert.AreEqual(CommandShareabilityState.Unresolved, CommandShareabilityPolicy.Evaluate(new bool?[] { true, null, false }).State);
            Assert.AreEqual(CommandShareabilityState.Unresolved, CommandShareabilityPolicy.Evaluate(new bool?[] { false, null }).State);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_05_CommandShareabilityEnumeratesInputAtMostOnce()
        {
            var values = new SingleUseEnumerable<bool?>(new bool?[] { true, true, false });
            Assert.AreEqual(CommandShareabilityState.Mixed, CommandShareabilityPolicy.Evaluate(values).State);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_06_ReadableClassNullEmptyAndUnknownRemainUnresolved()
        {
            var nullDecision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(null, MarkerLanguage.English);
            var emptyDecision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(Array.Empty<MarkerClassKind>(), MarkerLanguage.English);
            var unknownDecision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier1, MarkerClassKind.Unknown }, MarkerLanguage.English);
            Assert.AreEqual(MarkerClassKind.Unknown, nullDecision.Kind);
            Assert.IsFalse(nullDecision.ExactClass);
            Assert.AreEqual(MarkerClassKind.Unknown, emptyDecision.Kind);
            Assert.IsFalse(emptyDecision.ExactClass);
            Assert.AreEqual(MarkerClassKind.Unknown, unknownDecision.Kind);
            Assert.IsFalse(unknownDecision.ExactClass);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_07_ReadableClassSingleAndRepeatedSameClassRemainExact()
        {
            var single = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier2 }, MarkerLanguage.English);
            var repeated = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier2, MarkerClassKind.Tier2 }, MarkerLanguage.Russian);
            Assert.AreEqual(MarkerClassKind.Tier2, single.Kind);
            Assert.IsTrue(single.ExactClass);
            Assert.AreEqual("Green", single.Label);
            Assert.AreEqual(MarkerClassKind.Tier2, repeated.Kind);
            Assert.IsTrue(repeated.ExactClass);
            Assert.AreEqual("Зелёный", repeated.Label);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_08_ReadableClassMixedOtherAndOtherPlusClassRemainSemanticallyExact()
        {
            var mixed = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier2 }, MarkerLanguage.English);
            var otherOnly = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Other, MarkerClassKind.Other }, MarkerLanguage.English);
            var otherMixed = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Other, MarkerClassKind.Boss }, MarkerLanguage.English);
            Assert.AreEqual(MarkerClassKind.Unknown, mixed.Kind);
            Assert.IsFalse(mixed.ExactClass);
            Assert.AreEqual(MarkerClassKind.Other, otherOnly.Kind);
            Assert.IsFalse(otherOnly.ExactClass);
            Assert.AreEqual(MarkerClassKind.Unknown, otherMixed.Kind);
            Assert.IsFalse(otherMixed.ExactClass);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_09_ReadableClassEnumeratesInputAtMostOnce()
        {
            var classes = new SingleUseEnumerable<MarkerClassKind>(new[] { MarkerClassKind.Lunar, MarkerClassKind.Lunar });
            var decision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(classes, MarkerLanguage.English);
            Assert.AreEqual(MarkerClassKind.Lunar, decision.Kind);
            Assert.IsTrue(decision.ExactClass);
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_10_TargetedCoreAggregationContainsNoLinqMaterializationOrRepeatEnumeration()
        {
            var shareability = MethodBody(ReadSource("src/ItemShareFix.Core/CommandShareabilityPolicy.cs"), "public static CommandShareabilityDecision Evaluate(");
            Assert.IsFalse(shareability.Contains(".ToArray()", StringComparison.Ordinal));
            Assert.IsFalse(shareability.Contains(".Any(", StringComparison.Ordinal));

            var marker = MethodBody(ReadSource("src/ItemShareFix.Core/MarkerClassPolicy.cs"), "public static CommandClassPresentation ResolveCommandClassForReadablePresentation(IEnumerable<MarkerClassKind>? choiceClasses, MarkerLanguage language)");
            Assert.IsFalse(marker.Contains(".ToArray()", StringComparison.Ordinal));
            Assert.IsFalse(marker.Contains(".Any(", StringComparison.Ordinal));
            Assert.IsFalse(marker.Contains(".Distinct(", StringComparison.Ordinal));
            StringAssert.Contains(marker, "foreach (var kind in choiceClasses)");
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_11_CommandMetadataUsesExactArraysAndNoSelectOrWhereMaterialization()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var metadata = MethodBody(client, "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            StringAssert.Contains(metadata, "var resolvedClasses = new MarkerClassKind[pickupIndexes.Length];");
            StringAssert.Contains(metadata, "var resolvedColors = new Color[pickupIndexes.Length];");
            StringAssert.Contains(metadata, "var upstreamShareability = new bool?[pickupIndexes.Length];");
            StringAssert.Contains(metadata, "ResolveCommandClassForReadablePresentation(resolvedClasses, language)");
            Assert.IsFalse(metadata.Contains("resolved.Select(", StringComparison.Ordinal));
            Assert.IsFalse(metadata.Contains("resolved.Where(", StringComparison.Ordinal));
            Assert.IsFalse(metadata.Contains(".Where(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_12_CommandMetadataPreservesFirstMatchingColorByIndexedLoop()
        {
            var metadata = MethodBody(ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs"), "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            var loop = metadata.IndexOf("for (var optionIndex = 0; optionIndex < resolvedClasses.Length; optionIndex++)", StringComparison.Ordinal);
            var match = metadata.IndexOf("if (resolvedClasses[optionIndex] != classPresentation.Kind) continue;", loop, StringComparison.Ordinal);
            var assign = metadata.IndexOf("color = resolvedColors[optionIndex];", match, StringComparison.Ordinal);
            var stop = metadata.IndexOf("break;", assign, StringComparison.Ordinal);
            Assert.IsTrue(loop >= 0 && match > loop && assign > match && stop > assign);
            StringAssert.Contains(metadata, "classPresentation.Kind == MarkerClassKind.Other && resolvedColors.Length > 0");
            StringAssert.Contains(metadata, "? resolvedColors[0]");
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_13_ExactUpstreamSignatureBindsTypedDelegateOnceDuringConstruction()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var ctor = MethodBody(upstream, "public UpstreamBridge(");
            var binder = MethodBody(upstream, "private static Func<PickupDef, bool>? TryBindIsShareableDelegate(MethodInfo method)");
            StringAssert.Contains(upstream, "private readonly Func<PickupDef, bool>? _isShareableDelegate;");
            StringAssert.Contains(ctor, "_isShareableDelegate = TryBindIsShareableDelegate(_isShareableMethod);");
            StringAssert.Contains(binder, "!method.IsStatic || method.ReturnType != typeof(bool)");
            StringAssert.Contains(binder, "parameters.Length != 1 || parameters[0].ParameterType != typeof(PickupDef)");
            StringAssert.Contains(binder, "method.CreateDelegate(typeof(Func<PickupDef, bool>)) as Func<PickupDef, bool>");
            Assert.IsFalse(ctor.Contains("_isShareableDelegate(def)", StringComparison.Ordinal), "Construction must not warm upstream gameplay calls.");
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_14_DelegatePathReturnsExactUpstreamBooleanWithoutLocalPolicy()
        {
            var body = MethodBody(ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs"), "public bool TryIsShareable(PickupDef def, out bool shareable)");
            var branch = body.IndexOf("if (_isShareableDelegate != null)", StringComparison.Ordinal);
            var invoke = body.IndexOf("shareable = _isShareableDelegate(def);", branch, StringComparison.Ordinal);
            var success = body.IndexOf("return true;", invoke, StringComparison.Ordinal);
            Assert.IsTrue(branch >= 0 && invoke > branch && success > invoke);
            Assert.IsFalse(body.Contains("itemTier", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(body.Contains("config", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_15_IncompatibleDelegateBindingRetainsExactReflectionFallback()
        {
            var upstream = ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs");
            var binder = MethodBody(upstream, "private static Func<PickupDef, bool>? TryBindIsShareableDelegate(MethodInfo method)");
            var body = MethodBody(upstream, "public bool TryIsShareable(PickupDef def, out bool shareable)");
            StringAssert.Contains(binder, "return null;");
            StringAssert.Contains(body, "var result = _isShareableMethod.Invoke(null, new object[] { def });");
            StringAssert.Contains(body, "if (result is not bool exact) return false;");
            StringAssert.Contains(body, "shareable = exact;");
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_16_UpstreamInvocationExceptionsRemainFailClosedAndModeAuditable()
        {
            var body = MethodBody(ReadSource("src/ItemShareFix.Plugin/UpstreamBridge.cs"), "public bool TryIsShareable(PickupDef def, out bool shareable)");
            StringAssert.Contains(body, "shareable = false;");
            StringAssert.Contains(body, "catch (Exception ex)");
            StringAssert.Contains(body, "var mode = _isShareableDelegate != null ? \"delegate\" : \"reflection\";");
            StringAssert.Contains(body, "return false;");
        }

        [TestMethod]
        public void ISF_V111_C2B_C12_17_Correction11BoundedProfileRemainsEnabledForValidationCycle()
        {
            var client = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(client, "private const int MaxSamples = 5;");
            StringAssert.Contains(client, "ISF_COMMAND_OPTION_PROFILE");
            var metadata = MethodBody(client, "private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(");
            StringAssert.Contains(metadata, "profileUpstreamShareabilityCalls++;");
            StringAssert.Contains(metadata, "CommandOptionResolutionDiagnostics.RecordMetadata(");
        }
    }
}
