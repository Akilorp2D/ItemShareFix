using System;
using System.IO;
using System.Linq;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public class AuditFixTests
    {
        [TestMethod]
        public void NestedDistributionCompletesOnlyItsOwnPickupOnce()
        {
            var ledger = new ClaimLedger();
            var user = new ParticipantKey(new StableUserKey("a"), "generation1");
            var first = new SharedPickupKey(10); var second = new SharedPickupKey(20);
            ledger.Ensure(first, user, ParticipantState.Alive, 1); ledger.Ensure(second, user, ParticipantState.Alive, 1);
            var stack = new OwnedContextStack<object>(); var a = new object(); var b = new object();
            var outer = stack.Enter(a); var inner = stack.Enter(b); var innerCalls = 0;
            inner.Complete(value => { Assert.AreSame(b, value); innerCalls++; Assert.IsTrue(ledger.MarkCollected(second, user, 1)); });
            inner.Dispose(); inner.Complete(_ => innerCalls++);
            Assert.AreEqual(1, innerCalls); Assert.AreSame(a, stack.Peek()); Assert.AreEqual(1, stack.Count);
            Assert.IsTrue(ledger.TryGet(first, user, out var firstRecord)); Assert.AreEqual(ClaimState.Pending, firstRecord.State);
            outer.Complete(value => { Assert.AreSame(a, value); Assert.IsTrue(ledger.MarkCollected(first, user, 1)); });
            Assert.AreEqual(0, stack.Count); Assert.IsFalse(ledger.MarkCollected(second, user, 1));
        }

        [TestMethod]
        public void RejectedNestedDistributionShadowsAndRestoresOuterContext()
        {
            var stack = new OwnedContextStack<object>(); var outerValue = new object(); var unowned = new object();
            using (stack.Enter(outerValue))
            {
                using (stack.Enter(unowned)) { Assert.AreSame(unowned, stack.Peek()); Assert.AreEqual(2, stack.Count); }
                Assert.AreSame(outerValue, stack.Peek()); Assert.AreEqual(1, stack.Count);
            }
            Assert.AreEqual(0, stack.Count);
            var runtime = Raw("ServerCoordinator");
            StringAssert.Contains(runtime, "new DistributionContext { TracksClaims = false }");
            StringAssert.Contains(runtime, "if (!context.TracksClaims) return false;");
            StringAssert.Contains(runtime, "if (!context.TracksClaims) return;");
        }

        [TestMethod]
        public void BeginFailureRollsBackOnlyNewScope()
        {
            var stack = new OwnedContextStack<object>(); var parent = new object();
            using (stack.Enter(parent))
            {
                Assert.ThrowsException<InvalidOperationException>(() => stack.Enter(new object(), _ => throw new InvalidOperationException("initialize")));
                Assert.AreEqual(1, stack.Count); Assert.AreSame(parent, stack.Peek());
            }
            Assert.AreEqual(0, stack.Count);
        }

        [TestMethod]
        public void CompletionFailureAndFinalizerDoNotPopParent()
        {
            var stack = new OwnedContextStack<object>(); var parent = new object();
            using (stack.Enter(parent))
            {
                var inner = stack.Enter(new object());
                Assert.ThrowsException<InvalidOperationException>(() => inner.Complete(_ => throw new InvalidOperationException("completion")));
                inner.Dispose(); Assert.AreEqual(1, stack.Count); Assert.AreSame(parent, stack.Peek());
            }
            Assert.AreEqual(0, stack.Count);
        }

        [TestMethod]
        public void WrongDisposalOrderCannotMutateStackAndRunResetRetiresLeases()
        {
            var stack = new OwnedContextStack<object>(); var outer = stack.Enter(new object()); var inner = stack.Enter(new object());
            Assert.ThrowsException<InvalidOperationException>(() => outer.Dispose()); Assert.AreEqual(2, stack.Count);
            stack.Clear(); inner.Dispose(); outer.Complete(_ => Assert.Fail("Retired run scope completed")); Assert.AreEqual(0, stack.Count);
            Assert.ThrowsException<ArgumentNullException>(() => stack.Enter(null!));
        }

        [TestMethod]
        public void LateClientRestoresOnlyAuthoritativeMixedOptions()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>();
            var picker = new object(); var allowed = new object(); var neverEnabled = new object();
            Assert.IsFalse(mirror.Contains(7, picker, allowed));
            var nonce = mirror.Request(7, picker, 0);
            Assert.IsTrue(mirror.Apply(7, picker, nonce, new[] { allowed }));
            Assert.IsTrue(mirror.Contains(7, picker, allowed)); Assert.IsFalse(mirror.Contains(7, picker, neverEnabled));
            Assert.IsTrue(mirror.HasRetainedOptions(7, picker)); Assert.IsFalse(mirror.HasRetainedOptions(7, new object()));
            Assert.IsFalse(mirror.Contains(8, new object(), allowed));
            Assert.IsFalse(mirror.Apply(7, picker, nonce, new[] { neverEnabled }), "A reply is accepted once.");
            Assert.IsTrue(mirror.Contains(7, picker, allowed));
        }

        [TestMethod]
        public void MirrorRejectsOldOwnerNonceAndRunReply()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>(); var old = new object(); var fresh = new object(); var item = new object();
            var a = mirror.Request(9, old, 0); var b = mirror.Request(9, fresh, 0);
            Assert.IsFalse(mirror.Apply(9, old, a, new[] { item })); Assert.IsFalse(mirror.Apply(9, fresh, a, new[] { item }));
            Assert.IsFalse(mirror.Apply(9, fresh, 0, new[] { item })); Assert.IsTrue(mirror.Apply(9, fresh, b, new[] { item }));
            mirror.Clear(); var c = mirror.Request(9, fresh, 0);
            Assert.AreNotEqual(b, c); Assert.IsFalse(mirror.Apply(9, fresh, b, new[] { item })); Assert.IsFalse(mirror.Contains(9, fresh, item));
            Assert.IsTrue(mirror.Apply(9, fresh, c, Array.Empty<object>())); Assert.IsFalse(mirror.Contains(9, fresh, item));
            Assert.IsFalse(mirror.HasRetainedOptions(9, fresh));
        }

        [TestMethod]
        public void MirrorThrottlesRequestsAndPrunesDestroyedOwners()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>(); var owner = new object(); var other = new object(); var item = new object();
            var first = mirror.Request(1, owner, 2);
            Assert.IsTrue(mirror.Apply(1, owner, first, new[] { item }));
            Assert.AreEqual(0u, mirror.Request(1, owner, 2.999));
            var next = mirror.Request(1, owner, 3); Assert.AreNotEqual(0u, next); Assert.AreNotEqual(first, next);
            Assert.IsTrue(mirror.Contains(1, owner, item), "In-flight retry must preserve accepted metadata.");
            mirror.Request(2, other, 3); mirror.Prune(o => ReferenceEquals(o, owner)); Assert.AreEqual(1, mirror.Count);
            Assert.IsTrue(mirror.Contains(1, owner, item)); mirror.Prune(_ => false); Assert.AreEqual(0, mirror.Count);
        }

        [TestMethod]
        public void SlowReplySurvivesRepeatedRetriesWithoutChangingNonce()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>(); var owner = new object(); var item = new object();
            var nonce = mirror.Request(1, owner, 0);
            Assert.AreEqual(0u, mirror.Request(1, owner, 0.99));
            Assert.AreEqual(nonce, mirror.Request(1, owner, 1));
            Assert.AreEqual(nonce, mirror.Request(1, owner, 5));
            Assert.IsTrue(mirror.Apply(1, owner, nonce, new[] { item })); Assert.IsTrue(mirror.Contains(1, owner, item));
            Assert.AreNotEqual(nonce, mirror.Request(1, owner, 6));
        }

        [TestMethod]
        public void LateSnapshotDistinguishesCollectedAndPendingLocalParticipants()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>(); var owner = new object(); var item = new object();
            Assert.IsFalse(mirror.TryCollected(1, owner, 10, out _));
            var request = mirror.Request(1, owner, 0);
            Assert.IsTrue(mirror.Apply(1, owner, request, new[] { item }, new uint[] { 10, 20 }));
            Assert.IsTrue(mirror.TryCollected(1, owner, 10, out var collected)); Assert.IsTrue(collected);
            Assert.IsTrue(mirror.TryCollected(1, owner, 30, out var pending)); Assert.IsFalse(pending);
            Assert.IsFalse(mirror.TryCollected(1, owner, 0, out _)); Assert.IsFalse(mirror.TryCollected(1, new object(), 10, out _));
        }

        [TestMethod]
        public void ResetSnapshotCannotReuseOldCollectorsOrAuthorizeUnknownOption()
        {
            var mirror = new AuthoritativeEligibilityMirror<object, object>(); var owner = new object(); var item = new object();
            var first = mirror.Request(1, owner, 0); Assert.IsTrue(mirror.Apply(1, owner, first, new[] { item }, new uint[] { 10 }));
            mirror.Clear(); Assert.IsFalse(mirror.TryCollected(1, owner, 10, out _));
            var next = mirror.Request(1, owner, 0); Assert.IsTrue(mirror.Apply(1, owner, next, Array.Empty<object>()));
            Assert.IsFalse(mirror.Contains(1, owner, item));
            Assert.IsTrue(mirror.TryCollected(1, owner, 10, out var pending)); Assert.IsFalse(pending);
        }

        [TestMethod]
        public void EligibilityEpochPruneAndInstanceReusePreserveOnlyLiveExactOwner()
        {
            var cache = new SharedEligibilityCache<object, object>(); var run = new object(); var owner = new object(); var item = new object();
            Assert.IsTrue(cache.EnsureEpoch(run)); var entry = cache.For(1, owner, false); entry.Options.Observe(item, false, true);
            Assert.IsFalse(cache.EnsureEpoch(run)); cache.Prune(o => ReferenceEquals(o, owner));
            Assert.IsTrue(cache.TryGet(1, owner, out var retained)); Assert.IsTrue(retained.Options.Contains(item));
            var replacement = new object(); Assert.IsFalse(cache.TryGet(1, replacement, out _));
            Assert.IsFalse(cache.For(1, replacement, true).Options.Contains(item));
            Assert.IsFalse(cache.TryGetLive(1, _ => false, out _));
            cache.Prune(_ => false); Assert.AreEqual(0, cache.Count);
            cache.For(2, owner, true).Options.Observe(item, true, true);
            Assert.IsTrue(cache.EnsureEpoch(new object())); Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.TryGet(2, owner, out _));
        }

        [TestMethod]
        public void UntouchedPolicyClearsButStartedPolicyRetainsExactOptions()
        {
            var cache = new SharedEligibilityCache<object, object>(); var owner = new object(); var item = new object(); var excluded = new object();
            cache.For(1, owner, false).Options.Observe(item, false, true);
            var started = cache.For(1, owner, true); started.Options.Observe(item, true, false); started.Options.Observe(excluded, true, false);
            Assert.IsTrue(started.Options.Contains(item)); Assert.IsFalse(started.Options.Contains(excluded));
            Assert.IsFalse(cache.For(1, owner, false).Options.Contains(item)); cache.Remove(1); Assert.AreEqual(0, cache.Count);
        }

        [DataTestMethod]
        [DataRow("identity; exactRemoteOperationPreset=true", "true")]
        [DataRow("exactRemoteOperationPreset=false; reason=dead", "false")]
        [DataRow("exactRemoteOperationPreset=true false", "true")]
        [DataRow("exactRemoteOperationPreset=trueish", "unavailable-fail-closed")]
        [DataRow("exactRemoteOperationPreset=", "unavailable-fail-closed")]
        [DataRow("fail-closed: invocation threw", "unavailable-or-not-probed")]
        [DataRow("IsDeadAndOutOfLivesServer=false", "unavailable-or-not-probed")]
        [DataRow("", "not-probed")]
        public void RemoteOperationDiagnosticReadsExactSignalAndRejectsAmbiguousText(string text, string expected)
            => Assert.AreEqual(expected, DiagnosticPolicy.ExactRemoteOperation(text));

        [DataTestMethod]
        [DataRow("Error", 0)] [DataRow("Warning", 1)] [DataRow("Info", 2)] [DataRow("Debug", 3)]
        [DataRow("warning", 1)] [DataRow("unknown", 2)]
        public void DiagnosticLevelsFilterInfoAndDebugWithoutDisablingErrors(string level, int threshold)
        {
            foreach (var severity in Enum.GetValues<DiagnosticSeverity>())
            {
                Assert.AreEqual((int)severity <= threshold, DiagnosticPolicy.Allows(true, level, severity));
                Assert.IsFalse(DiagnosticPolicy.Allows(false, level, severity));
            }
        }

        [TestMethod]
        public void RuntimeBindingsUseOwnedScopesServerSnapshotsAndDiagnosticFilter()
        {
            var runtime = Raw("RuntimePatches"); var server = Raw("ServerCoordinator"); var sync = Raw("EligibilityNetworkSync");
            StringAssert.Contains(runtime, "__state = _server.BeginDistribution"); Assert.IsFalse(runtime.Contains("__state = _server.InDistribution"));
            StringAssert.Contains(server, "_distribution.Enter(context, EnsureClaims)"); StringAssert.Contains(server, "scope.Complete(CompleteDistribution)");
            StringAssert.Contains(server, "_sharedEligibility.EnsureEpoch(Run.instance)"); StringAssert.Contains(server, "_sharedEligibility.TryGetLive");
            StringAssert.Contains(sync, "message.conn.Send(SnapshotMessageId"); StringAssert.Contains(sync, "MatchesEpoch(snapshot.Header)");
            StringAssert.Contains(sync, "ReferenceEquals(message.conn, _client.connection)");
            StringAssert.Contains(sync, "_mirror.Apply(owner.GetInstanceID(), owner, snapshot.Header.Nonce, allowed, snapshot.Collectors)");
            Assert.IsFalse(sync.Contains("GiveDirect")); Assert.IsFalse(sync.Contains("MarkCollected"));
            StringAssert.Contains(Raw("ClientPresentation"), "_config.DiagnosticInfoEnabled"); StringAssert.Contains(Raw("NativeHudMarkerRenderer"), "_diagnosticConfig.DiagnosticInfoEnabled");
            StringAssert.Contains(Raw("NativeHudMarkerRenderer"), "if (_isolatedColdPrimer || !DiagnosticInfoEnabled) return;");
        }

        [TestMethod]
        public void PrivateDeadBranchesAreRemovedWithoutReintroducingGlobalDiscovery()
        {
            var hud = Raw("LocalHudPresentationProbe");
            Assert.IsFalse(hud.Contains("RefreshBlockingModalCandidates")); Assert.IsFalse(hud.Contains("RefreshMessageHudCandidates"));
            Assert.IsFalse(hud.Contains("_nextBlockingDiscovery"));
            var core = File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath("src/ItemShareFix.Core/MarkerClusterPresentationPolicy.cs"));
            Assert.IsFalse(core.Contains("AppendLifetimeSummary"));
            StringAssert.Contains(hud, "PrimeGlobalDiscoveryDuringInitialization"); StringAssert.Contains(hud, "ObserveHudLifecycle");
        }

        [TestMethod]
        public void AuditNormalizationRejectsUnreviewedAndWithinSeamDrift()
        {
            var path = "src/ItemShareFix.Plugin/ServerCoordinator.cs"; var raw = Raw("ServerCoordinator");
            var parent = AuditFixReconciliation.Normalize(path, raw); Assert.AreNotEqual(raw, parent);
            Assert.ThrowsException<AssertFailedException>(() => AuditFixReconciliation.Normalize(path, raw + "// unrelated drift"));
            Assert.ThrowsException<AssertFailedException>(() => AuditFixReconciliation.Normalize(path, raw.Replace("scope.Complete(CompleteDistribution)", "scope.Dispose()")));
        }

        private static string Raw(string name) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath("src/ItemShareFix.Plugin/" + name + ".cs"));
    }
}
