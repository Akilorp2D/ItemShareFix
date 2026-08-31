using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ItemShareFix.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemShareFix
{
    internal sealed class ServerCoordinator
    {
        private sealed class DistributionContext
        {
            public SharedPickupKey Pickup;
            public PickupDef? PickupDef;
            public object? BoxedUniquePickup;
            public CharacterMaster? Collector;
            public bool Instant;
        }

        private sealed class DeferredGrantPayload
        {
            public PickupDef PickupDef { get; set; } = null!;
            public object BoxedUniquePickup { get; set; } = null!;
        }

        private sealed class DisconnectCandidate
        {
            public float ObservedAt;
            public string DestroyedRuntimeType { get; set; } = string.Empty;
            public bool AuthoritativePresenceAtObservation;
        }

        private readonly PluginConfig _config;
        private readonly UpstreamBridge _upstream;
        private readonly ManualLogSource _log;
        private readonly ParticipantClassifier _classifier;
        private readonly ClaimLedger _ledger = new ClaimLedger();
        private readonly Dictionary<ParticipantKey, ParticipantSnapshot> _participants = new Dictionary<ParticipantKey, ParticipantSnapshot>();
        private readonly Dictionary<ParticipantKey, float> _missingSince = new Dictionary<ParticipantKey, float>();
        private readonly Dictionary<ClaimKey, DeferredGrantPayload> _deferredPayloads = new Dictionary<ClaimKey, DeferredGrantPayload>();
        private readonly Stack<DistributionContext> _distribution = new Stack<DistributionContext>();
        private readonly Dictionary<int, float> _pendingTransferRebroadcast = new Dictionary<int, float>();
        private readonly Dictionary<ClaimKey, float> _pendingHistoricalMirrorRetry = new Dictionary<ClaimKey, float>();
        private readonly Dictionary<uint, GenerationProbeGate> _generationProbes = new Dictionary<uint, GenerationProbeGate>();
        private readonly HashSet<string> _identityDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _claimEnsureDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ParticipantKey, DisconnectCandidate> _disconnectCandidates = new Dictionary<ParticipantKey, DisconnectCandidate>();
        private readonly HashSet<string> _disconnectGateDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _itemShareActiveGateDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _commandRetentionDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _temporaryPolicyDiagnosticLogged = new HashSet<string>(StringComparer.Ordinal);
        private float _nextParticipantSweep;
        private float _lastStageChangeTime;
        private Run? _runInstance;
        private int _stage;
        private int _deferredGrantDepth;

        public ServerCoordinator(PluginConfig config, UpstreamBridge upstream, ManualLogSource log)
        {
            _config = config;
            _upstream = upstream;
            _log = log;
            _classifier = new ParticipantClassifier();
            _runInstance = Run.instance;
            _stage = CurrentStageToken();
            _lastStageChangeTime = Time.unscaledTime;
        }

        public IReadOnlyCollection<ParticipantSnapshot> CurrentParticipants => _participants.Values;
        public ClaimLedger Ledger => _ledger;
        public bool InDistribution => _distribution.Count > 0;
        public bool InDeferredGrant => _deferredGrantDepth > 0;
        public bool ShareTemporaryItemsEnabled => _config.ShareTemporaryItems.Value;

        public void LogTemporaryPolicy(string kind, int instanceId, bool shareTemporaryItems, string action, string reason)
        {
            if (!_config.DiagnosticLogging.Value) return;
            var key = kind + "|" + instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|shareTemporary=" + shareTemporaryItems + "|action=" + action + "|reason=" + reason;
            if (!_temporaryPolicyDiagnosticLogged.Add(key)) return;
            _log.LogInfo("[ItemShareFix] ISF_C21_TEMP_POLICY"
                + " kind=" + kind
                + " temporary=true"
                + " shareTemporary=" + (shareTemporaryItems ? "true" : "false")
                + " action=" + action
                + " instance=" + instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " reason=" + reason);
        }

        public void Tick()
        {
            if (!_config.Enabled.Value || !NetworkServer.active) return;
            var currentRun = Run.instance;
            if (!ReferenceEquals(currentRun, _runInstance))
                ResetForRunBoundary(currentRun);
            if (currentRun == null) return;

            var currentStage = CurrentStageToken();
            if (currentStage != _stage) OnStageTransition(currentStage);
            RetryTransferBroadcasts();
            RetryHistoricalMirrors();
            if (Time.unscaledTime < _nextParticipantSweep) return;
            _nextParticipantSweep = Time.unscaledTime + Math.Max(0.10f, _config.ParticipantSweepSeconds.Value);
            SweepParticipants();
            TryGrantDeferred();
        }

        public void BeginDistribution(object[] args, bool instant)
        {
            if (!_config.Enabled.Value || !NetworkServer.active) return;
            var pickup = args.OfType<GenericPickupController>().FirstOrDefault();
            if (pickup == null || !_upstream.IsShareable(pickup)) return;
            var collectorBody = args.OfType<CharacterBody>().FirstOrDefault();
            var pickupDef = args.OfType<PickupDef>().FirstOrDefault() ?? PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
            var context = new DistributionContext
            {
                Pickup = new SharedPickupKey(pickup.GetInstanceID()),
                PickupDef = pickupDef,
                BoxedUniquePickup = pickup.pickup,
                Collector = collectorBody != null ? collectorBody.master : null,
                Instant = instant,
            };
            _distribution.Push(context);
            EnsureClaims(context);
        }

        public void EndDistribution(bool successful)
        {
            if (!NetworkServer.active || _distribution.Count == 0) return;
            var context = _distribution.Pop();
            if (!successful) return;

            if (context.Instant)
            {
                foreach (var participant in _participants.Values)
                {
                    if (!CanUseExactParticipantState(participant)) continue;
                    if (participant.State == ParticipantState.Alive || participant.State == ParticipantState.SupportDrone)
                        _ledger.MarkCollected(context.Pickup, participant.Key, _stage);
                }
            }
            else if (context.Collector != null)
            {
                var participant = FindParticipant(context.Collector);
                // Exact collector evidence is sufficient to complete an already-existing PENDING claim.
                // If the generation is frozen and no claim exists, MarkCollected is a no-op.
                if (participant != null)
                    _ledger.MarkCollected(context.Pickup, participant.Key, _stage);
            }
        }

        public bool ShouldTreatAsActive(CharacterMaster master)
        {
            if (!_config.Enabled.Value || !NetworkServer.active || master == null) return false;
            if (!TryResolveFreshItemShareParticipant(master, out var participant, out var freshSnapshot, out var reason))
            {
                LogItemShareActiveGate(participant, freshSnapshot, upstreamOriginalIsDown: true, correctedIsDown: true, reason: reason);
                return false;
            }

            var shouldCorrect = ItemShareActiveGatePolicy.ShouldCorrectIsDown(
                upstreamOriginalIsDown: true,
                authoritativeControllerPresent: true,
                exactGenerationProven: true,
                participantState: freshSnapshot!.State);
            var correctedIsDown = ItemShareActiveGatePolicy.CorrectIsDown(
                upstreamOriginalIsDown: true,
                authoritativeControllerPresent: true,
                exactGenerationProven: true,
                participantState: freshSnapshot.State);
            LogItemShareActiveGate(
                participant,
                freshSnapshot,
                upstreamOriginalIsDown: true,
                correctedIsDown: correctedIsDown,
                reason: shouldCorrect ? "exact-supportdrone-active-override" : "exact-state-remains-down");
            return shouldCorrect;
        }

        public void OnItemShareCommandSelectionCompleted(PickupPickerController picker)
        {
            if (!_config.Enabled.Value || !NetworkServer.active) return;
            if (picker is not PickupPickerController livePicker) return;
            if (!livePicker || !_upstream.IsCommandCube(livePicker)) return;

            var pickerInstanceId = livePicker.GetInstanceID();
            var providerState = _upstream.HasPickerProviderState(livePicker);
            foreach (var controller in PlayerCharacterMasterController.instances)
            {
                if (controller == null || controller.master == null) continue;
                var master = controller.master;
                var participant = FindParticipant(master);
                if (participant == null || !CanUseExactParticipantState(participant)) continue;
                if (!_classifier.TrySnapshot(controller, out var freshSnapshot, out _)) continue;
                if (!freshSnapshot.Key.Equals(participant.Key) || freshSnapshot.State != ParticipantState.SupportDrone) continue;

                var pickedResolved = _upstream.TryHasCommandPicked(livePicker, master, out var picked);
                var pending = pickedResolved && !picked;
                var diagnosticKey = pickerInstanceId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "|" + freshSnapshot.Key.Value
                    + "|provider=" + providerState
                    + "|resolved=" + pickedResolved
                    + "|picked=" + picked;
                if (!_commandRetentionDiagnosticLogged.Add(diagnosticKey)) continue;

                _log.LogInfo(
                    "[ItemShareFix] ISF_C20_COMMAND_RETENTION"
                    + " picker=" + pickerInstanceId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " participant=" + freshSnapshot.Key
                    + " generation=" + SanitizeDiagnosticToken(freshSnapshot.Key.Generation)
                    + " participantState=" + freshSnapshot.State
                    + " providerState=" + (providerState ? "present" : "missing")
                    + " choiceStateResolved=" + (pickedResolved ? "true" : "false")
                    + " supportDronePending=" + (pending ? "true" : "false")
                    + " pickerObjectAlive=" + (livePicker && livePicker.gameObject != null ? "true" : "false")
                    + " reason=" + (providerState && pending ? "command-retained-for-supportdrone" : "command-retention-not-proven"));
            }
        }

        public bool ShouldSuppressImmediateGive(Inventory inventory)
        {
            if (!_config.Enabled.Value || !InDistribution || InDeferredGrant || inventory == null) return false;

            var participant = _participants.Values.FirstOrDefault(x => x.Master != null && ReferenceEquals(x.Master.inventory, inventory));
            if (participant == null)
            {
                // Fail closed for ItemShareFix state ownership, not for the upstream grant.
                // Without a proven stable participant identity we cannot prove FULLY_DEAD ownership
                // or a reconnect historical barrier, so ItemShare must remain authoritative.
                return ImmediateGivePolicy.ShouldSuppress(
                    stableIdentityResolved: false,
                    participantState: null,
                    deadPlayerDeferredItemsEnabled: _config.DeadPlayerDeferredItemsEnabled.Value,
                    historicalBarrierForCurrentPickup: false);
            }

            var context = _distribution.Peek();
            var historicalBarrier = _ledger.IsHistoricallyBlocked(context.Pickup, participant.Key.StableUser);
            var participantState = CanUseExactParticipantState(participant) ? participant.State : (ParticipantState?)null;
            var deferredEntitlementReady = _ledger.TryGet(context.Pickup, participant.Key, out var currentClaim)
                && currentClaim.State == ClaimState.Deferred
                && _deferredPayloads.ContainsKey(currentClaim.Key);
            return ImmediateGivePolicy.ShouldSuppress(
                stableIdentityResolved: HasProvenGenerationIdentity(participant),
                participantState: participantState,
                deadPlayerDeferredItemsEnabled: _config.DeadPlayerDeferredItemsEnabled.Value,
                historicalBarrierForCurrentPickup: historicalBarrier,
                deferredEntitlementReadyForCurrentPickup: deferredEntitlementReady);
        }

        public void OnPickupTransferred(int oldInstanceId, int newInstanceId)
        {
            if (!_config.Enabled.Value || !NetworkServer.active || oldInstanceId == 0 || newInstanceId == 0 || oldInstanceId == newInstanceId) return;
            try
            {
                _ledger.TransferPickup(new SharedPickupKey(oldInstanceId), new SharedPickupKey(newInstanceId));
                foreach (var pair in _deferredPayloads.Where(x => x.Key.Pickup.Value == oldInstanceId).ToArray())
                {
                    _deferredPayloads.Remove(pair.Key);
                    _deferredPayloads[new ClaimKey(new SharedPickupKey(newInstanceId), pair.Key.Participant)] = pair.Value;
                }
                foreach (var participant in _participants.Values) ReconcileHistoricalBarriers(participant);
            }
            catch (Exception ex)
            {
                _log.LogError("[ItemShareFix] state transfer failed closed: " + ex);
                return;
            }

            if (!_upstream.TryBroadcastTransferredOrbState(newInstanceId))
                _pendingTransferRebroadcast[newInstanceId] = Time.unscaledTime + 2f;
        }

        public void OnNetworkDestroyObserved(object instance)
        {
            if (!_config.Enabled.Value || !NetworkServer.active || !_config.DisconnectCleanupEnabled.Value || instance == null) return;

            CharacterMaster? master = null;
            if (instance is PlayerCharacterMasterController controller) master = controller.master;
            else
            {
                master = ParticipantIdentityResolver.GetMember(instance, "master") as CharacterMaster;
                if (master == null)
                {
                    var masterObject = ParticipantIdentityResolver.GetMember(instance, "masterObject") as GameObject;
                    master = masterObject != null ? masterObject.GetComponent<CharacterMaster>() : null;
                }
            }

            if (master == null) return;
            var participant = FindParticipant(master);
            if (participant == null) return;

            var authoritativePresence = IsParticipantAuthoritativelyPresent(participant);
            var disposition = DisconnectConfirmationPolicy.EvaluateNetworkDestroy(
                participantResolved: true,
                authoritativePresence: authoritativePresence);
            var runtimeType = instance.GetType().FullName ?? instance.GetType().Name;

            _disconnectCandidates[participant.Key] = new DisconnectCandidate
            {
                ObservedAt = Time.unscaledTime,
                DestroyedRuntimeType = runtimeType,
                AuthoritativePresenceAtObservation = authoritativePresence,
            };

            LogDisconnectGate(
                participant,
                eventName: "network_destroy_observed",
                decision: disposition == NetworkDestroyDisposition.IgnoreStillAuthoritative
                    ? "ignored_participant_still_authoritative"
                    : "held_for_authoritative_confirmation",
                destroyedRuntimeType: runtimeType,
                authoritativePresence: authoritativePresence,
                reason: authoritativePresence
                    ? "same exact participant generation remains in PlayerCharacterMasterController.instances"
                    : "generic network destroy is not sufficient; authoritative absence sweep must confirm disconnect");
        }

        private bool IsParticipantAuthoritativelyPresent(ParticipantSnapshot participant)
        {
            if (participant.Master == null) return false;
            foreach (var controller in PlayerCharacterMasterController.instances)
            {
                if (controller == null || controller.master == null) continue;
                if (ReferenceEquals(controller.master, participant.Master)) return true;
            }
            return false;
        }

        private void LogDisconnectGate(
            ParticipantSnapshot participant,
            string eventName,
            string decision,
            string destroyedRuntimeType,
            bool authoritativePresence,
            string reason)
        {
            if (!_config.DiagnosticLogging.Value) return;
            var diagnosticKey = participant.Key.Value + "|" + eventName + "|" + decision + "|" + destroyedRuntimeType + "|" + authoritativePresence;
            if (!_disconnectGateDiagnosticLogged.Add(diagnosticKey)) return;
            _log.LogInfo(
                "[ItemShareFix] ISF_C20_DISCONNECT_GATE"
                + " event=" + SanitizeDiagnosticToken(eventName)
                + " decision=" + SanitizeDiagnosticToken(decision)
                + " participant=" + participant.Key
                + " generation=" + SanitizeDiagnosticToken(participant.Key.Generation)
                + " destroyedType=" + SanitizeDiagnosticToken(destroyedRuntimeType)
                + " authoritativePresence=" + (authoritativePresence ? "true" : "false")
                + " participantState=" + participant.State
                + " reason=" + SanitizeDiagnosticToken(reason));
        }

        private void EnsureClaims(DistributionContext context)
        {
            SweepParticipants();
            foreach (var participant in _participants.Values)
            {
                if (!CanCreateClaims(participant)) continue;
                if (!_ledger.TryEnsure(context.Pickup, participant.Key, participant.State, _stage, out var record))
                {
                    MirrorHistoricalBarrier(context.Pickup, participant);
                    continue;
                }
                if (participant.State == ParticipantState.SupportDrone && record.State == ClaimState.Pending)
                {
                    var ensureDiagnosticKey = record.Key + "|participantState=" + participant.State + "|claimState=" + record.State;
                    if (_claimEnsureDiagnosticLogged.Add(ensureDiagnosticKey))
                    {
                        LogClaimState(
                            record,
                            participant.State,
                            "ensure",
                            "active-claim-created-or-retained",
                            participant.Evidence);
                    }
                }
                if (record.State == ClaimState.Deferred && context.PickupDef != null && context.BoxedUniquePickup != null)
                {
                    _deferredPayloads[record.Key] = new DeferredGrantPayload
                    {
                        PickupDef = context.PickupDef,
                        BoxedUniquePickup = context.BoxedUniquePickup,
                    };
                }
            }
        }

        private void SweepParticipants()
        {
            var seen = new HashSet<ParticipantKey>();
            foreach (var controller in PlayerCharacterMasterController.instances)
            {
                if (controller == null || controller.master == null) continue;
                var master = controller.master;
                var masterNetId = master.netId.Value;
                var probe = masterNetId != 0u ? GetOrCreateGenerationProbe(masterNetId) : null;

                if (!_classifier.TrySnapshot(controller, out var snapshot, out var unsupportedEvidence))
                {
                    var disposition = probe?.ObserveUnsupported() ?? UnsupportedProbeDisposition.NoOwnershipEstablished;
                    if (disposition == UnsupportedProbeDisposition.FreezePreserveExistingState && probe != null)
                    {
                        var provenKey = probe.ProvenParticipant;
                        seen.Add(provenKey);
                        _missingSince.Remove(provenKey);
                        LogProbeFailureOnce(
                            "frozen:" + provenKey.Value,
                            "[ItemShareFix] FAIL-CLOSED transient participant probe: " + unsupportedEvidence
                            + "; generation=" + provenKey
                            + "; existing claims/deferred/history preserved and frozen; no new claims or deferred grants until exact recovery; upstream normal grants remain allowed.");
                    }
                    else
                    {
                        var diagnosticGeneration = masterNetId != 0u
                            ? "masterNetId=" + masterNetId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : "controllerInstanceId=" + controller.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture);
                        LogProbeFailureOnce(
                            "never:" + diagnosticGeneration,
                            "[ItemShareFix] FAIL-CLOSED never-resolved participant identity/state: " + unsupportedEvidence
                            + "; generationDiagnostic=" + diagnosticGeneration
                            + "; no ItemShareFix ownership/claims/deferred state created; upstream ItemShare grants remain allowed; exact probe will retry.");
                    }
                    continue;
                }

                if (probe == null)
                {
                    // TrySnapshot already requires nonzero authoritative master netId; retain a defensive
                    // fail-closed branch in case target runtime behavior changes.
                    LogProbeFailureOnce(
                        "resolved-zero:" + controller.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "[ItemShareFix] FAIL-CLOSED resolved snapshot had no usable master generation; upstream ItemShare remains authoritative.");
                    continue;
                }

                var wasFrozen = probe.State == GenerationProbeState.Frozen;
                if (!probe.TryResolve(snapshot.Key))
                {
                    var provenKey = probe.ProvenParticipant;
                    probe.ObserveUnsupported();
                    seen.Add(provenKey);
                    _missingSince.Remove(provenKey);
                    LogProbeFailureOnce(
                        "identity-mutation:" + provenKey.Value,
                        "[ItemShareFix] FAIL-CLOSED stable identity changed inside one master generation; preserving/freeze prior entitlements for " + provenKey + ".");
                    continue;
                }

                if (wasFrozen)
                {
                    _identityDiagnosticLogged.Remove("frozen:" + snapshot.Key.Value);
                    if (_config.DiagnosticLogging.Value)
                        _log.LogInfo("[ItemShareFix] participant probe RECOVERED same generation " + snapshot.Key + "; preserved claims/deferred state resumed without recreation.");
                }

                foreach (var previousGeneration in _participants.Keys
                    .Where(x => x.StableUser.Equals(snapshot.Key.StableUser) && !x.Equals(snapshot.Key))
                    .ToArray())
                {
                    if (_participants.TryGetValue(previousGeneration, out var replacedParticipant))
                    {
                        var destroyedRuntimeType = _disconnectCandidates.TryGetValue(previousGeneration, out var replacementCandidate)
                            ? replacementCandidate.DestroyedRuntimeType
                            : "<none-observed>";
                        LogDisconnectGate(
                            replacedParticipant,
                            eventName: "authoritative_participant_replacement",
                            decision: "confirmed_disconnect",
                            destroyedRuntimeType: destroyedRuntimeType,
                            authoritativePresence: false,
                            reason: "same stable user observed with replacement master generation");
                    }
                    CancelParticipant(previousGeneration, "stable user observed with a replacement connection/master generation");
                }

                seen.Add(snapshot.Key);
                _missingSince.Remove(snapshot.Key);
                if (_disconnectCandidates.TryGetValue(snapshot.Key, out var recoveredDestroyCandidate))
                {
                    if (!recoveredDestroyCandidate.AuthoritativePresenceAtObservation)
                    {
                        LogDisconnectGate(
                            snapshot,
                            eventName: "network_destroy_observed",
                            decision: "ignored_participant_still_authoritative",
                            destroyedRuntimeType: recoveredDestroyCandidate.DestroyedRuntimeType,
                            authoritativePresence: true,
                            reason: "authoritative controller sweep retained same exact participant generation after lifecycle destroy");
                    }
                    _disconnectCandidates.Remove(snapshot.Key);
                }
                var isNewGeneration = !_participants.ContainsKey(snapshot.Key);

                if (_participants.TryGetValue(snapshot.Key, out var previous) && previous.State != snapshot.State)
                {
                    _ledger.TransitionParticipant(snapshot.Key, previous.State, snapshot.State, _stage);
                    if (snapshot.State == ParticipantState.FullyDead)
                    {
                        foreach (var deferred in _ledger.Records.Where(x => x.Key.Participant.Equals(snapshot.Key) && x.State == ClaimState.Deferred))
                        {
                            if (!_deferredPayloads.ContainsKey(deferred.Key))
                            {
                                var pickup = UpstreamBridge.FindPickupByInstanceId(deferred.Key.Pickup.Value);
                                if (pickup != null)
                                {
                                    var pickupDef = PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
                                    if (pickupDef != null)
                                    {
                                        _deferredPayloads[deferred.Key] = new DeferredGrantPayload
                                        {
                                            PickupDef = pickupDef,
                                            BoxedUniquePickup = pickup.pickup,
                                        };
                                    }
                                }
                            }
                        }
                    }
                    LogTransition(snapshot.Key, previous.State, snapshot.State, snapshot.Evidence);
                    if (snapshot.State == ParticipantState.SupportDrone || previous.State == ParticipantState.SupportDrone)
                    {
                        LogParticipantClaimSnapshot(
                            snapshot.Key,
                            snapshot.State,
                            "participant-transition",
                            "state-change",
                            previous.State,
                            snapshot.State,
                            snapshot.Evidence);
                    }
                }
                _participants[snapshot.Key] = snapshot;
                if (isNewGeneration) ReconcileHistoricalBarriers(snapshot);
            }

            if (!_config.DisconnectCleanupEnabled.Value) return;
            foreach (var key in _participants.Keys.Where(x => !seen.Contains(x)).ToArray())
            {
                if (!_missingSince.TryGetValue(key, out var since))
                {
                    _missingSince[key] = Time.unscaledTime;
                    continue;
                }
                var absenceGraceElapsed = Time.unscaledTime - since >= 2f && Time.unscaledTime - _lastStageChangeTime >= 2f;
                if (!DisconnectConfirmationPolicy.ShouldConfirmDisconnect(
                    participantAbsentFromAuthoritativeControllerList: true,
                    absenceGraceElapsed: absenceGraceElapsed)) continue;

                if (_participants.TryGetValue(key, out var missingParticipant))
                {
                    var candidateObserved = _disconnectCandidates.TryGetValue(key, out var confirmedCandidate);
                    var destroyedRuntimeType = candidateObserved
                        ? confirmedCandidate.DestroyedRuntimeType
                        : "<none-observed>";
                    LogDisconnectGate(
                        missingParticipant,
                        eventName: candidateObserved ? "network_destroy_observed" : "authoritative_absence_observed",
                        decision: "confirmed_disconnect",
                        destroyedRuntimeType: destroyedRuntimeType,
                        authoritativePresence: false,
                        reason: "participant absent from PlayerCharacterMasterController.instances after existing 2s grace");
                }
                CancelParticipant(key, "participant absent from authoritative controller list");
            }
        }

        private bool TryResolveFreshItemShareParticipant(
            CharacterMaster master,
            out ParticipantSnapshot? participant,
            out ParticipantSnapshot? freshSnapshot,
            out string reason)
        {
            participant = FindParticipant(master);
            freshSnapshot = null;
            reason = "participant-not-proven";
            if (participant == null) return false;
            if (!CanUseExactParticipantState(participant))
            {
                reason = "generation-not-resolved";
                return false;
            }

            var controller = PlayerCharacterMasterController.instances
                .FirstOrDefault(x => x != null && x.master != null && ReferenceEquals(x.master, master));
            if (controller == null)
            {
                reason = "authoritative-controller-absent";
                return false;
            }

            if (!_classifier.TrySnapshot(controller, out var snapshot, out var unsupportedEvidence))
            {
                reason = "fresh-snapshot-unavailable-" + SanitizeDiagnosticToken(unsupportedEvidence);
                return false;
            }

            freshSnapshot = snapshot;
            if (!snapshot.Key.Equals(participant.Key))
            {
                reason = "generation-mismatch";
                return false;
            }

            reason = "fresh-exact-participant-state";
            return true;
        }

        private void LogItemShareActiveGate(
            ParticipantSnapshot? participant,
            ParticipantSnapshot? freshSnapshot,
            bool upstreamOriginalIsDown,
            bool correctedIsDown,
            string reason)
        {
            if (!_config.DiagnosticLogging.Value) return;
            var snapshot = freshSnapshot ?? participant;
            var participantToken = snapshot != null ? snapshot.Key.Value : "unresolved";
            var generationToken = snapshot != null ? SanitizeDiagnosticToken(snapshot.Key.Generation) : "unresolved";
            var stateToken = snapshot != null ? snapshot.State.ToString() : "Unresolved";
            var diagnosticKey = participantToken
                + "|state=" + stateToken
                + "|original=" + upstreamOriginalIsDown
                + "|corrected=" + correctedIsDown
                + "|distribution=" + InDistribution
                + "|reason=" + reason;
            if (!_itemShareActiveGateDiagnosticLogged.Add(diagnosticKey)) return;

            _log.LogInfo(
                "[ItemShareFix] ISF_C20_ITEMSHARE_ACTIVE_GATE"
                + " participant=" + participantToken
                + " generation=" + generationToken
                + " participantState=" + stateToken
                + " upstreamOriginalIsDown=" + (upstreamOriginalIsDown ? "true" : "false")
                + " correctedIsDown=" + (correctedIsDown ? "true" : "false")
                + " inDistribution=" + (InDistribution ? "true" : "false")
                + " reason=" + SanitizeDiagnosticToken(reason));
        }

        private void ReconcileHistoricalBarriers(ParticipantSnapshot participant)
        {
            if (participant.Master == null || !HasProvenGenerationIdentity(participant)) return;
            foreach (var historical in _ledger.HistoricalFor(participant.Key.StableUser))
                MirrorHistoricalBarrier(historical.Key.Pickup, participant);
        }

        private void MirrorHistoricalBarrier(SharedPickupKey pickup, ParticipantSnapshot participant)
        {
            if (participant.Master == null) return;
            if (_upstream.TryMirrorHistoricalBarrier(pickup.Value, participant.Master, out var evidence))
            {
                if (_config.DiagnosticLogging.Value)
                    _log.LogInfo("[ItemShareFix] reconnect historical barrier " + pickup + "/" + participant.Key + ": " + evidence);
                return;
            }
            _pendingHistoricalMirrorRetry[new ClaimKey(pickup, participant.Key)] = Time.unscaledTime + 0.25f;
            if (_config.DiagnosticLogging.Value)
                _log.LogWarning("[ItemShareFix] reconnect historical barrier pending " + pickup + "/" + participant.Key + ": " + evidence);
        }

        private void CancelParticipant(ParticipantKey key, string reason)
        {
            if (!_participants.TryGetValue(key, out var participant)) return;
            var masterNetId = participant.Master != null ? participant.Master.netId.Value : 0u;
            var previousState = participant.State;
            _ledger.TransitionParticipant(key, previousState, ParticipantState.Disconnected, _stage);
            participant.State = ParticipantState.Disconnected;
            LogParticipantClaimSnapshot(
                key,
                ParticipantState.Disconnected,
                "participant-disconnect",
                SanitizeDiagnosticToken(reason),
                previousState,
                ParticipantState.Disconnected,
                participant.Evidence);
            foreach (var payload in _deferredPayloads.Keys.Where(x => x.Participant.Equals(key)).ToArray()) _deferredPayloads.Remove(payload);
            _participants.Remove(key);
            _missingSince.Remove(key);
            _disconnectCandidates.Remove(key);
            foreach (var pending in _pendingHistoricalMirrorRetry.Keys.Where(x => x.Participant.Equals(key)).ToArray())
                _pendingHistoricalMirrorRetry.Remove(pending);
            if (masterNetId != 0u) _generationProbes.Remove(masterNetId);
            _classifier.Forget(key);
            if (_config.DiagnosticLogging.Value) _log.LogInfo("[ItemShareFix] participant DISCONNECTED " + key + " reason=" + reason);
        }

        private void TryGrantDeferred()
        {
            if (!_config.DeadPlayerDeferredItemsEnabled.Value) return;
            if (Time.unscaledTime - _lastStageChangeTime < 0.75f) return;

            foreach (var participant in _participants.Values.ToArray())
            {
                if (!CanGrantDeferred(participant)) continue;
                if (participant.State != ParticipantState.Alive || participant.Master == null || participant.Master.inventory == null || participant.Master.GetBody() == null) continue;
                foreach (var record in _ledger.DeferredFor(participant.Key, _stage))
                {
                    if (!_deferredPayloads.TryGetValue(record.Key, out var payload)) continue;
                    try
                    {
                        _deferredGrantDepth++;
                        if (!_upstream.GiveDeferred(participant.Master.inventory, payload.PickupDef, payload.BoxedUniquePickup))
                        {
                            _log.LogWarning("[ItemShareFix] deferred grant returned false; entitlement retained " + record.Key);
                            continue;
                        }
                        if (!_ledger.MarkDeferredGranted(record.Key, _stage))
                        {
                            // The authoritative grant already happened. Never retry this payload and risk a duplicate grant.
                            _deferredPayloads.Remove(record.Key);
                            _log.LogError("[ItemShareFix] deferred grant succeeded but ledger transition failed; payload retired to prevent duplicate grant " + record.Key);
                            continue;
                        }
                        _deferredPayloads.Remove(record.Key);
                        if (_config.DiagnosticLogging.Value) _log.LogInfo("[ItemShareFix] granted deferred entitlement " + record.Key);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("[ItemShareFix] deferred grant retained after failure " + record.Key + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally { _deferredGrantDepth--; }
                }
            }
        }

        private GenerationProbeGate GetOrCreateGenerationProbe(uint masterNetId)
        {
            if (!_generationProbes.TryGetValue(masterNetId, out var probe))
            {
                probe = new GenerationProbeGate();
                _generationProbes.Add(masterNetId, probe);
            }
            return probe;
        }

        private bool TryGetGenerationProbe(ParticipantSnapshot participant, out GenerationProbeGate probe)
        {
            probe = null!;
            if (participant.Master == null) return false;
            var masterNetId = participant.Master.netId.Value;
            if (masterNetId == 0u || !_generationProbes.TryGetValue(masterNetId, out var resolvedProbe)) return false;
            probe = resolvedProbe;
            return true;
        }

        private bool HasProvenGenerationIdentity(ParticipantSnapshot participant)
            => TryGetGenerationProbe(participant, out var probe)
               && probe.HasProvenParticipant
               && probe.ProvenParticipant.Equals(participant.Key);

        private bool CanUseExactParticipantState(ParticipantSnapshot participant)
            => TryGetGenerationProbe(participant, out var probe)
               && probe.State == GenerationProbeState.Resolved
               && probe.ProvenParticipant.Equals(participant.Key);

        private bool CanCreateClaims(ParticipantSnapshot participant)
            => TryGetGenerationProbe(participant, out var probe)
               && probe.CanCreateClaims
               && probe.ProvenParticipant.Equals(participant.Key);

        private bool CanGrantDeferred(ParticipantSnapshot participant)
            => TryGetGenerationProbe(participant, out var probe)
               && probe.ProvenParticipant.Equals(participant.Key)
               && probe.CanGrantDeferred(participant.State);

        private void LogProbeFailureOnce(string diagnosticKey, string message)
        {
            if (_identityDiagnosticLogged.Add(diagnosticKey)) _log.LogError(message);
        }

        private ParticipantSnapshot? FindParticipant(CharacterMaster master)
            => _participants.Values.FirstOrDefault(x => x.Master != null && ReferenceEquals(x.Master, master));

        private void ResetForRunBoundary(Run? currentRun)
        {
            var oldRun = _runInstance;
            _runInstance = currentRun;
            _distribution.Clear();
            _ledger.Clear();
            _participants.Clear();
            _missingSince.Clear();
            _deferredPayloads.Clear();
            _pendingTransferRebroadcast.Clear();
            _pendingHistoricalMirrorRetry.Clear();
            _generationProbes.Clear();
            _identityDiagnosticLogged.Clear();
            _claimEnsureDiagnosticLogged.Clear();
            _disconnectCandidates.Clear();
            _disconnectGateDiagnosticLogged.Clear();
            _itemShareActiveGateDiagnosticLogged.Clear();
            _commandRetentionDiagnosticLogged.Clear();
            _temporaryPolicyDiagnosticLogged.Clear();
            _classifier.Reset();
            _deferredGrantDepth = 0;
            _nextParticipantSweep = 0f;
            _stage = CurrentStageToken();
            _lastStageChangeTime = Time.unscaledTime;
            if (_config.DiagnosticLogging.Value)
                _log.LogInfo("[ItemShareFix] run boundary reset old=" + (oldRun == null ? "<null>" : oldRun.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture))
                             + " new=" + (currentRun == null ? "<null>" : currentRun.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        private void OnStageTransition(int newStage)
        {
            _stage = newStage;
            _lastStageChangeTime = Time.unscaledTime;
            _ledger.OnStageTransition(newStage);
            foreach (var pair in _deferredPayloads.ToArray())
            {
                if (!_ledger.TryGet(pair.Key.Pickup, pair.Key.Participant, out var record) || record.State != ClaimState.Deferred)
                    _deferredPayloads.Remove(pair.Key);
            }
            _pendingTransferRebroadcast.Clear();
            _pendingHistoricalMirrorRetry.Clear();
            _claimEnsureDiagnosticLogged.Clear();
            _temporaryPolicyDiagnosticLogged.Clear();
            if (_config.DiagnosticLogging.Value) _log.LogInfo("[ItemShareFix] stage transition token=" + newStage);
        }

        private void RetryTransferBroadcasts()
        {
            foreach (var pair in _pendingTransferRebroadcast.ToArray())
            {
                if (_upstream.TryBroadcastTransferredOrbState(pair.Key) || Time.unscaledTime >= pair.Value)
                    _pendingTransferRebroadcast.Remove(pair.Key);
            }
        }

        private void RetryHistoricalMirrors()
        {
            foreach (var pair in _pendingHistoricalMirrorRetry.ToArray())
            {
                if (Time.unscaledTime < pair.Value) continue;

                if (!_ledger.IsHistoricallyBlocked(pair.Key.Pickup, pair.Key.Participant.StableUser)
                    || !_participants.TryGetValue(pair.Key.Participant, out var participant)
                    || participant.Master == null
                    || UpstreamBridge.FindPickupByInstanceId(pair.Key.Pickup.Value) == null)
                {
                    _pendingHistoricalMirrorRetry.Remove(pair.Key);
                    continue;
                }

                if (_upstream.TryMirrorHistoricalBarrier(pair.Key.Pickup.Value, participant.Master, out var evidence))
                {
                    _pendingHistoricalMirrorRetry.Remove(pair.Key);
                    if (_config.DiagnosticLogging.Value)
                        _log.LogInfo("[ItemShareFix] reconnect historical barrier retry PASS " + pair.Key + ": " + evidence);
                    continue;
                }

                _pendingHistoricalMirrorRetry[pair.Key] = Time.unscaledTime + 0.25f;
                if (_config.DiagnosticLogging.Value)
                    _log.LogDebug("[ItemShareFix] reconnect historical barrier retry pending " + pair.Key + ": " + evidence);
            }
        }

        private void LogTransition(ParticipantKey key, ParticipantState from, ParticipantState to, string evidence)
        {
            if (_config.DiagnosticLogging.Value) _log.LogInfo("[ItemShareFix] participant " + key + " " + from + " -> " + to + " (" + evidence + ")");
        }

        private void LogParticipantClaimSnapshot(
            ParticipantKey participant,
            ParticipantState participantState,
            string action,
            string reason,
            ParticipantState from,
            ParticipantState to,
            string participantEvidence)
        {
            if (!_config.DiagnosticLogging.Value) return;
            foreach (var record in _ledger.Records.Where(x => x.Key.Participant.Equals(participant)).OrderBy(x => x.Key.Pickup.Value))
            {
                LogClaimState(record, participantState, action, reason, participantEvidence, from, to);
            }
        }

        private void LogClaimState(
            ClaimRecord record,
            ParticipantState participantState,
            string action,
            string reason,
            string participantEvidence,
            ParticipantState? from = null,
            ParticipantState? to = null)
        {
            if (!_config.DiagnosticLogging.Value) return;
            var transition = from.HasValue && to.HasValue
                ? " from=" + from.Value + " to=" + to.Value
                : string.Empty;
            _log.LogInfo(
                "[ItemShareFix] ISF_C20_CLAIM_STATE"
                + " participant=" + record.Key.Participant
                + " pickup=" + record.Key.Pickup.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " participantState=" + participantState
                + " claimState=" + record.State
                + " action=" + action
                + " reason=" + reason
                + transition
                + " exactRemoteOp=" + ExactRemoteOperationDiagnostic(participantEvidence));
        }

        private static string ExactRemoteOperationDiagnostic(string participantEvidence)
        {
            const string marker = "CharacterMaster.GetInRemoteOp()=";
            if (string.IsNullOrEmpty(participantEvidence)) return "not-probed";
            var markerIndex = participantEvidence.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) return "not-probed-classifier-order";
            var valueIndex = markerIndex + marker.Length;
            if (participantEvidence.IndexOf("true", valueIndex, StringComparison.Ordinal) == valueIndex) return "true";
            if (participantEvidence.IndexOf("false", valueIndex, StringComparison.Ordinal) == valueIndex) return "false";
            return "unavailable-fail-closed";
        }

        private static string SanitizeDiagnosticToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unspecified";
            var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_').ToArray();
            return new string(chars);
        }

        private static int CurrentStageToken()
        {
            var run = Run.instance;
            if (run == null) return 0;
            var value = ParticipantIdentityResolver.GetMember(run, "stageClearCount");
            try { return value != null ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) : 0; }
            catch { return 0; }
        }
    }
}
