using System;
using System.Diagnostics;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Invalidation policy for the active-marker frame pipeline. Expensive placement work is not a Unity-frame
    /// obligation: structural state changes invalidate immediately, while ordinary camera/projected motion is bounded by
    /// explicit screen-space epsilons and a short multi-marker solve cadence.
    /// </summary>
    public static class MarkerFramePipelinePolicy
    {
        public const float ProjectionPositionEpsilonPixels = 2.0f;
        public const float ProjectionRotationEpsilonDegrees = 1.0f;
        public const float UiPositionWriteEpsilonPixels = 0.50f;
        public const float UiRotationWriteEpsilonDegrees = 0.50f;
        public const float HudRectEpsilonPixels = 0.75f;
        public const float MultiMarkerMotionSolveIntervalSeconds = 1f / 30f;
        public const float BlockingModalFallbackDiscoverySeconds = 5.0f;
        public const float MessageHudFallbackDiscoverySeconds = 8.0f;

        public static bool ShouldEnterActiveMarkerPipeline(int validMarkerCount) => validMarkerCount > 0;
        public static bool CanUseSingleMarkerFastPath(int validMarkerCount) => validMarkerCount == 1;

        public static bool ProjectionMateriallyChanged(MarkerHudProjection previous, MarkerHudProjection current)
        {
            if (previous.Valid != current.Valid) return true;
            if (!current.Valid) return false;
            if (previous.Mode != current.Mode || previous.Edge != current.Edge) return true;
            if (Math.Abs(previous.X - current.X) > ProjectionPositionEpsilonPixels) return true;
            if (Math.Abs(previous.Y - current.Y) > ProjectionPositionEpsilonPixels) return true;
            if (current.Mode == MarkerHudMode.OffScreenEdge
                && AngularDifference(previous.ArrowRotationDegrees, current.ArrowRotationDegrees) > ProjectionRotationEpsilonDegrees)
                return true;
            return false;
        }

        public static bool ShouldWriteScreenPosition(bool hasAppliedPosition, float previousX, float previousY, float nextX, float nextY)
            => !hasAppliedPosition
               || Math.Abs(previousX - nextX) > UiPositionWriteEpsilonPixels
               || Math.Abs(previousY - nextY) > UiPositionWriteEpsilonPixels;

        public static bool ShouldWriteRotation(bool hasAppliedRotation, float previousDegrees, float nextDegrees)
            => !hasAppliedRotation || AngularDifference(previousDegrees, nextDegrees) > UiRotationWriteEpsilonDegrees;

        public static bool HudRectMateriallyChanged(bool hadRect, MarkerHudRect previous, bool hasRect, MarkerHudRect current)
        {
            if (hadRect != hasRect) return true;
            if (!hasRect) return false;
            return Math.Abs(previous.CenterX - current.CenterX) > HudRectEpsilonPixels
                   || Math.Abs(previous.CenterY - current.CenterY) > HudRectEpsilonPixels
                   || Math.Abs(previous.Width - current.Width) > HudRectEpsilonPixels
                   || Math.Abs(previous.Height - current.Height) > HudRectEpsilonPixels;
        }

        public static bool ShouldRunMultiMarkerSolve(
            bool hasPlacementCache,
            bool structuralInvalidation,
            bool projectionMateriallyChanged,
            float now,
            float nextMotionSolveAt)
        {
            if (!hasPlacementCache || structuralInvalidation) return true;
            return projectionMateriallyChanged && now >= nextMotionSolveAt;
        }

        public static bool ShouldRunGlobalUiDiscovery(
            bool lifecycleInvalidated,
            bool urgentUiSignal,
            bool cacheMissingOrStale,
            float now,
            float nextFallbackAt)
            => lifecycleInvalidated || urgentUiSignal || (cacheMissingOrStale && now >= nextFallbackAt);

        private static float AngularDifference(float left, float right)
        {
            var delta = Math.Abs(left - right) % 360f;
            return delta > 180f ? 360f - delta : delta;
        }
    }

    public readonly struct MarkerRuntimePerformanceSnapshot
    {
        public MarkerRuntimePerformanceSnapshot(
            long unityUpdateCalls,
            long renderFrameCalls,
            long fullPlacementSolves,
            long singleMarkerFastPathCalls,
            long globalHudDiscoveries,
            long tmpPreferredMeasurements,
            long uiLayoutWrites,
            long diagnosticRecords,
            long heavySolveStopwatchTicks,
            long maxHeavySolveStopwatchTicks)
        {
            UnityUpdateCalls = unityUpdateCalls;
            RenderFrameCalls = renderFrameCalls;
            FullPlacementSolves = fullPlacementSolves;
            SingleMarkerFastPathCalls = singleMarkerFastPathCalls;
            GlobalHudDiscoveries = globalHudDiscoveries;
            TmpPreferredMeasurements = tmpPreferredMeasurements;
            UiLayoutWrites = uiLayoutWrites;
            DiagnosticRecords = diagnosticRecords;
            HeavySolveStopwatchTicks = heavySolveStopwatchTicks;
            MaxHeavySolveStopwatchTicks = maxHeavySolveStopwatchTicks;
        }

        public long UnityUpdateCalls { get; }
        public long RenderFrameCalls { get; }
        public long FullPlacementSolves { get; }
        public long SingleMarkerFastPathCalls { get; }
        public long GlobalHudDiscoveries { get; }
        public long TmpPreferredMeasurements { get; }
        public long UiLayoutWrites { get; }
        public long DiagnosticRecords { get; }
        public long HeavySolveStopwatchTicks { get; }
        public long MaxHeavySolveStopwatchTicks { get; }
        public double HeavySolveMilliseconds => HeavySolveStopwatchTicks * 1000.0 / Stopwatch.Frequency;
        public double MaxHeavySolveMilliseconds => MaxHeavySolveStopwatchTicks * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Low-overhead cumulative counters for marker-pipeline diagnostics. No strings, collections, timers, or logging are created
    /// by the record methods; formatting occurs only in the coordinator's bounded summary path.
    /// </summary>
    public sealed class MarkerRuntimePerformanceCounters
    {
        private long _unityUpdateCalls;
        private long _renderFrameCalls;
        private long _fullPlacementSolves;
        private long _singleMarkerFastPathCalls;
        private long _globalHudDiscoveries;
        private long _tmpPreferredMeasurements;
        private long _uiLayoutWrites;
        private long _diagnosticRecords;
        private long _heavySolveStopwatchTicks;
        private long _maxHeavySolveStopwatchTicks;

        public void RecordUnityUpdate() => _unityUpdateCalls++;
        public void RecordRenderFrame() => _renderFrameCalls++;
        public void RecordFullPlacementSolve(long elapsedStopwatchTicks)
        {
            _fullPlacementSolves++;
            if (elapsedStopwatchTicks <= 0) return;
            _heavySolveStopwatchTicks += elapsedStopwatchTicks;
            if (elapsedStopwatchTicks > _maxHeavySolveStopwatchTicks) _maxHeavySolveStopwatchTicks = elapsedStopwatchTicks;
        }
        public void RecordSingleMarkerFastPath() => _singleMarkerFastPathCalls++;
        public void RecordGlobalHudDiscovery() => _globalHudDiscoveries++;
        public void RecordTmpPreferredMeasurement() => _tmpPreferredMeasurements++;
        public void RecordUiLayoutWrite(int count = 1)
        {
            if (count > 0) _uiLayoutWrites += count;
        }
        public void RecordDiagnosticRecord() => _diagnosticRecords++;

        public MarkerRuntimePerformanceSnapshot Snapshot()
            => new MarkerRuntimePerformanceSnapshot(
                _unityUpdateCalls,
                _renderFrameCalls,
                _fullPlacementSolves,
                _singleMarkerFastPathCalls,
                _globalHudDiscoveries,
                _tmpPreferredMeasurements,
                _uiLayoutWrites,
                _diagnosticRecords,
                _heavySolveStopwatchTicks,
                _maxHeavySolveStopwatchTicks);
    }
}
