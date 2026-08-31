using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public readonly struct MarkerExpansionCandidate
    {
        public MarkerExpansionCandidate(long clusterKey, int memberCount, float distanceMeters, float reticleDistancePixels1080)
        {
            ClusterKey = clusterKey;
            MemberCount = Math.Max(0, memberCount);
            DistanceMeters = distanceMeters;
            ReticleDistancePixels1080 = reticleDistancePixels1080;
        }

        public long ClusterKey { get; }
        public int MemberCount { get; }
        public float DistanceMeters { get; }
        public float ReticleDistancePixels1080 { get; }
    }

    /// <summary>
    /// Presentation-only adaptive LOD state. It selects at most one dense semantic cluster and never receives member
    /// identities or world-clustering thresholds, so focus/near hysteresis cannot mutate semantic membership.
    /// </summary>
    public sealed class MarkerAdaptiveLodTracker
    {
        public const int DenseClusterMinimumMembers = 3;
        public const float FocusEnterPixels1080 = 72f;
        public const float FocusExitPixels1080 = 110f;
        public const float NearEnterMeters = 18f;
        public const float NearExitMeters = 22f;
        public const float UniqueNearestMarginMeters = 1.0f;
        public const double FocusAcquireDwellSeconds = 0.12;
        public const double NearAcquireDwellSeconds = 0.25;
        public const double ReleaseDwellSeconds = 0.20;

        private long? _expandedKey;
        private long? _pendingKey;
        private double _pendingSince;
        private bool _pendingRelease;

        public long? ExpandedKey => _expandedKey;

        public long? Update(IReadOnlyList<MarkerExpansionCandidate> candidates, double nowSeconds)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) nowSeconds = 0d;

            if (_expandedKey.HasValue && IsCurrentStillQualified(candidates, _expandedKey.Value))
            {
                var focusedOverride = FindFocused(candidates, useExitForCurrent: true);
                if (!focusedOverride.HasValue || focusedOverride.Value == _expandedKey.Value)
                {
                    ResetPending();
                    return _expandedKey;
                }
            }

            var desiredFocus = FindFocused(candidates, useExitForCurrent: false);
            if (desiredFocus.HasValue)
                return AdvanceToward(desiredFocus.Value, nowSeconds, FocusAcquireDwellSeconds);

            var desiredNear = FindUniqueNearest(candidates);
            if (desiredNear.HasValue)
                return AdvanceToward(desiredNear.Value, nowSeconds, NearAcquireDwellSeconds);

            if (!_expandedKey.HasValue)
            {
                ResetPending();
                return null;
            }

            if (!_pendingRelease)
            {
                _pendingRelease = true;
                _pendingKey = null;
                _pendingSince = nowSeconds;
                return _expandedKey;
            }
            if (nowSeconds - _pendingSince < ReleaseDwellSeconds) return _expandedKey;
            _expandedKey = null;
            ResetPending();
            return null;
        }

        public void Clear()
        {
            _expandedKey = null;
            ResetPending();
        }

        private long? AdvanceToward(long desiredKey, double nowSeconds, double dwellSeconds)
        {
            _pendingRelease = false;
            if (_expandedKey.HasValue && _expandedKey.Value == desiredKey)
            {
                ResetPending();
                return _expandedKey;
            }

            if (!_pendingKey.HasValue || _pendingKey.Value != desiredKey)
            {
                _pendingKey = desiredKey;
                _pendingSince = nowSeconds;
                return _expandedKey;
            }

            if (nowSeconds - _pendingSince < dwellSeconds) return _expandedKey;
            _expandedKey = desiredKey;
            ResetPending();
            return _expandedKey;
        }

        private bool IsCurrentStillQualified(IReadOnlyList<MarkerExpansionCandidate> candidates, long key)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.ClusterKey != key || candidate.MemberCount < DenseClusterMinimumMembers) continue;
                return candidate.ReticleDistancePixels1080 <= FocusExitPixels1080
                       || candidate.DistanceMeters <= NearExitMeters;
            }
            return false;
        }

        private long? FindFocused(IReadOnlyList<MarkerExpansionCandidate> candidates, bool useExitForCurrent)
        {
            var threshold = useExitForCurrent ? FocusExitPixels1080 : FocusEnterPixels1080;
            var found = false;
            MarkerExpansionCandidate best = default;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.MemberCount < DenseClusterMinimumMembers
                    || !IsFiniteNonNegative(candidate.ReticleDistancePixels1080)
                    || candidate.ReticleDistancePixels1080 > threshold) continue;
                if (!found
                    || candidate.ReticleDistancePixels1080 < best.ReticleDistancePixels1080
                    || (Math.Abs(candidate.ReticleDistancePixels1080 - best.ReticleDistancePixels1080) <= 0.001f
                        && (candidate.DistanceMeters < best.DistanceMeters
                            || (Math.Abs(candidate.DistanceMeters - best.DistanceMeters) <= 0.001f && candidate.ClusterKey < best.ClusterKey))))
                {
                    found = true;
                    best = candidate;
                }
            }
            return found ? best.ClusterKey : (long?)null;
        }

        private static long? FindUniqueNearest(IReadOnlyList<MarkerExpansionCandidate> candidates)
        {
            var found = false;
            MarkerExpansionCandidate best = default;
            var secondDistance = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.MemberCount < DenseClusterMinimumMembers
                    || !IsFiniteNonNegative(candidate.DistanceMeters)
                    || candidate.DistanceMeters > NearEnterMeters) continue;
                if (!found || candidate.DistanceMeters < best.DistanceMeters
                    || (Math.Abs(candidate.DistanceMeters - best.DistanceMeters) <= 0.001f && candidate.ClusterKey < best.ClusterKey))
                {
                    if (found) secondDistance = best.DistanceMeters;
                    best = candidate;
                    found = true;
                }
                else if (candidate.DistanceMeters < secondDistance)
                {
                    secondDistance = candidate.DistanceMeters;
                }
            }

            if (!found) return null;
            if (!float.IsPositiveInfinity(secondDistance)
                && secondDistance - best.DistanceMeters < UniqueNearestMarginMeters) return null;
            return best.ClusterKey;
        }

        private void ResetPending()
        {
            _pendingKey = null;
            _pendingRelease = false;
            _pendingSince = 0d;
        }

        private static bool IsFiniteNonNegative(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
