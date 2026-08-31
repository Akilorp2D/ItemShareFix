using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using ItemShareFix.Core;
using RoR2;
using UnityEngine;

namespace ItemShareFix
{
    internal sealed class LocalPickupPresentationGate : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Light, bool> _lights = new Dictionary<Light, bool>();
        private readonly Dictionary<Behaviour, bool> _visualBehaviours = new Dictionary<Behaviour, bool>();
        private bool _hidden;

        public void ApplyHidden()
        {
            _hidden = true;
            RefreshAndApply();
        }

        public void Restore()
        {
            _hidden = false;
            foreach (var pair in _renderers.ToArray()) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            foreach (var pair in _lights.ToArray()) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in _visualBehaviours.ToArray()) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _renderers.Clear();
            _lights.Clear();
            _visualBehaviours.Clear();
        }

        public void RefreshAndApply()
        {
            if (!_hidden) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!_renderers.ContainsKey(renderer)) _renderers[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light == null) continue;
                if (!_lights.ContainsKey(light)) _lights[light] = light.enabled;
                light.enabled = false;
            }
            foreach (var behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour is Light) continue;
                var typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                if (!IsPresentationOnlyBehaviour(typeName)) continue;
                if (!_visualBehaviours.ContainsKey(behaviour)) _visualBehaviours[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }
            PruneDestroyed();
        }

        private static bool IsPresentationOnlyBehaviour(string typeName)
            => typeName.IndexOf("Highlight", StringComparison.OrdinalIgnoreCase) >= 0
               || typeName.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) >= 0
               || typeName.IndexOf("PickupDisplayGlow", StringComparison.OrdinalIgnoreCase) >= 0;

        private void PruneDestroyed()
        {
            foreach (var key in _renderers.Keys.Where(x => x == null).ToArray()) _renderers.Remove(key);
            foreach (var key in _lights.Keys.Where(x => x == null).ToArray()) _lights.Remove(key);
            foreach (var key in _visualBehaviours.Keys.Where(x => x == null).ToArray()) _visualBehaviours.Remove(key);
        }

        private void OnDestroy()
        {
            if (_hidden) Restore();
        }
    }

    internal sealed class LocalCommandPresentationGate : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Light, bool> _lights = new Dictionary<Light, bool>();
        private readonly Dictionary<Behaviour, bool> _visualBehaviours = new Dictionary<Behaviour, bool>();
        private bool _hidden;

        public bool IsHidden => _hidden;

        public void ApplyHidden()
        {
            _hidden = true;
            RefreshAndApply();
        }

        public void Restore()
        {
            _hidden = false;
            foreach (var pair in _renderers.ToArray()) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            foreach (var pair in _lights.ToArray()) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in _visualBehaviours.ToArray()) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _renderers.Clear();
            _lights.Clear();
            _visualBehaviours.Clear();
        }

        public void RefreshAndApply()
        {
            if (!_hidden) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!_renderers.ContainsKey(renderer)) _renderers[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light == null) continue;
                if (!_lights.ContainsKey(light)) _lights[light] = light.enabled;
                light.enabled = false;
            }
            foreach (var behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour is Light || behaviour is PickupPickerController || behaviour is UnityEngine.Networking.NetworkBehaviour) continue;
                var typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                if (!IsPresentationOnlyBehaviour(typeName)) continue;
                if (!_visualBehaviours.ContainsKey(behaviour)) _visualBehaviours[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }
            PruneDestroyed();
        }

        private static bool IsPresentationOnlyBehaviour(string typeName)
            => typeName.IndexOf("Highlight", StringComparison.OrdinalIgnoreCase) >= 0
               || typeName.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) >= 0
               || typeName.IndexOf("PickupDisplayGlow", StringComparison.OrdinalIgnoreCase) >= 0;

        private void PruneDestroyed()
        {
            foreach (var key in _renderers.Keys.Where(x => x == null).ToArray()) _renderers.Remove(key);
            foreach (var key in _lights.Keys.Where(x => x == null).ToArray()) _lights.Remove(key);
            foreach (var key in _visualBehaviours.Keys.Where(x => x == null).ToArray()) _visualBehaviours.Remove(key);
        }

        private void OnDestroy()
        {
            if (_hidden) Restore();
        }
    }

    // Kept shape-compatible with external observers: _markers remains enumerable and Pickup stays GenericPickupController.
    internal sealed class PersonalPickupMarker
    {
        public int InstanceId { get; set; }
        public GenericPickupController Pickup { get; set; } = null!;
        public string Label { get; set; } = string.Empty;
        public string ItemSemanticKey { get; set; } = string.Empty;
        public string ClassName { get; set; } = "UNKNOWN";
        public MarkerClassKind Kind { get; set; } = MarkerClassKind.Unknown;
        public Color TextColor { get; set; } = Color.white;
        public MarkerLifetimeKind Lifetime { get; set; } = MarkerLifetimeKind.Permanent;
    }

    internal sealed class PersonalCommandMarker
    {
        public int InstanceId { get; set; }
        public PickupPickerController Picker { get; set; } = null!;
        public string Label { get; set; } = string.Empty;
        public string ItemSemanticKey { get; set; } = string.Empty;
        public string ClassName { get; set; } = "UNKNOWN";
        public MarkerClassKind Kind { get; set; } = MarkerClassKind.Unknown;
        public Color TextColor { get; set; } = Color.white;
        public MarkerLifetimeKind Lifetime { get; set; } = MarkerLifetimeKind.Unknown;
    }

    internal readonly struct MarkerRuntimeMetadata
    {
        public MarkerRuntimeMetadata(MarkerClassKind kind, string className, string label, Color textColor, bool exactClass)
        {
            Kind = kind;
            ClassName = className;
            Label = label;
            TextColor = textColor;
            ExactClass = exactClass;
        }

        public MarkerClassKind Kind { get; }
        public string ClassName { get; }
        public string Label { get; }
        public Color TextColor { get; }
        public bool ExactClass { get; }
    }

    internal sealed class ClientPresentationCoordinator
    {
        private readonly PluginConfig _config;
        private readonly UpstreamBridge _upstream;
        private readonly ManualLogSource _log;
        private readonly Dictionary<int, LocalPickupPresentationGate> _gates = new Dictionary<int, LocalPickupPresentationGate>();
        private readonly Dictionary<int, LocalCommandPresentationGate> _commandGates = new Dictionary<int, LocalCommandPresentationGate>();
        private readonly Dictionary<int, bool> _localCommandGateDiagnosticState = new Dictionary<int, bool>();
        private readonly Dictionary<int, string> _localPickupVisualDiagnosticState = new Dictionary<int, string>();
        private readonly Dictionary<long, bool> _localPickupInteractionDiagnosticState = new Dictionary<long, bool>();
        private readonly HashSet<int> _upstreamVisibilityGateObserved = new HashSet<int>();
        private readonly List<PersonalPickupMarker> _markers = new List<PersonalPickupMarker>();
        private readonly List<PersonalCommandMarker> _commandMarkers = new List<PersonalCommandMarker>();
        private readonly PersonalMarkerRegistry _markerRegistry = new PersonalMarkerRegistry(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly HashSet<int> _ordinaryRenderLogged = new HashSet<int>();
        private readonly Dictionary<int, string> _commandRemovalReasons = new Dictionary<int, string>();
        private readonly HashSet<int> _commandOptionDisagreementLogged = new HashSet<int>();
        private readonly HashSet<int> _commandLifetimeUnknownLogged = new HashSet<int>();
        private readonly Dictionary<int, string> _commandShareabilityDiagnosticState = new Dictionary<int, string>();
        private float _nextSweep;
        private int _lastStageToken = int.MinValue;
        private int _localMastersFrame = -1;
        private CharacterMaster[] _cachedLocalMasters = Array.Empty<CharacterMaster>();
        private bool _refreshRequested;
        private int _upstreamNormalizationDepth;
        private readonly MarkerRuntimePerformanceCounters _performance = new MarkerRuntimePerformanceCounters();
        private readonly NativeHudMarkerRenderer _hudRenderer;
        private readonly Dictionary<long, MarkerHudDiagnosticState> _markerProjectionDiagnosticState = new Dictionary<long, MarkerHudDiagnosticState>();
        private readonly LocalHudPresentationProbe _localHudProbe;
        private readonly List<MarkerHudExclusionZone> _dynamicHudZones = new List<MarkerHudExclusionZone>(1);
        private readonly List<MarkerRenderInput> _renderInputs = new List<MarkerRenderInput>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private bool? _modalHudSuppressionDiagnosticState;
        private string _modalHudSuppressionReason = string.Empty;
        private bool? _messageHudDiagnosticState;
        private bool _hasMessageHudDiagnosticRect;
        private MarkerHudRect _messageHudDiagnosticRect;
        private Camera? _presentationCamera;
        private float _nextPresentationCameraRefresh;
        private float _nextPerformanceSummary;
        private int _lastPerformanceMarkerCount;

        public ClientPresentationCoordinator(PluginConfig config, UpstreamBridge upstream, ManualLogSource log)
        {
            _config = config;
            _upstream = upstream;
            _log = log;
            _localHudProbe = new LocalHudPresentationProbe(_performance);
            _hudRenderer = new NativeHudMarkerRenderer(log, _performance);
            _config.MarkerPresentationSettingChanged += OnMarkerPresentationSettingChanged;
            _log.LogInfo("ISF_MARKER_WORLD_CLUSTER_CONFIG merge=4.50 split=6.00 dwell=0.35 solve=0.20");
        }

        private void OnMarkerPresentationSettingChanged(object? sender, EventArgs args)
        {
            _hudRenderer.InvalidatePresentationSettings(_config.MarkerSettingsSnapshot(), _config.MarkerVisualSettingsSnapshot());
            if (ReferenceEquals(sender, _config.PersonalMarkersEnabled))
            {
                if (!_config.PersonalMarkersEnabled.Value) _hudRenderer.Clear();
                RequestRefresh();
            }
            else if (ReferenceEquals(sender, _config.ShareTemporaryItems))
            {
                RequestRefresh();
            }
        }

        public void RecordUnityUpdate() => _performance.RecordUnityUpdate();

        public void RequestRefresh() => _refreshRequested = true;

        public void OnBlockingModalLifecycleObserved(Component component)
        {
            // Presentation-only invalidation. No marker registry/state mutation and no full layout solve is requested here.
            _localHudProbe.ObserveBlockingModalLifecycle(component);
        }

        public bool TryEvaluateLocalCollected(GenericPickupController pickup, out bool collectedByAllLocalParticipants)
        {
            collectedByAllLocalParticipants = false;
            if (!_config.Enabled.Value
                || !_config.PersonalPickupVisibilityRepairEnabled.Value
                || !_upstream.IsIndividualMode
                || !_upstream.HideCollectedOrbsEnabled
                || pickup == null
                || !_upstream.IsShareable(pickup)) return false;

            var localMasters = GetLocalMastersSnapshot();
            if (localMasters.Length == 0) return false;
            collectedByAllLocalParticipants = localMasters.All(master => _upstream.HasCollected(pickup, master));
            return true;
        }

        public bool ShouldSuppressLocalPickupInteraction(GenericPickupController pickup, Interactor interactor)
        {
            if (!_config.Enabled.Value
                || !_config.PersonalPickupVisibilityRepairEnabled.Value
                || !_upstream.IsIndividualMode
                || !_upstream.HideCollectedOrbsEnabled
                || pickup == null
                || interactor == null) return false;

            var shareable = _upstream.IsShareable(pickup);
            var body = interactor.GetComponent<CharacterBody>();
            if (body == null || body.master == null) return false;
            var master = body.master;
            var interactorIsLocal = master.hasAuthority;
            var collected = interactorIsLocal && _upstream.HasCollected(pickup, master);
            var suppress = LocalPickupSuppressionPolicy.ShouldSuppressInteractor(
                featureEnabled: true,
                upstreamHideCollectedEnabled: true,
                shareablePickup: shareable,
                interactorIsLocal: interactorIsLocal,
                interactorHasCollected: collected);

            if (interactorIsLocal) LogLocalInteractionGateTransition(pickup, master, collected, suppress);
            return suppress;
        }

        public void OnUpstreamVisibilityApplied(GenericPickupController pickup)
        {
            if (pickup == null || _upstreamNormalizationDepth > 0) return;
            var instanceId = pickup.GetInstanceID();
            if (!TryEvaluateLocalCollected(pickup, out var collectedByAllLocalParticipants))
            {
                ReleaseGate(instanceId);
                RequestRefresh();
                return;
            }

            if (!collectedByAllLocalParticipants)
            {
                ReleaseGate(instanceId);
                return;
            }

            // ItemShare may have just partially hidden the historical pickupDisplay subtree.
            // Normalize that exact subtree inside the same call stack, then apply our full-root gate.
            // The depth guard prevents our normalization call from recursively re-entering this postfix.
            NormalizeUpstreamForGate(pickup);
            if (!_gates.TryGetValue(instanceId, out var gate) || gate == null)
            {
                gate = pickup.gameObject.GetComponent<LocalPickupPresentationGate>() ?? pickup.gameObject.AddComponent<LocalPickupPresentationGate>();
                _gates[instanceId] = gate;
            }
            gate.ApplyHidden();
            if (_config.DiagnosticLogging.Value && _upstreamVisibilityGateObserved.Add(instanceId))
            {
                LogInfo("ISF_C20_LOCAL_PICKUP_GATE pickup=" + instanceId.ToString(CultureInfo.InvariantCulture)
                    + " localMaster=process localCollected=all visualSuppressed=true interactionSuppressed=participant-specific"
                    + " action=upstream-visibility-applied reason=all-local-collected");
            }
        }

        public void Tick()
        {
            var stageToken = CurrentStageToken();
            if (stageToken != _lastStageToken)
            {
                RestoreAll("stage-or-run-boundary");
                _localHudProbe.InvalidateLifecycle();
                _presentationCamera = null;
                _nextPresentationCameraRefresh = 0f;
                _lastStageToken = stageToken;
                _refreshRequested = true;
            }

            if (!_refreshRequested && Time.unscaledTime < _nextSweep) return;
            _refreshRequested = false;
            _nextSweep = Time.unscaledTime + Math.Max(0.08f, _config.PresentationSweepSeconds.Value);
            Sweep();
        }

        public void RenderFrame()
        {
            _performance.RecordRenderFrame();
            var markerFeatureActive = _config.Enabled.Value
                && _config.PersonalMarkersEnabled.Value
                && (_markers.Count > 0 || _commandMarkers.Count > 0);
            if (!markerFeatureActive)
            {
                if (_lastPerformanceMarkerCount > 0) EmitPerformanceSummary("marker-teardown", 0, force: true);
                _lastPerformanceMarkerCount = 0;
                _hudRenderer.SetPresentationSuppressed(false);
                _hudRenderer.Clear();
                return;
            }

            var markerCount = _markers.Count + _commandMarkers.Count;
            _lastPerformanceMarkerCount = markerCount;
            MaybeEmitPerformanceSummary(markerCount);

            var modalActive = _localHudProbe.TryGetBlockingModal(out var modalReason);
            if (!_modalHudSuppressionDiagnosticState.HasValue
                || _modalHudSuppressionDiagnosticState.Value != modalActive
                || (modalActive && !string.Equals(_modalHudSuppressionReason, modalReason, StringComparison.Ordinal)))
            {
                _modalHudSuppressionDiagnosticState = modalActive;
                _modalHudSuppressionReason = modalReason;
                LogInfo("ISF_MARKER_HUD_SUPPRESS reason=" + (modalActive ? modalReason : "pause-menu") + " active=" + modalActive);
            }

            _hudRenderer.SetPresentationSuppressed(modalActive);
            if (!MarkerPresentationPolicy.ShouldRenderHudMarkers(markerFeatureActive, modalActive)) return;

            _dynamicHudZones.Clear();
            var messageHudActive = _localHudProbe.TryGetVisibleMessageHudRect(out var messageHudRect);
            if (messageHudActive)
            {
                _dynamicHudZones.Add(new MarkerHudExclusionZone(
                    "message-hud-runtime",
                    messageHudRect.Left, messageHudRect.Right, messageHudRect.Bottom, messageHudRect.Top));
            }
            var messageRectChanged = messageHudActive
                && (!_hasMessageHudDiagnosticRect || !SameDiagnosticRect(_messageHudDiagnosticRect, messageHudRect));
            if (!_messageHudDiagnosticState.HasValue
                || _messageHudDiagnosticState.Value != messageHudActive
                || messageRectChanged)
            {
                _messageHudDiagnosticState = messageHudActive;
                _hasMessageHudDiagnosticRect = messageHudActive;
                _messageHudDiagnosticRect = messageHudRect;
                LogInfo("ISF_MARKER_MESSAGE_HUD active=" + messageHudActive
                    + (messageHudActive ? " rect=" + FormatMarkerRect(messageHudRect) : string.Empty));
            }

            var camera = GetPresentationCamera();
            if (camera == null || !TryGetFiniteCameraPosition(camera, out var cameraPosition))
            {
                _hudRenderer.Clear();
                return;
            }

            var markerLanguage = CurrentMarkerLanguage();
            _renderInputs.Clear();
            for (var i = 0; i < _markers.Count;)
            {
                var marker = _markers[i];
                if (!TryResolveOrdinaryMarkerTarget(marker.Pickup, out var basePosition, out var invalidReason))
                {
                    RemoveOrdinaryMarkerNow(marker, invalidReason);
                    continue;
                }
                if (!TryResolvePresentationDistance(cameraPosition, basePosition, out var distance, out invalidReason))
                {
                    RemoveOrdinaryMarkerNow(marker, invalidReason);
                    continue;
                }
                _renderInputs.Add(new MarkerRenderInput(
                    new PersonalMarkerIdentity(PersonalMarkerKind.OrdinaryPickup, marker.InstanceId),
                    basePosition + Vector3.up * 1.2f,
                    distance, PickupLabel(marker.Pickup, markerLanguage), marker.ItemSemanticKey, marker.ClassName, marker.Kind, marker.TextColor, ResolvePickupIcon(marker.Pickup), marker.Lifetime));
                i++;
            }

            for (var i = 0; i < _commandMarkers.Count;)
            {
                var marker = _commandMarkers[i];
                if (!TryResolveCommandMarkerTarget(marker.Picker, out var basePosition, out var invalidReason))
                {
                    RemoveCommandMarkerNow(marker, invalidReason);
                    continue;
                }
                if (!TryResolvePresentationDistance(cameraPosition, basePosition, out var distance, out invalidReason))
                {
                    RemoveCommandMarkerNow(marker, invalidReason);
                    continue;
                }
                _renderInputs.Add(new MarkerRenderInput(
                    new PersonalMarkerIdentity(PersonalMarkerKind.CommandPicker, marker.InstanceId),
                    basePosition + Vector3.up * 1.35f,
                    distance, MarkerClassPolicy.LocalizedReadableClassLabel(marker.Kind, markerLanguage), marker.ItemSemanticKey, marker.ClassName, marker.Kind, marker.TextColor, nativeIcon: null, lifetime: marker.Lifetime));
                i++;
            }

            _hudRenderer.Render(
                camera,
                _renderInputs,
                _config.MarkerSettingsSnapshot(),
                _config.MarkerVisualSettingsSnapshot(),
                markerLanguage,
                OnMarkerRendered,
                _dynamicHudZones);
        }

        public void Dispose()
        {
            _config.MarkerPresentationSettingChanged -= OnMarkerPresentationSettingChanged;
            RestoreAll("external-or-plugin-teardown");
            _hudRenderer.Dispose();
        }

        public void RestoreAll() => RestoreAll("external-or-plugin-teardown");

        private void RestoreAll(string reason)
        {
            if (_markers.Count > 0 || _commandMarkers.Count > 0) EmitPerformanceSummary(reason, 0, force: true);
            _lastPerformanceMarkerCount = 0;
            foreach (var gate in _gates.Values.ToArray())
            {
                if (gate == null) continue;
                gate.Restore();
                UnityEngine.Object.Destroy(gate);
            }
            _gates.Clear();
            foreach (var gate in _commandGates.Values.ToArray())
            {
                if (gate == null) continue;
                gate.Restore();
                UnityEngine.Object.Destroy(gate);
            }
            _commandGates.Clear();
            _localCommandGateDiagnosticState.Clear();
            _localPickupVisualDiagnosticState.Clear();
            _localPickupInteractionDiagnosticState.Clear();
            _upstreamVisibilityGateObserved.Clear();

            foreach (var marker in _commandMarkers.ToArray())
            {
                LogCommandCleanup(marker.InstanceId, reason);
            }
            _markers.Clear();
            _commandMarkers.Clear();
            _markerRegistry.Clear();
            _ordinaryRenderLogged.Clear();
            _commandRemovalReasons.Clear();
            _commandOptionDisagreementLogged.Clear();
            _commandShareabilityDiagnosticState.Clear();
            _markerProjectionDiagnosticState.Clear();
            _modalHudSuppressionDiagnosticState = null;
            _modalHudSuppressionReason = string.Empty;
            _messageHudDiagnosticState = null;
            _hasMessageHudDiagnosticRect = false;
            _messageHudDiagnosticRect = default;
            _localHudProbe.InvalidateLifecycle();
            _presentationCamera = null;
            _nextPresentationCameraRefresh = 0f;
            _hudRenderer.SetPresentationSuppressed(false);
            _hudRenderer.Clear();
        }

        private void Sweep()
        {
            if (!_config.Enabled.Value || !_upstream.IsIndividualMode)
            {
                RestoreAll("feature-disabled-or-non-individual-mode");
                return;
            }

            var localMasters = GetLocalMastersSnapshot();
            if (localMasters.Length == 0)
            {
                RestoreAll("no-local-master");
                return;
            }
            var localStates = localMasters.Select(LocalParticipantResolver.ClassifyLocal).ToArray();
            var markerLanguage = CurrentMarkerLanguage();
            var sweepCamera = GetPresentationCamera();
            var sweepCameraPosition = default(Vector3);
            var hasSweepCameraPosition = sweepCamera != null && TryGetFiniteCameraPosition(sweepCamera, out sweepCameraPosition);

            _markerRegistry.BeginSweep();
            var nextOrdinaryMarkers = new List<PersonalPickupMarker>();
            var seenPickups = new HashSet<int>();
            var previousOrdinaryIds = new HashSet<int>(_markers.Select(x => x.InstanceId));
            var ordinaryCount = 0;
            foreach (var pickup in InstanceTracker.GetInstancesList<GenericPickupController>())
            {
                if (pickup == null) continue;
                var instanceId = pickup.GetInstanceID();
                if (!TryResolveOrdinaryMarkerTarget(pickup, out var worldPosition, out var invalidReason)
                    || (hasSweepCameraPosition && !TryResolvePresentationDistance(sweepCameraPosition, worldPosition, out _, out invalidReason)))
                {
                    ReleaseGate(instanceId);
                    _markerRegistry.Remove(PersonalMarkerKind.OrdinaryPickup, instanceId);
                    _ordinaryRenderLogged.Remove(instanceId);
                    if (previousOrdinaryIds.Contains(instanceId)) LogOrdinaryCleanup(instanceId, invalidReason);
                    continue;
                }
                if (!_upstream.IsShareable(pickup)) continue;
                seenPickups.Add(instanceId);

                var collected = localMasters.Select(master => _upstream.HasCollected(pickup, master)).ToArray();
                var collectedLocalCount = collected.Count(value => value);
                var hide = LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual(
                    _config.PersonalPickupVisibilityRepairEnabled.Value,
                    _upstream.HideCollectedOrbsEnabled,
                    collected.Length,
                    collectedLocalCount);

                if (hide)
                {
                    if (!_gates.TryGetValue(instanceId, out var gate) || gate == null)
                    {
                        // Reset the exact upstream subtree first so our captured restore state is not poisoned by its old partial hide.
                        NormalizeUpstreamForGate(pickup);
                        gate = pickup.gameObject.GetComponent<LocalPickupPresentationGate>() ?? pickup.gameObject.AddComponent<LocalPickupPresentationGate>();
                        _gates[instanceId] = gate;
                    }
                    gate.ApplyHidden();
                }
                else
                {
                    ReleaseGate(instanceId);
                }
                LogLocalVisualGateTransition(pickup, collectedLocalCount, collected.Length, hide);

                var lifetime = MarkerLifetimePolicy.FromTemporaryFlag(pickup.pickup.isTempItem);
                if (_config.PersonalMarkersEnabled.Value
                    && MarkerLifetimePolicy.IsMarkerEligible(lifetime, _config.ShareTemporaryItems.Value)
                    && ordinaryCount < MarkerPresentationPolicy.MaxOrdinaryMarkers
                    && ShouldShowMarker(localStates, collected))
                {
                    var label = MarkerPresentationPolicy.NormalizeLabel(PickupLabel(pickup, markerLanguage), MarkerTextLocalization.FallbackSharedPickup(markerLanguage));
                    var metadata = ResolvePickupMarkerMetadata(pickup.pickup.pickupIndex);
                    var transition = _markerRegistry.MarkPending(PersonalMarkerKind.OrdinaryPickup, instanceId, label);
                    if (transition != PersonalMarkerTransition.CapacityRejected)
                    {
                        nextOrdinaryMarkers.Add(new PersonalPickupMarker
                        {
                            InstanceId = instanceId,
                            Pickup = pickup,
                            Label = label,
                            ItemSemanticKey = PickupSemanticKey(pickup),
                            ClassName = metadata.ClassName,
                            Kind = metadata.Kind,
                            TextColor = metadata.TextColor,
                            Lifetime = lifetime,
                        });
                        ordinaryCount++;
                    }
                }
            }

            foreach (var stale in _gates.Keys.Where(x => !seenPickups.Contains(x)).ToArray()) ReleaseGate(stale);

            var nextCommandMarkers = new List<PersonalCommandMarker>();
            var seenCommandCandidateIds = new HashSet<int>();
            _commandRemovalReasons.Clear();
            var previousCommandIds = new HashSet<int>(_commandMarkers.Select(x => x.InstanceId));
            var commandCount = 0;
            if (_upstream.ShareCommandPicksEnabled)
            {
                foreach (var picker in InstanceTracker.GetInstancesList<PickupPickerController>())
                {
                    if (picker == null) continue;
                    var instanceId = picker.GetInstanceID();
                    var isCommand = _upstream.IsCommandCube(picker);
                    if (!isCommand)
                    {
                        ReleaseCommandGate(instanceId, "classifier-false");
                        if (previousCommandIds.Contains(instanceId)) _commandRemovalReasons[instanceId] = "classifier-false";
                        continue;
                    }

                    seenCommandCandidateIds.Add(instanceId);
                    var localPickedStates = new bool?[localMasters.Length];
                    var anyLocalPending = false;
                    var allLocalStateResolved = true;
                    for (var i = 0; i < localMasters.Length; i++)
                    {
                        if (!_upstream.TryHasCommandPicked(picker, localMasters[i], out var picked))
                        {
                            localPickedStates[i] = null;
                            allLocalStateResolved = false;
                            continue;
                        }
                        localPickedStates[i] = picked;
                        if (ProjectionPolicy.ShowPersonalMarker(true, localStates[i], picked)) anyLocalPending = true;
                    }

                    var commandPresentation = LocalCommandPresentationPolicy.Evaluate(localPickedStates);
                    ApplyLocalCommandPresentation(picker, commandPresentation);

                    if (!_config.PersonalMarkersEnabled.Value)
                    {
                        if (previousCommandIds.Contains(instanceId)) _commandRemovalReasons[instanceId] = "markers-disabled";
                        continue;
                    }

                    if (!TryResolveCommandMarkerTarget(picker, out var worldPosition, out var invalidReason)
                        || (hasSweepCameraPosition && !TryResolvePresentationDistance(sweepCameraPosition, worldPosition, out _, out invalidReason)))
                    {
                        _markerRegistry.Remove(PersonalMarkerKind.CommandPicker, instanceId);
                        if (previousCommandIds.Contains(instanceId)) LogCommandCleanup(instanceId, invalidReason);
                        continue;
                    }

                    var exactPendingProof = anyLocalPending || allLocalStateResolved;
                    var shouldTrack = commandCount < MarkerPresentationPolicy.MaxCommandMarkers
                                      && MarkerPresentationPolicy.ShouldTrackCommandMarker(
                                          _config.PersonalMarkersEnabled.Value,
                                          _upstream.IsIndividualMode,
                                          _upstream.ShareCommandPicksEnabled,
                                          classifierIsCommand: true,
                                          exactLocalStateResolved: exactPendingProof,
                                          anyLocalPending: anyLocalPending);
                    if (!shouldTrack)
                    {
                        if (previousCommandIds.Contains(instanceId))
                        {
                            _commandRemovalReasons[instanceId] = allLocalStateResolved ? "local-completed" : "local-state-unresolved";
                        }
                        continue;
                    }

                    var metadata = ResolveCommandMarkerMetadata(
                        picker,
                        markerLanguage,
                        out var optionSource,
                        out var resolvedOptionCount,
                        out var sourceDisagreement,
                        out var shareability);
                    var commandLifetime = MarkerLifetimeKind.Unknown;
                    var exactLifetimeOptionCount = 0;
                    var unresolvedLifetimeOptionCount = 0;
                    _upstream.TryGetCommandChoiceLifetime(
                        picker,
                        out commandLifetime,
                        out exactLifetimeOptionCount,
                        out unresolvedLifetimeOptionCount);
                    var label = MarkerPresentationPolicy.NormalizeLabel(metadata.Label, MarkerTextLocalization.FallbackCommandChoice(markerLanguage));
                    if (commandLifetime == MarkerLifetimeKind.Unknown && _commandLifetimeUnknownLogged.Add(instanceId))
                    {
                        LogInfo("ISF_COMMAND_LIFETIME pickerInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture)
                            + " lifetime=Unknown exactNestedAvailable=" + exactLifetimeOptionCount.ToString(CultureInfo.InvariantCulture)
                            + " unresolvedAvailable=" + unresolvedLifetimeOptionCount.ToString(CultureInfo.InvariantCulture)
                            + " assertion=none");
                    }
                    if (sourceDisagreement && _commandOptionDisagreementLogged.Add(instanceId))
                    {
                        LogInfo("ISF_COMMAND_OPTION_SOURCE_DISAGREEMENT pickerInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture)
                            + " nestedWins=True optionSource=" + optionSource);
                    }

                    if (!MarkerLifetimePolicy.IsMarkerEligible(commandLifetime, _config.ShareTemporaryItems.Value))
                    {
                        _commandShareabilityDiagnosticState[instanceId] = "temporary-sharing-disabled";
                        if (previousCommandIds.Contains(instanceId)) _commandRemovalReasons[instanceId] = "temporary-sharing-disabled";
                        continue;
                    }

                    if (!shareability.MarkerEligible)
                    {
                        var diagnosticState = shareability.DiagnosticToken + ":" + shareability.FilterReason;
                        if (!_commandShareabilityDiagnosticState.TryGetValue(instanceId, out var previousDiagnosticState)
                            || !string.Equals(previousDiagnosticState, diagnosticState, StringComparison.Ordinal))
                        {
                            _commandShareabilityDiagnosticState[instanceId] = diagnosticState;
                            LogInfo("ISF_COMMAND_MARKER filtered pickerInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture)
                                + " optionSource=" + optionSource
                                + " resolvedOptionCount=" + resolvedOptionCount.ToString(CultureInfo.InvariantCulture)
                                + " class=" + metadata.ClassName
                                + " label=" + label
                                + " color=" + ColorEvidence(metadata.TextColor)
                                + " shareability=" + shareability.DiagnosticToken
                                + " reason=" + shareability.FilterReason);
                        }
                        if (previousCommandIds.Contains(instanceId)) _commandRemovalReasons[instanceId] = shareability.FilterReason;
                        continue;
                    }

                    _commandShareabilityDiagnosticState[instanceId] = shareability.DiagnosticToken + ":eligible";
                    var transition = _markerRegistry.MarkPending(PersonalMarkerKind.CommandPicker, instanceId, label);
                    if (transition == PersonalMarkerTransition.CapacityRejected) continue;
                    nextCommandMarkers.Add(new PersonalCommandMarker
                    {
                        InstanceId = instanceId,
                        Picker = picker,
                        Label = label,
                        ItemSemanticKey = "COMMAND:" + instanceId.ToString(CultureInfo.InvariantCulture),
                        ClassName = metadata.ClassName,
                        Kind = metadata.Kind,
                        TextColor = metadata.TextColor,
                        Lifetime = commandLifetime,
                    });
                    commandCount++;
                    if (transition == PersonalMarkerTransition.Added || transition == PersonalMarkerTransition.Updated)
                    {
                        LogInfo("ISF_COMMAND_MARKER pending pickerInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture)
                            + " localPending=True providerHasState=" + _upstream.HasPickerProviderState(picker)
                            + " discovery=InstanceTracker"
                            + " optionSource=" + optionSource
                            + " resolvedOptionCount=" + resolvedOptionCount.ToString(CultureInfo.InvariantCulture)
                            + " class=" + metadata.ClassName
                            + " label=" + label
                            + " color=" + ColorEvidence(metadata.TextColor)
                            + " style=" + MarkerPresentationPolicy.NativeHudStyleToken
                            + " renderer=canvas-tmp-ui-graphic"
                            + " fontSize=" + MarkerPresentationPolicy.BuildNativeHudFontSize(Screen.height).ToString(CultureInfo.InvariantCulture)
                            + " shareability=" + shareability.DiagnosticToken
                            + " exactClass=" + metadata.ExactClass
                            + " lifetime=" + commandLifetime
                            + " lifetimeExactNested=" + exactLifetimeOptionCount.ToString(CultureInfo.InvariantCulture)
                            + " lifetimeUnresolved=" + unresolvedLifetimeOptionCount.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            foreach (var staleGate in _commandGates.Keys.Where(x => !seenCommandCandidateIds.Contains(x)).ToArray())
                ReleaseCommandGate(staleGate, "picker-destroyed-stale-or-sharing-disabled");

            foreach (var staleDiagnostic in _commandShareabilityDiagnosticState.Keys.Where(x => !seenCommandCandidateIds.Contains(x)).ToArray())
                _commandShareabilityDiagnosticState.Remove(staleDiagnostic);
            _commandLifetimeUnknownLogged.RemoveWhere(x => !seenCommandCandidateIds.Contains(x));

            var removed = _markerRegistry.EndSweep();
            foreach (var descriptor in removed)
            {
                if (descriptor.Identity.Kind == PersonalMarkerKind.OrdinaryPickup)
                {
                    _ordinaryRenderLogged.Remove(descriptor.Identity.InstanceId);
                    continue;
                }

                var id = descriptor.Identity.InstanceId;
                var reason = _commandRemovalReasons.TryGetValue(id, out var exactReason) ? exactReason : "destroyed-or-untracked";
                LogCommandCleanup(id, reason);
            }

            _markers.Clear();
            _markers.AddRange(nextOrdinaryMarkers);
            _commandMarkers.Clear();
            _commandMarkers.AddRange(nextCommandMarkers);
        }

        private void OnMarkerRendered(MarkerRenderDiagnostic diagnostic)
        {
            var identity = diagnostic.Input.Identity;
            var placement = diagnostic.Placement;
            var state = MarkerRuntimeHotPathPolicy.BuildHudDiagnosticState(
                placement.Mode,
                placement.Edge,
                placement.LaneSlot,
                placement.RailSlot,
                placement.HudRelocated,
                placement.MessageHudRelocated,
                placement.CollisionRelocated,
                diagnostic.UsedMeasurementFallback,
                diagnostic.LabelPreferredWidth,
                diagnostic.Footprint.Width);
            var projectionStateChanged = !_markerProjectionDiagnosticState.TryGetValue(diagnostic.ClusterKey, out var previousState)
                || !previousState.Equals(state);
            if (projectionStateChanged) _markerProjectionDiagnosticState[diagnostic.ClusterKey] = state;

            var firstOrdinaryRender = identity.Kind == PersonalMarkerKind.OrdinaryPickup
                && _ordinaryRenderLogged.Add(identity.InstanceId);
            if (!projectionStateChanged && !firstOrdinaryRender) return;

            var mode = placement.Mode == MarkerHudMode.OnScreenWorldAnchor ? "onscreen" : "edge";
            var edge = FormatEdge(placement.Edge);
            if (projectionStateChanged)
            {
                var rect = placement.FinalRect;
                LogInfo("ISF_MARKER_HUD clusterKey=" + diagnostic.ClusterKey.ToString(CultureInfo.InvariantCulture)
                    + " fingerprint=" + diagnostic.MemberFingerprint
                    + " total=" + diagnostic.ClusterTotal.ToString(CultureInfo.InvariantCulture)
                    + " representative=" + identity
                    + " mode=" + mode
                    + " semantic=" + diagnostic.SemanticText.Replace("\n", " | ")
                    + " edge=" + edge
                    + " stackSlot=" + placement.StackSlot.ToString(CultureInfo.InvariantCulture)
                    + " laneSlot=" + placement.LaneSlot.ToString(CultureInfo.InvariantCulture)
                    + " railSlot=" + placement.RailSlot.ToString(CultureInfo.InvariantCulture)
                    + " rect=" + FormatMarkerRect(rect)
                    + " hudRelocated=" + placement.HudRelocated
                    + " messageHudRelocated=" + placement.MessageHudRelocated
                    + " collisionRelocated=" + placement.CollisionRelocated
                    + " anchorDisplacement=" + MarkerHudNavigationPolicy.OnScreenAnchorDisplacement(diagnostic.SourceProjection, placement).ToString("F1", CultureInfo.InvariantCulture)
                    + " anchorDisplacementBound=" + MarkerHudNavigationPolicy.GetMaxOnScreenAnchorDisplacement(diagnostic.Footprint, Screen.width, Screen.height).ToString("F1", CultureInfo.InvariantCulture)
                    + " labelPreferredWidth=" + diagnostic.LabelPreferredWidth.ToString("F1", CultureInfo.InvariantCulture)
                    + " footprintWidth=" + diagnostic.Footprint.Width.ToString("F1", CultureInfo.InvariantCulture)
                    + " measurementFallback=" + diagnostic.UsedMeasurementFallback
                    + " semanticModel=world-space-cluster"
                    + " renderer=canvas-tmp-ui-graphic");
            }

            if (firstOrdinaryRender)
            {
                LogInfo("ISF_MARKER_RENDER ordinary pickupInstanceId=" + identity.InstanceId.ToString(CultureInfo.InvariantCulture)
                    + " clusterKey=" + diagnostic.ClusterKey.ToString(CultureInfo.InvariantCulture)
                    + " fingerprint=" + diagnostic.MemberFingerprint
                    + " total=" + diagnostic.ClusterTotal.ToString(CultureInfo.InvariantCulture)
                    + " indicatorSource=" + MarkerPresentationPolicy.IndicatorAssetSourceToken
                    + " style=" + MarkerPresentationPolicy.NativeHudStyleToken
                    + " renderer=canvas-tmp-ui-graphic"
                    + " mode=" + mode
                    + " edge=" + edge);
            }
        }

        private static string FormatEdge(MarkerHudEdge edge)
        {
            switch (edge)
            {
                case MarkerHudEdge.Left: return "left";
                case MarkerHudEdge.Right: return "right";
                case MarkerHudEdge.Top: return "top";
                case MarkerHudEdge.Bottom: return "bottom";
                default: return "none";
            }
        }

        private static bool SameDiagnosticRect(MarkerHudRect left, MarkerHudRect right)
            => Math.Abs(left.Left - right.Left) < 0.5f
               && Math.Abs(left.Right - right.Right) < 0.5f
               && Math.Abs(left.Bottom - right.Bottom) < 0.5f
               && Math.Abs(left.Top - right.Top) < 0.5f;

        private static string FormatMarkerRect(MarkerHudRect rect)
            => rect.Left.ToString("F1", CultureInfo.InvariantCulture) + ","
               + rect.Bottom.ToString("F1", CultureInfo.InvariantCulture) + ","
               + rect.Right.ToString("F1", CultureInfo.InvariantCulture) + ","
               + rect.Top.ToString("F1", CultureInfo.InvariantCulture);

        private static bool ShouldShowMarker(ParticipantState[] localStates, bool[] collected)
        {
            for (var i = 0; i < localStates.Length; i++)
            {
                if (ProjectionPolicy.ShowPersonalMarker(true, localStates[i], collected[i])) return true;
            }
            return false;
        }

        private CharacterMaster[] GetLocalMastersSnapshot()
        {
            if (_localMastersFrame == Time.frameCount) return _cachedLocalMasters;
            _localMastersFrame = Time.frameCount;
            _cachedLocalMasters = LocalParticipantResolver.GetLocalMasters().Where(x => x != null).ToArray();
            return _cachedLocalMasters;
        }

        private void NormalizeUpstreamForGate(GenericPickupController pickup)
        {
            try
            {
                _upstreamNormalizationDepth++;
                _upstream.NormalizeUpstreamVisualSubtree(pickup);
            }
            finally
            {
                _upstreamNormalizationDepth--;
            }
        }

        private void ReleaseGate(int instanceId)
        {
            if (!_gates.TryGetValue(instanceId, out var gate)) return;
            if (gate != null)
            {
                gate.Restore();
                UnityEngine.Object.Destroy(gate);
            }
            _gates.Remove(instanceId);
        }

        private void ApplyLocalCommandPresentation(PickupPickerController picker, LocalCommandPresentationDecision decision)
        {
            var instanceId = picker.GetInstanceID();
            if (decision.SuppressWorldPresentation)
            {
                if (!_commandGates.TryGetValue(instanceId, out var gate) || gate == null)
                {
                    gate = picker.gameObject.GetComponent<LocalCommandPresentationGate>() ?? picker.gameObject.AddComponent<LocalCommandPresentationGate>();
                    _commandGates[instanceId] = gate;
                }
                gate.ApplyHidden();
                LogLocalCommandGateTransition(instanceId, true, "all-local-completed");
                return;
            }

            var reason = !decision.AllLocalStateResolved
                ? "local-state-unresolved"
                : decision.AnyLocalPending ? "local-participant-pending" : "not-all-local-completed";
            ReleaseCommandGate(instanceId, reason);
        }

        private void ReleaseCommandGate(int instanceId, string reason)
        {
            var hadGate = _commandGates.TryGetValue(instanceId, out var gate);
            if (hadGate)
            {
                if (gate != null)
                {
                    gate.Restore();
                    UnityEngine.Object.Destroy(gate);
                }
                _commandGates.Remove(instanceId);
            }

            var hadSuppressedDiagnostic = _localCommandGateDiagnosticState.TryGetValue(instanceId, out var previous) && previous;
            if (hadGate || hadSuppressedDiagnostic) LogLocalCommandGateTransition(instanceId, false, reason);
        }

        private void LogLocalCommandGateTransition(int instanceId, bool visualSuppressed, string reason)
        {
            if (!_config.DiagnosticLogging.Value) return;
            if (_localCommandGateDiagnosticState.TryGetValue(instanceId, out var previous) && previous == visualSuppressed) return;
            _localCommandGateDiagnosticState[instanceId] = visualSuppressed;
            LogInfo("ISF_C20_LOCAL_COMMAND_GATE picker=" + instanceId.ToString(CultureInfo.InvariantCulture)
                + " allLocalCompleted=" + visualSuppressed.ToString().ToLowerInvariant()
                + " visualSuppressed=" + visualSuppressed.ToString().ToLowerInvariant()
                + " reason=" + reason);
        }

        private void LogLocalVisualGateTransition(GenericPickupController pickup, int collectedCount, int localCount, bool visualSuppressed)
        {
            if (!_config.DiagnosticLogging.Value || pickup == null) return;
            var fingerprint = collectedCount.ToString(CultureInfo.InvariantCulture) + "/"
                + localCount.ToString(CultureInfo.InvariantCulture) + ":" + visualSuppressed;
            var instanceId = pickup.GetInstanceID();
            if (_localPickupVisualDiagnosticState.TryGetValue(instanceId, out var previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal)) return;
            _localPickupVisualDiagnosticState[instanceId] = fingerprint;
            LogInfo("ISF_C20_LOCAL_PICKUP_GATE pickup=" + instanceId.ToString(CultureInfo.InvariantCulture)
                + " localMaster=process localCollected=" + collectedCount.ToString(CultureInfo.InvariantCulture)
                + "/" + localCount.ToString(CultureInfo.InvariantCulture)
                + " visualSuppressed=" + visualSuppressed.ToString().ToLowerInvariant()
                + " interactionSuppressed=participant-specific"
                + " action=visual-state reason=" + (visualSuppressed ? "all-local-collected" : "local-participant-still-needs-pickup"));
        }

        private void LogLocalInteractionGateTransition(GenericPickupController pickup, CharacterMaster master, bool collected, bool interactionSuppressed)
        {
            if (!_config.DiagnosticLogging.Value || pickup == null || master == null) return;
            var pickupId = pickup.GetInstanceID();
            var masterId = master.GetInstanceID();
            var key = ((long)(uint)pickupId << 32) | (uint)masterId;
            if (_localPickupInteractionDiagnosticState.TryGetValue(key, out var previous) && previous == interactionSuppressed) return;
            _localPickupInteractionDiagnosticState[key] = interactionSuppressed;
            LogInfo("ISF_C20_LOCAL_PICKUP_GATE pickup=" + pickupId.ToString(CultureInfo.InvariantCulture)
                + " localMaster=masterNetId=" + master.netId.Value.ToString(CultureInfo.InvariantCulture)
                + " localCollected=" + collected.ToString().ToLowerInvariant()
                + " visualSuppressed=" + _gates.ContainsKey(pickupId).ToString().ToLowerInvariant()
                + " interactionSuppressed=" + interactionSuppressed.ToString().ToLowerInvariant()
                + " action=interactability-state reason=" + (interactionSuppressed ? "local-collector-already-collected" : "local-participant-not-collected"));
        }

        private static bool TryGetFiniteCameraPosition(Camera camera, out Vector3 cameraPosition)
        {
            cameraPosition = default;
            try
            {
                cameraPosition = camera.transform.position;
                return MarkerPresentationPolicy.AreCoordinatesFinite(cameraPosition.x, cameraPosition.y, cameraPosition.z);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveOrdinaryMarkerTarget(GenericPickupController pickup, out Vector3 worldPosition, out string invalidReason)
        {
            worldPosition = default;
            invalidReason = string.Empty;
            if (pickup == null)
            {
                invalidReason = "destroyed-or-missing";
                return false;
            }

            try
            {
                if (pickup.gameObject == null || !pickup.gameObject.activeInHierarchy)
                {
                    invalidReason = "inactive-or-removed";
                    return false;
                }
                if (PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex) == null)
                {
                    invalidReason = "pickup-def-unresolved";
                    return false;
                }
                worldPosition = pickup.transform.position;
                invalidReason = MarkerPresentationPolicy.ValidateWorldPosition(worldPosition.x, worldPosition.y, worldPosition.z);
                return string.IsNullOrEmpty(invalidReason);
            }
            catch
            {
                invalidReason = "target-resolution-failed";
                return false;
            }
        }

        private static bool TryResolveCommandMarkerTarget(PickupPickerController picker, out Vector3 worldPosition, out string invalidReason)
        {
            worldPosition = default;
            invalidReason = string.Empty;
            if (picker == null)
            {
                invalidReason = "destroyed-or-missing";
                return false;
            }

            try
            {
                if (picker.gameObject == null || !picker.gameObject.activeInHierarchy)
                {
                    invalidReason = "inactive-or-removed";
                    return false;
                }
                worldPosition = picker.transform.position;
                invalidReason = MarkerPresentationPolicy.ValidateWorldPosition(worldPosition.x, worldPosition.y, worldPosition.z);
                return string.IsNullOrEmpty(invalidReason);
            }
            catch
            {
                invalidReason = "target-resolution-failed";
                return false;
            }
        }

        private static bool TryResolvePresentationDistance(Vector3 cameraPosition, Vector3 worldPosition, out int roundedDistanceMeters, out string invalidReason)
        {
            roundedDistanceMeters = 0;
            invalidReason = string.Empty;
            var distance = Vector3.Distance(cameraPosition, worldPosition);
            invalidReason = MarkerPresentationPolicy.ValidatePresentationDistance(distance);
            if (!string.IsNullOrEmpty(invalidReason)) return false;
            roundedDistanceMeters = Mathf.Max(0, Mathf.RoundToInt(distance));
            return true;
        }

        private void RemoveOrdinaryMarkerNow(PersonalPickupMarker marker, string reason)
        {
            _markers.Remove(marker);
            _markerRegistry.Remove(PersonalMarkerKind.OrdinaryPickup, marker.InstanceId);
            _ordinaryRenderLogged.Remove(marker.InstanceId);
            ReleaseGate(marker.InstanceId);
            LogOrdinaryCleanup(marker.InstanceId, reason);
        }

        private void RemoveCommandMarkerNow(PersonalCommandMarker marker, string reason)
        {
            _commandMarkers.Remove(marker);
            _markerRegistry.Remove(PersonalMarkerKind.CommandPicker, marker.InstanceId);
            _commandShareabilityDiagnosticState.Remove(marker.InstanceId);
            LogCommandCleanup(marker.InstanceId, reason);
        }

        private void LogOrdinaryCleanup(int instanceId, string reason)
            => LogInfo("ISF_MARKER_CLEANUP ordinary pickupInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason);


        private static Sprite? ResolvePickupIcon(GenericPickupController pickup)
        {
            try
            {
                var def = PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
                return def != null ? def.iconSprite : null;
            }
            catch
            {
                return null;
            }
        }

        private static string PickupSemanticKey(GenericPickupController pickup)
        {
            try
            {
                var pickupIndex = pickup.pickup.pickupIndex;
                var def = PickupCatalog.GetPickupDef(pickupIndex);
                if (def != null && !string.IsNullOrWhiteSpace(def.nameToken)) return "PICKUP:" + def.nameToken;
                return "PICKUP_INDEX:" + pickupIndex.ToString();
            }
            catch
            {
                return "PICKUP_INSTANCE:" + pickup.GetInstanceID().ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string PickupLabel(GenericPickupController pickup, MarkerLanguage language)
        {
            try
            {
                var def = PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
                if (def != null && !string.IsNullOrEmpty(def.nameToken))
                {
                    var localized = Language.GetString(def.nameToken);
                    if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, def.nameToken, StringComparison.Ordinal)) return localized;
                }
            }
            catch { }
            return MarkerTextLocalization.FallbackSharedPickup(language);
        }

        private MarkerRuntimeMetadata ResolveCommandMarkerMetadata(
            PickupPickerController picker,
            MarkerLanguage language,
            out string optionSource,
            out int resolvedOptionCount,
            out bool sourceDisagreement,
            out CommandShareabilityDecision shareability)
        {
            optionSource = CommandOptionSourcePolicy.UnresolvedSourceToken;
            resolvedOptionCount = 0;
            sourceDisagreement = false;
            shareability = CommandShareabilityPolicy.Evaluate(Array.Empty<bool?>());
            if (!_upstream.TryGetCommandChoicePickupIndexes(
                    picker,
                    out var pickupIndexes,
                    out optionSource,
                    out var exactSource,
                    out sourceDisagreement)
                || pickupIndexes.Length == 0)
            {
                var fallback = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(Array.Empty<MarkerClassKind>(), language);
                return new MarkerRuntimeMetadata(fallback.Kind, MarkerClassPolicy.DiagnosticClassName(fallback.Kind), fallback.Label, Color.white, false);
            }

            resolvedOptionCount = pickupIndexes.Length;
            var resolved = new List<MarkerRuntimeMetadata>(pickupIndexes.Length);
            var upstreamShareability = new List<bool?>(pickupIndexes.Length);
            foreach (var pickupIndex in pickupIndexes)
            {
                var def = PickupCatalog.GetPickupDef(pickupIndex);
                if (def == null)
                {
                    resolved.Add(UnknownPickupMetadata());
                    upstreamShareability.Add(null);
                    continue;
                }

                resolved.Add(ResolvePickupMarkerMetadata(def));
                upstreamShareability.Add(_upstream.TryIsShareable(def, out var isShareable) ? isShareable : (bool?)null);
            }
            shareability = exactSource
                ? CommandShareabilityPolicy.Evaluate(upstreamShareability)
                : CommandShareabilityPolicy.Evaluate(Array.Empty<bool?>());
            var classPresentation = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(resolved.Select(x => x.Kind), language);
            if (!classPresentation.ExactClass)
            {
                var fallbackColor = classPresentation.Kind == MarkerClassKind.Other && resolved.Count > 0
                    ? resolved[0].TextColor
                    : Color.white;
                return new MarkerRuntimeMetadata(classPresentation.Kind, MarkerClassPolicy.DiagnosticClassName(classPresentation.Kind), classPresentation.Label, fallbackColor, false);
            }

            var matching = resolved.Where(x => x.Kind == classPresentation.Kind).ToArray();
            var color = matching.Length > 0 ? matching[0].TextColor : Color.white;
            return new MarkerRuntimeMetadata(
                classPresentation.Kind,
                MarkerClassPolicy.DiagnosticClassName(classPresentation.Kind),
                classPresentation.Label,
                color,
                exactSource);
        }

        private static MarkerRuntimeMetadata ResolvePickupMarkerMetadata(PickupIndex pickupIndex)
        {
            try
            {
                var def = PickupCatalog.GetPickupDef(pickupIndex);
                return def != null ? ResolvePickupMarkerMetadata(def) : UnknownPickupMetadata();
            }
            catch
            {
                return UnknownPickupMetadata();
            }
        }

        private static MarkerRuntimeMetadata ResolvePickupMarkerMetadata(PickupDef def)
        {
            try
            {
                var isEquipment = def.equipmentIndex != EquipmentIndex.None;
                var lunarEquipment = false;
                if (isEquipment)
                {
                    var equipmentDef = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                    lunarEquipment = equipmentDef != null && equipmentDef.isLunar;
                }

                var kind = MarkerClassPolicy.Classify(def.itemTier.ToString(), isEquipment, lunarEquipment);
                return new MarkerRuntimeMetadata(
                    kind,
                    MarkerClassPolicy.DiagnosticClassName(kind),
                    string.Empty,
                    SanitizeMarkerColor(def.baseColor),
                    kind != MarkerClassKind.Unknown);
            }
            catch
            {
                return UnknownPickupMetadata();
            }
        }

        private static MarkerRuntimeMetadata UnknownPickupMetadata()
            => new MarkerRuntimeMetadata(MarkerClassKind.Unknown, MarkerClassPolicy.DiagnosticClassName(MarkerClassKind.Unknown), string.Empty, Color.white, false);

        private static Color SanitizeMarkerColor(Color color)
        {
            if (float.IsNaN(color.r) || float.IsInfinity(color.r)
                || float.IsNaN(color.g) || float.IsInfinity(color.g)
                || float.IsNaN(color.b) || float.IsInfinity(color.b)
                || color.a <= 0.01f) return Color.white;
            color.a = 1f;
            return color;
        }

        private static string ColorEvidence(Color color)
            => "#" + ColorUtility.ToHtmlStringRGB(SanitizeMarkerColor(color));

        private static MarkerLanguage CurrentMarkerLanguage()
        {
            try { return MarkerTextLocalization.ResolveLanguage(Language.currentLanguageName); }
            catch { return MarkerLanguage.English; }
        }

        private void LogCommandCleanup(int instanceId, string reason)
            => LogInfo("ISF_COMMAND_MARKER cleanup pickerInstanceId=" + instanceId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason);

        private Camera? GetPresentationCamera()
        {
            var now = Time.unscaledTime;
            if (_presentationCamera != null
                && _presentationCamera.gameObject != null
                && _presentationCamera.gameObject.activeInHierarchy
                && now < _nextPresentationCameraRefresh)
                return _presentationCamera;

            _nextPresentationCameraRefresh = now + 2.0f;
            try { _presentationCamera = Camera.main; }
            catch { _presentationCamera = null; }
            return _presentationCamera;
        }

        private void MaybeEmitPerformanceSummary(int markerCount)
        {
            if (!_config.DiagnosticLogging.Value) return;
            var now = Time.unscaledTime;
            if (now < _nextPerformanceSummary) return;
            _nextPerformanceSummary = now + 5f;
            EmitPerformanceSummary("periodic", markerCount, force: false);
        }

        private void EmitPerformanceSummary(string reason, int markerCount, bool force)
        {
            if (!_config.DiagnosticLogging.Value && !force) return;
            var snapshot = _performance.Snapshot();
            _log.LogInfo("[ItemShareFix] ISF_MARKER_PERF_SUMMARY reason=" + reason
                + " markers=" + markerCount.ToString(CultureInfo.InvariantCulture)
                + " updates=" + snapshot.UnityUpdateCalls.ToString(CultureInfo.InvariantCulture)
                + " renderFrames=" + snapshot.RenderFrameCalls.ToString(CultureInfo.InvariantCulture)
                + " fullSolves=" + snapshot.FullPlacementSolves.ToString(CultureInfo.InvariantCulture)
                + " singleFast=" + snapshot.SingleMarkerFastPathCalls.ToString(CultureInfo.InvariantCulture)
                + " globalHudDiscovery=" + snapshot.GlobalHudDiscoveries.ToString(CultureInfo.InvariantCulture)
                + " tmpMeasures=" + snapshot.TmpPreferredMeasurements.ToString(CultureInfo.InvariantCulture)
                + " uiWrites=" + snapshot.UiLayoutWrites.ToString(CultureInfo.InvariantCulture)
                + " diagnostics=" + snapshot.DiagnosticRecords.ToString(CultureInfo.InvariantCulture)
                + " heavyMsTotal=" + snapshot.HeavySolveMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + " heavyMsMax=" + snapshot.MaxHeavySolveMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        private void LogInfo(string message)
        {
            if (!_config.DiagnosticLogging.Value) return;
            _performance.RecordDiagnosticRecord();
            _log.LogInfo("[ItemShareFix] " + message);
        }

        private static int CurrentStageToken()
        {
            var run = Run.instance;
            if (run == null) return -1;
            var value = ParticipantIdentityResolver.GetMember(run, "stageClearCount");
            try { return value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0; }
            catch { return 0; }
        }
    }
}
