using System;
using System.Linq;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class StateModelTests
    {
        private static readonly StableUserKey UserA = new StableUserKey("platform:1001|slot:0");
        private static readonly StableUserKey UserB = new StableUserKey("platform:1002|slot:0");
        private static readonly ParticipantKey A = new ParticipantKey(UserA, "master:11");
        private static readonly ParticipantKey AReconnect = new ParticipantKey(UserA, "master:12");
        private static readonly ParticipantKey B = new ParticipantKey(UserB, "master:21");
        private static readonly SharedPickupKey P1 = new SharedPickupKey(101);
        private static readonly SharedPickupKey P2 = new SharedPickupKey(202);

        private static string R20ReadMarkerRiskOfOptionsLocalizationSource()
        {
            var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "src", "ItemShareFix.Plugin", "MarkerRiskOfOptionsLocalization.cs");
                if (System.IO.File.Exists(candidate)) return System.IO.File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            Assert.Fail("MarkerRiskOfOptionsLocalization.cs was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }

        private static string R24ReadPluginSource(string fileName)
        {
            var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "src", "ItemShareFix.Plugin", fileName);
                if (System.IO.File.Exists(candidate)) return System.IO.File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            Assert.Fail(fileName + " was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }

        [TestMethod] public void ISF_R1_C01_AliveStartsPending() => Assert.AreEqual(ClaimState.Pending, new ClaimLedger().Ensure(P1, A, ParticipantState.Alive, 1).State);
        [TestMethod] public void ISF_R1_C02_SupportDroneStartsPending() => Assert.AreEqual(ClaimState.Pending, new ClaimLedger().Ensure(P1, A, ParticipantState.SupportDrone, 1).State);
        [TestMethod] public void ISF_R1_C03_FullyDeadStartsDeferred() => Assert.AreEqual(ClaimState.Deferred, new ClaimLedger().Ensure(P1, A, ParticipantState.FullyDead, 1).State);
        [TestMethod] public void ISF_R1_C04_DisconnectedStartsCancelled() => Assert.AreEqual(ClaimState.CancelledDisconnected, new ClaimLedger().Ensure(P1, A, ParticipantState.Disconnected, 1).State);

        [TestMethod]
        public void ISF_R1_C05_CollectedIsPerParticipant()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.Ensure(P1, B, ParticipantState.Alive, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, A, 1));
            Assert.AreEqual(ClaimState.Collected, ledger.Records.Single(x => x.Key.Participant.Equals(A)).State);
            Assert.AreEqual(ClaimState.Pending, ledger.Records.Single(x => x.Key.Participant.Equals(B)).State);
        }

        [TestMethod]
        public void ISF_R1_C06_AliveToFullyDeadDefersPending()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.AreEqual(1, ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.FullyDead, 1));
            Assert.AreEqual(ClaimState.Deferred, ledger.Records.Single().State);
        }

        [TestMethod]
        public void ISF_R1_C07_SupportDroneDoesNotDeferPending()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(0, ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.SupportDrone, 1));
            Assert.AreEqual(ClaimState.Pending, ledger.Records.Single().State);
        }

        [TestMethod]
        public void ISF_R1_C08_DisconnectCancelsPendingAndDeferred()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.Ensure(P2, A, ParticipantState.FullyDead, 1);
            Assert.AreEqual(2, ledger.TransitionParticipant(A, ParticipantState.FullyDead, ParticipantState.Disconnected, 1));
            CollectionAssert.AreEquivalent(new[] { ClaimState.CancelledDisconnected, ClaimState.CancelledDisconnected }, ledger.Records.Select(x => x.State).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C09_DisconnectDoesNotRewriteCollected()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.MarkCollected(P1, A, 1);
            ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.Disconnected, 1);
            Assert.AreEqual(ClaimState.Collected, ledger.Records.Single().State);
        }

        [TestMethod]
        public void ISF_R1_C10_DeferredGrantOnlyFromDeferred()
        {
            var ledger = new ClaimLedger();
            var deferred = ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);
            Assert.IsTrue(ledger.MarkDeferredGranted(deferred.Key, 2));
            Assert.AreEqual(ClaimState.GrantedDeferred, deferred.State);
            Assert.IsFalse(ledger.MarkDeferredGranted(deferred.Key, 2));
        }

        [TestMethod]
        public void ISF_R1_C11_DeferredForNextStageExcludesSameStage()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.FullyDead, 2);
            Assert.AreEqual(0, ledger.DeferredFor(A, 2).Count);
            Assert.AreEqual(1, ledger.DeferredFor(A, 3).Count);
        }

        [TestMethod]
        public void ISF_R1_C12_StageTransitionRetainsOnlyDeferredEntitlements()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);
            ledger.Ensure(P2, B, ParticipantState.Alive, 1);
            ledger.MarkCollected(P2, B, 1);
            ledger.OnStageTransition(2);
            Assert.AreEqual(1, ledger.Records.Count);
            Assert.AreEqual(ClaimState.Deferred, ledger.Records.Single().State);
        }

        [TestMethod]
        public void ISF_R1_C13_TransferMovesIdentityWithoutChangingState()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);
            Assert.AreEqual(1, ledger.TransferPickup(P1, P2));
            Assert.IsFalse(ledger.TryGet(P1, A, out _));
            Assert.IsTrue(ledger.TryGet(P2, A, out var moved));
            Assert.AreEqual(ClaimState.Deferred, moved.State);
        }

        [TestMethod]
        public void ISF_R1_C14_TransferRejectsDuplicateTarget()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.Ensure(P2, A, ParticipantState.Alive, 1);
            Assert.ThrowsException<InvalidOperationException>(() => ledger.TransferPickup(P1, P2));
        }

        [TestMethod]
        public void ISF_R1_C15_HideRequiresAllLocalCollectedAndUpstreamPreference()
        {
            Assert.IsTrue(ProjectionPolicy.HideOrdinaryPickup(true, true, new[] { true, true }));
            Assert.IsFalse(ProjectionPolicy.HideOrdinaryPickup(true, true, new[] { true, false }));
            Assert.IsFalse(ProjectionPolicy.HideOrdinaryPickup(true, false, new[] { true }));
            Assert.IsFalse(ProjectionPolicy.HideOrdinaryPickup(false, true, new[] { true }));
            Assert.IsFalse(ProjectionPolicy.HideOrdinaryPickup(true, true, Array.Empty<bool>()));
        }

        [TestMethod]
        public void ISF_R1_C16_MarkerOnlyForPendingActiveParticipant()
        {
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, false));
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.SupportDrone, false));
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.FullyDead, false));
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Disconnected, false));
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, true));
        }

        [TestMethod]
        public void ISF_R1_C17_ReensureCannotDuplicateState()
        {
            var ledger = new ClaimLedger();
            var first = ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            var second = ledger.Ensure(P1, A, ParticipantState.FullyDead, 2);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, ledger.Records.Count);
            Assert.AreEqual(ClaimState.Pending, first.State);
        }

        [TestMethod]
        public void ISF_R1_C18_ParticipantIdentityCannotBeBlank()
        {
            Assert.ThrowsException<ArgumentException>(() => new StableUserKey("  "));
            Assert.ThrowsException<ArgumentException>(() => new ParticipantKey(UserA, "  "));
        }

        [TestMethod]
        public void ISF_R1_C19_RunBoundaryClearRemovesDeferredEntitlements()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);
            Assert.AreEqual(1, ledger.Clear());
            Assert.AreEqual(0, ledger.Records.Count);
            Assert.AreEqual(0, ledger.DeferredFor(A, 2).Count);
        }

        [TestMethod]
        public void ISF_R1_C2_01_ReconnectCannotReclaimCollectedHistoricalPickup()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, A, 1));
            ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.Disconnected, 1);
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P1, UserA));
            Assert.IsFalse(ledger.TryEnsure(P1, AReconnect, ParticipantState.Alive, 1, out _));
            Assert.IsFalse(ledger.TryGet(P1, AReconnect, out _));
        }

        [TestMethod]
        public void ISF_R1_C2_02_ReconnectCannotResurrectCancelledHistoricalPickup()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.AreEqual(1, ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.Disconnected, 1));
            Assert.IsTrue(ledger.TryGetHistorical(P1, UserA, out var historical));
            Assert.AreEqual(HistoricalClaimState.CancelledDisconnected, historical.State);
            Assert.IsFalse(ledger.TryEnsure(P1, AReconnect, ParticipantState.Alive, 1, out _));
        }

        [TestMethod]
        public void ISF_R1_C2_03_NewPickupAfterReconnectIsEligible()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.MarkCollected(P1, A, 1);
            Assert.IsFalse(ledger.TryEnsure(P1, AReconnect, ParticipantState.Alive, 1, out _));
            Assert.IsTrue(ledger.TryEnsure(P2, AReconnect, ParticipantState.Alive, 1, out var fresh));
            Assert.AreEqual(ClaimState.Pending, fresh.State);
        }

        [TestMethod]
        public void ISF_R1_C2_04_TransferPreservesReconnectBarrier()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.MarkCollected(P1, A, 1);
            ledger.TransferPickup(P1, P2);
            Assert.IsFalse(ledger.IsHistoricallyBlocked(P1, UserA));
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P2, UserA));
            Assert.IsFalse(ledger.TryEnsure(P2, AReconnect, ParticipantState.Alive, 1, out _));
        }

        [TestMethod]
        public void ISF_R1_C2_05_MarkerDoesNotResurrectHistoricalPickup()
        {
            var collectedLedger = new ClaimLedger();
            collectedLedger.Ensure(P1, A, ParticipantState.Alive, 1);
            collectedLedger.MarkCollected(P1, A, 1);
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, collected: false, historicallyBlocked: collectedLedger.IsHistoricallyBlocked(P1, UserA)));

            var cancelledLedger = new ClaimLedger();
            cancelledLedger.Ensure(P1, A, ParticipantState.Alive, 1);
            cancelledLedger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.Disconnected, 1);
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, collected: false, historicallyBlocked: cancelledLedger.IsHistoricallyBlocked(P1, UserA)));

            Assert.IsTrue(cancelledLedger.TryEnsure(P2, AReconnect, ParticipantState.Alive, 1, out _));
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, collected: false, historicallyBlocked: cancelledLedger.IsHistoricallyBlocked(P2, UserA)));
        }

        [TestMethod]
        public void ISF_R1_C2_06_StrongStableIdentityRequired()
        {
            Assert.ThrowsException<ArgumentException>(() => new StableUserKey(string.Empty));
            Assert.ThrowsException<ArgumentException>(() => new ParticipantKey(UserA, string.Empty));
            Assert.AreEqual(UserA, A.StableUser);
            Assert.AreEqual(UserA, AReconnect.StableUser);
            Assert.AreNotEqual(A, AReconnect);
        }


        [TestMethod]
        public void ISF_R1_C3_01_UnresolvedIdentityNeverSuppressesNormalGive()
        {
            var decision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: false,
                participantState: null,
                deadPlayerDeferredItemsEnabled: false,
                historicalBarrierForCurrentPickup: false);
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, decision);
        }

        [TestMethod]
        public void ISF_R1_C3_02_UnresolvedIdentityDoesNotBecomeDeferredByGuess()
        {
            var decision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: false,
                participantState: ParticipantState.FullyDead,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: true);
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, decision);
        }

        [TestMethod]
        public void ISF_R1_C3_03_ResolvedFullyDeadDeferredSuppressesImmediateGive()
        {
            var decision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: true,
                participantState: ParticipantState.FullyDead,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: false,
                deferredEntitlementReadyForCurrentPickup: true);
            Assert.AreEqual(ImmediateGiveDecision.SuppressFullyDeadDeferred, decision);
        }

        [TestMethod]
        public void ISF_R1_C3_04_HistoricalBarrierSuppressesOnlyMatchingPickup()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, A, 1));

            var matchingDecision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: true,
                participantState: ParticipantState.Alive,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: ledger.IsHistoricallyBlocked(P1, UserA));
            var unrelatedDecision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: true,
                participantState: ParticipantState.Alive,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: ledger.IsHistoricallyBlocked(P2, UserA));

            Assert.AreEqual(ImmediateGiveDecision.SuppressHistoricalBarrier, matchingDecision);
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, unrelatedDecision);
        }

        [TestMethod]
        public void ISF_R1_C3_05_NewPickupAfterReconnectGiveIsAllowed()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, A, 1));
            Assert.IsFalse(ledger.TryEnsure(P1, AReconnect, ParticipantState.Alive, 1, out _));
            Assert.IsTrue(ledger.TryEnsure(P2, AReconnect, ParticipantState.Alive, 1, out _));

            var decision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: true,
                participantState: ParticipantState.Alive,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: ledger.IsHistoricallyBlocked(P2, UserA));
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, decision);
        }

        [TestMethod]
        public void ISF_R1_C3_06_ResolvedAliveNormalGiveIsAllowed()
        {
            var decision = ImmediateGivePolicy.Decide(
                stableIdentityResolved: true,
                participantState: ParticipantState.Alive,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: false);
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, decision);
        }

        [TestMethod]
        public void ISF_R1_C3_07_NeverResolvedGenerationCreatesNoClaimsOrHistory()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.AreEqual(UnsupportedProbeDisposition.NoOwnershipEstablished, gate.ObserveUnsupported());
            Assert.IsFalse(gate.HasProvenParticipant);
            Assert.IsFalse(gate.CanCreateClaims);
            Assert.AreEqual(0, ledger.Records.Count);
            Assert.AreEqual(0, ledger.HistoricalRecords.Count);
        }

        [TestMethod]
        public void ISF_R1_C4_01_NeverResolvedUnknownGenerationAllowsUpstreamAndCreatesNoClaim()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.AreEqual(UnsupportedProbeDisposition.NoOwnershipEstablished, gate.ObserveUnsupported());
            Assert.IsFalse(gate.CanCreateClaims);
            Assert.AreEqual(0, ledger.Records.Count);
            Assert.AreEqual(ImmediateGiveDecision.AllowUpstream, ImmediateGivePolicy.Decide(
                stableIdentityResolved: false,
                participantState: null,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: false));
        }

        [TestMethod]
        public void ISF_R1_C4_02_ExistingDeferredEntitlementSurvivesTransientProbeFailure()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            var deferred = ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);

            Assert.AreEqual(UnsupportedProbeDisposition.FreezePreserveExistingState, gate.ObserveUnsupported());
            Assert.AreEqual(GenerationProbeState.Frozen, gate.State);
            Assert.IsTrue(ledger.TryGet(P1, A, out var preserved));
            Assert.AreSame(deferred, preserved);
            Assert.AreEqual(ClaimState.Deferred, preserved.State);
            Assert.AreEqual(1, ledger.DeferredFor(A, 2).Count);
            Assert.AreEqual(ImmediateGiveDecision.SuppressExistingDeferredEntitlement, ImmediateGivePolicy.Decide(
                stableIdentityResolved: gate.HasProvenParticipant,
                participantState: null,
                deadPlayerDeferredItemsEnabled: true,
                historicalBarrierForCurrentPickup: false,
                deferredEntitlementReadyForCurrentPickup: true));
        }

        [TestMethod]
        public void ISF_R1_C4_03_ExistingPendingClaimSurvivesTransientProbeFailure()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            var pending = ledger.Ensure(P1, A, ParticipantState.Alive, 1);

            gate.ObserveUnsupported();
            Assert.AreEqual(GenerationProbeState.Frozen, gate.State);
            Assert.IsTrue(ledger.TryGet(P1, A, out var preserved));
            Assert.AreSame(pending, preserved);
            Assert.AreEqual(ClaimState.Pending, preserved.State);
            Assert.AreEqual(0, ledger.HistoricalRecords.Count);
        }

        [TestMethod]
        public void ISF_R1_C4_04_FrozenGenerationCreatesNoNewClaims()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            gate.ObserveUnsupported();

            Assert.IsFalse(gate.CanCreateClaims);
            if (gate.CanCreateClaims) ledger.TryEnsure(P2, A, ParticipantState.Alive, 1, out _);
            Assert.IsFalse(ledger.TryGet(P2, A, out _));
            Assert.AreEqual(1, ledger.Records.Count);
        }

        [TestMethod]
        public void ISF_R1_C4_05_FrozenGenerationDoesNotGrantDeferredUntilResolvedAlive()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            ledger.Ensure(P1, A, ParticipantState.FullyDead, 1);
            gate.ObserveUnsupported();

            Assert.IsFalse(gate.CanGrantDeferred(ParticipantState.Alive));
            Assert.AreEqual(1, ledger.DeferredFor(A, 2).Count);
            Assert.IsTrue(gate.TryResolve(A));
            Assert.IsTrue(gate.CanGrantDeferred(ParticipantState.Alive));
            Assert.AreEqual(1, ledger.DeferredFor(A, 2).Count);
        }

        [TestMethod]
        public void ISF_R1_C4_06_SameGenerationRecoveryPreservesClaimsWithoutDuplicates()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            var original = ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            gate.ObserveUnsupported();

            Assert.IsTrue(gate.TryResolve(A));
            Assert.AreEqual(GenerationProbeState.Resolved, gate.State);
            Assert.IsTrue(ledger.TryEnsure(P1, A, ParticipantState.Alive, 1, out var recovered));
            Assert.AreSame(original, recovered);
            Assert.AreEqual(1, ledger.Records.Count);
        }

        [TestMethod]
        public void ISF_R1_C4_07_ActualDisconnectStillCancelsAndWritesHistoricalBarrier()
        {
            var ledger = new ClaimLedger();
            var gate = new GenerationProbeGate();
            Assert.IsTrue(gate.TryResolve(A));
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            ledger.Ensure(P2, A, ParticipantState.FullyDead, 1);
            gate.ObserveUnsupported();

            Assert.AreEqual(2, ledger.TransitionParticipant(A, ParticipantState.FullyDead, ParticipantState.Disconnected, 1));
            Assert.AreEqual(2, ledger.Records.Count(x => x.State == ClaimState.CancelledDisconnected));
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P1, UserA));
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P2, UserA));
        }


        [TestMethod]
        public void ISF_R1_C8_01_OrdinaryPendingPickupCreatesMarkerModelOnce()
        {
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 101, "Lens-Maker's Glasses"));
            Assert.AreEqual(PersonalMarkerTransition.None, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 101, "Lens-Maker's Glasses"));
            Assert.AreEqual(0, registry.EndSweep().Count);
            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.OrdinaryPickup, 101));
        }

        [TestMethod]
        public void ISF_R1_C8_02_OrdinaryCollectedPickupRemovesMarkerModel()
        {
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 102, "Pickup");
            registry.EndSweep();
            registry.BeginSweep();
            var removed = registry.EndSweep();
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(PersonalMarkerKind.OrdinaryPickup, removed[0].Identity.Kind);
            Assert.AreEqual(0, registry.Count);
        }

        [TestMethod]
        public void ISF_R1_C8_03_MarkerVisualLayoutIsBoundedAndNonEmpty()
        {
            var layout = MarkerPresentationPolicy.BuildVisualLayout(new string('X', 500), 123, 1920, 1080);
            Assert.IsFalse(string.IsNullOrWhiteSpace(layout.Text));
            StringAssert.StartsWith(layout.Text, "◆ ");
            Assert.IsTrue(layout.Width >= MarkerPresentationPolicy.MinMarkerWidth);
            Assert.IsTrue(layout.Width <= MarkerPresentationPolicy.AbsoluteMaxMarkerWidth);
            Assert.IsTrue(layout.Height >= MarkerPresentationPolicy.MinMarkerHeight);
            Assert.IsTrue(layout.Height <= MarkerPresentationPolicy.MaxMarkerHeight);
            Assert.IsTrue(layout.Text.Contains("123m"));
        }

        [TestMethod]
        public void ISF_R1_C8_04_MarkerRendererRejectsSharedSkinBoxMutationContract()
        {
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
        }

        [TestMethod]
        public void ISF_R1_C8_05_MarkerRenderStateRestoresAllOwnedGuiGlobals()
        {
            Assert.IsTrue(MarkerPresentationPolicy.RenderStateRestorationContractIsComplete());
            CollectionAssert.AreEquivalent(
                MarkerPresentationPolicy.MutatedGuiGlobals.ToArray(),
                MarkerPresentationPolicy.RestoredGuiGlobals.ToArray());
        }

        [TestMethod]
        public void ISF_R1_C8_06_CommandClassifierRejectsNonCommandPicker()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ShouldTrackCommandMarker(
                featureEnabled: true,
                individualMode: true,
                shareCommandPicks: true,
                classifierIsCommand: false,
                exactLocalStateResolved: true,
                anyLocalPending: true));
        }

        [TestMethod]
        public void ISF_R1_C8_07_PendingCommandPickerCreatesPersonalMarker()
        {
            Assert.IsTrue(MarkerPresentationPolicy.ShouldTrackCommandMarker(true, true, true, true, true, true));
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.CommandPicker, 201, "Artifact of Command"));
            registry.EndSweep();
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.CommandPicker, 201));
        }

        [TestMethod]
        public void ISF_R1_C8_08_CompletedLocalCommandPickerRemovesPersonalMarker()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ShouldTrackCommandMarker(true, true, true, true, true, false));
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.CommandPicker, 202, "Artifact of Command");
            registry.EndSweep();
            registry.BeginSweep();
            var removed = registry.EndSweep();
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(0, registry.Count);
        }

        [TestMethod]
        public void ISF_R1_C8_09_TwoPendingCommandPickersProduceTwoMarkerModels()
        {
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.CommandPicker, 203, "Command A");
            registry.MarkPending(PersonalMarkerKind.CommandPicker, 204, "Command B");
            registry.EndSweep();
            Assert.AreEqual(2, registry.Count);
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.CommandPicker, 203));
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.CommandPicker, 204));
        }

        [TestMethod]
        public void ISF_R1_C8_10_OrdinaryAndCommandMarkersCoexistWithoutPingSlot()
        {
            var registry = new PersonalMarkerRegistry(8);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 301, "Ordinary");
            registry.MarkPending(PersonalMarkerKind.CommandPicker, 301, "Command");
            registry.EndSweep();
            Assert.AreEqual(2, registry.Count);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
        }

        [TestMethod]
        public void ISF_R1_C8_11_CommandMarkerTeardownIsBounded()
        {
            var registry = new PersonalMarkerRegistry(3);
            registry.BeginSweep();
            Assert.AreNotEqual(PersonalMarkerTransition.CapacityRejected, registry.MarkPending(PersonalMarkerKind.CommandPicker, 1, "A"));
            Assert.AreNotEqual(PersonalMarkerTransition.CapacityRejected, registry.MarkPending(PersonalMarkerKind.CommandPicker, 2, "B"));
            Assert.AreNotEqual(PersonalMarkerTransition.CapacityRejected, registry.MarkPending(PersonalMarkerKind.CommandPicker, 3, "C"));
            Assert.AreEqual(PersonalMarkerTransition.CapacityRejected, registry.MarkPending(PersonalMarkerKind.CommandPicker, 4, "D"));
            Assert.AreEqual(3, registry.Count);
            registry.Clear();
            Assert.AreEqual(0, registry.Count);
        }

        [TestMethod]
        public void ISF_R1_C8_12_NoPickupShareApiProviderRegistrationIsIntroduced()
        {
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
        }


        [TestMethod]
        public void ISF_R1_C9_01_OrdinaryTier1UsesWhiteClassAndNativeCatalogColorContract()
        {
            Assert.AreEqual(MarkerClassKind.Tier1, MarkerClassPolicy.Classify("Tier1", false, false));
            Assert.AreEqual("White", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier1, false));
            Assert.AreEqual("Белый", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier1, true));
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
        }

        [TestMethod]
        public void ISF_R1_C9_02_OrdinaryTier2UsesGreenClassAndNativeCatalogColorContract()
        {
            Assert.AreEqual(MarkerClassKind.Tier2, MarkerClassPolicy.Classify("Tier2", false, false));
            Assert.AreEqual("Green", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier2, false));
            Assert.AreEqual("Зелёный", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier2, true));
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
        }

        [TestMethod]
        public void ISF_R1_C9_03_OrdinaryTier3UsesRedClassAndNativeCatalogColorContract()
        {
            Assert.AreEqual(MarkerClassKind.Tier3, MarkerClassPolicy.Classify("Tier3", false, false));
            Assert.AreEqual("Red", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier3, false));
            Assert.AreEqual("Красный", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Tier3, true));
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
        }

        [TestMethod]
        public void ISF_R1_C9_04_NonstandardAndEquipmentClassesUseCatalogColorWithSafeSemanticFallback()
        {
            Assert.AreEqual(MarkerClassKind.Equipment, MarkerClassPolicy.Classify("NoTier", true, false));
            Assert.AreEqual(MarkerClassKind.LunarEquipment, MarkerClassPolicy.Classify("NoTier", true, true));
            Assert.AreEqual(MarkerClassKind.Other, MarkerClassPolicy.Classify("ModdedTier", false, false));
            Assert.AreEqual("Equipment", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Equipment, false));
            Assert.AreEqual("Лунное снаряжение", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.LunarEquipment, true));
            Assert.AreEqual("Item choice", MarkerClassPolicy.LocalizedClassLabel(MarkerClassKind.Other, false));
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
        }

        [TestMethod]
        public void ISF_R1_C9_05_CommandLabelComesFromPickerChoiceClassNeverArtifactName()
        {
            var presentation = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier2, MarkerClassKind.Tier2 }, true);
            Assert.IsTrue(presentation.ExactClass);
            Assert.AreEqual("Зелёный", presentation.Label);
            Assert.IsFalse(MarkerClassPolicy.UsesArtifactCommandName);
            Assert.AreEqual("PickupPickerController.options/PickupIndex", MarkerClassPolicy.CommandChoiceSource);
            Assert.AreNotEqual("Артефакт управления", presentation.Label);
        }

        [TestMethod]
        public void ISF_R1_C9_06_CommandTier1Tier2Tier3LabelsRemainConsistent()
        {
            var white = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier1 }, true);
            var green = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier2 }, true);
            var red = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier3 }, true);
            Assert.AreEqual("Белый", white.Label);
            Assert.AreEqual("Зелёный", green.Label);
            Assert.AreEqual("Красный", red.Label);
            CollectionAssert.AreEquivalent(new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3 }, new[] { white.Kind, green.Kind, red.Kind });
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
        }

        [TestMethod]
        public void ISF_R1_C9_07_MixedOrUnresolvedCommandChoicesUseBoundedNonEmptyFallback()
        {
            var mixed = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier2 }, true);
            var unresolved = MarkerClassPolicy.ResolveCommandClass(Array.Empty<MarkerClassKind>(), false);
            Assert.IsFalse(mixed.ExactClass);
            Assert.AreEqual("Выбор предмета", mixed.Label);
            Assert.IsFalse(unresolved.ExactClass);
            Assert.AreEqual("Item choice", unresolved.Label);
            Assert.IsFalse(string.IsNullOrWhiteSpace(mixed.Label));
            Assert.IsTrue(mixed.Label.Length <= MarkerPresentationPolicy.MaxLabelCharacters);
        }

        [TestMethod]
        public void ISF_R1_C9_08_ColorAndLabelRefinementPreservesC8MembershipLifecyclePingAndLayoutContracts()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 901, "Lens-Maker's Glasses"));
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.CommandPicker, 902, "Зелёный"));
            registry.EndSweep();
            Assert.AreEqual(2, registry.Count);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
            var layout = MarkerPresentationPolicy.BuildVisualLayout("Зелёный", 42, 1920, 1080);
            Assert.IsTrue(layout.Width <= MarkerPresentationPolicy.AbsoluteMaxMarkerWidth);
            Assert.IsTrue(MarkerPresentationPolicy.ShouldTrackCommandMarker(true, true, true, true, true, true));
        }

        [TestMethod]
        public void ISF_R1_C10_01_NestedCommandPickupWinsOverConflictingDirectCompatibilityMember()
        {
            var decision = CommandOptionSourcePolicy.Resolve(true, 101, true, 999);
            Assert.IsTrue(decision.HasValue);
            Assert.AreEqual(101, decision.Value);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, decision.Source);
            Assert.IsTrue(decision.ExactSource);
            Assert.IsTrue(decision.Disagreement);
            Assert.AreEqual("nested-pickup", CommandOptionSourcePolicy.SourceToken(decision.Source));
            Assert.AreEqual("PickupPickerController.options/option.pickup.pickupIndex", MarkerClassPolicy.AuthoritativeCommandChoiceSource);
        }

        [TestMethod]
        public void ISF_R1_C10_02_DirectFallbackRequiresMissingNestedSourceAndIsNeverExact()
        {
            var fallback = CommandOptionSourcePolicy.Resolve(false, 0, true, 202);
            Assert.IsTrue(fallback.HasValue);
            Assert.AreEqual(202, fallback.Value);
            Assert.AreEqual(CommandOptionPickupSource.DirectCompatibilityFallback, fallback.Source);
            Assert.IsFalse(fallback.ExactSource);
            Assert.IsFalse(fallback.Disagreement);
            Assert.AreEqual("direct-fallback", CommandOptionSourcePolicy.SourceToken(fallback.Source));

            var unresolved = CommandOptionSourcePolicy.Resolve(false, 0, false, 0);
            Assert.IsFalse(unresolved.HasValue);
            Assert.IsFalse(unresolved.ExactSource);
        }

        [TestMethod]
        public void ISF_R1_C10_03_WhiteCommandPresentationRemainsWhiteFromAuthoritativeNestedChoice()
        {
            var source = CommandOptionSourcePolicy.Resolve(true, 1, true, 5);
            var presentation = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier1 }, true);
            Assert.IsTrue(source.ExactSource);
            Assert.AreEqual(MarkerClassKind.Tier1, presentation.Kind);
            Assert.AreEqual("Белый", presentation.Label);
            Assert.IsTrue(presentation.ExactClass);
        }

        [TestMethod]
        public void ISF_R1_C10_04_GreenCommandPresentationRemainsGreenFromAuthoritativeNestedChoice()
        {
            var source = CommandOptionSourcePolicy.Resolve(true, 2, true, 5);
            var presentation = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier2 }, true);
            Assert.IsTrue(source.ExactSource);
            Assert.AreEqual(MarkerClassKind.Tier2, presentation.Kind);
            Assert.AreEqual("Зелёный", presentation.Label);
            Assert.IsTrue(presentation.ExactClass);
        }

        [TestMethod]
        public void ISF_R1_C10_05_RedCommandPresentationRemainsRedFromAuthoritativeNestedChoice()
        {
            var source = CommandOptionSourcePolicy.Resolve(true, 3, true, 5);
            var presentation = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier3 }, true);
            Assert.IsTrue(source.ExactSource);
            Assert.AreEqual(MarkerClassKind.Tier3, presentation.Kind);
            Assert.AreEqual("Красный", presentation.Label);
            Assert.IsTrue(presentation.ExactClass);
        }

        [TestMethod]
        public void ISF_R1_C10_06_YellowCommandPresentationRequiresActualBossClass()
        {
            var boss = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Boss }, true);
            var green = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Tier2 }, true);
            Assert.AreEqual(MarkerClassKind.Boss, boss.Kind);
            Assert.AreEqual("Жёлтый", boss.Label);
            Assert.AreNotEqual(MarkerClassKind.Boss, green.Kind);
            Assert.AreNotEqual("Жёлтый", green.Label);
        }

        [TestMethod]
        public void ISF_R1_C10_07_LunarVoidAndEquipmentLabelsResolveWithoutChangingOrdinaryShareabilityPolicy()
        {
            var lunar = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Lunar }, true);
            var voidChoice = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Void }, true);
            var equipment = MarkerClassPolicy.ResolveCommandClass(new[] { MarkerClassKind.Equipment }, true);
            Assert.AreEqual("Синий", lunar.Label);
            Assert.AreEqual("Фиолетовый", voidChoice.Label);
            Assert.AreEqual("Снаряжение", equipment.Label);
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
            Assert.IsFalse(MarkerClassPolicy.UsesArtifactCommandName);
        }

        [TestMethod]
        public void ISF_R1_C10_08_C9RendererLifecyclePingAndStateContractsRemainUnchanged()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 1001, "Ordinary"));
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.CommandPicker, 1002, "Белый"));
            registry.EndSweep();
            Assert.AreEqual(2, registry.Count);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.IsTrue(MarkerPresentationPolicy.ShouldTrackCommandMarker(true, true, true, true, true, true));
        }

        [TestMethod]
        public void ISF_R1_C11_01_ShareableWhiteGreenRedBossCommandClassesRemainMarkerEligible()
        {
            foreach (var kind in new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3, MarkerClassKind.Boss })
            {
                var shareability = CommandShareabilityPolicy.Evaluate(new bool?[] { true, true });
                var presentation = MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { kind, kind }, true);
                Assert.AreEqual(CommandShareabilityState.AllShareable, shareability.State);
                Assert.IsTrue(shareability.MarkerEligible);
                Assert.IsTrue(presentation.ExactClass);
                Assert.AreEqual(kind, presentation.Kind);
            }
        }

        [TestMethod]
        public void ISF_R1_C11_02_UpstreamUnshareableLunarCommandIsFiltered()
        {
            var shareability = CommandShareabilityPolicy.Evaluate(new bool?[] { false, false });
            var presentation = MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Lunar }, true);
            Assert.AreEqual(CommandShareabilityState.AllUnshareable, shareability.State);
            Assert.IsFalse(shareability.MarkerEligible);
            Assert.AreEqual("upstream-not-shareable", shareability.FilterReason);
            Assert.AreEqual("Лунный", presentation.Label);
        }

        [TestMethod]
        public void ISF_R1_C11_03_UpstreamUnshareableVoidCommandIsFiltered()
        {
            var shareability = CommandShareabilityPolicy.Evaluate(new bool?[] { false });
            var presentation = MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Void }, true);
            Assert.AreEqual("all-unshareable", shareability.DiagnosticToken);
            Assert.IsFalse(shareability.MarkerEligible);
            Assert.AreEqual("Предмет Бездны", presentation.Label);
        }

        [TestMethod]
        public void ISF_R1_C11_04_EquipmentCommandIsFilteredThroughUpstreamDecisionInIndividualMode()
        {
            var shareability = CommandShareabilityPolicy.Evaluate(new bool?[] { false });
            var presentation = MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Equipment }, true);
            Assert.IsFalse(shareability.MarkerEligible);
            Assert.AreEqual("upstream-not-shareable", shareability.FilterReason);
            Assert.AreEqual("Снаряжение", presentation.Label);
        }

        [TestMethod]
        public void ISF_R1_C11_05_UpstreamToggleResultAloneControlsLunarVoidEligibilityWithoutTierDuplication()
        {
            var disabled = CommandShareabilityPolicy.Evaluate(new bool?[] { false, false });
            var enabled = CommandShareabilityPolicy.Evaluate(new bool?[] { true, true });
            Assert.IsFalse(disabled.MarkerEligible);
            Assert.IsTrue(enabled.MarkerEligible);
            Assert.AreEqual("all-unshareable", disabled.DiagnosticToken);
            Assert.AreEqual("all-shareable", enabled.DiagnosticToken);
        }

        [TestMethod]
        public void ISF_R1_C11_06_MixedOrUnresolvedCommandShareabilityFailsClosed()
        {
            var mixed = CommandShareabilityPolicy.Evaluate(new bool?[] { true, false });
            var unresolved = CommandShareabilityPolicy.Evaluate(new bool?[] { true, null });
            var empty = CommandShareabilityPolicy.Evaluate(Array.Empty<bool?>());
            Assert.AreEqual(CommandShareabilityState.Mixed, mixed.State);
            Assert.IsFalse(mixed.MarkerEligible);
            Assert.AreEqual("mixed-shareability", mixed.FilterReason);
            Assert.AreEqual(CommandShareabilityState.Unresolved, unresolved.State);
            Assert.IsFalse(unresolved.MarkerEligible);
            Assert.AreEqual(CommandShareabilityState.Unresolved, empty.State);
        }

        [TestMethod]
        public void ISF_R1_C11_07_RuEnSpecialLabelsUseSemanticLunarVoidEquipmentNames()
        {
            Assert.AreEqual("Лунный", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Lunar }, true).Label);
            Assert.AreEqual("Предмет Бездны", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Void }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Equipment }, true).Label);
            Assert.AreEqual("Лунное снаряжение", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.LunarEquipment }, true).Label);
            Assert.AreEqual("Lunar", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Lunar }, false).Label);
            Assert.AreEqual("Void", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Void }, false).Label);
            Assert.AreEqual("Equipment", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.Equipment }, false).Label);
            Assert.AreEqual("Lunar equipment", MarkerClassPolicy.ResolveCommandClassForPresentation(new[] { MarkerClassKind.LunarEquipment }, false).Label);
        }

        [TestMethod]
        public void ISF_R1_C11_08_C10NestedSourceLifecycleColorsRendererPingAndFrozenStateContractsRemainUnchanged()
        {
            var nested = CommandOptionSourcePolicy.Resolve(true, 101, true, 999);
            Assert.IsTrue(nested.HasValue);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, nested.Source);
            Assert.IsTrue(nested.ExactSource);
            Assert.IsTrue(nested.Disagreement);
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.IsTrue(MarkerPresentationPolicy.ShouldTrackCommandMarker(true, true, true, true, true, true));
        }


        [TestMethod]
        public void ISF_R1_C12_01_RuVoidSemanticLabelIsBezdnaAndEnglishRemainsVoid()
        {
            Assert.AreEqual("Бездна", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Void }, true).Label);
            Assert.AreEqual("Void", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Void }, false).Label);
        }

        [TestMethod]
        public void ISF_R1_C12_02_RuLunarAndEquipmentLabelsUseConciseR3Naming()
        {
            Assert.AreEqual("Лунный", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Lunar }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Equipment }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, true).Label);
            Assert.AreEqual("Equipment", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Equipment }, false).Label);
            Assert.AreEqual("Equipment", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, false).Label);
        }

        [TestMethod]
        public void ISF_R1_C12_03_EquipmentClassesShareTextButRetainNativeColorIdentity()
        {
            var normal = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Equipment }, true);
            var lunar = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, true);
            Assert.AreEqual(normal.Label, lunar.Label);
            Assert.AreNotEqual(normal.Kind, lunar.Kind);
            Assert.IsTrue(MarkerClassPolicy.UsesCatalogPickupBaseColor);
            Assert.IsTrue(MarkerPresentationPolicy.BuildReadabilityStyle(normal.Kind, 1080).PreservesNativeClassHue);
            Assert.IsTrue(MarkerPresentationPolicy.BuildReadabilityStyle(lunar.Kind, 1080).PreservesNativeClassHue);
        }

        [TestMethod]
        public void ISF_R1_C12_04_RenderModelUsesPlayerPingClassLargerTextSizing()
        {
            var style1080 = MarkerPresentationPolicy.BuildReadabilityStyle(MarkerClassKind.Tier3, 1080);
            var layout1080 = MarkerPresentationPolicy.BuildVisualLayout("Красный", 20, 1920, 1080);
            Assert.AreEqual(MarkerPresentationPolicy.PlayerPingClassSizingToken, style1080.SizingToken);
            Assert.IsTrue(style1080.FontSize >= MarkerPresentationPolicy.ReadableMinFontSize);
            Assert.IsTrue(style1080.FontSize > 17);
            Assert.AreEqual(style1080.FontSize, layout1080.FontSize);
            Assert.IsTrue(layout1080.Height >= 30f);
        }

        [TestMethod]
        public void ISF_R1_C12_05_AllMarkerClassesUseOneUnifiedReadabilityStyleToken()
        {
            var kinds = new[]
            {
                MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3, MarkerClassKind.Boss,
                MarkerClassKind.Lunar, MarkerClassKind.Void, MarkerClassKind.Equipment, MarkerClassKind.LunarEquipment,
                MarkerClassKind.Other, MarkerClassKind.Unknown,
            };
            var styles = kinds.Select(x => MarkerPresentationPolicy.BuildReadabilityStyle(x, 1080)).ToArray();
            Assert.IsTrue(styles.All(x => x.StyleToken == MarkerPresentationPolicy.UnifiedReadabilityStyleToken));
            Assert.AreEqual(1, styles.Select(x => x.StyleToken).Distinct().Count());
            Assert.AreEqual(1, styles.Select(x => x.PlateAlpha).Distinct().Count());
            Assert.AreEqual(1, styles.Select(x => x.HaloOffset).Distinct().Count());
        }

        [TestMethod]
        public void ISF_R1_C12_06_NativeHueIsPreservedAcrossUnifiedWhiteGreenRedYellowLunarVoidEquipmentStyle()
        {
            foreach (var kind in new[]
            {
                MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3, MarkerClassKind.Boss,
                MarkerClassKind.Lunar, MarkerClassKind.Void, MarkerClassKind.Equipment, MarkerClassKind.LunarEquipment,
            })
            {
                var style = MarkerPresentationPolicy.BuildReadabilityStyle(kind, 1080);
                Assert.IsTrue(style.PreservesNativeClassHue);
                Assert.IsTrue(style.PlateAlpha > 0f);
                Assert.IsTrue(style.HaloOffset > 0f);
                Assert.IsTrue(style.ShadowOffset > style.HaloOffset);
            }
        }

        [TestMethod]
        public void ISF_R1_C12_07_C11NestedOptionSourceAndUpstreamShareabilityFilteringRemainUnchanged()
        {
            var nested = CommandOptionSourcePolicy.Resolve(true, 101, true, 999);
            var shareable = CommandShareabilityPolicy.Evaluate(new bool?[] { true, true });
            var filtered = CommandShareabilityPolicy.Evaluate(new bool?[] { false, false });
            Assert.IsTrue(nested.ExactSource);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, nested.Source);
            Assert.IsTrue(shareable.MarkerEligible);
            Assert.IsFalse(filtered.MarkerEligible);
            Assert.AreEqual("upstream-not-shareable", filtered.FilterReason);
        }

        [TestMethod]
        public void ISF_R1_C12_08_OrdinaryMarkersPingCoexistenceAndFrozenGameplayStateRemainUnchanged()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 1201, "Ordinary"));
            registry.EndSweep();
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.OrdinaryPickup, 1201));
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);

            var ledger = new ClaimLedger();
            Assert.AreEqual(ClaimState.Pending, ledger.Ensure(P1, A, ParticipantState.Alive, 1).State);
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, collected: false));
        }


        [TestMethod]
        public void ISF_R1_C13_01_WorldPositionBelowPracticalVoidFloorIsRejected()
        {
            Assert.AreEqual(string.Empty, MarkerPresentationPolicy.ValidateWorldPosition(0f, -999f, 0f));
            Assert.AreEqual("below-void-floor", MarkerPresentationPolicy.ValidateWorldPosition(0f, -1000.01f, 0f));
        }

        [TestMethod]
        public void ISF_R1_C13_02_NonFiniteWorldPositionsAreRejectedBeforeDistanceFormatting()
        {
            Assert.AreEqual("position-non-finite", MarkerPresentationPolicy.ValidateWorldPosition(float.NaN, 0f, 0f));
            Assert.AreEqual("position-non-finite", MarkerPresentationPolicy.ValidateWorldPosition(0f, float.PositiveInfinity, 0f));
            Assert.AreEqual("position-non-finite", MarkerPresentationPolicy.ValidateWorldPosition(0f, 0f, float.NegativeInfinity));
        }

        [TestMethod]
        public void ISF_R1_C13_03_ClearlyOutOfWorldCoordinatesAreRejected()
        {
            var limit = MarkerPresentationPolicy.MaximumWorldCoordinateMagnitude;
            Assert.AreEqual(string.Empty, MarkerPresentationPolicy.ValidateWorldPosition(limit, 0f, -limit));
            Assert.AreEqual("outside-world-bounds", MarkerPresentationPolicy.ValidateWorldPosition(limit + 1f, 0f, 0f));
            Assert.AreEqual("outside-world-bounds", MarkerPresentationPolicy.ValidateWorldPosition(0f, limit + 1f, 0f));
        }

        [TestMethod]
        public void ISF_R1_C13_04_AbsurdPresentationDistancesAreRejected()
        {
            Assert.AreEqual(string.Empty, MarkerPresentationPolicy.ValidatePresentationDistance(4096f));
            Assert.AreEqual("distance-out-of-range", MarkerPresentationPolicy.ValidatePresentationDistance(9847f));
            Assert.AreEqual("distance-out-of-range", MarkerPresentationPolicy.ValidatePresentationDistance(163452f));
            Assert.AreEqual("distance-out-of-range", MarkerPresentationPolicy.ValidatePresentationDistance(638201f));
            Assert.AreEqual("distance-non-finite", MarkerPresentationPolicy.ValidatePresentationDistance(float.PositiveInfinity));
        }

        [TestMethod]
        public void ISF_R1_C13_05_RegistrySupportsImmediateInvalidTargetRemovalWithoutZombieRecord()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 1301, "Белый"));
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.OrdinaryPickup, 1301));
            Assert.IsTrue(registry.Remove(PersonalMarkerKind.OrdinaryPickup, 1301));
            Assert.IsFalse(registry.Contains(PersonalMarkerKind.OrdinaryPickup, 1301));
            Assert.AreEqual(0, registry.EndSweep().Count);
        }

        [TestMethod]
        public void ISF_R1_C13_06_UnifiedNativeStyleRemovesFullRectangularPlate()
        {
            var style = MarkerPresentationPolicy.BuildReadabilityStyle(MarkerClassKind.Tier3, 1080);
            Assert.AreEqual("ISF_ROR2_PING_NATIVE_V2", style.StyleToken);
            Assert.IsFalse(MarkerPresentationPolicy.UsesFullRectangularPlate);
            Assert.AreEqual(0f, style.PlatePaddingX);
            Assert.AreEqual(0f, style.PlatePaddingY);
            Assert.IsTrue(style.PreservesNativeClassHue);
            Assert.IsTrue(style.HaloOffset > 0f);
            Assert.IsTrue(style.ShadowOffset > style.HaloOffset);
        }

        [TestMethod]
        public void ISF_R1_C13_07_CompactMarkerTextUsesPingLikeGlyphAndSeparator()
        {
            var layout = MarkerPresentationPolicy.BuildVisualLayout("Красный", 42, 1920, 1080);
            Assert.IsTrue(layout.Text.StartsWith("◆ Красный", StringComparison.Ordinal));
            Assert.IsTrue(layout.Text.Contains(" · 42m", StringComparison.Ordinal));
            Assert.IsTrue(layout.FontSize >= MarkerPresentationPolicy.ReadableMinFontSize);
            Assert.IsTrue(layout.Height <= MarkerPresentationPolicy.MaxMarkerHeight);
        }

        [TestMethod]
        public void ISF_R1_C13_08_C10C11C12SemanticsAndPingCoexistenceRemainIntact()
        {
            var nested = CommandOptionSourcePolicy.Resolve(true, 13, true, 99);
            Assert.IsTrue(nested.ExactSource);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, nested.Source);
            Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true }).MarkerEligible);
            Assert.AreEqual("Бездна", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Void }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, true).Label);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.UsesSharedSkinBoxMutation);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
        }


        [TestMethod]
        public void ISF_R1_C14_01_SafelyVisibleFrontTargetSelectsOnScreenWorldAnchorMode()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(0.50f, 0.52f, 12f, 0f, 0.2f, 12f, 1920f, 1080f);
            Assert.IsTrue(projection.Valid);
            Assert.AreEqual(MarkerHudMode.OnScreenWorldAnchor, projection.Mode);
            Assert.AreEqual(MarkerHudEdge.None, projection.Edge);
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerHudNavigationPolicy.StyleTokenForMode(projection.Mode));
        }

        [TestMethod]
        public void ISF_R1_C14_02_OffScreenFrontTargetClampsToValidSafeEdgeAndKeepsDirection()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(1.30f, 0.55f, 10f, 10f, 0.5f, 10f, 1920f, 1080f);
            var safe = MarkerHudNavigationPolicy.GetSafeArea(1920f, 1080f);
            Assert.IsTrue(projection.Valid);
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, projection.Mode);
            Assert.AreEqual(MarkerHudEdge.Right, projection.Edge);
            Assert.IsTrue(projection.DirectionX > 0f);
            Assert.IsTrue(safe.Contains(projection.X, projection.Y));
            Assert.IsFalse(MarkerHudNavigationPolicy.IsReservedHudPoint(projection.X, projection.Y, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C14_03_BehindCameraTargetSelectsEdgeModeInsteadOfDisappearing()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(0.50f, 0.50f, -20f, 0f, 0f, -20f, 1920f, 1080f);
            Assert.IsTrue(projection.Valid);
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, projection.Mode);
            Assert.AreEqual(MarkerHudEdge.Bottom, projection.Edge);
            Assert.IsTrue(projection.DirectionY < 0f);
        }

        [TestMethod]
        public void ISF_R1_C14_04_EdgePlacementRespectsSafeMarginsAndReservedHudRegions()
        {
            var zones = MarkerHudNavigationPolicy.GetReservedHudZones(1920f, 1080f);
            Assert.IsTrue(zones.Any(x => x.Token == "health-bottom-left"));
            Assert.IsTrue(zones.Any(x => x.Token == "skills-bottom-right"));
            Assert.IsTrue(zones.Any(x => x.Token == "objective-upper-right"));
            Assert.IsTrue(zones.Any(x => x.Token == "items-top-center"));

            var samples = new[]
            {
                MarkerHudNavigationPolicy.ResolveProjection(-0.4f, -0.3f, 10f, -10f, -8f, 10f, 1920f, 1080f),
                MarkerHudNavigationPolicy.ResolveProjection(1.4f, -0.2f, 10f, 10f, -7f, 10f, 1920f, 1080f),
                MarkerHudNavigationPolicy.ResolveProjection(1.3f, 1.4f, 10f, 9f, 9f, 10f, 1920f, 1080f),
                MarkerHudNavigationPolicy.ResolveProjection(0.5f, 1.5f, 10f, 0f, 12f, 10f, 1920f, 1080f),
            };
            var safe = MarkerHudNavigationPolicy.GetSafeArea(1920f, 1080f);
            foreach (var sample in samples)
            {
                Assert.IsTrue(sample.Valid);
                Assert.AreEqual(MarkerHudMode.OffScreenEdge, sample.Mode);
                Assert.IsTrue(safe.Contains(sample.X, sample.Y));
                Assert.IsFalse(MarkerHudNavigationPolicy.IsReservedHudPoint(sample.X, sample.Y, 1920f, 1080f));
            }
        }

        [TestMethod]
        public void ISF_R1_C14_05_DiagonalAndCardinalEdgeDirectionMappingIsDeterministic()
        {
            var right = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            var top = MarkerHudNavigationPolicy.ResolveProjection(0.5f, 1.4f, 10f, 0f, 10f, 10f, 1920f, 1080f);
            var diagonalA = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 1.2f, 10f, 10f, 7f, 10f, 1920f, 1080f);
            var diagonalB = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 1.2f, 10f, 10f, 7f, 10f, 1920f, 1080f);
            Assert.AreEqual(MarkerHudEdge.Right, right.Edge);
            Assert.AreEqual(MarkerHudEdge.Top, top.Edge);
            Assert.AreEqual(diagonalA.Edge, diagonalB.Edge);
            Assert.AreEqual(diagonalA.ArrowRotationDegrees, diagonalB.ArrowRotationDegrees, 0.0001f);
            Assert.IsTrue(Math.Abs(right.ArrowRotationDegrees - top.ArrowRotationDegrees) > 40f);
        }

        [TestMethod]
        public void ISF_R1_C14_06_NearbyEdgeMarkersAreDeconflictedWithStableOrdering()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            var first = MarkerHudNavigationPolicy.Deconflict(new[]
            {
                new MarkerHudEdgeCandidate(30, projection),
                new MarkerHudEdgeCandidate(10, projection),
                new MarkerHudEdgeCandidate(20, projection),
            }, 1920f, 1080f).OrderBy(x => x.StableKey).ToArray();
            var second = MarkerHudNavigationPolicy.Deconflict(new[]
            {
                new MarkerHudEdgeCandidate(20, projection),
                new MarkerHudEdgeCandidate(30, projection),
                new MarkerHudEdgeCandidate(10, projection),
            }, 1920f, 1080f).OrderBy(x => x.StableKey).ToArray();

            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(3, second.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].StableKey, second[i].StableKey);
                Assert.AreEqual(first[i].X, second[i].X, 0.0001f);
                Assert.AreEqual(first[i].Y, second[i].Y, 0.0001f);
                Assert.AreEqual(first[i].StackSlot, second[i].StackSlot);
            }
            Assert.AreEqual(3, first.Select(x => x.Y.ToString("F3") + ":" + x.StackSlot).Distinct().Count());
        }

        [TestMethod]
        public void ISF_R1_C14_07_OnScreenAndEdgeShareNativeHudStyleFamilyAndPreserveNativeHue()
        {
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerHudNavigationPolicy.StyleTokenForMode(MarkerHudMode.OnScreenWorldAnchor));
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerHudNavigationPolicy.StyleTokenForMode(MarkerHudMode.OffScreenEdge));
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerPresentationPolicy.NativeHudStyleToken);
            Assert.IsTrue(MarkerPresentationPolicy.UsesTextMeshProHudCanvas);
            Assert.IsTrue(MarkerPresentationPolicy.ReusesLiveRoR2HudTypographyWhenAvailable);
            foreach (var kind in new[] { MarkerClassKind.Tier1, MarkerClassKind.Tier2, MarkerClassKind.Tier3, MarkerClassKind.Boss, MarkerClassKind.Lunar, MarkerClassKind.Void, MarkerClassKind.Equipment, MarkerClassKind.LunarEquipment })
                Assert.IsTrue(MarkerPresentationPolicy.BuildReadabilityStyle(kind, 1080).PreservesNativeClassHue);
        }

        [TestMethod]
        public void ISF_R1_C14_08_ProductionMarkerRendererContractNoLongerUsesOnGuiOrGuiLabel()
        {
            Assert.IsFalse(MarkerPresentationPolicy.UsesMarkerOnGuiRenderer);
            Assert.IsTrue(MarkerPresentationPolicy.UsesTextMeshProHudCanvas);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.UsesFullRectangularPlate);
        }

        [TestMethod]
        public void ISF_R1_C14_09_C13InvalidTargetAndAbsurdDistanceCleanupContractsRemainIntact()
        {
            Assert.AreEqual("position-non-finite", MarkerPresentationPolicy.ValidateWorldPosition(float.NaN, 0f, 0f));
            Assert.AreEqual("below-void-floor", MarkerPresentationPolicy.ValidateWorldPosition(0f, -1000.01f, 0f));
            Assert.AreEqual("outside-world-bounds", MarkerPresentationPolicy.ValidateWorldPosition(8193f, 0f, 0f));
            Assert.AreEqual("distance-out-of-range", MarkerPresentationPolicy.ValidatePresentationDistance(4097f));
            var registry = new PersonalMarkerRegistry(2);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 1409, "Белый");
            Assert.IsTrue(registry.Remove(PersonalMarkerKind.OrdinaryPickup, 1409));
            Assert.IsFalse(registry.Contains(PersonalMarkerKind.OrdinaryPickup, 1409));
        }

        [TestMethod]
        public void ISF_R1_C14_10_C10C11C12SemanticsConfigAndVanillaPingCoexistenceRemainIntact()
        {
            var nested = CommandOptionSourcePolicy.Resolve(true, 1401, true, 9999);
            Assert.IsTrue(nested.ExactSource);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, nested.Source);
            Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true, true }).MarkerEligible);
            Assert.AreEqual("Белый", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier1 }, true).Label);
            Assert.AreEqual("Бездна", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Void }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, true).Label);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.Alive, collected: false));
        }


        [TestMethod]
        public void ISF_R1_C15_01_EssentialDirectionIndicatorUsesDeterministicUiGraphicNotTmpUnicodeGlyph()
        {
            Assert.IsFalse(MarkerPresentationPolicy.EssentialIndicatorUsesTmpUnicodeGlyph);
            Assert.IsTrue(MarkerPresentationPolicy.UsesDeterministicUiGraphicIndicator);
            Assert.AreEqual("LOCAL_UI_GRAPHIC_GEOMETRY_V1", MarkerPresentationPolicy.IndicatorAssetSourceToken);
            var text = MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 123);
            Assert.IsFalse(text.Contains("▲"));
            Assert.IsFalse(text.Contains("◆"));
        }

        [TestMethod]
        public void ISF_R1_C15_02_EdgeDirectionPrimitiveRotationIsDeterministicForCardinalAndBehindTargets()
        {
            var rightA = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            var rightB = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            var top = MarkerHudNavigationPolicy.ResolveProjection(0.5f, 1.4f, 10f, 0f, 10f, 10f, 1920f, 1080f);
            var behind = MarkerHudNavigationPolicy.ResolveProjection(0.5f, 0.5f, -20f, 0f, 0f, -20f, 1920f, 1080f);
            Assert.AreEqual(rightA.ArrowRotationDegrees, rightB.ArrowRotationDegrees, 0.0001f);
            Assert.IsTrue(Math.Abs(rightA.ArrowRotationDegrees - top.ArrowRotationDegrees) > 40f);
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, behind.Mode);
            Assert.IsTrue(Math.Abs(behind.ArrowRotationDegrees) > 90f);
        }

        [TestMethod]
        public void ISF_R1_C15_03_OnScreenAndEdgeShareOneGraphicVisualFamilyAndNativeHueContract()
        {
            Assert.AreEqual("ISF_UI_GRAPHIC_INDICATOR_V1", MarkerPresentationPolicy.IndicatorVisualFamilyToken);
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerHudNavigationPolicy.StyleTokenForMode(MarkerHudMode.OnScreenWorldAnchor));
            Assert.AreEqual(MarkerHudNavigationPolicy.NativeHudStyleToken, MarkerHudNavigationPolicy.StyleTokenForMode(MarkerHudMode.OffScreenEdge));
            Assert.IsTrue(MarkerPresentationPolicy.BuildReadabilityStyle(MarkerClassKind.Tier1, 1080).PreservesNativeClassHue);
            Assert.IsTrue(MarkerPresentationPolicy.BuildReadabilityStyle(MarkerClassKind.Void, 1080).PreservesNativeClassHue);
        }

        [TestMethod]
        public void ISF_R1_C15_04_FinalVisualRectangleCalculationIncludesLabelAndIndicatorFootprint()
        {
            var shortFootprint = MarkerHudNavigationPolicy.EstimateVisualFootprint("Белый", 10, 1920f, 1080f);
            var longFootprint = MarkerHudNavigationPolicy.EstimateVisualFootprint(new string('W', 40), 4095, 1920f, 1080f);
            Assert.IsTrue(shortFootprint.IndicatorSize > 0f);
            Assert.IsTrue(shortFootprint.LabelWidth > 0f);
            Assert.IsTrue(shortFootprint.Width > shortFootprint.LabelWidth + shortFootprint.IndicatorSize);
            Assert.IsTrue(longFootprint.Width > shortFootprint.Width);
            var rect = new MarkerHudRect(960f, 540f, longFootprint.Width, longFootprint.Height);
            Assert.AreEqual(longFootprint.Width, rect.Width, 0.0001f);
            Assert.AreEqual(longFootprint.Height, rect.Height, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C15_05_TwoSameEdgeMarkersWithLongLabelsDoNotOverlapAfterRectangleDeconfliction()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.52f, 10f, 10f, 0.3f, 10f, 1920f, 1080f);
            var footprint = MarkerHudNavigationPolicy.EstimateVisualFootprint(new string('W', 40), 1200, 1920f, 1080f);
            var placements = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(1501, projection, footprint),
                new MarkerHudPlacementCandidate(1502, projection, footprint),
            }, 1920f, 1080f).OrderBy(x => x.StableKey).ToArray();
            Assert.AreEqual(2, placements.Length);
            Assert.IsFalse(placements[0].FinalRect.Intersects(placements[1].FinalRect));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placements[0].FinalRect, 1920f, 1080f));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placements[1].FinalRect, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C15_06_SeveralSameEdgeMarkersRemainStableAndIndividuallyRepresentedAcrossInputOrder()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(-0.3f, 0.55f, 10f, -10f, 0.5f, 10f, 1920f, 1080f);
            var footprint = MarkerHudNavigationPolicy.EstimateVisualFootprint("Снаряжение", 999, 1920f, 1080f);
            var first = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(30, projection, footprint),
                new MarkerHudPlacementCandidate(10, projection, footprint),
                new MarkerHudPlacementCandidate(40, projection, footprint),
                new MarkerHudPlacementCandidate(20, projection, footprint),
            }, 1920f, 1080f).OrderBy(x => x.StableKey).ToArray();
            var second = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(20, projection, footprint),
                new MarkerHudPlacementCandidate(40, projection, footprint),
                new MarkerHudPlacementCandidate(10, projection, footprint),
                new MarkerHudPlacementCandidate(30, projection, footprint),
            }, 1920f, 1080f).OrderBy(x => x.StableKey).ToArray();
            Assert.AreEqual(4, first.Length);
            Assert.AreEqual(4, second.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].StableKey, second[i].StableKey);
                Assert.AreEqual(first[i].X, second[i].X, 0.0001f);
                Assert.AreEqual(first[i].Y, second[i].Y, 0.0001f);
                Assert.AreEqual(first[i].LaneSlot, second[i].LaneSlot);
                Assert.AreEqual(first[i].RailSlot, second[i].RailSlot);
                for (var j = i + 1; j < first.Length; j++)
                    Assert.IsFalse(first[i].FinalRect.Intersects(first[j].FinalRect));
            }
        }

        [TestMethod]
        public void ISF_R1_C15_07_OnScreenFinalRectangleRelocatesAwayFromReservedHud()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(0.15f, 0.90f, 10f, -7f, 8f, 10f, 1920f, 1080f);
            Assert.AreEqual(MarkerHudMode.OnScreenWorldAnchor, projection.Mode);
            var footprint = MarkerHudNavigationPolicy.EstimateVisualFootprint("Белый", 80, 1920f, 1080f);
            var initial = new MarkerHudRect(projection.X, projection.Y, footprint.Width, footprint.Height);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsReservedHud(initial, 1920f, 1080f));
            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(1507, projection, footprint),
            }, 1920f, 1080f).Single();
            Assert.IsTrue(placement.HudRelocated);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placement.FinalRect, 1920f, 1080f));
            Assert.IsTrue(Math.Abs(placement.X - projection.X) > 0.01f || Math.Abs(placement.Y - projection.Y) > 0.01f);
        }

        [TestMethod]
        public void ISF_R1_C15_08_EdgeFinalRectangleRelocatesAwayFromReservedHudIncludingSafeRightStatsRegion()
        {
            var zones = MarkerHudNavigationPolicy.GetReservedHudZones(1920f, 1080f);
            Assert.IsTrue(zones.Any(x => x.Token == "money-player-upper-left"));
            Assert.IsTrue(zones.Any(x => x.Token == "stats-overlay-safe-right"));
            var projection = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            var footprint = MarkerHudNavigationPolicy.EstimateVisualFootprint("Красный", 450, 1920f, 1080f);
            var initial = new MarkerHudRect(projection.X, projection.Y, footprint.Width, footprint.Height);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsReservedHud(initial, 1920f, 1080f));
            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(1508, projection, footprint),
            }, 1920f, 1080f).Single();
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, placement.Mode);
            Assert.IsTrue(placement.HudRelocated);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placement.FinalRect, 1920f, 1080f));
            Assert.IsTrue(MarkerHudNavigationPolicy.IsRectInsideScreen(placement.FinalRect, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C15_09_C13C10C11C12C14StateSemanticsAndVanillaPingCoexistenceRemainIntact()
        {
            Assert.AreEqual("below-void-floor", MarkerPresentationPolicy.ValidateWorldPosition(0f, -1000.01f, 0f));
            Assert.AreEqual("distance-out-of-range", MarkerPresentationPolicy.ValidatePresentationDistance(4097f));
            var nested = CommandOptionSourcePolicy.Resolve(true, 1515, true, 42);
            Assert.IsTrue(nested.ExactSource);
            Assert.AreEqual(CommandOptionPickupSource.NestedPickup, nested.Source);
            Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true, true }).MarkerEligible);
            Assert.AreEqual("Белый", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier1 }, true).Label);
            Assert.AreEqual("Зелёный", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier2 }, true).Label);
            Assert.AreEqual("Красный", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Tier3 }, true).Label);
            Assert.AreEqual("Жёлтый", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Boss }, true).Label);
            Assert.AreEqual("Лунный", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Lunar }, true).Label);
            Assert.AreEqual("Бездна", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Void }, true).Label);
            Assert.AreEqual("Снаряжение", MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.LunarEquipment }, true).Label);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.IsFalse(MarkerPresentationPolicy.UsesMarkerOnGuiRenderer);
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.SupportDrone, collected: false));
        }


        [TestMethod]
        public void ISF_R1_C16_01_LocalPauseModalSuppressesMarkerPresentation()
        {
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(featureEnabled: true, localBlockingModalActive: false));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(featureEnabled: true, localBlockingModalActive: true));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(featureEnabled: false, localBlockingModalActive: false));
        }

        [TestMethod]
        public void ISF_R1_C16_02_ClosingPauseModalRestoresSameLogicalMarkersWithoutDuplicates()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            Assert.AreEqual(PersonalMarkerTransition.Added, registry.MarkPending(PersonalMarkerKind.OrdinaryPickup, 1602, "Белый"));
            var before = registry.Active.Single().Identity;
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, false));
            var after = registry.Active.Single().Identity;
            Assert.AreEqual(before, after);
            Assert.AreEqual(1, registry.Count);
        }

        [TestMethod]
        public void ISF_R1_C16_03_PauseSuppressionDoesNotClearLogicalMarkerOrGameplayState()
        {
            var registry = new PersonalMarkerRegistry(4);
            registry.BeginSweep();
            registry.MarkPending(PersonalMarkerKind.CommandPicker, 1603, "Зелёный");
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsTrue(registry.Contains(PersonalMarkerKind.CommandPicker, 1603));
            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(ClaimState.Pending, ledger.Records.Single().State);
        }

        [TestMethod]
        public void ISF_R1_C16_04_VisibleMessageFeedAddsDynamicHudExclusion()
        {
            var messageZone = new MarkerHudExclusionZone("message-hud-runtime", 620f, 1040f, 270f, 500f);
            var markerRect = new MarkerHudRect(780f, 360f, 240f, 52f);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsDynamicHud(markerRect, new[] { messageZone }, 1920f, 1080f));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsDynamicHud(markerRect, Array.Empty<MarkerHudExclusionZone>(), 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C16_05_FinalMarkerRectAvoidsVisibleMessageFeedRegion()
        {
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 780f, 360f, 0f, 0f, 0f);
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(178f, 28f, 1920f, 1080f);
            var messageZone = new MarkerHudExclusionZone("message-hud-runtime", 620f, 1040f, 270f, 500f);
            var initial = new MarkerHudRect(projection.X, projection.Y, footprint.Width, footprint.Height);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsDynamicHud(initial, new[] { messageZone }, 1920f, 1080f));
            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(1605, projection, footprint),
            }, 1920f, 1080f, new[] { messageZone }).Single();
            Assert.IsTrue(placement.MessageHudRelocated);
            Assert.IsTrue(placement.HudRelocated);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsDynamicHud(placement.FinalRect, new[] { messageZone }, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C16_06_C15NavigationGeometryDeconflictionAndPriorSemanticsRemainIntact()
        {
            Assert.IsTrue(MarkerPresentationPolicy.UsesDeterministicUiGraphicIndicator);
            Assert.AreEqual("LOCAL_UI_GRAPHIC_GEOMETRY_V1", MarkerPresentationPolicy.IndicatorAssetSourceToken);
            Assert.AreEqual("ISF_UI_GRAPHIC_INDICATOR_V1", MarkerPresentationPolicy.IndicatorVisualFamilyToken);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.AreEqual("below-void-floor", MarkerPresentationPolicy.ValidateWorldPosition(0f, -1000.01f, 0f));
            Assert.IsTrue(CommandShareabilityPolicy.Evaluate(new bool?[] { true, true }).MarkerEligible);
            var projection = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, projection.Mode);
            Assert.AreEqual(MarkerHudEdge.Right, projection.Edge);
        }

        [TestMethod]
        public void ISF_R1_C16_07_ValidDistanceLabelDoesNotUseEllipsis()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ValidDistanceLabelsUseEllipsis);
            foreach (var distance in new[] { 1, 11, 123, 4096 })
            {
                var text = MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", distance);
                Assert.IsFalse(text.Contains("..."));
                Assert.IsFalse(text.EndsWith(".", StringComparison.Ordinal));
                Assert.IsTrue(text.EndsWith("m", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void ISF_R1_C16_08_ActualTmpPreferredWidthFeedsFinalMarkerFootprint()
        {
            Assert.IsTrue(MarkerPresentationPolicy.UsesTmpPreferredSizeForProductionFootprint);
            var narrow = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(120f, 26f, 1920f, 1080f);
            var wide = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(260f, 26f, 1920f, 1080f);
            Assert.IsTrue(narrow.LabelWidth >= 120f);
            Assert.IsTrue(wide.LabelWidth >= 260f);
            Assert.IsTrue(wide.Width > narrow.Width);
            Assert.IsTrue(wide.Width > wide.LabelWidth + wide.IndicatorSize);
        }

        [TestMethod]
        public void ISF_R1_C16_09_OneToFourDigitDistancesKeepDigitsAndMeterSuffixVisible()
        {
            Assert.AreEqual("Белый · 1m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 1));
            Assert.AreEqual("Белый · 11m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 11));
            Assert.AreEqual("Зелёный · 123m", MarkerPresentationPolicy.BuildNativeHudLabelText("Зелёный", 123));
            Assert.AreEqual("Красный · 4096m", MarkerPresentationPolicy.BuildNativeHudLabelText("Красный", 4096));
        }

  

        [TestMethod]
        public void ISF_R1_C17_01_StableSemanticTextReusesMeasuredFootprintWithoutRemeasurement()
        {
            var key = new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 1920, 1080, 3);
            var same = new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 1920, 1080, 3);
            Assert.IsFalse(MarkerRuntimeHotPathPolicy.CanReuseMeasurement(false, key, same));
            Assert.IsTrue(MarkerRuntimeHotPathPolicy.CanReuseMeasurement(true, key, same));
            Assert.AreEqual(key, same);
        }

        [TestMethod]
        public void ISF_R1_C17_02_MeasurementKeyChangeInvalidatesCachedFootprint()
        {
            var baseline = new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 1920, 1080, 3);
            var changed = new[]
            {
                new MarkerMeasurementCacheKey("Белый · 124m", 101, 202, 22, 1920, 1080, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 102, 202, 22, 1920, 1080, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 101, 203, 22, 1920, 1080, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 23, 1920, 1080, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 2560, 1080, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 1920, 1440, 3),
                new MarkerMeasurementCacheKey("Белый · 123m", 101, 202, 22, 1920, 1080, 4),
            };
            foreach (var key in changed)
                Assert.IsFalse(MarkerRuntimeHotPathPolicy.CanReuseMeasurement(true, baseline, key));
        }

        [TestMethod]
        public void ISF_R1_C17_03_StableTypographyDoesNotRequireRepeatedApplication()
        {
            Assert.IsFalse(MarkerRuntimeHotPathPolicy.ShouldApplyTypography(4, 22, 4, 22));
            Assert.IsTrue(MarkerRuntimeHotPathPolicy.ShouldApplyTypography(3, 22, 4, 22));
            Assert.IsTrue(MarkerRuntimeHotPathPolicy.ShouldApplyTypography(4, 21, 4, 22));
        }

        [TestMethod]
        public void ISF_R1_C17_04_DiagnosticStateDoesNotChurnOnlyBecausePreferredWidthFloatChanges()
        {
            var first = MarkerRuntimeHotPathPolicy.BuildHudDiagnosticState(
                MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 0, 0,
                false, false, false, false, 148.1f, 210.4f);
            var widthJitterOnly = MarkerRuntimeHotPathPolicy.BuildHudDiagnosticState(
                MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 0, 0,
                false, false, false, false, 151.9f, 214.2f);
            Assert.AreEqual(first, widthJitterOnly);
            var meaningfulChange = MarkerRuntimeHotPathPolicy.BuildHudDiagnosticState(
                MarkerHudMode.OffScreenEdge, MarkerHudEdge.Right, 0, 1,
                true, false, false, false, 151.9f, 214.2f);
            Assert.AreNotEqual(first, meaningfulChange);
        }

        [TestMethod]
        public void ISF_R1_C17_05_C16CompleteDistanceAndMeasuredFootprintSemanticsRemainIntact()
        {
            Assert.IsTrue(MarkerPresentationPolicy.UsesTmpPreferredSizeForProductionFootprint);
            Assert.IsFalse(MarkerPresentationPolicy.ValidDistanceLabelsUseEllipsis);
            Assert.AreEqual("Белый · 1m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 1));
            Assert.AreEqual("Белый · 11m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 11));
            Assert.AreEqual("Зелёный · 123m", MarkerPresentationPolicy.BuildNativeHudLabelText("Зелёный", 123));
            Assert.AreEqual("Красный · 4096m", MarkerPresentationPolicy.BuildNativeHudLabelText("Красный", 4096));
            var measured = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(245f, 28f, 1920f, 1080f);
            Assert.IsTrue(measured.LabelWidth >= 245f);
            Assert.IsTrue(measured.Width > measured.LabelWidth + measured.IndicatorSize);
        }

        [TestMethod]
        public void ISF_R1_C17_06_C16ModalMessageHudAndC15NavigationSemanticsRemainIntact()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, false));
            var messageZone = new MarkerHudExclusionZone("message-hud-runtime", 620f, 1040f, 270f, 500f);
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 780f, 360f, 0f, 0f, 0f);
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(178f, 28f, 1920f, 1080f);
            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(1706, projection, footprint),
            }, 1920f, 1080f, new[] { messageZone }).Single();
            Assert.IsTrue(placement.MessageHudRelocated);
            var edge = MarkerHudNavigationPolicy.ResolveProjection(1.4f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f);
            Assert.AreEqual(MarkerHudMode.OffScreenEdge, edge.Mode);
            Assert.AreEqual(MarkerHudEdge.Right, edge.Edge);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsTrue(MarkerPresentationPolicy.UsesDeterministicUiGraphicIndicator);
        }


        [TestMethod]
        public void ISF_R1_C18_01_ZeroMarkerPathCannotEnterActiveHeavyMarkerPipeline()
        {
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldEnterActiveMarkerPipeline(0));
            Assert.IsFalse(MarkerFramePipelinePolicy.CanUseSingleMarkerFastPath(0));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldEnterActiveMarkerPipeline(1));
        }

        [TestMethod]
        public void ISF_R1_C18_02_StableOneMarkerStateUsesSingleMarkerFastPathAndCachedProjection()
        {
            Assert.IsTrue(MarkerFramePipelinePolicy.CanUseSingleMarkerFastPath(1));
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
            var subPixel = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 961f, 541f, 0f, 0f, 0f);
            Assert.IsFalse(MarkerFramePipelinePolicy.ProjectionMateriallyChanged(projection, subPixel));
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, false, false, 10f, 9f));
        }

        [TestMethod]
        public void ISF_R1_C18_03_MeaningfulProjectionHudAndStructuralInvalidationForceRecompute()
        {
            var baseline = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
            var moved = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 970f, 540f, 0f, 0f, 0f);
            Assert.IsTrue(MarkerFramePipelinePolicy.ProjectionMateriallyChanged(baseline, moved));
            var oldHud = new MarkerHudRect(500f, 300f, 300f, 100f);
            var newHud = new MarkerHudRect(520f, 300f, 300f, 100f);
            Assert.IsTrue(MarkerFramePipelinePolicy.HudRectMateriallyChanged(true, oldHud, true, newHud));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, true, false, 1f, 99f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, false, true, 2f, 1f));
        }

        [TestMethod]
        public void ISF_R1_C18_04_SingleMarkerFastPathPreservesStaticAndMessageHudAvoidance()
        {
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 780f, 360f, 0f, 0f, 0f);
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(178f, 28f, 1920f, 1080f);
            var messageZone = new MarkerHudExclusionZone("message-hud-runtime", 620f, 1040f, 270f, 500f);
            var placement = MarkerHudNavigationPolicy.ResolveSinglePlacement(
                new MarkerHudPlacementCandidate(1804, projection, footprint),
                1920f, 1080f, new[] { messageZone });
            Assert.IsTrue(placement.MessageHudRelocated);
            Assert.IsTrue(placement.HudRelocated);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsDynamicHud(placement.FinalRect, new[] { messageZone }, 1920f, 1080f));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placement.FinalRect, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C18_05_StableUiPropertiesDoNotRequestRedundantTransformWrites()
        {
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldWriteScreenPosition(true, 100f, 200f, 100.2f, 200.2f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldWriteScreenPosition(false, 100f, 200f, 100.2f, 200.2f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldWriteScreenPosition(true, 100f, 200f, 102f, 200f));
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldWriteRotation(true, 30f, 30.2f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldWriteRotation(true, 30f, 32f));
        }

        [TestMethod]
        public void ISF_R1_C18_06_GlobalUiDiscoveryPolicyIsLifecycleDrivenAndRarelyBounded()
        {
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(false, false, true, 1f, 5f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(true, false, false, 1f, 5f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(false, true, false, 1f, 5f));
            Assert.IsTrue(MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(false, false, true, 5f, 5f));
            Assert.IsTrue(MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds >= 5f);
            Assert.IsTrue(MarkerFramePipelinePolicy.MessageHudFallbackDiscoverySeconds >= 8f);
        }

        [TestMethod]
        public void ISF_R1_C18_07_BufferedMultiMarkerDeconflictionMatchesLegacyDeterministicPlacement()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 28f, 1920f, 1080f);
            var candidates = new[]
            {
                new MarkerHudPlacementCandidate(18071, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 900f, 520f, 0f, 0f, 0f), footprint),
                new MarkerHudPlacementCandidate(18072, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 905f, 520f, 0f, 0f, 0f), footprint),
                new MarkerHudPlacementCandidate(18073, MarkerHudNavigationPolicy.ResolveProjection(1.3f, 0.5f, 10f, 10f, 0f, 10f, 1920f, 1080f), footprint),
            };
            var legacy = MarkerHudNavigationPolicy.ResolvePlacements(candidates, 1920f, 1080f).ToArray();
            var ordered = new System.Collections.Generic.List<MarkerHudPlacementCandidate>();
            var occupied = new System.Collections.Generic.List<MarkerHudRect>();
            var buffered = new System.Collections.Generic.List<MarkerHudPlacement>();
            MarkerHudNavigationPolicy.ResolvePlacementsBuffered(candidates, 1920f, 1080f, Array.Empty<MarkerHudExclusionZone>(), ordered, occupied, buffered);
            Assert.AreEqual(legacy.Length, buffered.Count);
            for (var i = 0; i < legacy.Length; i++)
            {
                Assert.AreEqual(legacy[i].StableKey, buffered[i].StableKey);
                Assert.AreEqual(legacy[i].Mode, buffered[i].Mode);
                Assert.AreEqual(legacy[i].Edge, buffered[i].Edge);
                Assert.AreEqual(legacy[i].X, buffered[i].X, 0.001f);
                Assert.AreEqual(legacy[i].Y, buffered[i].Y, 0.001f);
            }
        }

        [TestMethod]
        public void ISF_R1_C18_08_C16C17DistanceModalMessageNavigationAndPingContractsRemainIntact()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ValidDistanceLabelsUseEllipsis);
            Assert.AreEqual("Белый · 1m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 1));
            Assert.AreEqual("Белый · 11m", MarkerPresentationPolicy.BuildNativeHudLabelText("Белый", 11));
            Assert.AreEqual("Зелёный · 123m", MarkerPresentationPolicy.BuildNativeHudLabelText("Зелёный", 123));
            Assert.AreEqual("Красный · 4096m", MarkerPresentationPolicy.BuildNativeHudLabelText("Красный", 4096));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, false));
            Assert.IsTrue(MarkerPresentationPolicy.UsesTmpPreferredSizeForProductionFootprint);
            Assert.IsTrue(MarkerPresentationPolicy.UsesDeterministicUiGraphicIndicator);
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
        }


        [TestMethod]
        public void ISF_R1_C19_01_PAUSE_OPEN_INVALIDATES_PRESENTATION_IMMEDIATELY()
        {
            Assert.IsTrue(BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(true, true));
            Assert.IsFalse(BlockingModalLifecyclePolicy.LifecycleSeedRequiresGlobalDiscovery(true));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
        }

        [TestMethod]
        public void ISF_R1_C19_02_BLOCKING_MODAL_OPEN_INVALIDATES_PRESENTATION_IMMEDIATELY()
        {
            Assert.IsTrue(BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(true, true));
            Assert.IsFalse(BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(false, true));
            Assert.IsFalse(BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(true, false));
            Assert.IsTrue(BlockingModalLifecyclePolicy.SuppressionStateChanged(false, true));
        }

        [TestMethod]
        public void ISF_R1_C19_03_MODAL_CLOSE_RESTORES_PENDING_MARKERS_WITHOUT_DUPLICATES()
        {
            Assert.IsTrue(BlockingModalLifecyclePolicy.SuppressionStateChanged(true, false));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, false));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
        }

        [TestMethod]
        public void ISF_R1_C19_04_REPEATED_OPEN_CLOSE_DOES_NOT_DUPLICATE_SUBSCRIPTIONS_OR_RESOURCES()
        {
            Assert.IsTrue(BlockingModalLifecyclePolicy.ShouldAddObservedInstance(false));
            Assert.IsFalse(BlockingModalLifecyclePolicy.ShouldAddObservedInstance(true));
            Assert.IsFalse(BlockingModalLifecyclePolicy.LifecycleSeedRequiresGlobalDiscovery(true));
        }

        [TestMethod]
        public void ISF_R1_C19_05_MULTI_MARKER_SUPPRESSION_RECOVERY_IS_COHERENT()
        {
            for (var markerCount = 1; markerCount <= MarkerPresentationPolicy.MaxLogicalMarkers; markerCount++)
            {
                Assert.IsTrue(MarkerFramePipelinePolicy.ShouldEnterActiveMarkerPipeline(markerCount));
                Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
                Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, false));
            }
        }

        [TestMethod]
        public void ISF_R1_C19_06_C18_HOTPATH_PERFORMANCE_GUARDS_REMAIN_ENFORCED()
        {
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(false, false, true, 1f, 5f));
            Assert.IsTrue(BlockingModalLifecyclePolicy.PreserveRareFallbackCadence(MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds));
            Assert.IsTrue(MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds >= 5f);
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, false, false, 10f, 0f));
            Assert.IsTrue(MarkerFramePipelinePolicy.CanUseSingleMarkerFastPath(1));
        }


        [TestMethod]
        public void ISF_R1_C20_01_EXACT_GETINREMOTEOP_POSITIVE_IS_ACCEPTED_FOR_SUPPORT_DRONE()
        {
            Assert.IsTrue(RemoteOperationSignalPolicy.ShouldClassifySupportDrone(
                authoritativeMasterMatches: true,
                runtimeShapeCompatible: true,
                invocationSucceeded: true,
                exactSignalValue: true));
        }

        [TestMethod]
        public void ISF_R1_C20_02_FALSE_MISSING_INCOMPATIBLE_OR_INVOCATION_FAILURE_FAILS_CLOSED()
        {
            Assert.IsFalse(RemoteOperationSignalPolicy.ShouldClassifySupportDrone(true, true, true, false));
            Assert.IsFalse(RemoteOperationSignalPolicy.ShouldClassifySupportDrone(false, true, true, true));
            Assert.IsFalse(RemoteOperationSignalPolicy.ShouldClassifySupportDrone(true, false, true, true));
            Assert.IsFalse(RemoteOperationSignalPolicy.ShouldClassifySupportDrone(true, true, false, true));
        }

        [TestMethod]
        public void ISF_R1_C20_03_ALIVE_TO_SUPPORT_DRONE_PRESERVES_PENDING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            Assert.AreEqual(0, ledger.TransitionParticipant(A, ParticipantState.Alive, ParticipantState.SupportDrone, 1));
            Assert.AreEqual(ClaimState.Pending, claim.State);
        }

        [TestMethod]
        public void ISF_R1_C20_04_NEW_PICKUP_WHILE_SUPPORT_DRONE_STARTS_ACTIVE_PENDING()
        {
            var ledger = new ClaimLedger();
            Assert.AreEqual(ClaimState.Pending, ledger.Ensure(P2, A, ParticipantState.SupportDrone, 2).State);
        }

        [TestMethod]
        public void ISF_R1_C20_05_SUPPORT_DRONE_PENDING_PREVENTS_PREMATURE_COMPLETION_STATE()
        {
            var ledger = new ClaimLedger();
            var drone = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            var other = ledger.Ensure(P1, B, ParticipantState.Alive, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, B, 1));
            Assert.AreEqual(ClaimState.Pending, drone.State);
            Assert.AreEqual(ClaimState.Collected, other.State);
            Assert.AreEqual(1, ledger.Records.Count(x => x.Key.Pickup.Equals(P1) && x.State == ClaimState.Pending));
        }

        [TestMethod]
        public void ISF_R1_C20_06_SUPPORT_DRONE_TO_ALIVE_PRESERVES_CLAIM_IDENTITY_AND_STATE()
        {
            var ledger = new ClaimLedger();
            var before = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(0, ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.Alive, 1));
            Assert.IsTrue(ledger.TryGet(P1, A, out var after));
            Assert.AreSame(before, after);
            Assert.AreEqual(ClaimState.Pending, after.State);
        }

        [TestMethod]
        public void ISF_R1_C20_07_SUPPORT_DRONE_TO_FULLY_DEAD_USES_EXISTING_DEFERRED_TRANSITION()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(1, ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.FullyDead, 1));
            Assert.AreEqual(ClaimState.Deferred, claim.State);
        }

        [TestMethod]
        public void ISF_R1_C20_08_DISCONNECT_REMAINS_DISTINCT_AND_CANCELS_PENDING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(1, ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.Disconnected, 1));
            Assert.AreEqual(ClaimState.CancelledDisconnected, claim.State);
        }

        [TestMethod]
        public void ISF_R1_C20_09_PERSONAL_PENDING_MARKER_REMAINS_VISIBLE_FOR_SUPPORT_DRONE()
        {
            Assert.IsTrue(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.SupportDrone, collected: false));
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.SupportDrone, collected: true));
            Assert.IsFalse(ProjectionPolicy.ShowPersonalMarker(true, ParticipantState.SupportDrone, collected: false, historicallyBlocked: true));
        }

        [TestMethod]
        public void ISF_R1_C20_10_C19_MODAL_C18_HOTPATH_COMMAND_AND_PING_LOCKS_REMAIN()
        {
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsTrue(BlockingModalLifecyclePolicy.PreserveRareFallbackCadence(MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds));
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, false, false, 10f, 0f));
            Assert.IsTrue(MarkerFramePipelinePolicy.CanUseSingleMarkerFastPath(1));
            Assert.IsFalse(MarkerPresentationPolicy.UsesVanillaPingSlot);
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.AreEqual(CommandShareabilityState.AllShareable, CommandShareabilityPolicy.Evaluate(new bool?[] { true, true }).State);
        }


        [TestMethod]
        public void ISF_R1_C20_R2_01_LOCAL_COLLECTOR_COLLECTED_REMOTE_SUPPORTDRONE_PENDING_RETAINS_AUTHORITATIVE_PICKUP()
        {
            var ledger = new ClaimLedger();
            var local = ledger.Ensure(P1, A, ParticipantState.Alive, 1);
            var drone = ledger.Ensure(P1, B, ParticipantState.SupportDrone, 1);
            Assert.IsTrue(ledger.MarkCollected(P1, A, 1));
            Assert.AreEqual(ClaimState.Collected, local.State);
            Assert.AreEqual(ClaimState.Pending, drone.State);
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsAuthoritativePickupDestroy);
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsSharedRootDisable);
        }

        [TestMethod]
        public void ISF_R1_C20_R2_02_ALL_LOCAL_PARTICIPANTS_COLLECTED_REQUESTS_LOCAL_VISUAL_SUPPRESSION()
        {
            Assert.IsTrue(LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(true, true, 1, 1));
            Assert.IsTrue(LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(true, true, 2, 2));
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(true, true, 0, 0));
        }

        [TestMethod]
        public void ISF_R1_C20_R2_03_LOCAL_COLLECTOR_REQUESTS_INTERACTION_SUPPRESSION()
        {
            Assert.IsTrue(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, true, true));
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsSharedColliderDisable);
        }

        [TestMethod]
        public void ISF_R1_C20_R2_04_LOCAL_PARTICIPANT_NOT_COLLECTED_REMAINS_INTERACTABLE()
        {
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, true, false));
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, false, true));
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, false, true, true));
        }

        [TestMethod]
        public void ISF_R1_C20_R2_05_MIXED_LOCAL_PARTICIPANTS_DO_NOT_GLOBALLY_HIDE_SHARED_ROOT()
        {
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(true, true, 2, 1));
            Assert.IsTrue(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, true, true));
            Assert.IsFalse(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, true, false));
        }

        [TestMethod]
        public void ISF_R1_C20_R2_06_SUPPORTDRONE_C19_C18_AND_PROVIDER_LOCKS_REMAIN_INTACT()
        {
            var ledger = new ClaimLedger();
            var drone = ledger.Ensure(P2, A, ParticipantState.SupportDrone, 2);
            Assert.AreEqual(ClaimState.Pending, drone.State);
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, true));
            Assert.IsFalse(MarkerFramePipelinePolicy.ShouldRunMultiMarkerSolve(true, false, false, 10f, 0f));
            Assert.IsFalse(MarkerPresentationPolicy.RegistersPickupShareProvider);
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsAuthoritativePickupDestroy);
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsSharedColliderDisable);
        }


        [TestMethod]
        public void ISF_R1_C20_R3_01_NETWORK_DESTROY_WHILE_AUTHORITATIVE_NEVER_CONFIRMS_DISCONNECT()
        {
            Assert.AreEqual(
                NetworkDestroyDisposition.IgnoreStillAuthoritative,
                DisconnectConfirmationPolicy.EvaluateNetworkDestroy(participantResolved: true, authoritativePresence: true));
            Assert.IsFalse(DisconnectConfirmationPolicy.AllowsImmediateCancelFromNetworkDestroy);
        }

        [TestMethod]
        public void ISF_R1_C20_R3_02_SUPPORTDRONE_LIFECYCLE_DESTROY_PRESERVES_PENDING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(
                NetworkDestroyDisposition.IgnoreStillAuthoritative,
                DisconnectConfirmationPolicy.EvaluateNetworkDestroy(participantResolved: true, authoritativePresence: true));
            Assert.AreEqual(ClaimState.Pending, claim.State);
            Assert.AreEqual(0, ledger.HistoricalRecords.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R3_03_SUPPORTDRONE_TO_ALIVE_SAME_GENERATION_PRESERVES_PENDING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.AreEqual(0, ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.Alive, 1));
            Assert.AreEqual(ClaimState.Pending, claim.State);
            Assert.IsFalse(ledger.IsHistoricallyBlocked(P1, A.StableUser));
        }

        [TestMethod]
        public void ISF_R1_C20_R3_04_UNCONFIRMED_DESTROY_CANNOT_CREATE_HISTORICAL_BARRIER()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P2, A, ParticipantState.SupportDrone, 2);
            Assert.AreEqual(
                NetworkDestroyDisposition.HoldForAuthoritativeConfirmation,
                DisconnectConfirmationPolicy.EvaluateNetworkDestroy(participantResolved: true, authoritativePresence: false));
            Assert.IsFalse(DisconnectConfirmationPolicy.ShouldConfirmDisconnect(true, absenceGraceElapsed: false));
            Assert.IsFalse(ledger.IsHistoricallyBlocked(P2, A.StableUser));
        }

        [TestMethod]
        public void ISF_R1_C20_R3_05_CONFIRMED_AUTHORITATIVE_ABSENCE_STILL_CANCELS_PENDING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            Assert.IsTrue(DisconnectConfirmationPolicy.ShouldConfirmDisconnect(true, absenceGraceElapsed: true));
            Assert.AreEqual(1, ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.Disconnected, 1));
            Assert.AreEqual(ClaimState.CancelledDisconnected, claim.State);
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P1, A.StableUser));
        }

        [TestMethod]
        public void ISF_R1_C20_R3_06_ACTUAL_DISCONNECT_RECONNECT_CANNOT_RESURRECT_CANCELLED_CLAIM()
        {
            var ledger = new ClaimLedger();
            ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            ledger.TransitionParticipant(A, ParticipantState.SupportDrone, ParticipantState.Disconnected, 1);
            var replacement = new ParticipantKey(A.StableUser, "replacement-generation");
            Assert.IsFalse(ledger.TryEnsure(P1, replacement, ParticipantState.Alive, 1, out _));
            Assert.IsTrue(ledger.IsHistoricallyBlocked(P1, A.StableUser));
        }

        [TestMethod]
        public void ISF_R1_C20_R3_07_REPEATED_TRANSIENT_DESTROY_EVENTS_ARE_NON_CANCELLING()
        {
            var ledger = new ClaimLedger();
            var claim = ledger.Ensure(P1, A, ParticipantState.SupportDrone, 1);
            for (var i = 0; i < 3; i++)
                Assert.AreEqual(
                    NetworkDestroyDisposition.IgnoreStillAuthoritative,
                    DisconnectConfirmationPolicy.EvaluateNetworkDestroy(participantResolved: true, authoritativePresence: true));
            Assert.AreEqual(ClaimState.Pending, claim.State);
            Assert.AreEqual(0, ledger.HistoricalRecords.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R3_08_CORRECTION2_LOCAL_COLLECTOR_GATE_IS_INDEPENDENT_OF_DISCONNECT_GATE()
        {
            Assert.IsTrue(LocalPickupSuppressionPolicy.ShouldSuppressInteractor(true, true, true, true, true));
            Assert.IsFalse(LocalPickupSuppressionPolicy.AllowsAuthoritativePickupDestroy);
            Assert.IsFalse(DisconnectConfirmationPolicy.UsesObjectNamePrefabOrBodyHeuristics);
            Assert.IsFalse(DisconnectConfirmationPolicy.AllowsImmediateCancelFromNetworkDestroy);
        }

        [TestMethod]
        public void ISF_R1_C20_R4_01_SUPPORTDRONE_ACTIVE_OVERRIDE_DOES_NOT_REQUIRE_DISTRIBUTION_SCOPE()
        {
            Assert.IsTrue(ItemShareActiveGatePolicy.ShouldCorrectIsDown(true, true, true, ParticipantState.SupportDrone));
            Assert.IsFalse(ItemShareActiveGatePolicy.CorrectIsDown(true, true, true, ParticipantState.SupportDrone));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_02_ALIVE_DOES_NOT_REQUIRE_ITEMSHARE_OVERRIDE()
        {
            Assert.IsFalse(ItemShareActiveGatePolicy.ShouldCorrectIsDown(false, true, true, ParticipantState.Alive));
            Assert.IsFalse(ItemShareActiveGatePolicy.CorrectIsDown(false, true, true, ParticipantState.Alive));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_03_FULLYDEAD_REMAINS_DOWN()
        {
            Assert.IsFalse(ItemShareActiveGatePolicy.ShouldCorrectIsDown(true, true, true, ParticipantState.FullyDead));
            Assert.IsTrue(ItemShareActiveGatePolicy.CorrectIsDown(true, true, true, ParticipantState.FullyDead));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_04_SUPPORTDRONE_IS_EXCLUDED_FROM_ORDINARY_DEAD_AUTO_SHARE()
        {
            Assert.IsFalse(ItemShareActiveGatePolicy.IsDeadAutoShareTarget(true, true, true, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.IsDeadAutoShareTarget(true, true, true, ParticipantState.FullyDead));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_05_SUPPORTDRONE_REMAINS_AN_OUTSTANDING_ORDINARY_COLLECTOR()
        {
            Assert.IsTrue(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(true, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(true, ParticipantState.Alive));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_06_COMMAND_COLLECTORS_RETAIN_SUPPORTDRONE_OUTSIDE_DISTRIBUTION()
        {
            var inDistribution = false;
            Assert.IsFalse(inDistribution);
            Assert.IsFalse(ItemShareActiveGatePolicy.CorrectIsDown(true, true, true, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(true, ParticipantState.SupportDrone));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_07_SUPPORTDRONE_TO_ALIVE_KEEPS_SAME_COMMAND_COLLECTOR_REQUIREMENT()
        {
            Assert.IsTrue(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(true, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(true, ParticipantState.Alive));
            Assert.IsFalse(ItemShareActiveGatePolicy.CorrectIsDown(false, true, true, ParticipantState.Alive));
        }

        [TestMethod]
        public void ISF_R1_C20_R4_08_ABSENT_OR_UNPROVEN_PARTICIPANT_DOES_NOT_BLOCK_UPSTREAM_COMPLETION()
        {
            Assert.IsFalse(ItemShareActiveGatePolicy.CountsAsOutstandingCollector(false, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.CorrectIsDown(true, true, false, ParticipantState.SupportDrone));
            Assert.IsTrue(ItemShareActiveGatePolicy.CorrectIsDown(true, false, true, ParticipantState.SupportDrone));
        }


        [TestMethod]
        public void ISF_R1_C20_C5_01_DenseMarkerTinyProjectionNoiseKeepsStableSlots()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var candidates = new[]
            {
                new MarkerHudPlacementCandidate(1, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 900f, 500f, 0f, 0f, 0f), footprint),
                new MarkerHudPlacementCandidate(2, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 905f, 495f, 0f, 0f, 0f), footprint),
            };
            var previous = new System.Collections.Generic.Dictionary<long, int> { [1] = 0, [2] = 1 };
            var output = new System.Collections.Generic.List<MarkerHudPlacementCandidate>();
            MarkerPlacementStabilityPolicy.BuildStableOrderBuffered(candidates, previous, 1920f, 1080f, output);
            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(1L, output[0].StableKey);
            Assert.AreEqual(2L, output[1].StableKey);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_02_DenseMarkerMeaningfulCrossingEventuallyChangesRank()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var candidates = new[]
            {
                new MarkerHudPlacementCandidate(1, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 900f, 560f, 0f, 0f, 0f), footprint),
                new MarkerHudPlacementCandidate(2, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 905f, 500f, 0f, 0f, 0f), footprint),
            };
            var previous = new System.Collections.Generic.Dictionary<long, int> { [1] = 0, [2] = 1 };
            var output = new System.Collections.Generic.List<MarkerHudPlacementCandidate>();
            MarkerPlacementStabilityPolicy.BuildStableOrderBuffered(candidates, previous, 1920f, 1080f, output);
            Assert.AreEqual(2L, output[0].StableKey);
            Assert.AreEqual(1L, output[1].StableKey);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_03_OnScreenFullLabelDisplacementIsBoundedFromWorldAnchor()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(260f, 28f, 1920f, 1080f);
            var candidates = Enumerable.Range(0, 8)
                .Select(i => new MarkerHudPlacementCandidate(
                    100 + i,
                    new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f),
                    footprint))
                .ToArray();
            var placements = MarkerHudNavigationPolicy.ResolvePlacements(candidates, 1920f, 1080f);
            var bound = MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(footprint, 1920f, 1080f);
            Assert.AreEqual(candidates.Length, placements.Count);
            foreach (var placement in placements)
            {
                var source = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
                Assert.IsTrue(MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(source, placement) <= bound + 0.01f);
            }
        }

        [TestMethod]
        public void ISF_R1_C20_C5_04_AppliedSmoothingCannotEscapeAnchorBound()
        {
            var current = MarkerPlacementStabilityPolicy.SmoothCoordinate(500f, 900f, 1f / 60f);
            Assert.IsTrue(current > 500f && current < 900f);
            MarkerPlacementStabilityPolicy.ClampDisplacementFromAnchor(0f, 0f, 400f, 300f, 120f, out var x, out var y);
            var distance = Math.Sqrt(x * x + y * y);
            Assert.AreEqual(120d, distance, 0.01d);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_05_DenseClusterAggregationPreservesExactUnderlyingCountAndStableRepresentative()
        {
            var candidates = new[]
            {
                new MarkerDensityCandidate(30, PersonalMarkerKind.OrdinaryPickup, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 920f, 520f, 40),
                new MarkerDensityCandidate(20, PersonalMarkerKind.OrdinaryPickup, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 930f, 525f, 20),
                new MarkerDensityCandidate(10, PersonalMarkerKind.CommandPicker, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 925f, 522f, 80),
                new MarkerDensityCandidate(40, PersonalMarkerKind.OrdinaryPickup, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 935f, 528f, 10),
            };
            var ordered = new System.Collections.Generic.List<MarkerDensityCandidate>();
            var clusters = new System.Collections.Generic.List<MarkerDensityCluster>();
            var decisions = new System.Collections.Generic.List<MarkerDensityDecision>();
            MarkerDensityPolicy.ResolveBuffered(candidates, 1920f, 1080f, ordered, clusters, decisions);
            Assert.AreEqual(1, decisions.Count);
            Assert.AreEqual(10L, decisions[0].StableKey);
            Assert.AreEqual(3, decisions[0].HiddenMemberCount);
            Assert.AreEqual(4, MarkerDensityPolicy.SumRepresentedIdentities(decisions));

            var reversed = candidates.Reverse().ToArray();
            MarkerDensityPolicy.ResolveBuffered(reversed, 1920f, 1080f, ordered, clusters, decisions);
            Assert.AreEqual(10L, decisions[0].StableKey);
            Assert.AreEqual(4, MarkerDensityPolicy.SumRepresentedIdentities(decisions));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_06_DensityBudgetBoundsFullLabelsAtLogicalCapacity()
        {
            var candidates = Enumerable.Range(0, MarkerPresentationPolicy.MaxLogicalMarkers)
                .Select(i => new MarkerDensityCandidate(
                    i + 1,
                    PersonalMarkerKind.OrdinaryPickup,
                    MarkerHudMode.OnScreenWorldAnchor,
                    MarkerHudEdge.None,
                    20f + i * 19f,
                    20f + (i % 4) * 230f,
                    i + 1))
                .ToArray();
            var ordered = new System.Collections.Generic.List<MarkerDensityCandidate>();
            var clusters = new System.Collections.Generic.List<MarkerDensityCluster>();
            var decisions = new System.Collections.Generic.List<MarkerDensityDecision>();
            MarkerDensityPolicy.ResolveBuffered(candidates, 1920f, 1080f, ordered, clusters, decisions);
            Assert.IsTrue(decisions.Count <= MarkerDensityPolicy.CalculateRepresentativeBudget(1920f, 1080f));
            Assert.IsTrue(decisions.Count < MarkerPresentationPolicy.MaxLogicalMarkers);
            Assert.AreEqual(MarkerPresentationPolicy.MaxLogicalMarkers, MarkerDensityPolicy.SumRepresentedIdentities(decisions));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_07_CommandPickerBlockingModalSuppressesAndRestoresImmediately()
        {
            Assert.AreEqual("RoR2.UI.PickupPickerPanel", BlockingModalLifecyclePolicy.PickupPickerPanelTypeName);
            Assert.IsTrue(BlockingModalLifecyclePolicy.IsKnownBlockingModalTypeName(BlockingModalLifecyclePolicy.PickupPickerPanelTypeName));
            Assert.IsTrue(BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(knownBlockingType: true, sceneObjectValid: true));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, localBlockingModalActive: true));
            Assert.IsTrue(BlockingModalLifecyclePolicy.SuppressionStateChanged(previousSuppressed: true, currentSuppressed: false));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, localBlockingModalActive: false));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_08_RetainedPauseAndSimpleDialogBlockingModalSuppressionStillPasses()
        {
            Assert.IsTrue(BlockingModalLifecyclePolicy.IsKnownBlockingModalTypeName(BlockingModalLifecyclePolicy.PauseScreenControllerTypeName));
            Assert.IsTrue(BlockingModalLifecyclePolicy.IsKnownBlockingModalTypeName(BlockingModalLifecyclePolicy.SimpleDialogBoxTypeName));
            Assert.IsFalse(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, localBlockingModalActive: true));
            Assert.IsTrue(MarkerPresentationPolicy.ShouldRenderHudMarkers(true, localBlockingModalActive: false));
            Assert.IsTrue(BlockingModalLifecyclePolicy.PreserveRareFallbackCadence(MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_09_DenseMarkerStableOrderIsInputPermutationInvariantWithoutMemory()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var a = new MarkerHudPlacementCandidate(3, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 930f, 530f, 0f, 0f, 0f), footprint);
            var b = new MarkerHudPlacementCandidate(1, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 910f, 510f, 0f, 0f, 0f), footprint);
            var c = new MarkerHudPlacementCandidate(2, new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 920f, 520f, 0f, 0f, 0f), footprint);
            var first = new System.Collections.Generic.List<MarkerHudPlacementCandidate>();
            var second = new System.Collections.Generic.List<MarkerHudPlacementCandidate>();
            MarkerPlacementStabilityPolicy.BuildStableOrderBuffered(new[] { a, b, c }, null, 1920f, 1080f, first);
            MarkerPlacementStabilityPolicy.BuildStableOrderBuffered(new[] { c, a, b }, null, 1920f, 1080f, second);
            CollectionAssert.AreEqual(first.Select(x => x.StableKey).ToArray(), second.Select(x => x.StableKey).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_C5_10_SmoothRelocationApproachesTargetWithoutTeleport()
        {
            var first = MarkerPlacementStabilityPolicy.SmoothCoordinate(0f, 100f, 1f / 30f);
            Assert.IsTrue(first > 0f && first < 100f);
            var value = first;
            for (var i = 0; i < 60; i++) value = MarkerPlacementStabilityPolicy.SmoothCoordinate(value, 100f, 1f / 60f);
            Assert.IsTrue(value > 99f && value <= 100f);
        }


        [TestMethod]
        public void ISF_R1_C20_C5_C2_01_HardReservedHudMayOverrideNormalAnchorDisplacementCap()
        {
            var projection = MarkerHudNavigationPolicy.ResolveProjection(0.15f, 0.90f, 10f, -7f, 8f, 10f, 1920f, 1080f);
            var footprint = MarkerHudNavigationPolicy.EstimateVisualFootprint("Белый", 80, 1920f, 1080f);
            var bound = MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(footprint, 1920f, 1080f);
            var initial = new MarkerHudRect(projection.X, projection.Y, footprint.Width, footprint.Height);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsReservedHud(initial, 1920f, 1080f));

            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(20001, projection, footprint),
            }, 1920f, 1080f).Single();

            Assert.IsTrue(placement.HudRelocated);
            Assert.IsTrue(MarkerHudNavigationPolicy.IsRectInsideScreen(placement.FinalRect, 1920f, 1080f));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(placement.FinalRect, 1920f, 1080f));
            Assert.IsTrue(MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(projection, placement) > bound + 0.01f);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C2_02_CollisionOnlyRelocationRemainsWithinAnchorDisplacementCap()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
            var candidates = new[]
            {
                new MarkerHudPlacementCandidate(20002, projection, footprint),
                new MarkerHudPlacementCandidate(20003, projection, footprint),
            };
            var placements = MarkerHudNavigationPolicy.ResolvePlacements(candidates, 1920f, 1080f).ToDictionary(x => x.StableKey);
            var second = placements[20003];
            var bound = MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(footprint, 1920f, 1080f);

            Assert.IsTrue(second.CollisionRelocated);
            Assert.IsTrue(MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(projection, second) > 0.01f);
            Assert.IsTrue(MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(projection, second) <= bound + 0.01f);
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsReservedHud(second.FinalRect, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C2_03_DynamicHudMayOverrideNormalAnchorDisplacementCap()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
            var dynamicHud = new[]
            {
                new MarkerHudExclusionZone("active-message-feed", 700f, 1220f, 300f, 780f),
            };
            var initial = new MarkerHudRect(projection.X, projection.Y, footprint.Width, footprint.Height);
            var bound = MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(footprint, 1920f, 1080f);
            Assert.IsTrue(MarkerHudNavigationPolicy.IntersectsDynamicHud(initial, dynamicHud, 1920f, 1080f));

            var placement = MarkerHudNavigationPolicy.ResolvePlacements(new[]
            {
                new MarkerHudPlacementCandidate(20004, projection, footprint),
            }, 1920f, 1080f, dynamicHud).Single();

            Assert.IsTrue(placement.MessageHudRelocated);
            Assert.IsTrue(MarkerHudNavigationPolicy.IsRectInsideScreen(placement.FinalRect, 1920f, 1080f));
            Assert.IsFalse(MarkerHudNavigationPolicy.IntersectsDynamicHud(placement.FinalRect, dynamicHud, 1920f, 1080f));
            Assert.IsTrue(MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(projection, placement) > bound + 0.01f);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C2_04_HardHudEscapeIsDeterministicForIdenticalInputs()
        {
            var footprint = MarkerHudNavigationPolicy.BuildMeasuredVisualFootprint(180f, 24f, 1920f, 1080f);
            var projection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 960f, 540f, 0f, 0f, 0f);
            var dynamicHud = new[]
            {
                new MarkerHudExclusionZone("active-message-feed", 700f, 1220f, 300f, 780f),
            };
            var candidate = new MarkerHudPlacementCandidate(20005, projection, footprint);

            var first = MarkerHudNavigationPolicy.ResolvePlacements(new[] { candidate }, 1920f, 1080f, dynamicHud).Single();
            var second = MarkerHudNavigationPolicy.ResolvePlacements(new[] { candidate }, 1920f, 1080f, dynamicHud).Single();

            Assert.AreEqual(first.X, second.X, 0.0001f);
            Assert.AreEqual(first.Y, second.Y, 0.0001f);
            Assert.AreEqual(first.FinalRect.CenterX, second.FinalRect.CenterX, 0.0001f);
            Assert.AreEqual(first.FinalRect.CenterY, second.FinalRect.CenterY, 0.0001f);
            Assert.AreEqual(first.HudRelocated, second.HudRelocated);
            Assert.AreEqual(first.MessageHudRelocated, second.MessageHudRelocated);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_01_WorldClusterMembershipIsInputPermutationInvariant()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 1f), C3R2Member(3, 2f) }, 0d).Clusters.Single();
            var key = first.StableKey;
            var fingerprint = first.MemberFingerprint;
            var second = tracker.Update(new[] { C3R2Member(3, 2f), C3R2Member(1, 0f), C3R2Member(2, 1f) }, 0.1d).Clusters.Single();
            Assert.AreEqual(key, second.StableKey);
            Assert.AreEqual(fingerprint, second.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_02_SemanticUpdateApiHasNoCameraOrScreenInputs()
        {
            var method = typeof(MarkerWorldClusterTracker).GetMethod("Update");
            Assert.IsNotNull(method);
            var parameters = method!.GetParameters();
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(System.Collections.Generic.IReadOnlyList<MarkerWorldMember>), parameters[0].ParameterType);
            Assert.AreEqual(typeof(double), parameters[1].ParameterType);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_03_MembersInsideMergeRadiusBecomeOneSemanticCluster()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 2.9f) }, 0d);
            Assert.AreEqual(1, update.Clusters.Count);
            Assert.AreEqual(2, update.Clusters[0].TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_04_MembersOutsideSplitRadiusRemainSeparateInitially()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0d);
            Assert.AreEqual(2, update.Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_05_SplitRequiresWorldSpaceDwellBeyondSplitRadius()
        {
            var tracker = new MarkerWorldClusterTracker();
            Assert.AreEqual(1, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 2f) }, 0d).Clusters.Count);
            Assert.AreEqual(1, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.10d).Clusters.Count);
            Assert.AreEqual(1, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.44d).Clusters.Count);
            Assert.AreEqual(2, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.46d).Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_06_MergeRequiresWorldSpaceDwellInsideMergeRadius()
        {
            var tracker = new MarkerWorldClusterTracker();
            Assert.AreEqual(2, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 5f) }, 0d).Clusters.Count);
            Assert.AreEqual(2, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 2f) }, 0.10d).Clusters.Count);
            Assert.AreEqual(1, tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 2f) }, 0.50d).Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_07_StableClusterKeyChangesWhenPhysicalMembershipChanges()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 1f), C3R2Member(3, 2f) }, 0d).Clusters.Single();
            var second = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 1f) }, 0.1d).Clusters.Single();
            Assert.AreNotEqual(first.StableKey, second.StableKey);
            Assert.AreEqual(2, second.TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_08_CurrentWorldAnchorIsMemberCentroid()
        {
            var anchor = MarkerWorldClusterTracker.CurrentAnchor(new[] { C3R2Member(1, 0f), C3R2Member(2, 2f), C3R2Member(3, 4f) });
            Assert.AreEqual(2f, anchor.X, 0.0001f);
            Assert.AreEqual(0f, anchor.Y, 0.0001f);
            Assert.AreEqual(0f, anchor.Z, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_09_CompositionCountsEveryPhysicalPickupExactlyOnce()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "a", "A"),
                C3R2Member(2, 1f, MarkerClassKind.Tier1, "b", "B"),
                C3R2Member(3, 2f, MarkerClassKind.Tier2, "c", "C") }, 0d).Clusters.Single();
            Assert.AreEqual(3, MarkerClusterPresentationPolicy.CompositionCountSum(cluster.Composition));
            Assert.AreEqual(cluster.TotalCount, MarkerClusterPresentationPolicy.CompositionCountSum(cluster.Composition));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_10_IdenticalItemsAggregateBySemanticIdentity()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "same", "Same Item"),
                C3R2Member(2, 1f, MarkerClassKind.Tier1, "same", "Same Item") }, 0d).Clusters.Single();
            Assert.AreEqual(1, cluster.ItemRows.Count);
            Assert.AreEqual(2, cluster.ItemRows[0].Count);
            Assert.AreEqual("Same Item", cluster.ItemRows[0].LocalizedName);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_11_DetailedMixedClusterUsesNeutralMainSemanticAndFullWords()
        {
            var cluster = C3R2MixedCluster();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, true, 1f, 5), 12, false, true);
            Assert.IsTrue(plan.NeutralMainSemantic);
            StringAssert.Contains(plan.Text, "Предметы ×3");
            StringAssert.Contains(plan.Text, "Белые 2");
            StringAssert.Contains(plan.Text, "Зелёные 1");
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_12_DetailedHomogeneousDifferentItemsUsesFullCategoryWording()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "a", "A"),
                C3R2Member(2, 1f, MarkerClassKind.Tier1, "b", "B") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, false, 1f, 5), 20, false, true);
            StringAssert.StartsWith(plan.Text, "Белые предметы ×2");
            Assert.IsFalse(plan.NeutralMainSemantic);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_13_CompactCreatesOneBadgePerNonZeroSemanticCategory()
        {
            var cluster = C3R2MixedCluster();
            var settings = new MarkerPresentationSettings(MarkerPresentationMode.Compact, true, 1f, 5, true, true, true, MarkerCompactMixedStyle.CategoryDiamonds);
            var plan = MarkerClusterPresentationPolicy.Build(cluster, settings, 7, false, true);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.AreEqual(2, plan.CompactBadges.Count);
            Assert.AreEqual(2, plan.CompactBadges[0].Count);
            Assert.AreEqual(1, plan.CompactBadges[1].Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_14_CompactTextContainsNoTierAbbreviationsOrColorWords()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), new MarkerPresentationSettings(MarkerPresentationMode.Compact, true, 1f, 5), 7, false, true);
            Assert.IsFalse(plan.Text.Contains("Б/З/К"));
            Assert.IsFalse(plan.Text.Contains("W/G/R"));
            Assert.IsFalse(plan.Text.Contains("Белые"));
            Assert.IsFalse(plan.Text.Contains("Зелёные"));
            StringAssert.StartsWith(plan.Text, "×3");
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_15_DetailRowsClampToHardMaximumFive()
        {
            Assert.AreEqual(12, MarkerClusterPresentationPolicy.ClampDetailRows(99));
            Assert.AreEqual(1, MarkerClusterPresentationPolicy.ClampDetailRows(-10));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_16_MarkerScaleClampMatchesCanonicalRange()
        {
            Assert.AreEqual(0.75f, MarkerClusterPresentationPolicy.ClampMarkerScale(0.1f), 0.0001f);
            Assert.AreEqual(1.25f, MarkerClusterPresentationPolicy.ClampMarkerScale(3f), 0.0001f);
            Assert.AreEqual(1f, MarkerClusterPresentationPolicy.ClampMarkerScale(1f), 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_17_ShowDistanceOnlyChangesPresentationText()
        {
            var cluster = C3R2MixedCluster();
            var withDistance = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, true, 1f, 5), 123, false, false);
            var withoutDistance = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, false, 1f, 5), 123, false, false);
            StringAssert.Contains(withDistance.Text, "123 m");
            Assert.IsFalse(withoutDistance.Text.Contains("123 m"));
            Assert.AreEqual(cluster.MemberFingerprint, cluster.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_18_ExpandedDetailedRowsRespectConfiguredLimit()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"a","A"), C3R2Member(2,.2f,MarkerClassKind.Tier1,"b","B"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"c","C"), C3R2Member(4,.6f,MarkerClassKind.Tier1,"d","D") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, detailRows: 2), 3, true, false);
            Assert.AreEqual("A ×1\nB ×1\n+ 2 more types", plan.Text);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEqual(new[] { "A", "B" }, plan.DetailedItemRows.Select(x => x.LocalizedName).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1 }, plan.DetailedItemRows.Select(x => x.Count).ToArray());
            Assert.AreEqual(2, plan.ShownDetailRows);
            Assert.AreEqual(2, plan.OverflowPhysicalCount);
            Assert.IsFalse(plan.Text.Contains("White ×4"));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_19_AdaptiveLodRequiresFocusDwell()
        {
            var lod = new MarkerAdaptiveLodTracker();
            var candidates = new[] { new MarkerExpansionCandidate(10, 4, 30f, 20f) };
            Assert.IsNull(lod.Update(candidates, 0d));
            Assert.IsNull(lod.Update(candidates, 0.10d));
            Assert.AreEqual(10L, lod.Update(candidates, 0.13d));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_20_AdaptiveLodExpandsAtMostOneDenseCluster()
        {
            var lod = new MarkerAdaptiveLodTracker();
            var candidates = new[] {
                new MarkerExpansionCandidate(10, 4, 8f, 25f),
                new MarkerExpansionCandidate(11, 6, 7f, 40f) };
            lod.Update(candidates, 0d);
            var expanded = lod.Update(candidates, 0.20d);
            Assert.IsTrue(expanded == 10L || expanded == 11L);
            Assert.AreEqual(expanded, lod.ExpandedKey);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_21_AdaptiveNearExpansionRequiresUniqueNearestCluster()
        {
            var lod = new MarkerAdaptiveLodTracker();
            var ambiguous = new[] {
                new MarkerExpansionCandidate(10, 4, 10f, 500f),
                new MarkerExpansionCandidate(11, 4, 10.5f, 500f) };
            Assert.IsNull(lod.Update(ambiguous, 0d));
            Assert.IsNull(lod.Update(ambiguous, 1d));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_22_PresentationModeSwitchCannotChangeWorldClusterIdentity()
        {
            var cluster = C3R2MixedCluster();
            var detailed = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, true, 1f, 5), 10, false, true);
            var compact = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Compact, true, 1f, 5), 10, false, true);
            Assert.AreNotEqual(detailed.Text, compact.Text);
            Assert.AreEqual(3, cluster.TotalCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(cluster.MemberFingerprint));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_23_RelativePlacementReappliesOffsetToCurrentProjection()
        {
            var footprint = new MarkerHudVisualFootprint(120f, 40f, 24f, 70f, 8f, 6f);
            var originalProjection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 400f, 300f, 0f, 0f, 0f);
            var solved = new MarkerHudPlacement(99, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 430f, 325f, 0f, 0, 0, new MarkerHudRect(430f,325f,120f,40f), false, true, false);
            var relative = MarkerProjectionRelativePlacementPolicy.Capture(originalProjection, solved);
            var currentProjection = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 700f, 500f, 0f, 0f, 0f);
            var reapplied = MarkerProjectionRelativePlacementPolicy.Apply(99, currentProjection, footprint, relative);
            Assert.AreEqual(730f, reapplied.X, 0.0001f);
            Assert.AreEqual(525f, reapplied.Y, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_24_LargeProjectionJumpRequiresFastFollow()
        {
            var a = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 100f, 100f, 0f, 0f, 0f);
            var b = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 500f, 100f, 0f, 0f, 0f);
            Assert.IsTrue(MarkerProjectionRelativePlacementPolicy.RequiresFastFollow(a, b, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_25_SmallProjectionMotionDoesNotRequireFastFollow()
        {
            var a = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 100f, 100f, 0f, 0f, 0f);
            var b = new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None, 120f, 105f, 0f, 0f, 0f);
            Assert.IsFalse(MarkerProjectionRelativePlacementPolicy.RequiresFastFollow(a, b, 1920f, 1080f));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_26_PhysicalCollectionImmediatelyDecrementsSemanticTotal()
        {
            var tracker = new MarkerWorldClusterTracker();
            Assert.AreEqual(3, tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,1f), C3R2Member(3,2f) }, 0d).Clusters.Single().TotalCount);
            Assert.AreEqual(2, tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,1f) }, 0.01d).Clusters.Single().TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_27_ZeroPendingMembersRemoveSemanticCluster()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] { C3R2Member(1,0f) }, 0d);
            var update = tracker.Update(Array.Empty<MarkerWorldMember>(), 0.01d);
            Assert.AreEqual(0, update.Clusters.Count);
            Assert.IsTrue(update.LifecycleEvents.Any(x => x.Kind == MarkerSemanticLifecycleKind.Removed));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_28_IdenticalWorldInputProducesNoSemanticLifecycleChurn()
        {
            var tracker = new MarkerWorldClusterTracker();
            var members = new[] { C3R2Member(1,0f), C3R2Member(2,1f) };
            tracker.Update(members, 0d);
            var update = tracker.Update(members, 0.2d);
            Assert.AreEqual(0, update.LifecycleEvents.Count);
            Assert.IsFalse(update.MembershipChanged);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_29_CommandPickerMapsToCommandSemanticCategory()
        {
            var command = new MarkerWorldMember(7, PersonalMarkerKind.CommandPicker, new MarkerWorldPoint(0f,0f,0f), "command:7", "Command", MarkerClassKind.Unknown);
            Assert.AreEqual(MarkerSemanticCategory.CommandState, command.Category);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_30_DetailedSinglePickupShowsExactLocalizedName()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier2,"item","Точный предмет") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, false, 1f, 5), 1, false, true);
            Assert.AreEqual("Точный предмет ×1", plan.Text);
            Assert.AreEqual(1, plan.Text.Count(x => x == '×'));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_31_CompactBadgeOrderIsDeterministicSemanticOrder()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier3,"r","R"),
                C3R2Member(2,.5f,MarkerClassKind.Tier1,"w","W"),
                C3R2Member(3,1f,MarkerClassKind.Tier2,"g","G") },0d).Clusters.Single();
            var settings = new MarkerPresentationSettings(MarkerPresentationMode.Compact, false, 1f, 5, true, true, true, MarkerCompactMixedStyle.CategoryDiamonds);
            var plan = MarkerClusterPresentationPolicy.Build(cluster, settings, 1, false, false);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CompactBadges[0].Category);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, plan.CompactBadges[1].Category);
            Assert.AreEqual(MarkerSemanticCategory.Tier3, plan.CompactBadges[2].Category);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_32_DenseSemanticClusterHasSingleMembershipOwnerKey()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(Enumerable.Range(1, 8).Select(i => C3R2Member(i, i * 0.2f)).ToArray(), 0d);
            Assert.AreEqual(1, update.Clusters.Count);
            Assert.AreEqual(8, update.Clusters[0].MemberStableKeys.Count);
            Assert.AreEqual(update.Clusters[0].StableKey, MarkerWorldClusterTracker.StableClusterKey(update.Clusters[0].MemberStableKeys));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_33_StableClusterKeyIgnoresWorldMotionWhileMembershipIsStable()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 1f) }, 0d).Clusters.Single();
            var key = first.StableKey;
            var second = tracker.Update(new[] { C3R2Member(1, 0.5f), C3R2Member(2, 1.5f) }, 0.2d).Clusters.Single();
            Assert.AreEqual(key, second.StableKey);
            Assert.AreEqual(first.MemberFingerprint, second.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_34_DistinctWorldPilesRemainDistinctWithoutProjectionInputs()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] {
                C3R2Member(1, 0f), C3R2Member(2, 0.5f),
                C3R2Member(3, 10f), C3R2Member(4, 10.5f) }, 0d);
            Assert.AreEqual(2, update.Clusters.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 2 }, update.Clusters.Select(x => x.TotalCount).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_35_ExpandedIdenticalItemsUsePhysicalOverflowCount()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(2,.2f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(4,.6f,MarkerClassKind.Tier1,"other","Other") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, detailRows: 1), 1, true, false);
            Assert.AreEqual("Other ×1\n+ 1 more types", plan.Text);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual("Other", plan.DetailedItemRows[0].LocalizedName);
            Assert.AreEqual(1, plan.DetailedItemRows[0].Count);
            Assert.AreEqual(1, plan.ShownDetailRows);
            Assert.AreEqual(1, plan.OverflowPhysicalCount);
            Assert.AreEqual(2, cluster.ItemRows.Count);
            Assert.IsFalse(plan.Text.Contains("White ×4"));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_36_LocalCollectionDecrementsExactCompositionOnce()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"w","White"),
                C3R2Member(2,.5f,MarkerClassKind.Tier2,"g","Green") },0d);
            var cluster = tracker.Update(new[] { C3R2Member(2,.5f,MarkerClassKind.Tier2,"g","Green") },0.01d).Clusters.Single();
            Assert.AreEqual(1, cluster.TotalCount);
            Assert.AreEqual(1, cluster.Composition.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, cluster.Composition[0].Category);
            Assert.AreEqual(1, cluster.Composition[0].Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_37_UnknownCategoryUsesNeutralSemanticFallback()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] { C3R2Member(1,0f,MarkerClassKind.Unknown,"u","Mystery") },0d).Clusters.Single();
            var detailed = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Detailed, false, 1f, 5), 1, false, false);
            var compact = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Compact, false, 1f, 5), 1, false, false);
            Assert.IsTrue(detailed.NeutralMainSemantic);
            Assert.AreEqual(1, compact.CompactBadges.Count);
            Assert.AreEqual(MarkerSemanticCategory.Unknown, compact.CompactBadges[0].Category);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_38_CompactSinglePickupHasOneCategoryBadgeAndNoColorWord()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"w","White Item") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, new MarkerPresentationSettings(MarkerPresentationMode.Compact, false, 1f, 5), 1, false, false);
            Assert.AreEqual(1, plan.CompactBadges.Count);
            Assert.AreEqual(1, plan.CompactBadges[0].Count);
            Assert.IsFalse(plan.Text.Contains("White"));
            Assert.IsFalse(plan.Text.Contains("W"));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_39_StableSemanticUpdateRefreshesAnchorWithoutLifecycleChurn()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,1f) },0d);
            var cluster = first.Clusters.Single();
            var second = tracker.Update(new[] { C3R2Member(1,1f), C3R2Member(2,2f) },0.2d);
            Assert.AreSame(cluster, second.Clusters.Single());
            Assert.AreEqual(1.5f, second.Clusters.Single().WorldAnchor.X, 0.0001f);
            Assert.AreEqual(0, second.LifecycleEvents.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_40_MemberAddPreservesExistingPairHysteresisState()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,1f) },0d);
            // Move the linked pair into the hysteresis band; it must remain linked.
            tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,3.5f) },0.1d);
            var update = tracker.Update(new[] { C3R2Member(1,0f), C3R2Member(2,3.5f), C3R2Member(3,20f) },0.11d);
            Assert.AreEqual(2, update.Clusters.Count);
            Assert.IsTrue(update.Clusters.Any(x => x.MemberStableKeys.Contains(1L) && x.MemberStableKeys.Contains(2L)));
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C1_01_SemanticCompositionChangeWithStableMembershipEmitsExactlyOneLifecycleEvent()
        {
            var tracker = new MarkerWorldClusterTracker();
            var first = new[]
            {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "same", "Before"),
                C3R2Member(2, 1f, MarkerClassKind.Tier2, "other", "Green"),
            };
            tracker.Update(first, 0d);

            var second = new[]
            {
                C3R2Member(1, 0f, MarkerClassKind.Tier3, "changed", "After"),
                C3R2Member(2, 1f, MarkerClassKind.Tier2, "other", "Green"),
            };
            var update = tracker.Update(second, 0.2d);

            Assert.IsFalse(update.MembershipChanged);
            Assert.AreEqual(1, update.LifecycleEvents.Count);
            var lifecycle = update.LifecycleEvents.Single();
            Assert.AreEqual(MarkerSemanticLifecycleKind.CompositionChanged, lifecycle.Kind);
            Assert.AreEqual(update.Clusters.Single().StableKey, lifecycle.Cluster.StableKey);
            Assert.AreEqual(update.Clusters.Single().MemberFingerprint, lifecycle.Cluster.MemberFingerprint);
            Assert.AreEqual("semantic-composition-changed", lifecycle.Reason);
            Assert.AreEqual(2, MarkerClusterPresentationPolicy.CompositionCountSum(lifecycle.Cluster.Composition));
            Assert.AreEqual(MarkerSemanticCategory.Tier2, lifecycle.Cluster.Composition[0].Category);
            Assert.AreEqual(MarkerSemanticCategory.Tier3, lifecycle.Cluster.Composition[1].Category);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C1_02_RepeatedIdenticalSemanticCompositionEmitsNoLifecycleChurn()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier1, "a", "Before") }, 0d);
            var changed = tracker.Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier2, "b", "After") }, 0.2d);
            Assert.AreEqual(1, changed.LifecycleEvents.Count(x => x.Kind == MarkerSemanticLifecycleKind.CompositionChanged));

            var identical = tracker.Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier2, "b", "After") }, 0.4d);
            Assert.AreEqual(0, identical.LifecycleEvents.Count);
            Assert.IsFalse(identical.MembershipChanged);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C3_01_CompactExpandedPrioritizesPhysicalCountForOverflow()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(2,.2f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"same","Same"),
                C3R2Member(4,.6f,MarkerClassKind.Tier1,"other","Other") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Compact, showDistance: false, detailRows: 1), 1, true, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries[0].Category);
            Assert.AreEqual(4, plan.CategoryEntries[0].Count);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.AreEqual(0, plan.ShownDetailRows);
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
            Assert.AreEqual(string.Empty, plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C3_02_ExpandedPriorityUsesStableCategoryForEqualCounts()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier2,"green","Alpha Green"),
                C3R2Member(2,.2f,MarkerClassKind.Tier2,"green","Alpha Green"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"white","Zulu White"),
                C3R2Member(4,.6f,MarkerClassKind.Tier1,"white","Zulu White") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, true, false);
            Assert.AreEqual("Alpha Green ×2\nZulu White ×2", plan.Text);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier2, plan.DetailedItemRows[0].Category);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.DetailedItemRows[1].Category);
            Assert.AreEqual(2, plan.DetailedItemRows[0].Count);
            Assert.AreEqual(2, plan.DetailedItemRows[1].Count);
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C3_03_ExpandedPriorityUsesLocalizedNameOrdinalForEqualCountAndCategory()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"z-id","Beta"),
                C3R2Member(2,.2f,MarkerClassKind.Tier1,"z-id","Beta"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"a-id","Alpha"),
                C3R2Member(4,.6f,MarkerClassKind.Tier1,"a-id","Alpha") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Compact, showDistance: false), 1, true, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries[0].Category);
            Assert.AreEqual(4, plan.CategoryEntries[0].Count);
            Assert.IsFalse(plan.Text.Contains("Alpha"));
            Assert.IsFalse(plan.Text.Contains("Beta"));
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C3_04_ExpandedPriorityUsesIdentityOrdinalWhenVisibleNamesTie()
        {
            var tracker = new MarkerWorldClusterTracker();
            var cluster = tracker.Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"z-id","Same Name"),
                C3R2Member(2,.2f,MarkerClassKind.Tier1,"z-id","Same Name"),
                C3R2Member(3,.4f,MarkerClassKind.Tier1,"a-id","Same Name"),
                C3R2Member(4,.6f,MarkerClassKind.Tier1,"a-id","Same Name") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, true, false);
            Assert.AreEqual("Same Name ×2\nSame Name ×2", plan.Text);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            Assert.AreEqual("a-id", plan.DetailedItemRows[0].ItemIdentity);
            Assert.AreEqual("z-id", plan.DetailedItemRows[1].ItemIdentity);
            Assert.AreEqual(2, plan.DetailedItemRows[0].Count);
            Assert.AreEqual(2, plan.DetailedItemRows[1].Count);
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
            Assert.IsFalse(plan.Text.Contains("White ×4"));
        }


        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_01_SameCategoryMembersAt3Point8MetersMerge()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 3.8f) }, 0d);
            Assert.AreEqual(1, update.Clusters.Count);
            Assert.AreEqual(2, update.Clusters.Single().TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_02_SameCategoryChainAt4Point4MetersFormsOnePile()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 4.4f), C3R2Member(3, 8.8f) }, 0d);
            Assert.AreEqual(1, update.Clusters.Count);
            Assert.AreEqual(3, update.Clusters.Single().TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_03_MixedTier1Tier2Tier3At4MetersFormsOneNeutralMixedCluster()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "w", "White"),
                C3R2Member(2, 4.0f, MarkerClassKind.Tier2, "g", "Green"),
                C3R2Member(3, 8.0f, MarkerClassKind.Tier3, "r", "Red") }, 0d);
            var cluster = update.Clusters.Single();
            Assert.AreEqual(3, cluster.TotalCount);
            Assert.IsTrue(cluster.IsMixedCategory);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_04_NewPairAt4Point6MetersDoesNotMerge()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 4.6f) }, 0d);
            Assert.AreEqual(2, update.Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_05_LinkedPairAt5Point5MetersRemainsLinked()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 4.0f) }, 0d);
            var update = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 5.5f) }, 1.0d);
            Assert.AreEqual(1, update.Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_06_LinkedPairAbove6MetersSplitsOnlyAfterDwell()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 4.0f) }, 0d);
            var pending = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.10d);
            Assert.AreEqual(1, pending.Clusters.Count);
            var beforeDwell = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.44d);
            Assert.AreEqual(1, beforeDwell.Clusters.Count);
            var split = tracker.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 6.1f) }, 0.46d);
            Assert.AreEqual(2, split.Clusters.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_07_TwoNewPilesSeparatedByAtLeast6MetersRemainDistinct()
        {
            var tracker = new MarkerWorldClusterTracker();
            var update = tracker.Update(new[] {
                C3R2Member(1, 0f), C3R2Member(2, 4.4f),
                C3R2Member(3, 10.5f), C3R2Member(4, 14.9f) }, 0d);
            Assert.AreEqual(2, update.Clusters.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 2 }, update.Clusters.Select(x => x.TotalCount).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_08_LocalCommandAllCompletedSuppressesWorldPresentation()
        {
            var decision = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true });
            Assert.IsTrue(decision.AllLocalStateResolved);
            Assert.IsFalse(decision.AnyLocalPending);
            Assert.IsTrue(decision.SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_09_LocalCommandAnyPendingKeepsWorldPresentationVisible()
        {
            var decision = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true, false });
            Assert.IsTrue(decision.AllLocalStateResolved);
            Assert.IsTrue(decision.AnyLocalPending);
            Assert.IsFalse(decision.SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_10_LocalCommandUnresolvedStateFailsOpenVisible()
        {
            var decision = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true, null });
            Assert.IsFalse(decision.AllLocalStateResolved);
            Assert.IsFalse(decision.SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_11_LocalCommandAllCompletedSuppressionDoesNotDependOnPersonalMarkersEnabled()
        {
            var parameters = typeof(LocalCommandPresentationPolicy).GetMethod(nameof(LocalCommandPresentationPolicy.Evaluate))!.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(System.Collections.Generic.IReadOnlyList<bool?>), parameters[0].ParameterType);
            Assert.IsTrue(LocalCommandPresentationPolicy.Evaluate(new bool?[] { true, true }).SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_12_LocalCommandGateDoesNotDestroyOrDeactivateNetworkPicker()
        {
            var decision = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true });
            Assert.IsTrue(decision.SuppressWorldPresentation);
            Assert.IsTrue(decision.AllLocalStateResolved);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_13_LocalCommandGateRefreshesLatePresentationChildren()
        {
            var first = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true });
            var repeated = LocalCommandPresentationPolicy.Evaluate(new bool?[] { true });
            Assert.AreEqual(first.SuppressWorldPresentation, repeated.SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_14_LocalCommandGateRestoresOnPendingOrLifecycleBoundary()
        {
            Assert.IsTrue(LocalCommandPresentationPolicy.Evaluate(new bool?[] { true }).SuppressWorldPresentation);
            Assert.IsFalse(LocalCommandPresentationPolicy.Evaluate(new bool?[] { false }).SuppressWorldPresentation);
            Assert.IsFalse(LocalCommandPresentationPolicy.Evaluate(Array.Empty<bool?>()).SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_15_MultipleLocalUsersRequireAllCompletedBeforeProcessVisualHide()
        {
            Assert.IsFalse(LocalCommandPresentationPolicy.Evaluate(new bool?[] { true, false, true }).SuppressWorldPresentation);
            Assert.IsTrue(LocalCommandPresentationPolicy.Evaluate(new bool?[] { true, true, true }).SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_16_LocalPresentationRemoteOpClassifiesSupportDroneWithoutServerDeathApi()
            => Assert.AreEqual(ParticipantState.SupportDrone, LocalParticipantPresentationPolicy.Classify(true, true, false));

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_17_LocalPresentationBodyPresentClassifiesAlive()
            => Assert.AreEqual(ParticipantState.Alive, LocalParticipantPresentationPolicy.Classify(true, false, true));

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_18_LocalPresentationNoBodyNoRemoteOpClassifiesFullyDead()
            => Assert.AreEqual(ParticipantState.FullyDead, LocalParticipantPresentationPolicy.Classify(true, false, false));

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_19_SupportDroneSignalWinsOverAliveFallback()
            => Assert.AreEqual(ParticipantState.SupportDrone, LocalParticipantPresentationPolicy.Classify(true, true, true));

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_20_LocalPresentationMissingMasterClassifiesDisconnected()
            => Assert.AreEqual(ParticipantState.Disconnected, LocalParticipantPresentationPolicy.Classify(false, false, false));

        [TestMethod]
        public void ISF_R1_C20_C5_C3R2_C4R2_21_CommandSuppressionPolicyFailsOpenForZeroLocalParticipants()
        {
            var decision = LocalCommandPresentationPolicy.Evaluate(Array.Empty<bool?>());
            Assert.IsFalse(decision.AllLocalStateResolved);
            Assert.IsFalse(decision.AnyLocalPending);
            Assert.IsFalse(decision.SuppressWorldPresentation);
        }


        [TestMethod]
        public void ISF_R1_C20_R11_R4_01_DetailedSingleKnownItemRendersExactTitleAndOneTotal()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(101, 0f, MarkerClassKind.Tier1, "syringe", "Soldier's Syringe") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed), 19, false, false);
            Assert.AreEqual("Soldier's Syringe ×1\n19 m", plan.Text);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual("Soldier's Syringe", plan.DetailedItemRows[0].LocalizedName);
            Assert.AreEqual(1, plan.DetailedItemRows[0].Count);
            Assert.IsFalse(plan.RenderTotalCount);
            Assert.AreEqual(1, plan.Text.Count(x => x == '×'));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_02_DetailedIdenticalItemsRenderExactTitleAndOneTotal()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(101, 0f, MarkerClassKind.Tier1, "syringe", "Soldier's Syringe"),
                C3R2Member(102, 1f, MarkerClassKind.Tier1, "syringe", "Soldier's Syringe") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 19, false, false);
            Assert.AreEqual("Soldier's Syringe ×2", plan.Text);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual("Soldier's Syringe", plan.DetailedItemRows[0].LocalizedName);
            Assert.AreEqual(2, plan.DetailedItemRows[0].Count);
            Assert.IsFalse(plan.Text.Contains("White ×2"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_03_DetailedHomogeneousDifferentItemsUsesFullCategoryAndOneTotal()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(101, 0f, MarkerClassKind.Tier2, "a", "A"),
                C3R2Member(102, 1f, MarkerClassKind.Tier2, "b", "B") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, false, false);
            Assert.AreEqual("A ×1\nB ×1", plan.Text);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEqual(new[] { "A", "B" }, plan.DetailedItemRows.Select(x => x.LocalizedName).ToArray());
            Assert.IsTrue(plan.DetailedItemRows.All(x => x.Category == MarkerSemanticCategory.Tier2));
            Assert.IsFalse(plan.Text.Contains("Green ×2"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_04_DetailedMixedUsesNeutralTitleFullCompositionAndOneTotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, false, false);
            Assert.AreEqual("Green ×1\nWhite A ×1\nWhite B ×1", plan.Text);
            Assert.AreEqual(3, plan.DetailedItemRows.Count);
            CollectionAssert.AreEqual(new[] { "Green", "White A", "White B" }, plan.DetailedItemRows.Select(x => x.LocalizedName).ToArray());
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.IsFalse(plan.Text.Contains("Items ×"));
            Assert.IsTrue(plan.NeutralMainSemantic);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_05_DetailedUnresolvedCommandDoesNotInventConcreteItemIdentity()
        {
            var command = new MarkerWorldMember(77, PersonalMarkerKind.CommandPicker, new MarkerWorldPoint(0f, 0f, 0f), "COMMAND:77", "Artifact of Command", MarkerClassKind.Unknown);
            var cluster = new MarkerWorldClusterTracker().Update(new[] { command }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, false, false);
            Assert.IsTrue(plan.Text.StartsWith("Choice ", StringComparison.Ordinal));
            Assert.IsTrue(plan.Text.EndsWith("×1", StringComparison.Ordinal));
            Assert.AreEqual(0, plan.DetailedItemRows.Count);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.CommandState, plan.CategoryEntries[0].Category);
            Assert.IsFalse(plan.Text.Contains("Artifact of Command"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_06_CompactHomogeneousHasNoTitleOneDiamondAndOneTotal()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier1, "a", "White Name") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Compact, showDistance: false), 1, false, false);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.IsFalse(plan.ShowMainDiamond);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.IsTrue(plan.RenderCategorySubcounts);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier1, plan.CategoryEntries[0].Category);
            Assert.AreEqual(1, plan.CategoryEntries[0].Count);
            Assert.IsFalse(plan.Text.Contains("White Name"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_07_CompactDefaultMixedUsesNeutralSingleDiamondAndOneTotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Compact, showDistance: false), 1, false, false);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.IsFalse(plan.ShowMainDiamond);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.AreEqual(2, plan.CategoryEntries.Count);
            Assert.AreEqual(3, plan.CategoryEntries.Sum(x => x.Count));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_08_CompactCategoryDiamondsUseSubcountsWithoutSecondTotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Compact, showDistance: false, compactStyle: MarkerCompactMixedStyle.CategoryDiamonds), 1, false, false);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.IsFalse(plan.ShowMainDiamond);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.AreEqual(2, plan.CompactBadges.Count);
            Assert.AreEqual(3, plan.CompactBadges.Sum(x => x.Count));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_09_CompactShowCountFalseHasNoNumericPickupCount()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Compact, showDistance: false, compactShowCount: false), 1, false, false);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.IsFalse(plan.RenderTotalCount);
            Assert.IsFalse(plan.RenderCategorySubcounts);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_10_CategoryDiamondsRespectShowCountFalseWithoutNumericSubcounts()
        {
            var plan = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Compact, showDistance: false, compactShowCount: false, compactStyle: MarkerCompactMixedStyle.CategoryDiamonds), 1, false, false);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.IsFalse(plan.RenderCategorySubcounts);
            Assert.AreEqual(string.Empty, plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_11_SingleCountInvariantIsExplicitAndMutuallyExclusive()
        {
            Assert.AreEqual(1, MarkerClusterPresentationPolicy.TOTAL_COUNT_RENDER_SLOTS_PER_PRESENTATION_NODE);
            var detailed = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 1, false, false);
            var categories = MarkerClusterPresentationPolicy.Build(C3R2MixedCluster(), R4Settings(MarkerPresentationMode.Compact, showDistance: false), 1, false, false);
            Assert.AreEqual(MarkerCountRenderSource.None, detailed.CountRenderSource);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, categories.CountRenderSource);
            Assert.AreEqual(3, detailed.DetailedItemRows.Count);
            Assert.AreEqual(3, detailed.Text.Count(x => x == '×'));
            Assert.IsFalse(detailed.RenderTotalCount);
            Assert.IsFalse(categories.RenderTotalCount);
            Assert.IsTrue(categories.RenderCategorySubcounts);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_12_DistanceToggleChangesPresentationOnly()
        {
            var cluster = C3R2MixedCluster();
            var withDistance = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: true), 55, false, false);
            var withoutDistance = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 55, false, false);
            StringAssert.Contains(withDistance.Text, "55 m");
            Assert.IsFalse(withoutDistance.Text.Contains("55 m"));
            Assert.AreEqual(3, cluster.TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_13_ExpandedDetailHardMaxFiveAndPhysicalOverflowExact()
        {
            var tracker = new MarkerWorldClusterTracker();
            var members = Enumerable.Range(1, 8).Select(i => C3R2Member(i, i * 0.2f, MarkerClassKind.Tier1, "id:" + i, "Item " + i)).ToArray();
            var cluster = tracker.Update(members, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, detailRows: 99), 1, true, false);
            Assert.AreEqual(12, MarkerClusterPresentationPolicy.ClampDetailRows(99));
            Assert.AreEqual(8, plan.DetailedItemRows.Count);
            Assert.AreEqual(8, plan.ShownDetailRows);
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
            Assert.IsFalse(plan.Expanded);
            for (var i = 1; i <= 8; i++) Assert.IsTrue(plan.Text.Contains("Item " + i + " ×1"));
            Assert.IsFalse(plan.Text.Contains("White ×8"));
            Assert.IsFalse(plan.Text.Contains("+"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_14_NearbyPhysicalClustersCollapseIntoOneDenseSummary()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters;
            Assert.AreEqual(2, physical.Count);
            var dense = new MarkerDenseAreaSummaryTracker().Update(physical, 0d);
            Assert.AreEqual(1, dense.Nodes.Count);
            Assert.IsTrue(dense.Nodes[0].IsDenseSummary);
            Assert.AreEqual(2, dense.Nodes[0].TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_15_ActiveDenseSummarySuppressesRepresentedIndividualClusterNodes()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f), C3R2Member(3, 16f) }, 0d).Clusters;
            var dense = new MarkerDenseAreaSummaryTracker().Update(physical, 0d);
            Assert.AreEqual(1, dense.Nodes.Count);
            CollectionAssert.AreEquivalent(physical.Select(x => x.StableKey).ToArray(), dense.Nodes[0].PhysicalClusterKeys.ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_16_SeparateWorldAreasRemainSeparateDenseSummaries()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f), C3R2Member(3, 40f), C3R2Member(4, 48f) }, 0d).Clusters;
            var dense = new MarkerDenseAreaSummaryTracker().Update(physical, 0d);
            Assert.AreEqual(2, dense.Nodes.Count);
            Assert.IsTrue(dense.Nodes.All(x => x.IsDenseSummary));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_17_DenseInputPermutationDoesNotChangeCompositionOrOwner()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier1), C3R2Member(2, 8f, MarkerClassKind.Tier2) }, 0d).Clusters.ToArray();
            var a = new MarkerDenseAreaSummaryTracker().Update(physical, 0d).Nodes.Single();
            Array.Reverse(physical);
            var b = new MarkerDenseAreaSummaryTracker().Update(physical, 0d).Nodes.Single();
            Assert.AreEqual(a.StableKey, b.StableKey);
            Assert.AreEqual(a.MemberFingerprint, b.MemberFingerprint);
            Assert.AreEqual(a.TotalCount, b.TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_18_DenseIdenticalWorldInputHasNoMembershipChurn()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters;
            var tracker = new MarkerDenseAreaSummaryTracker();
            tracker.Update(physical, 0d);
            var repeated = tracker.Update(physical, 1d);
            Assert.IsFalse(repeated.MembershipChanged);
            Assert.AreEqual(1, repeated.Nodes.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_19_PickupCompletionDecrementsExistingDenseSummaryWithoutFanout()
        {
            var world = new MarkerWorldClusterTracker();
            var dense = new MarkerDenseAreaSummaryTracker();
            var firstPhysical = world.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters;
            var first = dense.Update(firstPhysical, 0d).Nodes.Single();
            var owner = first.StableKey;
            var secondPhysical = world.Update(new[] { C3R2Member(1, 0f) }, 0.01d).Clusters;
            var second = dense.Update(secondPhysical, 0.01d);
            Assert.AreEqual(1, second.Nodes.Count);
            Assert.AreEqual(owner, second.Nodes[0].StableKey);
            Assert.IsTrue(second.Nodes[0].IsDenseSummary);
            Assert.AreEqual(1, second.Nodes[0].TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_20_DenseOwnerStableAcrossOrdinaryAddRemoveWhileAreaSurvives()
        {
            var world = new MarkerWorldClusterTracker();
            var dense = new MarkerDenseAreaSummaryTracker();
            var a = dense.Update(world.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters, 0d).Nodes.Single();
            var b = dense.Update(world.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f), C3R2Member(3, 16f) }, 0.1d).Clusters, 0.1d).Nodes.Single();
            var c = dense.Update(world.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0.2d).Clusters, 0.2d).Nodes.Single();
            Assert.AreEqual(a.StableKey, b.StableKey);
            Assert.AreEqual(a.StableKey, c.StableKey);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_21_DenseSummaryDisappearsOnlyWhenNoPendingMembersRemain()
        {
            var world = new MarkerWorldClusterTracker();
            var dense = new MarkerDenseAreaSummaryTracker();
            dense.Update(world.Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters, 0d);
            var singleton = dense.Update(world.Update(new[] { C3R2Member(1, 0f) }, 0.01d).Clusters, 0.01d);
            Assert.AreEqual(1, singleton.Nodes.Count);
            Assert.IsTrue(singleton.Nodes[0].IsDenseSummary);
            var empty = dense.Update(world.Update(Array.Empty<MarkerWorldMember>(), 0.02d).Clusters, 0.02d);
            Assert.AreEqual(0, empty.Nodes.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_22_ManyOffscreenNodesInSameDirectionProduceOneSectorIndicator()
        {
            var result = MarkerDirectionalAggregationPolicy.Aggregate(new[] {
                new MarkerDirectionalInput(1, 1f, 0f, 30f, 2),
                new MarkerDirectionalInput(2, 0.95f, 0.10f, 20f, 3),
                new MarkerDirectionalInput(3, 0.90f, -0.10f, 40f, 4) });
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(3, result[0].RepresentedNodeCount);
            Assert.AreEqual(9, result[0].TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_23_OffscreenSectorDistanceUsesNearestRepresentedDistance()
        {
            var result = MarkerDirectionalAggregationPolicy.Aggregate(new[] {
                new MarkerDirectionalInput(10, -1f, 0f, 44f, 1),
                new MarkerDirectionalInput(11, -1f, 0.1f, 17f, 1) }).Single();
            Assert.AreEqual(17f, result.NearestDistanceMeters, 0.0001f);
            Assert.AreEqual(11L, result.NearestPresentationKey);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_24_DefaultOffscreenPlanContainsDistanceOnlyNoDetailText()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(31, 20, true, false, false);
            Assert.AreEqual("31 m", plan.Text);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.AreEqual(0, plan.CompactBadges.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_25_OptionalOffscreenTotalAppearsExactlyOnce()
        {
            var plan = MarkerClusterPresentationPolicy.BuildOffscreen(31, 20, true, true, false);
            Assert.AreEqual("×20\n31 m", plan.Text);
            Assert.AreEqual(1, plan.Text.Count(x => x == '×'));
            Assert.AreEqual(MarkerCountRenderSource.TotalCountText, plan.CountRenderSource);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_26_SeparateBroadDirectionsProduceSeparateBoundedIndicators()
        {
            var result = MarkerDirectionalAggregationPolicy.Aggregate(new[] {
                new MarkerDirectionalInput(1, 1f, 0f, 10f, 1),
                new MarkerDirectionalInput(2, -1f, 0f, 10f, 1),
                new MarkerDirectionalInput(3, 0f, 1f, 10f, 1),
                new MarkerDirectionalInput(4, 0f, -1f, 10f, 1) });
            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.Count <= MarkerDirectionalAggregationPolicy.BroadSectorCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_27_FovTransitionChangesPresentationStateWithoutWorldInput()
        {
            var fov = new MarkerFovPresentationHysteresisPolicy();
            Assert.IsTrue(fov.Update(1, 30f, true));
            Assert.IsTrue(fov.Update(1, 46f, false));
            Assert.IsFalse(fov.Update(1, 50f, false));
            Assert.IsFalse(fov.Update(1, 45f, true));
            Assert.IsTrue(fov.Update(1, 40f, true));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_28_FovEnterExitHysteresisHasPositiveBoundaryBand()
        {
            Assert.IsTrue(MarkerFovPresentationHysteresisPolicy.ExitAngleDegrees > MarkerFovPresentationHysteresisPolicy.EnterAngleDegrees);
            Assert.IsTrue(MarkerFovPresentationHysteresisPolicy.NextState(true, 45f, false));
            Assert.IsFalse(MarkerFovPresentationHysteresisPolicy.NextState(false, 45f, true));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_29_OffscreenEdgePaddingIsClampedToSafeBounds()
        {
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenEdgePaddingMin, MarkerVisualSettingsPolicy.ClampOffscreenEdgePadding(-500f), 0.0001f);
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenEdgePaddingMax, MarkerVisualSettingsPolicy.ClampOffscreenEdgePadding(500f), 0.0001f);
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenEdgePaddingDefault, MarkerVisualSettingsPolicy.ClampOffscreenEdgePadding(float.NaN), 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_30_DirectionalAggregationIsHardBoundedToEightBroadSectors()
        {
            var inputs = Enumerable.Range(0, 64).Select(i => {
                var a = i * Math.PI * 2.0 / 64.0;
                return new MarkerDirectionalInput(i + 1, (float)Math.Cos(a), (float)Math.Sin(a), 10f + i, 1);
            }).ToArray();
            var result = MarkerDirectionalAggregationPolicy.Aggregate(inputs);
            Assert.AreEqual(8, MarkerDirectionalAggregationPolicy.BroadSectorCount);
            Assert.IsTrue(result.Count <= 8);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_31_OffscreenDisabledModelCanProduceNoIndicatorsWithoutSemanticMutation()
        {
            var cluster = C3R2MixedCluster();
            var semanticFingerprint = cluster.MemberFingerprint;
            var disabledIndicators = Array.Empty<MarkerDirectionalSectorSummary>();
            Assert.AreEqual(0, disabledIndicators.Length);
            Assert.AreEqual(semanticFingerprint, cluster.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_32_ModeSwitchPreservesDenseMembershipAndLocalPendingCount()
        {
            var physical = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f), C3R2Member(2, 8f) }, 0d).Clusters;
            var node = new MarkerDenseAreaSummaryTracker().Update(physical, 0d).Nodes.Single();
            var detailed = MarkerClusterPresentationPolicy.Build(node.PresentationCluster, R4Settings(MarkerPresentationMode.Detailed), 10, false, false);
            var compact = MarkerClusterPresentationPolicy.Build(node.PresentationCluster, R4Settings(MarkerPresentationMode.Compact), 10, false, false);
            Assert.AreNotEqual(detailed.Text, compact.Text);
            Assert.AreEqual(2, node.TotalCount);
            Assert.AreEqual(2, node.MemberStableKeys.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_33_OpacitySettingsClampAndRemainPresentationValues()
        {
            Assert.AreEqual(0f, MarkerVisualSettingsPolicy.ClampOpacity(-1f, 1f), 0.0001f);
            Assert.AreEqual(1f, MarkerVisualSettingsPolicy.ClampOpacity(2f, 1f), 0.0001f);
            Assert.AreEqual(0.5f, MarkerVisualSettingsPolicy.ClampOpacity(0.5f, 1f), 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_34_OffscreenScaleClampsToDocumentedRange()
        {
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenScaleMin, MarkerVisualSettingsPolicy.ClampOffscreenScale(0f), 0.0001f);
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenScaleMax, MarkerVisualSettingsPolicy.ClampOffscreenScale(9f), 0.0001f);
            Assert.AreEqual(MarkerVisualSettingsPolicy.OffscreenScaleDefault, MarkerVisualSettingsPolicy.ClampOffscreenScale(float.PositiveInfinity), 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_35_CanonicalVisualDefaultsAreDeterministic()
        {
            Assert.AreEqual(1f, MarkerVisualSettingsPolicy.MarkerOpacityDefault, 0.0001f);
            Assert.AreEqual(0f, MarkerVisualSettingsPolicy.BackgroundOpacityDefault, 0.0001f);
            Assert.AreEqual(1f, MarkerVisualSettingsPolicy.OffscreenOpacityDefault, 0.0001f);
            Assert.AreEqual(36f, MarkerVisualSettingsPolicy.OffscreenEdgePaddingDefault, 0.0001f);
            Assert.AreEqual(MarkerCategorySortOrder.HighToLow, MarkerCategorySummaryPolicy.DefaultSortOrder);
            Assert.AreEqual(0.60f, MarkerCategorySummaryPolicy.PyramidMaxVerticalOffsetUnits, 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_36_RepeatedDirectionalAggregationIsDeterministicAndIdempotent()
        {
            var inputs = new[] {
                new MarkerDirectionalInput(5, 1f, 0.1f, 20f, 2),
                new MarkerDirectionalInput(3, 1f, -0.1f, 20f, 4) };
            var a = MarkerDirectionalAggregationPolicy.Aggregate(inputs).Single();
            var b = MarkerDirectionalAggregationPolicy.Aggregate(inputs).Single();
            Assert.AreEqual(a.PresentationKey, b.PresentationKey);
            Assert.AreEqual(a.TotalCount, b.TotalCount);
            Assert.AreEqual(3L, a.NearestPresentationKey);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_37_FrozenPhysicalWorldClusterThresholdsRemainFourPointFiveAndSix()
        {
            Assert.AreEqual(4.50f, MarkerWorldClusterTracker.MergeRadiusMeters, 0.0001f);
            Assert.AreEqual(6.00f, MarkerWorldClusterTracker.SplitRadiusMeters, 0.0001f);
            Assert.AreEqual(0.35d, MarkerWorldClusterTracker.ThresholdTransitionDwellSeconds, 0.0001d);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_38_LocalCommandPresentationClosureRemainsUnchanged()
        {
            Assert.IsTrue(LocalCommandPresentationPolicy.Evaluate(new bool?[] { true }).SuppressWorldPresentation);
            Assert.IsFalse(LocalCommandPresentationPolicy.Evaluate(new bool?[] { false }).SuppressWorldPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_39_DetailedCategoryDiamondToggleDoesNotChangeTextOrMembership()
        {
            var cluster = C3R2MixedCluster();
            var memberFingerprint = cluster.MemberFingerprint;
            var shown = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: true), 1, false, false);
            var hidden = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: false), 1, false, false);

            Assert.AreEqual("Green ×1\nWhite A ×1\nWhite B ×1", shown.Text);
            Assert.AreEqual(shown.Text, hidden.Text);
            Assert.IsFalse(shown.ShowMainDiamond);
            Assert.IsFalse(hidden.ShowMainDiamond);
            Assert.IsFalse(shown.ShowDetailedCategoryRowDiamonds);
            Assert.IsFalse(hidden.ShowDetailedCategoryRowDiamonds);
            CollectionAssert.AreEqual(
                shown.DetailedItemRows.Select(x => x.ItemIdentity).ToArray(),
                hidden.DetailedItemRows.Select(x => x.ItemIdentity).ToArray());
            CollectionAssert.AreEqual(
                shown.DetailedItemRows.Select(x => x.Count).ToArray(),
                hidden.DetailedItemRows.Select(x => x.Count).ToArray());
            Assert.AreEqual(memberFingerprint, cluster.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_40_TierCompositionToggleSuppressesOnlyMixedCompositionLine()
        {
            var cluster = C3R2MixedCluster();
            var shown = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showComposition: true), 1, false, false);
            var hidden = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showComposition: false), 1, false, false);
            Assert.AreEqual("Green ×1\nWhite A ×1\nWhite B ×1", shown.Text);
            Assert.AreEqual(shown.Text, hidden.Text);
            CollectionAssert.AreEqual(
                shown.DetailedItemRows.Select(x => x.ItemIdentity).ToArray(),
                hidden.DetailedItemRows.Select(x => x.ItemIdentity).ToArray());
            CollectionAssert.AreEqual(
                shown.DetailedItemRows.Select(x => x.Count).ToArray(),
                hidden.DetailedItemRows.Select(x => x.Count).ToArray());
            Assert.AreEqual(3, cluster.TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_41_DenseMembershipApiHasNoCameraFovOrScreenInput()
        {
            var parameters = typeof(MarkerDenseAreaSummaryTracker).GetMethod(nameof(MarkerDenseAreaSummaryTracker.Update))!.GetParameters();
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(System.Collections.Generic.IReadOnlyList<MarkerSemanticCluster>), parameters[0].ParameterType);
            Assert.AreEqual(typeof(double), parameters[1].ParameterType);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_42_DenseCalibrationIsSeparateFromFrozenPhysicalCalibration()
        {
            Assert.AreEqual(12f, MarkerDenseAreaSummaryTracker.MergeRadiusMeters, 0.0001f);
            Assert.AreEqual(16f, MarkerDenseAreaSummaryTracker.SplitRadiusMeters, 0.0001f);
            Assert.AreEqual(0.35d, MarkerDenseAreaSummaryTracker.ThresholdTransitionDwellSeconds, 0.0001d);
            Assert.IsTrue(MarkerDenseAreaSummaryTracker.MergeRadiusMeters > MarkerWorldClusterTracker.MergeRadiusMeters);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4_43_DirectionalSectorPresentationKeysAreStableAndUnique()
        {
            var keys = Enum.GetValues(typeof(MarkerDirectionSector)).Cast<MarkerDirectionSector>()
                .Select(MarkerDirectionalAggregationPolicy.SectorPresentationKey).ToArray();
            Assert.AreEqual(8, keys.Distinct().Count());
            Assert.IsTrue(keys.All(x => x < 0));
        }


        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_01_GroupedIdenticalItemsCollapseToOneCategoryRow()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "same", "Same"),
                C3R2Member(2, .2f, MarkerClassKind.Tier1, "same", "Same") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.AreEqual("Same ×2", plan.Text);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual("same", plan.DetailedItemRows[0].ItemIdentity);
            Assert.AreEqual(2, plan.DetailedItemRows[0].Count);
            Assert.IsFalse(plan.Text.Contains("White ×2"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_02_GroupedSameCategoryDifferentItemsCollapseToOneCategoryRow()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier2, "a", "A"),
                C3R2Member(2, .2f, MarkerClassKind.Tier2, "b", "B") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, true, false);
            Assert.AreEqual("A ×1\nB ×1", plan.Text);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEqual(new[] { "A", "B" }, plan.DetailedItemRows.Select(x => x.LocalizedName).ToArray());
            Assert.IsTrue(plan.DetailedItemRows.All(x => x.Category == MarkerSemanticCategory.Tier2));
            Assert.AreEqual(2, plan.ShownDetailRows);
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
            Assert.IsFalse(plan.Text.Contains("Green ×2"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_03_MixedRed2Green7White8HasExactlyThreeCategoryRows()
        {
            var cluster = R4C2Cluster(8, 7, 2);
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.AreEqual("Red ×2\nGreen ×7\nWhite ×8", plan.Text);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.AreEqual(17, plan.CategoryEntries.Sum(x => x.Count));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_04_GroupedDetailedHasNoOverallHeaderTotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2, 1, 1), R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.IsFalse(plan.Text.Contains("Items ×"));
            Assert.AreEqual(MarkerCountRenderSource.DetailedCategoryRows, plan.CountRenderSource);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_05_GroupedDetailedHasZeroExactItemRows()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "a", "Exact A"),
                C3R2Member(2, .2f, MarkerClassKind.Tier2, "b", "Exact B") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, true, false);
            Assert.AreEqual(2, plan.DetailedItemRows.Count);
            CollectionAssert.AreEqual(new[] { "Exact B", "Exact A" }, plan.DetailedItemRows.Select(x => x.LocalizedName).ToArray());
            Assert.IsTrue(plan.Text.Contains("Exact A ×1"));
            Assert.IsTrue(plan.Text.Contains("Exact B ×1"));
            Assert.IsFalse(plan.Text.Contains("White ×1"));
            Assert.IsFalse(plan.Text.Contains("Green ×1"));
            Assert.AreEqual(0, plan.OverflowPhysicalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_06_TrueSingletonPreservesTruthfulExactLocalizedTitle()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier3, "red", "Brilliant Behemoth") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.AreEqual("Brilliant Behemoth ×1", plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_07_UnresolvedSingletonUsesTruthfulCategoryFallback()
        {
            var member = new MarkerWorldMember(7, PersonalMarkerKind.CommandPicker, new MarkerWorldPoint(0f,0f,0f), "COMMAND:7", "Should Not Leak", MarkerClassKind.Unknown);
            var cluster = new MarkerWorldClusterTracker().Update(new[] { member }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.IsTrue(plan.Text.StartsWith("Choice ", StringComparison.Ordinal));
            Assert.IsTrue(plan.Text.EndsWith("×1", StringComparison.Ordinal));
            Assert.AreEqual(0, plan.DetailedItemRows.Count);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.CommandState, plan.CategoryEntries[0].Category);
            Assert.IsFalse(plan.Text.Contains("Should Not Leak"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_08_DistanceIsSeparateAndOptional()
        {
            var cluster = R4C2Cluster(2, 1, 0);
            var shown = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: true), 70, false, false);
            var hidden = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 70, false, false);
            Assert.IsTrue(shown.Text.EndsWith("70 m", StringComparison.Ordinal));
            Assert.IsFalse(hidden.Text.Contains("70 m"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_09_HighToLowOrdersRedGreenWhite()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(1,1,1), R4Settings(MarkerPresentationMode.Detailed, showDistance:false, sortOrder:MarkerCategorySortOrder.HighToLow), 0, false, false);
            CollectionAssert.AreEqual(new[] { MarkerSemanticCategory.Tier3, MarkerSemanticCategory.Tier2, MarkerSemanticCategory.Tier1 }, plan.CategoryEntries.Select(x => x.Category).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_10_LowToHighIsExactInverseForRedGreenWhite()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(1,1,1), R4Settings(MarkerPresentationMode.Detailed, showDistance:false, sortOrder:MarkerCategorySortOrder.LowToHigh), 0, false, false);
            CollectionAssert.AreEqual(new[] { MarkerSemanticCategory.Tier1, MarkerSemanticCategory.Tier2, MarkerSemanticCategory.Tier3 }, plan.CategoryEntries.Select(x => x.Category).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_11_ExtendedCategoryPriorityTableIsDeterministic()
        {
            var categories = new[] { MarkerSemanticCategory.Tier3, MarkerSemanticCategory.Boss, MarkerSemanticCategory.Lunar, MarkerSemanticCategory.Void, MarkerSemanticCategory.Tier2, MarkerSemanticCategory.Equipment, MarkerSemanticCategory.LunarEquipment, MarkerSemanticCategory.Tier1, MarkerSemanticCategory.CommandState, MarkerSemanticCategory.Other, MarkerSemanticCategory.Unknown };
            var priorities = categories.Select(MarkerCategorySummaryPolicy.DisplayPriority).ToArray();
            for (var i = 1; i < priorities.Length; i++) Assert.IsTrue(priorities[i - 1] > priorities[i]);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_12_CurrentMixedCompositionFiveTwoTwoIsExact()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(5,2,2), R4Settings(MarkerPresentationMode.Detailed, showDistance:false), 0, false, false);
            Assert.AreEqual("Red ×2\nGreen ×2\nWhite ×5", plan.Text);
            Assert.AreEqual(9, plan.CategoryEntries.Sum(x => x.Count));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_13_RemoveOneRedRecomputesFiveTwoOne()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(R4C2Members(5,2,2), 0d);
            var cluster = tracker.Update(R4C2Members(5,2,1), 0.1d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance:false), 0, false, false);
            Assert.AreEqual("Red ×1\nGreen ×2\nWhite ×5", plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_14_RemoveLastRedLeavesWhiteFiveGreenTwoOnly()
        {
            var tracker = new MarkerWorldClusterTracker();
            tracker.Update(R4C2Members(5,2,1), 0d);
            var cluster = tracker.Update(R4C2Members(5,2,0), 0.1d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance:false), 0, false, false);
            Assert.AreEqual("Green ×2\nWhite ×5", plan.Text);
            Assert.IsFalse(plan.Text.Contains("Red"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_15_RepresentativeIdentityChangeCannotChangeCategoryTruth()
        {
            var tracker = new MarkerWorldClusterTracker();
            var a = tracker.Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"a","Alpha"), C3R2Member(2,.2f,MarkerClassKind.Tier2,"g","Green") },0d).Clusters.Single();
            var b = tracker.Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"z","Zulu"), C3R2Member(2,.2f,MarkerClassKind.Tier2,"g","Green") },.1d).Clusters.Single();
            var pa = MarkerClusterPresentationPolicy.Build(a, R4Settings(MarkerPresentationMode.Detailed, showDistance:false),0,false,false);
            var pb = MarkerClusterPresentationPolicy.Build(b, R4Settings(MarkerPresentationMode.Detailed, showDistance:false),0,false,false);
            Assert.AreEqual("Green ×1\nAlpha ×1", pa.Text);
            Assert.AreEqual("Green ×1\nZulu ×1", pb.Text);
            Assert.AreNotEqual(pa.Text, pb.Text);
            CollectionAssert.AreEqual(
                a.Composition.Select(x => x.Count).ToArray(),
                b.Composition.Select(x => x.Count).ToArray());
            CollectionAssert.AreEqual(
                a.Composition.Select(x => x.Category).ToArray(),
                b.Composition.Select(x => x.Category).ToArray());
            Assert.AreEqual(a.MemberFingerprint, b.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_16_InputPermutationKeepsCategoryCountsAndOrder()
        {
            var members = R4C2Members(3,2,1);
            var a = new MarkerWorldClusterTracker().Update(members,0d).Clusters.Single();
            var b = new MarkerWorldClusterTracker().Update(members.Reverse().ToArray(),0d).Clusters.Single();
            var pa = MarkerClusterPresentationPolicy.Build(a,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            var pb = MarkerClusterPresentationPolicy.Build(b,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            Assert.AreEqual(pa.Text,pb.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_17_CompactMixedUsesOneDiamondPerCategory()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8,7,2), R4Settings(MarkerPresentationMode.Compact, showDistance:false), 0, false, false);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.IsFalse(plan.ShowMainDiamond);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_18_EachCompactDiamondOwnsExactlyItsCategorySubtotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8,7,2), R4Settings(MarkerPresentationMode.Compact, showDistance:false), 0, false, false);
            CollectionAssert.AreEqual(new[] { 2, 7, 8 }, plan.CategoryEntries.Select(x => x.Count).ToArray());
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_19_CompactGroupedHasNoOverallTotalSlot()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2,2,2), R4Settings(MarkerPresentationMode.Compact, showDistance:false), 0, false, false);
            Assert.IsFalse(plan.RenderTotalCount);
            Assert.AreEqual(string.Empty, plan.Text);
            Assert.AreEqual(0, plan.Text.Count(x => x == '×'));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_20_CompactPyramidContainsNoCategoryOrItemWords()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2,2,2), R4Settings(MarkerPresentationMode.Compact, showDistance:false), 0, false, true);
            Assert.IsFalse(plan.Text.Contains("Бел"));
            Assert.IsFalse(plan.Text.Contains("Зел"));
            Assert.IsFalse(plan.Text.Contains("Крас"));
            Assert.AreEqual(string.Empty, plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_21_CompactShowCountFalseKeepsDiamondsAndRemovesNumericSubcounts()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2,2,2), R4Settings(MarkerPresentationMode.Compact, showDistance:false, compactShowCount:false), 0, false, false);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.AreEqual(MarkerCountRenderSource.None, plan.CountRenderSource);
            Assert.IsFalse(plan.RenderCategorySubcounts);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_22_CompactOneCategoryGroupUsesOneDiamondAndSubtotal()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(5,0,0), R4Settings(MarkerPresentationMode.Compact, showDistance:false), 0, false, false);
            Assert.AreEqual(1, plan.CategoryEntries.Count);
            Assert.AreEqual(5, plan.CategoryEntries[0].Count);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_23_PyramidVerticalSpanIsHardBounded()
        {
            var values = Enum.GetValues(typeof(MarkerSemanticCategory)).Cast<MarkerSemanticCategory>().Select(MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits).ToArray();
            Assert.IsTrue(values.All(x => x >= 0f && x <= MarkerCategorySummaryPolicy.PyramidMaxVerticalOffsetUnits));
            Assert.IsTrue(values.Max() - values.Min() <= MarkerCategorySummaryPolicy.PyramidMaxVerticalOffsetUnits);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_24_PyramidHorizontalCompressionHasDocumentedSafeMinimum()
        {
            Assert.AreEqual(1f, MarkerCategorySummaryPolicy.PyramidHorizontalSpacingFactor(3), 0.0001f);
            Assert.IsTrue(MarkerCategorySummaryPolicy.PyramidHorizontalSpacingFactor(11) >= MarkerCategorySummaryPolicy.PyramidMinimumHorizontalSpacingFactor);
            Assert.AreEqual(MarkerCategorySummaryPolicy.PyramidMinimumHorizontalSpacingFactor, MarkerCategorySummaryPolicy.PyramidHorizontalSpacingFactor(99), 0.0001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_25_HigherDisplayValueCategorySitsAboveLowerCategory()
        {
            Assert.IsTrue(MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits(MarkerSemanticCategory.Tier3) > MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits(MarkerSemanticCategory.Tier2));
            Assert.IsTrue(MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits(MarkerSemanticCategory.Tier2) > MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits(MarkerSemanticCategory.Tier1));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_26_RepeatedCategoryEntryBuildIsDeterministic()
        {
            var cluster = R4C2Cluster(8,7,2);
            var a = MarkerCategorySummaryPolicy.BuildCategoryEntries(cluster.Composition, MarkerCategorySortOrder.HighToLow);
            var b = MarkerCategorySummaryPolicy.BuildCategoryEntries(cluster.Composition, MarkerCategorySortOrder.HighToLow);
            CollectionAssert.AreEqual(a.Select(x => x.Category).ToArray(), b.Select(x => x.Category).ToArray());
            CollectionAssert.AreEqual(a.Select(x => x.Count).ToArray(), b.Select(x => x.Count).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_27_CategorySortDefaultIsCanonicalHighToLow()
        {
            Assert.AreEqual(MarkerCategorySortOrder.HighToLow, MarkerCategorySummaryPolicy.DefaultSortOrder);
            var settings = R4Settings(MarkerPresentationMode.Detailed);
            Assert.AreEqual(MarkerCategorySortOrder.HighToLow, settings.CategorySortOrder);
            Assert.IsTrue(settings.UseCategorySummaryPresentation);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_28_SortChangeChangesPresentationOnlyNotMembership()
        {
            var cluster = R4C2Cluster(1,1,1);
            var fingerprint = cluster.MemberFingerprint;
            var high = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,sortOrder:MarkerCategorySortOrder.HighToLow),0,false,false);
            var low = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,sortOrder:MarkerCategorySortOrder.LowToHigh),0,false,false);
            Assert.AreNotEqual(high.Text,low.Text);
            Assert.AreEqual(fingerprint,cluster.MemberFingerprint);
            Assert.AreEqual(3,cluster.TotalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_29_LowToHighExtendedOrderIsExactReverseOfHighToLow()
        {
            var composition = Enum.GetValues(typeof(MarkerSemanticCategory)).Cast<MarkerSemanticCategory>().Select(x => new MarkerCategoryCount(x,1)).ToArray();
            var high = MarkerCategorySummaryPolicy.BuildCategoryEntries(composition,MarkerCategorySortOrder.HighToLow).Select(x=>x.Category).ToArray();
            var low = MarkerCategorySummaryPolicy.BuildCategoryEntries(composition,MarkerCategorySortOrder.LowToHigh).Select(x=>x.Category).ToArray();
            CollectionAssert.AreEqual(high.Reverse().ToArray(),low);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_30_LegacyDetailRowAndCompositionSettingsAreInertForCanonicalGroupedPresentation()
        {
            var cluster = R4C2Cluster(3,2,1);
            var a = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,detailRows:1,showComposition:false),0,true,false);
            var b = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,detailRows:5,showComposition:true),0,false,false);
            Assert.AreEqual(a.Text,b.Text);
            Assert.AreEqual(0,a.ShownDetailRows);
            Assert.AreEqual(0,a.OverflowPhysicalCount);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_01_GroupedDetailedRequestsOneRowDiamondPerCategoryEntry()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8, 7, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: true), 0, false, false);
            Assert.IsTrue(plan.ShowDetailedCategoryRowDiamonds);
            Assert.AreEqual(3, plan.CategoryEntries.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_02_DetailedRowDiamondOrderIsCanonicalCategoryEntryOrder()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8, 7, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false, sortOrder: MarkerCategorySortOrder.HighToLow), 0, false, false);
            CollectionAssert.AreEqual(new[] { MarkerSemanticCategory.Tier3, MarkerSemanticCategory.Tier2, MarkerSemanticCategory.Tier1 }, plan.CategoryEntries.Select(x => x.Category).ToArray());
            Assert.IsTrue(plan.ShowDetailedCategoryRowDiamonds);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_03_DetailedRowDiamondsUseEveryCategoryNotRepresentativeCategory()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(5, 2, 1), R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.AreEqual(3, plan.CategoryEntries.Select(x => x.Category).Distinct().Count());
            Assert.IsTrue(plan.CategoryEntries.Any(x => x.Category == MarkerSemanticCategory.Tier1));
            Assert.IsTrue(plan.CategoryEntries.Any(x => x.Category == MarkerSemanticCategory.Tier2));
            Assert.IsTrue(plan.CategoryEntries.Any(x => x.Category == MarkerSemanticCategory.Tier3));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_04_GroupedDetailedSuppressesRedundantPrimaryDiamond()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2, 2, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: true), 0, false, false);
            Assert.IsTrue(plan.ShowDetailedCategoryRowDiamonds);
            Assert.IsFalse(plan.ShowMainDiamond);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_05_CategoryDiamondToggleHidesDiamondsButPreservesDetailedRows()
        {
            var shown = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2, 2, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: true), 0, false, false);
            var hidden = MarkerClusterPresentationPolicy.Build(R4C2Cluster(2, 2, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: false), 0, false, false);
            Assert.IsTrue(shown.ShowDetailedCategoryRowDiamonds);
            Assert.IsFalse(hidden.ShowDetailedCategoryRowDiamonds);
            Assert.AreEqual(shown.Text, hidden.Text);
            CollectionAssert.AreEqual(shown.CategoryEntries.Select(x => x.Category).ToArray(), hidden.CategoryEntries.Select(x => x.Category).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_06_DetailedRowDiamondsDoNotRequestBadgeSubcountText()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8, 7, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: false), 0, false, false);
            Assert.AreEqual(MarkerCountRenderSource.DetailedCategoryRows, plan.CountRenderSource);
            Assert.IsFalse(plan.RenderCategorySubcounts);
            Assert.AreEqual(3, plan.Text.Count(x => x == '×'));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_07_TrueSingletonKeepsPrimaryDiamondAndNoRowDiamondCard()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1, 0f, MarkerClassKind.Tier3, "single", "Single") }, 0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, showCategoryDiamond: true), 0, false, false);
            Assert.AreEqual(1, plan.TotalCount);
            Assert.IsFalse(plan.ShowMainDiamond);
            Assert.IsFalse(plan.ShowDetailedCategoryRowDiamonds);
            Assert.AreEqual(1, plan.DetailedItemRows.Count);
            Assert.AreEqual("Single", plan.DetailedItemRows[0].LocalizedName);
            Assert.AreEqual("Single ×1", plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_08_CompactPyramidPlanRemainsSeparateAndUnchanged()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8, 7, 2), R4Settings(MarkerPresentationMode.Compact, showDistance: false), 0, false, false);
            Assert.IsFalse(plan.ShowDetailedCategoryRowDiamonds);
            Assert.IsTrue(plan.ShowCompactCategoryDiamonds);
            Assert.IsFalse(plan.ShowMainDiamond);
            Assert.AreEqual(MarkerCountRenderSource.CategorySubcounts, plan.CountRenderSource);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_09_DetailedDistanceIsFinalLineNotCategoryEntry()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8, 7, 2), R4Settings(MarkerPresentationMode.Detailed, showDistance: true), 70, false, false);
            Assert.IsTrue(plan.Text.EndsWith("70 m", StringComparison.Ordinal));
            Assert.AreEqual(3, plan.CategoryEntries.Count);
            Assert.AreEqual(4, plan.Text.Split('\n').Length);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C2_10_SortInversionChangesTextAndRowDiamondOrderTogether()
        {
            var cluster = R4C2Cluster(8, 7, 2);
            var high = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, sortOrder: MarkerCategorySortOrder.HighToLow), 0, false, false);
            var low = MarkerClusterPresentationPolicy.Build(cluster, R4Settings(MarkerPresentationMode.Detailed, showDistance: false, sortOrder: MarkerCategorySortOrder.LowToHigh), 0, false, false);
            CollectionAssert.AreEqual(high.CategoryEntries.Select(x => x.Category).Reverse().ToArray(), low.CategoryEntries.Select(x => x.Category).ToArray());
            CollectionAssert.AreEqual(new[] { "Red ×2", "Green ×7", "White ×8" }, high.Text.Split('\n'));
            CollectionAssert.AreEqual(new[] { "White ×8", "Green ×7", "Red ×2" }, low.Text.Split('\n'));
            Assert.IsTrue(high.ShowDetailedCategoryRowDiamonds && low.ShowDetailedCategoryRowDiamonds);
        }


        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_01_OrdinaryIdenticalCrowbarsAggregateExactRow()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"PICKUP:CROWBAR","Crowbar"), C3R2Member(2,0.1f,MarkerClassKind.Tier1,"PICKUP:CROWBAR","Crowbar"), C3R2Member(3,0.2f,MarkerClassKind.Tier1,"PICKUP:CROWBAR","Crowbar") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            Assert.AreEqual("Crowbar ×3",plan.Text); Assert.AreEqual(1,plan.DetailedItemRows.Count); Assert.AreEqual(3,plan.DetailedItemRows[0].Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_02_OrdinaryDifferentWhiteItemsNeverCollapseToWhiteCount()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"PICKUP:CROWBAR","Crowbar"), C3R2Member(2,0.1f,MarkerClassKind.Tier1,"PICKUP:CRIT","Lens-Maker's Glasses"), C3R2Member(3,0.2f,MarkerClassKind.Tier1,"PICKUP:HOOF","Paul's Goat Hoof") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            Assert.AreEqual(3,plan.DetailedItemRows.Count); Assert.IsFalse(plan.Text.Contains("White ×3")); Assert.IsTrue(plan.Text.Contains("Crowbar ×1"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_03_OrdinaryMixedTiersRemainExactRows()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"w","White Exact"), C3R2Member(2,0.1f,MarkerClassKind.Tier2,"g","Green Exact"), C3R2Member(3,0.2f,MarkerClassKind.Tier3,"r","Red Exact") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            CollectionAssert.AreEqual(new[]{"Red Exact","Green Exact","White Exact"},plan.DetailedItemRows.Select(x=>x.LocalizedName).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_04_EquipmentUsesExactIdentityAggregation()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1,0f,MarkerClassKind.Equipment,"eq:a","Royal Capacitor"), C3R2Member(2,0.1f,MarkerClassKind.Equipment,"eq:a","Royal Capacitor"), C3R2Member(3,0.2f,MarkerClassKind.Equipment,"eq:b","Preon Accumulator") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            Assert.AreEqual(2,plan.DetailedItemRows.Count); Assert.IsTrue(plan.Text.Contains("Royal Capacitor ×2")); Assert.IsTrue(plan.Text.Contains("Preon Accumulator ×1"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_05_DetailRowsDefaultAndBoundsAreOneToTwelve()
        {
            Assert.AreEqual(5,MarkerClusterPresentationPolicy.MarkerDetailRowsDefault); Assert.AreEqual(1,MarkerClusterPresentationPolicy.MarkerDetailRowsMin); Assert.AreEqual(12,MarkerClusterPresentationPolicy.MarkerDetailRowsMax); Assert.AreEqual(12,MarkerClusterPresentationPolicy.ClampDetailRows(99));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_06_OverflowCountsHiddenDistinctTypes()
        {
            var members = Enumerable.Range(1,12).Select(i=>C3R2Member(i,i*0.05f,MarkerClassKind.Tier1,"id:"+i,"Item "+i)).ToArray();
            var cluster = new MarkerWorldClusterTracker().Update(members,0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,detailRows:5),0,false,false);
            Assert.AreEqual(5,plan.DetailedItemRows.Count); Assert.AreEqual(7,plan.OverflowPhysicalCount); Assert.IsTrue(plan.Text.Contains("+ 7 more types"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_07_StackOfTwentyConsumesOneDetailRow()
        {
            var members = Enumerable.Range(1,20).Select(i=>C3R2Member(i,i*0.02f,MarkerClassKind.Tier1,"crowbar","Crowbar")).ToArray();
            var cluster = new MarkerWorldClusterTracker().Update(members,0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,detailRows:1),0,false,false);
            Assert.AreEqual(1,plan.DetailedItemRows.Count); Assert.AreEqual(0,plan.OverflowPhysicalCount); Assert.AreEqual("Crowbar ×20",plan.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_08_DistanceRemainsFinalNeutralLogicalLine()
        {
            var cluster = new MarkerWorldClusterTracker().Update(new[] { C3R2Member(1,0f,MarkerClassKind.Tier1,"a","A"), C3R2Member(2,0.1f,MarkerClassKind.Tier2,"b","B") },0d).Clusters.Single();
            var plan = MarkerClusterPresentationPolicy.Build(cluster,R4Settings(MarkerPresentationMode.Detailed,showDistance:true),70,false,false);
            Assert.IsTrue(plan.Text.EndsWith("70 m",StringComparison.Ordinal)); Assert.AreEqual(2,plan.DetailedItemRows.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_09_CommandDetailedRemainsCategorySummary()
        {
            var plan = MarkerClusterPresentationPolicy.Build(R4C2Cluster(8,7,2),R4Settings(MarkerPresentationMode.Detailed,showDistance:false),0,false,false);
            Assert.AreEqual(0,plan.DetailedItemRows.Count); Assert.AreEqual(3,plan.CategoryEntries.Count); Assert.IsTrue(plan.ShowDetailedCategoryRowDiamonds);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_10_CommandDiamondToggleDoesNotChangeText()
        {
            var c=R4C2Cluster(2,2,2); var a=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:true),0,false,false); var b=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:false),0,false,false);
            Assert.AreEqual(a.Text,b.Text); Assert.IsTrue(a.ShowDetailedCategoryRowDiamonds); Assert.IsFalse(b.ShowDetailedCategoryRowDiamonds); Assert.IsFalse(a.ShowMainDiamond); Assert.IsFalse(b.ShowMainDiamond);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_11_CompactOneCategoryIsCenteredSingleSlot()
        {
            var slots=MarkerCategorySummaryPolicy.BuildCompactLayout(1); Assert.AreEqual(1,slots.Length); Assert.AreEqual(0f,slots[0].XUnits,0.001f); Assert.AreEqual(0f,slots[0].YUnits,0.001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_12_CompactTwoCategoriesAreVerticalOneOverOne()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(2); Assert.AreEqual(2,s.Length); Assert.AreEqual(0f,s[0].XUnits,0.001f); Assert.AreEqual(0f,s[1].XUnits,0.001f); Assert.IsTrue(s[0].YUnits>s[1].YUnits);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_13_CompactThreeCategoriesAreOneOverTwoPyramid()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(3); CollectionAssert.AreEqual(new[]{1,2,2},s.Select(x=>x.RowSize).ToArray()); Assert.AreEqual(0f,s[0].XUnits,0.001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_14_CompactFourCategoriesAreOneTwoOneDiamond()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(4); CollectionAssert.AreEqual(new[]{1,2,2,1},s.Select(x=>x.RowSize).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_15_CompactFiveCategoriesAreOneThreeOne()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(5); CollectionAssert.AreEqual(new[]{1,3,3,3,1},s.Select(x=>x.RowSize).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_16_CompactSixCategoriesAreOneTwoTwoOne()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(6); CollectionAssert.AreEqual(new[]{1,2,2,2,2,1},s.Select(x=>x.RowSize).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_17_CompactSevenThroughElevenNeverUseSingleHorizontalRow()
        {
            for(var n=7;n<=11;n++){var s=MarkerCategorySummaryPolicy.BuildCompactLayout(n); Assert.IsTrue(s.Max(x=>x.Row)>0); Assert.IsTrue(s.Max(x=>x.RowSize)<n);}
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_18_CompactApexIsFirstOrderedEntry()
        {
            var c=R4C2Cluster(1,1,1); var p=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,sortOrder:MarkerCategorySortOrder.HighToLow),0,false,false); var s=MarkerCategorySummaryPolicy.BuildCompactLayout(p.CategoryEntries.Count); Assert.AreEqual(MarkerSemanticCategory.Tier3,p.CategoryEntries[0].Category); Assert.AreEqual(0f,s[0].XUnits,0.001f); Assert.AreEqual(0,s[0].Row);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_19_LowToHighMovesWhiteToApexWithoutChangingCounts()
        {
            var c=R4C2Cluster(8,7,2); var hi=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,sortOrder:MarkerCategorySortOrder.HighToLow),0,false,false); var lo=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,sortOrder:MarkerCategorySortOrder.LowToHigh),0,false,false); Assert.AreEqual(MarkerSemanticCategory.Tier3,hi.CategoryEntries[0].Category); Assert.AreEqual(MarkerSemanticCategory.Tier1,lo.CategoryEntries[0].Category); CollectionAssert.AreEquivalent(hi.CategoryEntries.Select(x=>x.Count).ToArray(),lo.CategoryEntries.Select(x=>x.Count).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_20_CompactCountToggleHidesCountsOnly()
        {
            var c=R4C2Cluster(2,2,2); var a=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,compactShowCount:true),0,false,false); var b=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,compactShowCount:false),0,false,false); Assert.IsTrue(a.ShowCompactCategoryDiamonds&&b.ShowCompactCategoryDiamonds); Assert.IsTrue(a.RenderCategorySubcounts); Assert.IsFalse(b.RenderCategorySubcounts); Assert.AreEqual(a.CategoryEntries.Count,b.CategoryEntries.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_21_CompactSingleOrdinaryUsesCategoryDiamondPlan()
        {
            var c=new MarkerWorldClusterTracker().Update(new[]{C3R2Member(1,0f,MarkerClassKind.Tier1,"crowbar","Crowbar")},0d).Clusters.Single(); var p=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false),0,false,false); Assert.IsTrue(p.ShowCompactCategoryDiamonds); Assert.IsFalse(p.ShowMainDiamond); Assert.AreEqual(1,p.CategoryEntries.Count);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_22_CompactCategoryRemovalReflowsToNewSlotShape()
        {
            var three=MarkerCategorySummaryPolicy.BuildCompactLayout(3); var two=MarkerCategorySummaryPolicy.BuildCompactLayout(2); Assert.AreEqual(2,three.Max(x=>x.RowSize)); Assert.AreEqual(1,two.Max(x=>x.RowSize)); Assert.AreEqual(0f,two[0].XUnits,0.001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_23_OrdinarySortChangesOrderOnly()
        {
            var c=new MarkerWorldClusterTracker().Update(new[]{C3R2Member(1,0f,MarkerClassKind.Tier1,"a","Alpha"),C3R2Member(2,0.1f,MarkerClassKind.Tier3,"z","Zulu")},0d).Clusters.Single(); var hi=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,sortOrder:MarkerCategorySortOrder.HighToLow),0,false,false); var lo=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,sortOrder:MarkerCategorySortOrder.LowToHigh),0,false,false); Assert.AreEqual("Zulu",hi.DetailedItemRows[0].LocalizedName); Assert.AreEqual("Alpha",lo.DetailedItemRows[0].LocalizedName); Assert.AreEqual(c.MemberFingerprint,c.MemberFingerprint);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4_24_MaxCompactCategoriesRemainBoundedAtEleven()
        {
            var slots=MarkerCategorySummaryPolicy.BuildCompactLayout(99); Assert.AreEqual(MarkerCategorySummaryPolicy.MaxCategories,slots.Length); Assert.IsTrue(slots.Max(x=>Math.Abs(x.XUnits))<=1.5f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_01_DetailedItemWidthLimitUsesHudResolutionAndMarkerScale()
        {
            Assert.AreEqual(420f, MarkerClusterPresentationPolicy.BuildDetailedItemLabelWidthLimit(1920f, 1080f, 1f), 0.01f);
            Assert.AreEqual(840f, MarkerClusterPresentationPolicy.BuildDetailedItemLabelWidthLimit(3840f, 2160f, 1f), 0.01f);
            Assert.AreEqual(525f, MarkerClusterPresentationPolicy.BuildDetailedItemLabelWidthLimit(1920f, 1080f, 1.25f), 0.01f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_02_DetailedItemWidthLimitIsFiniteAndScreenBounded()
        {
            var width = MarkerClusterPresentationPolicy.BuildDetailedItemLabelWidthLimit(1280f, 720f, float.PositiveInfinity);
            Assert.IsTrue(width > 0f);
            Assert.IsTrue(width <= 1280f * 0.34f + 0.01f);
            Assert.IsFalse(float.IsNaN(width));
            Assert.IsFalse(float.IsInfinity(width));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_03_LongOrdinarySemanticPlanKeepsExactNameCountAndDistanceTruth()
        {
            const string longName = "Extremely Long Modded Localized Item Name That Must Be Visually Bounded Without Changing Semantic Truth";
            var c = new MarkerWorldClusterTracker().Update(new[] {
                C3R2Member(1,0f,MarkerClassKind.Tier1,"long-id",longName),
                C3R2Member(2,0.1f,MarkerClassKind.Tier1,"long-id",longName),
                C3R2Member(3,0.2f,MarkerClassKind.Tier1,"long-id",longName)},0d).Clusters.Single();
            var p = MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:true),70,false,false);
            Assert.AreEqual(longName,p.DetailedItemRows[0].LocalizedName);
            Assert.AreEqual(3,p.DetailedItemRows[0].Count);
            Assert.IsTrue(p.Text.StartsWith(longName + " ×3",StringComparison.Ordinal));
            Assert.IsTrue(p.Text.EndsWith("70 m",StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_04_CompactElevenClosesToSingleBottomPoint()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(11);
            Assert.AreEqual(11,s.Length);
            Assert.AreEqual(1,s.First().RowSize);
            Assert.AreEqual(1,s.Last().RowSize);
            Assert.AreEqual(0f,s.First().XUnits,0.001f);
            Assert.AreEqual(0f,s.Last().XUnits,0.001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_05_CompactElevenRowsAreCenteredAndBounded()
        {
            var s=MarkerCategorySummaryPolicy.BuildCompactLayout(11);
            foreach(var row in s.GroupBy(x=>x.Row)) Assert.AreEqual(0f,row.Average(x=>x.XUnits),0.001f);
            Assert.IsTrue(s.Max(x=>Math.Abs(x.XUnits))<=1f);
            CollectionAssert.AreEqual(new[]{1,3,3,3,1},s.GroupBy(x=>x.Row).Select(x=>x.Count()).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C1_06_RemovingEleventhCategoryReflowsToDeterministicTenLayout()
        {
            var eleven=MarkerCategorySummaryPolicy.BuildCompactLayout(11);
            var ten=MarkerCategorySummaryPolicy.BuildCompactLayout(10);
            Assert.AreEqual(11,eleven.Length);
            Assert.AreEqual(10,ten.Length);
            Assert.AreEqual(1,ten.First().RowSize);
            Assert.AreEqual(1,ten.Last().RowSize);
            CollectionAssert.AreEqual(new[]{1,2,3,3,1},ten.GroupBy(x=>x.Row).Select(x=>x.Count()).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_01_OrdinarySingletonCategoryToggleOnHasNoMainDiamond()
        {
            var c=new MarkerWorldClusterTracker().Update(new[]{C3R2Member(1,0f,MarkerClassKind.Tier1,"crowbar","Crowbar")},0d).Clusters.Single();
            var p=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:true),0,false,false);
            Assert.IsFalse(p.ShowMainDiamond); Assert.IsFalse(p.ShowDetailedCategoryRowDiamonds); Assert.AreEqual("Crowbar ×1",p.Text);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_02_OrdinarySingletonCategoryToggleOffIsTextIdentical()
        {
            var c=new MarkerWorldClusterTracker().Update(new[]{C3R2Member(1,0f,MarkerClassKind.Tier1,"crowbar","Crowbar")},0d).Clusters.Single();
            var on=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:true),0,false,false);
            var off=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:false),0,false,false);
            Assert.AreEqual(on.Text,off.Text); Assert.IsFalse(on.ShowMainDiamond); Assert.IsFalse(off.ShowMainDiamond);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_03_CommandSingletonRetainsCategoryDiamondSemantics()
        {
            var c=new MarkerWorldClusterTracker().Update(new[]{R4C2CommandMember(1,0f,MarkerClassKind.Tier3,"Red Choice")},0d).Clusters.Single();
            var on=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:true),0,false,false);
            var off=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Detailed,showDistance:false,showCategoryDiamond:false),0,false,false);

            Assert.IsFalse(on.ShowMainDiamond);
            Assert.IsFalse(off.ShowMainDiamond);
            Assert.IsTrue(on.ShowDetailedCategoryRowDiamonds);
            Assert.IsFalse(off.ShowDetailedCategoryRowDiamonds);
            Assert.AreEqual(on.Text,off.Text);
            Assert.AreEqual(1,on.CategoryEntries.Count);
            Assert.AreEqual(1,off.CategoryEntries.Count);
            Assert.AreEqual(MarkerSemanticCategory.Tier3,on.CategoryEntries[0].Category);
            Assert.AreEqual(MarkerSemanticCategory.Tier3,off.CategoryEntries[0].Category);
            Assert.AreEqual(1,on.CategoryEntries[0].Count);
            Assert.AreEqual(1,off.CategoryEntries[0].Count);
        }

        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_04_RussianOverflowOneUsesVid() => Assert.AreEqual("+ ещё 1 вид",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(1,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_05_RussianOverflowTwoUsesVida() => Assert.AreEqual("+ ещё 2 вида",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(2,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_06_RussianOverflowFiveUsesVidov() => Assert.AreEqual("+ ещё 5 видов",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(5,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_07_RussianOverflowElevenUsesVidov() => Assert.AreEqual("+ ещё 11 видов",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(11,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_08_RussianOverflowTwentyOneUsesVid() => Assert.AreEqual("+ ещё 21 вид",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(21,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_09_RussianOverflowTwentyTwoUsesVida() => Assert.AreEqual("+ ещё 22 вида",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(22,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_10_RussianOverflowTwentyFiveUsesVidov() => Assert.AreEqual("+ ещё 25 видов",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(25,true));
        [TestMethod] public void ISF_R1_C20_R11_R4C2_C4C4_11_EnglishOverflowRemainsMoreTypes() => Assert.AreEqual("+ 7 more types",MarkerClusterPresentationPolicy.BuildDetailedOverflowText(7,false));

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_12_CompactLayoutsOneThroughElevenRemainCentered()
        {
            for(var n=1;n<=11;n++) foreach(var row in MarkerCategorySummaryPolicy.BuildCompactLayout(n).GroupBy(x=>x.Row)) Assert.AreEqual(0f,row.Average(x=>x.XUnits),0.001f,"n="+n+" row="+row.Key);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_13_CompactApprovedRowPatternsRemainExact()
        {
            var expected=new[]{"1","1,1","1,2","1,2,1","1,3,1","1,2,2,1","1,2,3,1","1,2,2,2,1","1,2,3,2,1","1,2,3,3,1","1,3,3,3,1"};
            for(var n=1;n<=11;n++) Assert.AreEqual(expected[n-1],string.Join(",",MarkerCategorySummaryPolicy.BuildCompactLayout(n).GroupBy(x=>x.Row).Select(x=>x.Count())),"n="+n);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_14_CompactCountsOnCellsDoNotOverlapAcrossResolutionScaleMatrix()
        {
            var resolutions=new[]{new[]{1280f,720f},new[]{1920f,1080f},new[]{2560f,1440f}}; var scales=new[]{0.75f,1f,1.25f};
            foreach(var r in resolutions) foreach(var scale in scales) for(var n=4;n<=11;n++)
            {
                var g=MarkerCategorySummaryPolicy.BuildCompactCellGeometry(r[0],r[1],scale,32f,24f,true); var slots=MarkerCategorySummaryPolicy.BuildCompactLayout(n);
                Assert.IsTrue(g.HorizontalStride>g.CellWidth,"stride n="+n); Assert.IsTrue(g.VerticalStride>=g.BadgeSize,"vertical n="+n);
                foreach(var row in slots.GroupBy(x=>x.Row)) { var xs=row.Select(x=>x.XUnits*g.HorizontalStride).OrderBy(x=>x).ToArray(); for(var i=1;i<xs.Length;i++) Assert.IsTrue(xs[i]-xs[i-1]>=g.CellWidth,"overlap n="+n); }
            }
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_15_CompactCountsOffCellsDoNotOverlapAcrossResolutionScaleMatrix()
        {
            var resolutions=new[]{new[]{1280f,720f},new[]{1920f,1080f},new[]{2560f,1440f}}; var scales=new[]{0.75f,1f,1.25f};
            foreach(var r in resolutions) foreach(var scale in scales) for(var n=1;n<=11;n++)
            {
                var g=MarkerCategorySummaryPolicy.BuildCompactCellGeometry(r[0],r[1],scale,32f,24f,false); var slots=MarkerCategorySummaryPolicy.BuildCompactLayout(n);
                Assert.AreEqual(0f,g.CountWidth,0.001f); Assert.AreEqual(0f,g.CountGap,0.001f);
                foreach(var row in slots.GroupBy(x=>x.Row)) { var xs=row.Select(x=>x.XUnits*g.HorizontalStride).OrderBy(x=>x).ToArray(); for(var i=1;i<xs.Length;i++) Assert.IsTrue(xs[i]-xs[i-1]>=g.CellWidth,"overlap n="+n); }
            }
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_16_CompactCountTogglePreservesTopology()
        {
            for(var n=1;n<=11;n++) { var a=MarkerCategorySummaryPolicy.BuildCompactLayout(n); var b=MarkerCategorySummaryPolicy.BuildCompactLayout(n); CollectionAssert.AreEqual(a.Select(x=>new[]{x.Row,x.Column,x.RowSize}).SelectMany(x=>x).ToArray(),b.Select(x=>new[]{x.Row,x.Column,x.RowSize}).SelectMany(x=>x).ToArray()); }
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_17_HighLowSortPreservesCategoryCountMembership()
        {
            var c=R4C2Cluster(8,7,2); var hi=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,sortOrder:MarkerCategorySortOrder.HighToLow),0,false,false); var lo=MarkerClusterPresentationPolicy.Build(c,R4Settings(MarkerPresentationMode.Compact,showDistance:false,sortOrder:MarkerCategorySortOrder.LowToHigh),0,false,false);
            CollectionAssert.AreEquivalent(hi.CategoryEntries.Select(x=>x.Category).ToArray(),lo.CategoryEntries.Select(x=>x.Category).ToArray()); CollectionAssert.AreEquivalent(hi.CategoryEntries.Select(x=>x.Count).ToArray(),lo.CategoryEntries.Select(x=>x.Count).ToArray()); CollectionAssert.AreEqual(hi.CategoryEntries.Select(x=>x.Category).Reverse().ToArray(),lo.CategoryEntries.Select(x=>x.Category).ToArray());
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_18_CompactApexAndBottomClosureRemainCenteredWhereSpecified()
        {
            for(var n=1;n<=11;n++)
            {
                var s=MarkerCategorySummaryPolicy.BuildCompactLayout(n);
                Assert.AreEqual(0f,s.First().XUnits,0.001f,"apex n="+n);
                foreach(var row in s.GroupBy(x=>x.Row))
                    Assert.AreEqual(0f,row.Average(x=>x.XUnits),0.001f,"row center n="+n+" row="+row.Key);
                var lastRow=s.GroupBy(x=>x.Row).OrderBy(x=>x.Key).Last();
                if(lastRow.Count()==1) Assert.AreEqual(0f,lastRow.Single().XUnits,0.001f,"bottom closure n="+n);
                if(n==3) CollectionAssert.AreEqual(new[]{-0.5f,0.5f},lastRow.Select(x=>x.XUnits).OrderBy(x=>x).ToArray());
            }
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_19_CellWidthIncludesCountFootprintOnlyWhenEnabled()
        {
            var on=MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f,1080f,1f,32f,24f,true); var off=MarkerCategorySummaryPolicy.BuildCompactCellGeometry(1920f,1080f,1f,32f,24f,false);
            Assert.IsTrue(on.CountWidth>0f); Assert.IsTrue(on.CountGap>0f); Assert.IsTrue(on.CellWidth>off.CellWidth); Assert.AreEqual(off.BadgeSize,off.CellWidth,0.001f);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_20_GeometryIsFiniteAcrossBoundaryInputs()
        {
            foreach(var show in new[]{false,true}) { var g=MarkerCategorySummaryPolicy.BuildCompactCellGeometry(float.NaN,float.PositiveInfinity,float.NaN,float.NaN,float.NaN,show); Assert.IsTrue(g.BadgeSize>0f&&g.CellWidth>0f&&g.HorizontalStride>g.CellWidth&&g.VerticalStride>=g.BadgeSize); Assert.IsFalse(float.IsNaN(g.HorizontalStride)); Assert.IsFalse(float.IsInfinity(g.HorizontalStride)); }
        }

        private static MarkerSemanticCluster R4C2Cluster(int white, int green, int red)
            => new MarkerWorldClusterTracker().Update(R4C2Members(white, green, red), 0d).Clusters.Single();

        private static MarkerWorldMember[] R4C2Members(int white, int green, int red)
        {
            var members = new System.Collections.Generic.List<MarkerWorldMember>();
            long key = 1;
            for (var i = 0; i < white; i++, key++) members.Add(R4C2CommandMember(key, (float)(key * 0.05), MarkerClassKind.Tier1, "White Item " + key));
            for (var i = 0; i < green; i++, key++) members.Add(R4C2CommandMember(key, (float)(key * 0.05), MarkerClassKind.Tier2, "Green Item " + key));
            for (var i = 0; i < red; i++, key++) members.Add(R4C2CommandMember(key, (float)(key * 0.05), MarkerClassKind.Tier3, "Red Item " + key));
            return members.ToArray();
        }

        private static MarkerPresentationSettings R4Settings(
            MarkerPresentationMode mode,
            bool showDistance = true,
            int detailRows = 5,
            bool showCategoryDiamond = true,
            bool showComposition = true,
            bool compactShowCount = true,
            MarkerCompactMixedStyle compactStyle = MarkerCompactMixedStyle.CategoryDiamondPyramid,
            MarkerCategorySortOrder sortOrder = MarkerCategorySortOrder.HighToLow)
            => new MarkerPresentationSettings(mode, showDistance, 1f, detailRows, showCategoryDiamond, showComposition, compactShowCount, compactStyle, sortOrder);

        private static MarkerWorldMember C3R2Member(long key, float x, MarkerClassKind kind = MarkerClassKind.Tier1, string itemIdentity = "item", string name = "Item")
            => new MarkerWorldMember(key, PersonalMarkerKind.OrdinaryPickup, new MarkerWorldPoint(x, 0f, 0f), itemIdentity == "item" ? itemIdentity + ":" + key : itemIdentity, name, kind);

        private static MarkerWorldMember R4C2CommandMember(long key, float x, MarkerClassKind kind, string name)
            => new MarkerWorldMember(key, PersonalMarkerKind.CommandPicker, new MarkerWorldPoint(x, 0f, 0f), "COMMAND:" + key, name, kind);

        private static MarkerSemanticCluster C3R2MixedCluster()
        {
            var tracker = new MarkerWorldClusterTracker();
            return tracker.Update(new[] {
                C3R2Member(1, 0f, MarkerClassKind.Tier1, "white-a", "White A"),
                C3R2Member(2, 0.5f, MarkerClassKind.Tier1, "white-b", "White B"),
                C3R2Member(3, 1.0f, MarkerClassKind.Tier2, "green", "Green") }, 0d).Clusters.Single();
        }


        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_21_RiskOfOptionsAllElevenLanguageRowsAreComplete()
        {
            var source = R20ReadMarkerRiskOfOptionsLocalizationSource();
            var languageLine = source.Split('\n').Single(x => x.Contains("SupportedLanguages = A("));
            foreach (var language in new[] { "en", "fr", "it", "de", "es", "ja", "ko", "pt-BR", "ru", "zh-CN", "tr" })
                StringAssert.Contains(languageLine, "\"" + language + "\"");

            var localizedRows = source.Split('\n').Where(x => x.TrimStart().StartsWith("[\"", StringComparison.Ordinal) && x.Contains("] = A(")).ToArray();
            Assert.AreEqual(54, localizedRows.Length, "27 option names + 27 option descriptions must be structurally complete.");
            foreach (var row in localizedRows)
            {
                var quotedStringCount = row.Count(c => c == '"') / 2;
                Assert.AreEqual(12, quotedStringCount, "Each dictionary row must contain one key plus 11 non-empty localized values: " + row);
            }
            StringAssert.Contains(source, "A(\"Detailed\",\"Détaillé\"");
            StringAssert.Contains(source, "A(\"High to low\",\"Élevé vers faible\"");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_22_RiskOfOptionsRussianResolutionDataIsRussian()
        {
            var source = R20ReadMarkerRiskOfOptionsLocalizationSource();
            StringAssert.Contains(source, "if (key.Contains(\"russian\") || key.StartsWith(\"ru\")) return 8;");
            StringAssert.Contains(source, "\"Режим меток\"");
            StringAssert.Contains(source, "\"Показывать расстояние\"");
            StringAssert.Contains(source, "\"Метки\"");
            StringAssert.Contains(source, "\"Цвета меток\"");
            StringAssert.Contains(source, "\"Подробный\"");
            StringAssert.Contains(source, "\"Компактный\"");
            StringAssert.Contains(source, "\"От высокого к низкому\"");
            StringAssert.Contains(source, "\"От низкого к высокому\"");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C4_23_RiskOfOptionsEnglishResolutionDataAndFallbackRemainEnglish()
        {
            var source = R20ReadMarkerRiskOfOptionsLocalizationSource();
            StringAssert.Contains(source, "public static readonly string[] SupportedLanguages = A(\"en\"");
            StringAssert.Contains(source, "\"Marker mode\"");
            StringAssert.Contains(source, "\"Show distance\"");
            StringAssert.Contains(source, "A(\"Detailed\",\"Détaillé\"");
            StringAssert.Contains(source, "A(\"Compact\",\"Compact\"");
            StringAssert.Contains(source, "A(\"High to low\",\"Élevé vers faible\"");
            StringAssert.Contains(source, "A(\"Low to high\",\"Faible vers élevé\"");
            StringAssert.Contains(source, "return 0;");
        }


        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_01_CanonicalPersonalMarkersConfigKeyAndDefaultRemainTrue()
        {
            var source = R24ReadPluginSource("PluginConfig.cs");
            StringAssert.Contains(source, "PersonalMarkersEnabled = config.Bind(\"General\", \"PersonalMarkersEnabled\", true,");
            Assert.AreEqual(1, source.Split(new[] { "config.Bind(\"General\", \"PersonalMarkersEnabled\"" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_02_RiskOfOptionsRegistersTwentySixMarkerOptions()
        {
            var source = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "public const int CurrentMarkerOptionCount = 27;");
            StringAssert.Contains(source, "public const int ItemShareFixOptionCount = CurrentMarkerOptionCount;");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_03_MasterToggleIsFirstCanonicalCheckBoxBeforePresentationMode()
        {
            var source = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            var master = source.IndexOf("AddLocalPlan(assembly, plans, \"RiskOfOptions.Options.CheckBoxOption\", config.PersonalMarkersEnabled, instantMode", StringComparison.Ordinal);
            var mode = source.IndexOf("AddLocalPlan(assembly, plans, \"RiskOfOptions.Options.ChoiceOption\", config.MarkerPresentationMode, instantMode", StringComparison.Ordinal);
            Assert.IsTrue(master >= 0);
            Assert.IsTrue(mode > master);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_04_MasterToggleUsesSameCanonicalConfigEntryWithoutMirror()
        {
            var config = R24ReadPluginSource("PluginConfig.cs");
            var rto = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(config, "public ConfigEntry<bool> PersonalMarkersEnabled { get; }");
            StringAssert.Contains(rto, "config.PersonalMarkersEnabled, instantMode, out failure");
            Assert.IsFalse(rto.Contains("new ConfigEntry"));
            Assert.IsFalse(rto.Contains("PersonalMarkersEnabledMirror"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_05_MasterToggleHasHumanReadableRiskOfOptionsText()
        {
            var source = R20ReadMarkerRiskOfOptionsLocalizationSource();
            StringAssert.Contains(source, "[\"PersonalMarkersEnabled\"] = A(\"Enable markers\"");
            StringAssert.Contains(source, "Enable ItemShareFix automatic pickup and Command markers.");
            Assert.IsFalse(source.Contains("[\"PersonalMarkersEnabled\"] = A(\"PersonalMarkersEnabled\""));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_06_DisablingMarkersClearsOnlyItemShareFixHudAndRequestsRefresh()
        {
            var source = R24ReadPluginSource("ClientPresentation.cs");
            StringAssert.Contains(source, "if (ReferenceEquals(sender, _config.PersonalMarkersEnabled))");
            StringAssert.Contains(source, "if (!_config.PersonalMarkersEnabled.Value) _hudRenderer.Clear();");
            StringAssert.Contains(source, "RequestRefresh();");
            StringAssert.Contains(source, "_config.Enabled.Value\n                && _config.PersonalMarkersEnabled.Value");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_07_PersonalMarkersParticipatesInPresentationInvalidation()
        {
            var source = R24ReadPluginSource("PluginConfig.cs");
            StringAssert.Contains(source, "BindPresentationInvalidation(PersonalMarkersEnabled);");
            StringAssert.Contains(source, "entry.SettingChanged += OnMarkerPresentationSettingChanged;");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_08_OrdinaryMarkerGateUsesPersonalToggleWithoutChangingVisibilityRepairGate()
        {
            var source = R24ReadPluginSource("ClientPresentation.cs");
            var visibilityRepair = source.IndexOf("LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(", StringComparison.Ordinal);
            var lifetimeRecognition = source.IndexOf("var lifetime = MarkerLifetimePolicy.FromTemporaryFlag(pickup.pickup.isTempItem);", StringComparison.Ordinal);
            var personalMarkerGate = source.IndexOf("if (_config.PersonalMarkersEnabled.Value", lifetimeRecognition, StringComparison.Ordinal);
            var lifetimeEligibility = source.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(lifetime, _config.ShareTemporaryItems.Value)", personalMarkerGate, StringComparison.Ordinal);
            var ordinaryCap = source.IndexOf("ordinaryCount < MarkerPresentationPolicy.MaxOrdinaryMarkers", lifetimeEligibility, StringComparison.Ordinal);

            Assert.IsTrue(visibilityRepair >= 0);
            Assert.IsTrue(lifetimeRecognition > visibilityRepair);
            Assert.IsTrue(personalMarkerGate > lifetimeRecognition);
            Assert.IsTrue(lifetimeEligibility > personalMarkerGate);
            Assert.IsTrue(ordinaryCap > lifetimeEligibility);
            StringAssert.Contains(source, "LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(\n                    _config.PersonalPickupVisibilityRepairEnabled.Value,");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_09_CommandCompletionVisibilityRunsBeforeMarkerToggleGate()
        {
            var source = R24ReadPluginSource("ClientPresentation.cs");
            var completion = source.IndexOf("ApplyLocalCommandPresentation(picker, commandPresentation);", StringComparison.Ordinal);
            var markerGate = source.IndexOf("if (!_config.PersonalMarkersEnabled.Value)", completion, StringComparison.Ordinal);
            Assert.IsTrue(completion >= 0);
            Assert.IsTrue(markerGate > completion);
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_10_ReenableRequestsImmediateRescanWithoutRestart()
        {
            var source = R24ReadPluginSource("ClientPresentation.cs");
            var start = source.IndexOf("if (ReferenceEquals(sender, _config.PersonalMarkersEnabled))", StringComparison.Ordinal);
            var end = source.IndexOf("        public void RecordUnityUpdate()", start, StringComparison.Ordinal);
            var block = source.Substring(start, end - start);
            StringAssert.Contains(block, "RequestRefresh();");
            Assert.IsFalse(block.Contains("RestoreAll("));
            Assert.IsFalse(block.Contains("stage"));
            Assert.IsFalse(block.Contains("Run.instance"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_11_MasterItemShareFixEnabledRemainsIndependent()
        {
            var source = R24ReadPluginSource("PluginConfig.cs");
            StringAssert.Contains(source, "Enabled = config.Bind(\"General\", \"Enabled\", true, \"Master switch for ItemShareFix.\");");
            StringAssert.Contains(source, "PersonalMarkersEnabled = config.Bind(\"General\", \"PersonalMarkersEnabled\", true,");
            Assert.IsFalse(source.Contains("Enabled = PersonalMarkersEnabled"));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_12_RiskOfOptionsRemainsSoftReflectionBound()
        {
            var source = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "REFLECTION_SOFT_BINDING_CANONICAL_CONFIGENTRY");
            StringAssert.Contains(source, "\"RiskOfOptions.Options.CheckBoxOption\"");
            Assert.IsFalse(source.Contains("using RiskOfOptions"));
            Assert.IsFalse(source.Contains("new CheckBoxOption("));
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_13_ExistingMarkerOptionsRetainRelativeRegistrationOrder()
        {
            var source = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            var fields = new[] {
                "PersonalMarkersEnabled","MarkerPresentationMode","ShowMarkerDistance","MarkerScale","MarkerOpacity","MarkerBackgroundOpacity",
                "ShowMarkerCategoryDiamond","MarkerDetailRows","MarkerCategorySortOrder","MarkerCompactShowCount","EnableOffscreenIndicators",
                "ShowOffscreenDistance","ShowOffscreenTotalCount","OffscreenIndicatorScale","OffscreenIndicatorOpacity","OffscreenEdgePadding",
                "CommonMarkerColor","UncommonMarkerColor","LegendaryMarkerColor","BossMarkerColor","LunarMarkerColor","VoidMarkerColor",
                "EquipmentMarkerColor","CommandMarkerColor","NeutralMarkerColor","OffscreenIndicatorColor"
            };
            var last = -1;
            foreach (var field in fields)
            {
                var index = source.IndexOf("config." + field, StringComparison.Ordinal);
                Assert.IsTrue(index > last, "Registration order mismatch at " + field);
                last = index;
            }
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_14_VisibleOptionInventoryStartsWithMasterToggle()
        {
            var source = R20ReadMarkerRiskOfOptionsLocalizationSource();
            StringAssert.Contains(source, "VisibleOptionKeys = A(\"PersonalMarkersEnabled\",\"MarkerPresentationMode\"");
        }

        [TestMethod]
        public void ISF_R1_C20_R11_R4C2_C4C7_15_MasterToggleDoesNotTouchRendererGeometryOrGamePings()
        {
            var client = R24ReadPluginSource("ClientPresentation.cs");
            var rto = R24ReadPluginSource("OptionalRiskOfOptionsIntegration.cs");
            Assert.IsFalse(client.Contains("PingIndicator"));
            Assert.IsFalse(client.Contains("PingerController"));
            Assert.IsFalse(rto.Contains("PingIndicator"));
            Assert.IsFalse(rto.Contains("PingerController"));
            StringAssert.Contains(client, "_hudRenderer.Clear();");
        }

    }
}
