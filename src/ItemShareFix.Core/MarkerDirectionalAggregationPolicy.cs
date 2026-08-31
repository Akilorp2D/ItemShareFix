using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public enum MarkerDirectionSector
    {
        Right = 0,
        UpRight = 1,
        Up = 2,
        UpLeft = 3,
        Left = 4,
        DownLeft = 5,
        Down = 6,
        DownRight = 7,
    }

    public readonly struct MarkerDirectionalInput
    {
        public MarkerDirectionalInput(long presentationKey, float directionX, float directionY, float distanceMeters, int totalCount)
        {
            PresentationKey = presentationKey;
            DirectionX = directionX;
            DirectionY = directionY;
            DistanceMeters = SanitizeDistance(distanceMeters);
            TotalCount = Math.Max(0, totalCount);
        }

        public long PresentationKey { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float DistanceMeters { get; }
        public int TotalCount { get; }

        private static float SanitizeDistance(float value)
            => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }

    public readonly struct MarkerDirectionalSectorSummary
    {
        public MarkerDirectionalSectorSummary(
            MarkerDirectionSector sector,
            long nearestPresentationKey,
            float directionX,
            float directionY,
            float nearestDistanceMeters,
            int totalCount,
            int representedNodeCount)
        {
            Sector = sector;
            NearestPresentationKey = nearestPresentationKey;
            DirectionX = directionX;
            DirectionY = directionY;
            NearestDistanceMeters = nearestDistanceMeters;
            TotalCount = Math.Max(0, totalCount);
            RepresentedNodeCount = Math.Max(0, representedNodeCount);
        }

        public MarkerDirectionSector Sector { get; }
        public long PresentationKey => MarkerDirectionalAggregationPolicy.SectorPresentationKey(Sector);
        public long NearestPresentationKey { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float NearestDistanceMeters { get; }
        public int TotalCount { get; }
        public int RepresentedNodeCount { get; }
    }

    /// <summary>
    /// Presentation-only broad-direction aggregation. It consumes projected direction vectors only after semantic
    /// world membership has been solved. Eight broad sectors are deliberately bounded to avoid edge-arrow fan-out.
    /// </summary>
    public static class MarkerDirectionalAggregationPolicy
    {
        public const int BroadSectorCount = 8;
        private const long SectorKeyBase = long.MinValue + 4096L;

        public static long SectorPresentationKey(MarkerDirectionSector sector)
            => SectorKeyBase + (int)sector;

        public static MarkerDirectionSector SectorForDirection(float directionX, float directionY)
        {
            if (!IsFinite(directionX) || !IsFinite(directionY) || (Math.Abs(directionX) < 0.0001f && Math.Abs(directionY) < 0.0001f))
                return MarkerDirectionSector.Up;
            var degrees = Math.Atan2(directionY, directionX) * 180.0 / Math.PI;
            if (degrees < 0d) degrees += 360d;
            var octant = ((int)Math.Floor((degrees + 22.5d) / 45d)) & 7;
            switch (octant)
            {
                case 0: return MarkerDirectionSector.Right;
                case 1: return MarkerDirectionSector.UpRight;
                case 2: return MarkerDirectionSector.Up;
                case 3: return MarkerDirectionSector.UpLeft;
                case 4: return MarkerDirectionSector.Left;
                case 5: return MarkerDirectionSector.DownLeft;
                case 6: return MarkerDirectionSector.Down;
                default: return MarkerDirectionSector.DownRight;
            }
        }

        public static IReadOnlyList<MarkerDirectionalSectorSummary> Aggregate(IReadOnlyList<MarkerDirectionalInput> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var buckets = new Bucket[BroadSectorCount];
            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var sector = SectorForDirection(input.DirectionX, input.DirectionY);
                var index = (int)sector;
                if (buckets[index] == null) buckets[index] = new Bucket(sector);
                buckets[index].Add(input);
            }

            var result = new List<MarkerDirectionalSectorSummary>(BroadSectorCount);
            for (var i = 0; i < buckets.Length; i++)
            {
                var bucket = buckets[i];
                if (bucket == null || bucket.Count == 0) continue;
                result.Add(bucket.Build());
            }
            return result;
        }

        private sealed class Bucket
        {
            private readonly MarkerDirectionSector _sector;
            private float _sumX;
            private float _sumY;
            private float _nearestDistance = float.MaxValue;
            private long _nearestKey;
            private int _totalCount;

            public Bucket(MarkerDirectionSector sector) => _sector = sector;
            public int Count { get; private set; }

            public void Add(MarkerDirectionalInput input)
            {
                Count++;
                var length = (float)Math.Sqrt(input.DirectionX * input.DirectionX + input.DirectionY * input.DirectionY);
                if (IsFinite(length) && length > 0.0001f)
                {
                    _sumX += input.DirectionX / length;
                    _sumY += input.DirectionY / length;
                }
                _totalCount += input.TotalCount;
                if (input.DistanceMeters < _nearestDistance || (Math.Abs(input.DistanceMeters - _nearestDistance) < 0.0001f && input.PresentationKey < _nearestKey))
                {
                    _nearestDistance = input.DistanceMeters;
                    _nearestKey = input.PresentationKey;
                }
            }

            public MarkerDirectionalSectorSummary Build()
            {
                var length = (float)Math.Sqrt(_sumX * _sumX + _sumY * _sumY);
                var x = _sumX;
                var y = _sumY;
                if (!IsFinite(length) || length <= 0.0001f)
                {
                    SectorCenter(_sector, out x, out y);
                }
                else
                {
                    x /= length;
                    y /= length;
                }
                return new MarkerDirectionalSectorSummary(
                    _sector,
                    _nearestKey,
                    x,
                    y,
                    _nearestDistance == float.MaxValue ? 0f : _nearestDistance,
                    _totalCount,
                    Count);
            }
        }

        public static void SectorCenter(MarkerDirectionSector sector, out float x, out float y)
        {
            var radians = ((int)sector * 45.0) * Math.PI / 180.0;
            x = (float)Math.Cos(radians);
            y = (float)Math.Sin(radians);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class MarkerFovPresentationHysteresisPolicy
    {
        public const float EnterAngleDegrees = 42.0f;
        public const float ExitAngleDegrees = 48.0f;
        private readonly Dictionary<long, bool> _states = new Dictionary<long, bool>();

        public bool Update(long presentationKey, float angleFromForwardDegrees, bool projectedInsideNormalFov)
        {
            var angle = SanitizeAngle(angleFromForwardDegrees);
            _states.TryGetValue(presentationKey, out var wasInFov);
            var next = NextState(wasInFov, angle, projectedInsideNormalFov);
            _states[presentationKey] = next;
            return next;
        }

        public static bool NextState(bool wasInFov, float angleFromForwardDegrees, bool projectedInsideNormalFov)
        {
            var angle = SanitizeAngle(angleFromForwardDegrees);
            if (wasInFov)
            {
                // Once a card is visible it is retained through the small angular exit band, even if projection has
                // crossed the exact viewport boundary. It becomes directional only beyond the wider exit threshold.
                return projectedInsideNormalFov || angle <= ExitAngleDegrees;
            }
            // An arrow becomes a normal card only after it is clearly back inside the normal FOV.
            return projectedInsideNormalFov && angle <= EnterAngleDegrees;
        }

        public void Prune(IEnumerable<long> activePresentationKeys)
        {
            if (activePresentationKeys == null) throw new ArgumentNullException(nameof(activePresentationKeys));
            var active = new HashSet<long>(activePresentationKeys);
            var stale = new List<long>();
            foreach (var key in _states.Keys)
                if (!active.Contains(key)) stale.Add(key);
            for (var i = 0; i < stale.Count; i++) _states.Remove(stale[i]);
        }

        public void Clear() => _states.Clear();

        private static float SanitizeAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 180f;
            if (value < 0f) return 0f;
            return value > 180f ? 180f : value;
        }
    }

    public static class MarkerVisualSettingsPolicy
    {
        public const float OpacityMin = 0.0f;
        public const float OpacityMax = 1.0f;
        public const float MarkerOpacityDefault = 1.0f;
        public const float BackgroundOpacityDefault = 0.0f;
        public const float OffscreenScaleMin = 0.75f;
        public const float OffscreenScaleMax = 1.25f;
        public const float OffscreenScaleDefault = 1.0f;
        public const float OffscreenOpacityDefault = 1.0f;
        public const float OffscreenEdgePaddingMin = 12.0f;
        public const float OffscreenEdgePaddingMax = 160.0f;
        public const float OffscreenEdgePaddingDefault = 36.0f;

        public static float ClampOpacity(float value, float defaultValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Clamp01(defaultValue);
            return Clamp01(value);
        }

        public static float ClampOffscreenScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return OffscreenScaleDefault;
            if (value < OffscreenScaleMin) return OffscreenScaleMin;
            return value > OffscreenScaleMax ? OffscreenScaleMax : value;
        }

        public static float ClampOffscreenEdgePadding(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return OffscreenEdgePaddingDefault;
            if (value < OffscreenEdgePaddingMin) return OffscreenEdgePaddingMin;
            return value > OffscreenEdgePaddingMax ? OffscreenEdgePaddingMax : value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
