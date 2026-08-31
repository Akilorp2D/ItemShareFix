using System;
using System.Collections.Generic;
using ItemShareFix.Core;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemShareFix
{
    /// <summary>
    /// Local presentation probe. Cached RoR2 UI references are evaluated cheaply every frame, while expensive
    /// Resources.FindObjectsOfTypeAll discovery is lifecycle/urgent-trigger driven with a rare bounded fallback.
    /// </summary>
    internal sealed class LocalHudPresentationProbe
    {
        internal static readonly string[] BlockingModalTypeNames =
        {
            BlockingModalLifecyclePolicy.PauseScreenControllerTypeName,
            BlockingModalLifecyclePolicy.SimpleDialogBoxTypeName,
            BlockingModalLifecyclePolicy.PickupPickerPanelTypeName,
        };

        private static readonly string[] MessageHudTypeNames =
        {
            "RoR2.UI.ChatBox",
            "RoR2.UI.ChatBoxController",
            "RoR2.UI.HUDChat",
        };

        private sealed class MessageHudCandidate
        {
            public MessageHudCandidate(RectTransform root)
            {
                Root = root;
                Texts = root.GetComponentsInChildren<TMP_Text>(true);
                Selectables = root.GetComponentsInChildren<Selectable>(true);
            }

            public RectTransform Root { get; }
            public TMP_Text[] Texts { get; }
            public Selectable[] Selectables { get; }
            public Vector3[] Corners { get; } = new Vector3[4];
        }

        private readonly Type[] _blockingModalTypes;
        private readonly Type[] _messageHudTypes;
        private readonly Type? _hudType;
        private readonly MarkerRuntimePerformanceCounters _performance;
        private readonly List<Component> _blockingModalComponents = new List<Component>();
        private readonly HashSet<int> _blockingModalIds = new HashSet<int>();
        private readonly List<MessageHudCandidate> _messageHudRoots = new List<MessageHudCandidate>();
        private readonly HashSet<int> _messageRootIds = new HashSet<int>();
        private float _nextBlockingDiscovery;
        private float _nextMessageDiscovery;
        private bool _blockingLifecycleInvalidated = true;
        private bool _messageLifecycleInvalidated = true;
        private bool _lastPauseSignal;

        public LocalHudPresentationProbe(MarkerRuntimePerformanceCounters performance)
        {
            _performance = performance ?? throw new ArgumentNullException(nameof(performance));
            var assembly = typeof(Run).Assembly;
            _blockingModalTypes = ResolveComponentTypes(assembly, BlockingModalTypeNames);
            _messageHudTypes = ResolveComponentTypes(assembly, MessageHudTypeNames);
            var hudType = assembly.GetType("RoR2.UI.HUD", throwOnError: false);
            _hudType = hudType != null && typeof(Component).IsAssignableFrom(hudType) ? hudType : null;
        }

        public void InvalidateLifecycle()
        {
            _blockingLifecycleInvalidated = true;
            _messageLifecycleInvalidated = true;
            _nextBlockingDiscovery = 0f;
            _nextMessageDiscovery = 0f;
            _blockingModalComponents.Clear();
            _blockingModalIds.Clear();
            _messageHudRoots.Clear();
            _messageRootIds.Clear();
        }

        /// <summary>
        /// Immediate path: a Harmony lifecycle hook gives us the exact modal component that just became live.
        /// Seeding that component is O(1)/small-cache work and deliberately does not trigger Resources.FindObjectsOfTypeAll.
        /// </summary>
        public void ObserveBlockingModalLifecycle(Component? component)
        {
            if (component == null || component.gameObject == null) return;
            var knownType = IsKnownBlockingModalType(component.GetType());
            var sceneValid = component.gameObject.scene.IsValid();
            if (!BlockingModalLifecyclePolicy.ShouldSeedObservedCandidate(knownType, sceneValid)) return;

            var id = component.GetInstanceID();
            if (BlockingModalLifecyclePolicy.ShouldAddObservedInstance(_blockingModalIds.Contains(id)))
            {
                _blockingModalIds.Add(id);
                _blockingModalComponents.Add(component);
            }

            // The observed exact component is now sufficient for the next normal presentation update.
            // Keep the rare fallback for genuinely missing/stale cache cases, but do not run it because of this transition.
            _blockingLifecycleInvalidated = false;
            _nextBlockingDiscovery = Time.unscaledTime + MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds;
        }

        public bool TryGetBlockingModal(out string reason)
        {
            RefreshBlockingModalCandidates();
            for (var i = 0; i < _blockingModalComponents.Count; i++)
            {
                var component = _blockingModalComponents[i];
                if (!IsLiveActiveComponent(component)) continue;
                var typeName = component.GetType().Name;
                reason = typeName.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "pause-menu"
                    : typeName.IndexOf("PickupPickerPanel", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "command-picker-modal"
                        : "blocking-modal";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public bool TryGetVisibleMessageHudRect(out MarkerHudRect rect)
        {
            RefreshMessageHudCandidates();
            var found = false;
            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var bottom = float.PositiveInfinity;
            var top = float.NegativeInfinity;

            for (var i = 0; i < _messageHudRoots.Count; i++)
            {
                var candidate = _messageHudRoots[i];
                var root = candidate.Root;
                if (root == null || root.gameObject == null || !root.gameObject.activeInHierarchy) continue;
                if (!root.gameObject.scene.IsValid()) continue;
                if (!HasVisibleMessageContent(candidate)) continue;
                if (!TryBuildScreenRect(root, candidate.Corners, out var candidateRect)) continue;
                found = true;
                left = Mathf.Min(left, candidateRect.Left);
                right = Mathf.Max(right, candidateRect.Right);
                bottom = Mathf.Min(bottom, candidateRect.Bottom);
                top = Mathf.Max(top, candidateRect.Top);
            }

            if (!found)
            {
                rect = default;
                return false;
            }

            rect = FromEdges(left, right, bottom, top);
            return rect.Width > 1f && rect.Height > 1f;
        }

        private void RefreshBlockingModalCandidates()
        {
            var pruned = PruneBlockingModalCandidates();
            var pauseSignal = Time.timeScale <= 0.001f;
            var urgent = pauseSignal != _lastPauseSignal;
            _lastPauseSignal = pauseSignal;
            urgent |= pruned;

            var now = Time.unscaledTime;
            if (!MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(
                    _blockingLifecycleInvalidated,
                    urgent,
                    _blockingModalComponents.Count == 0,
                    now,
                    _nextBlockingDiscovery)) return;

            _blockingLifecycleInvalidated = false;
            _nextBlockingDiscovery = now + MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds;
            _blockingModalComponents.Clear();
            _blockingModalIds.Clear();
            _performance.RecordGlobalHudDiscovery();
            for (var i = 0; i < _blockingModalTypes.Length; i++)
            {
                var type = _blockingModalTypes[i];
                try
                {
                    foreach (var instance in Resources.FindObjectsOfTypeAll(type))
                    {
                        if (instance is Component component && component.gameObject != null && component.gameObject.scene.IsValid())
                            AddBlockingModalCandidate(component);
                    }
                }
                catch
                {
                    // Presentation probe is fail-open: unavailable UI reflection must not alter gameplay/state.
                }
            }
        }

        private void RefreshMessageHudCandidates()
        {
            var pruned = PruneMessageHudCandidates();
            var now = Time.unscaledTime;
            if (!MarkerFramePipelinePolicy.ShouldRunGlobalUiDiscovery(
                    _messageLifecycleInvalidated,
                    pruned,
                    _messageHudRoots.Count == 0,
                    now,
                    _nextMessageDiscovery)) return;

            _messageLifecycleInvalidated = false;
            _nextMessageDiscovery = now + MarkerFramePipelinePolicy.MessageHudFallbackDiscoverySeconds;
            _messageHudRoots.Clear();
            _messageRootIds.Clear();
            _performance.RecordGlobalHudDiscovery();

            for (var i = 0; i < _messageHudTypes.Length; i++)
            {
                var type = _messageHudTypes[i];
                try
                {
                    foreach (var instance in Resources.FindObjectsOfTypeAll(type))
                    {
                        if (!(instance is Component component) || component.gameObject == null || !component.gameObject.scene.IsValid()) continue;
                        AddMessageRoot(component.transform as RectTransform);
                    }
                }
                catch
                {
                    // Continue to rare bounded HUD/name discovery below.
                }
            }

            // Known concrete chat roots are authoritative. The HUD-tree fallback happens only on lifecycle/fallback
            // discovery, never as continuous marker-active polling.
            if (_messageHudRoots.Count > 0 || _hudType == null) return;
            try
            {
                foreach (var instance in Resources.FindObjectsOfTypeAll(_hudType))
                {
                    if (!(instance is Component hud) || hud.gameObject == null || !hud.gameObject.scene.IsValid()) continue;
                    foreach (var candidate in hud.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (!LooksLikeMessageHud(candidate)) continue;
                        AddMessageRoot(candidate);
                    }
                }
            }
            catch
            {
                // Fail-open presentation behavior.
            }
        }

        private void AddBlockingModalCandidate(Component component)
        {
            if (component == null || component.gameObject == null || !component.gameObject.scene.IsValid()) return;
            var id = component.GetInstanceID();
            if (!_blockingModalIds.Add(id)) return;
            _blockingModalComponents.Add(component);
        }

        private bool IsKnownBlockingModalType(Type type)
        {
            for (var i = 0; i < _blockingModalTypes.Length; i++)
            {
                var known = _blockingModalTypes[i];
                if (known == type || known.IsAssignableFrom(type)) return true;
            }
            return false;
        }

        private void AddMessageRoot(RectTransform? root)
        {
            if (root == null || root.gameObject == null || !root.gameObject.scene.IsValid()) return;
            var id = root.GetInstanceID();
            if (!_messageRootIds.Add(id)) return;
            try { _messageHudRoots.Add(new MessageHudCandidate(root)); }
            catch
            {
                // A transient/destroying UI tree is ignored until the next bounded discovery pass.
            }
        }

        private bool PruneBlockingModalCandidates()
        {
            var removed = false;
            for (var i = _blockingModalComponents.Count - 1; i >= 0; i--)
            {
                var component = _blockingModalComponents[i];
                if (component != null && component.gameObject != null) continue;
                try { if (!ReferenceEquals(component, null)) _blockingModalIds.Remove(component.GetInstanceID()); } catch { }
                _blockingModalComponents.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        private bool PruneMessageHudCandidates()
        {
            var removed = false;
            for (var i = _messageHudRoots.Count - 1; i >= 0; i--)
            {
                var root = _messageHudRoots[i].Root;
                if (root != null && root.gameObject != null) continue;
                _messageHudRoots.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        private static Type[] ResolveComponentTypes(System.Reflection.Assembly assembly, IEnumerable<string> names)
        {
            var resolved = new List<Type>();
            foreach (var name in names)
            {
                var type = assembly.GetType(name, throwOnError: false);
                if (type != null && typeof(Component).IsAssignableFrom(type)) resolved.Add(type);
            }
            return resolved.ToArray();
        }

        private static bool LooksLikeMessageHud(RectTransform root)
        {
            if (root == null) return false;
            var name = (root.gameObject.name ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            if (name.Contains("chatbox") || name.Contains("chatfeed") || name.Contains("messagefeed") || name.Contains("chatlog")) return true;

            foreach (var component in root.GetComponents<Component>())
            {
                if (component == null) continue;
                var typeName = component.GetType().Name.ToLowerInvariant();
                if (typeName.Contains("chatbox") || typeName.Contains("chatfeed")) return true;
            }
            return false;
        }

        private static bool IsLiveActiveComponent(Component? component)
        {
            if (component == null || component.gameObject == null || !component.gameObject.scene.IsValid() || !component.gameObject.activeInHierarchy) return false;
            if (component is Behaviour behaviour && !behaviour.enabled) return false;
            return EffectiveCanvasGroupAlpha(component.transform) > 0.04f;
        }

        private static bool HasVisibleMessageContent(MessageHudCandidate candidate)
        {
            var root = candidate.Root;
            var rootAlpha = EffectiveCanvasGroupAlpha(root);
            if (rootAlpha <= 0.04f) return false;

            for (var i = 0; i < candidate.Texts.Length; i++)
            {
                var text = candidate.Texts[i];
                if (text == null || text.gameObject == null || !text.gameObject.activeInHierarchy || !text.enabled) continue;
                if (string.IsNullOrWhiteSpace(text.text)) continue;
                var alpha = rootAlpha * text.color.a;
                try { alpha *= text.canvasRenderer.GetAlpha(); } catch { }
                if (alpha > 0.04f) return true;
            }

            // When the local player has opened chat, protect the input row even before any new text is typed.
            for (var i = 0; i < candidate.Selectables.Length; i++)
            {
                var selectable = candidate.Selectables[i];
                if (selectable == null || selectable.gameObject == null || !selectable.gameObject.activeInHierarchy) continue;
                var typeName = selectable.GetType().Name;
                if (typeName.IndexOf("InputField", StringComparison.OrdinalIgnoreCase) >= 0 && selectable.IsActive()) return true;
            }
            return false;
        }

        private static float EffectiveCanvasGroupAlpha(Transform transform)
        {
            var alpha = 1f;
            var current = transform;
            while (current != null)
            {
                var group = current.GetComponent<CanvasGroup>();
                if (group != null) alpha *= Mathf.Clamp01(group.alpha);
                current = current.parent;
            }
            return alpha;
        }

        private static bool TryBuildScreenRect(RectTransform root, Vector3[] corners, out MarkerHudRect rect)
        {
            rect = default;
            try { root.GetWorldCorners(corners); }
            catch { return false; }

            var canvas = root.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < corners.Length; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                if (!IsFinite(p.x) || !IsFinite(p.y)) return false;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            if (maxX - minX < 8f || maxY - minY < 8f) return false;
            var padding = Mathf.Max(8f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * 12f);
            minX = Mathf.Clamp(minX - padding, 0f, Screen.width);
            maxX = Mathf.Clamp(maxX + padding, 0f, Screen.width);
            minY = Mathf.Clamp(minY - padding, 0f, Screen.height);
            maxY = Mathf.Clamp(maxY + padding, 0f, Screen.height);
            rect = FromEdges(minX, maxX, minY, maxY);
            return rect.Width > 1f && rect.Height > 1f;
        }

        private static MarkerHudRect FromEdges(float left, float right, float bottom, float top)
            => new MarkerHudRect((left + right) * 0.5f, (bottom + top) * 0.5f, Math.Max(0f, right - left), Math.Max(0f, top - bottom));

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
