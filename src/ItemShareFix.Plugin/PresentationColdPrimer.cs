using System;
using System.Diagnostics;
using System.Globalization;
using BepInEx.Logging;
using ItemShareFix.Core;
using UnityEngine;

namespace ItemShareFix
{
    /// <summary>
    /// Initialization-only relocation of pure ItemShareFix presentation JIT/static-initialization work.
    /// This primer intentionally has no upstream bridge, game catalog, pickup, HUD, marker-view, or resource-discovery input.
    /// </summary>
    internal static class PresentationColdPrimer
    {
        public static bool PrimeCommandPresentationDuringInitialization(ManualLogSource log)
        {
            var started = Stopwatch.GetTimestamp();
            var success = false;
            string result;
            try
            {
                var shareability = CommandShareabilityPolicy.Evaluate(new bool?[] { true, false });
                var classPresentation = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(
                    new[] { MarkerClassKind.Tier1 },
                    MarkerLanguage.English);
                var readableLabel = MarkerClassPolicy.LocalizedReadableClassLabel(MarkerClassKind.Tier1, MarkerLanguage.English);
                var diagnosticClass = MarkerClassPolicy.DiagnosticClassName(MarkerClassKind.Tier1);
                var metadata = new MarkerRuntimeMetadata(
                    classPresentation.Kind,
                    diagnosticClass,
                    readableLabel,
                    Color.white,
                    classPresentation.ExactClass);

                if (shareability.State != CommandShareabilityState.Mixed
                    || metadata.Kind != MarkerClassKind.Tier1
                    || !metadata.ExactClass
                    || string.IsNullOrEmpty(metadata.ClassName)
                    || string.IsNullOrEmpty(metadata.Label))
                {
                    throw new InvalidOperationException("pure presentation primer invariant failed");
                }

                success = true;
                result = "pass";
            }
            catch (Exception ex)
            {
                result = "failed:" + ex.GetType().Name;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            var elapsedUs = elapsedTicks * 1000000.0 / Stopwatch.Frequency;
            var message = "ISF_COMMAND_PRESENTATION_PRIMER elapsed_us="
                          + elapsedUs.ToString("F1", CultureInfo.InvariantCulture)
                          + " result=" + result;
            if (success) log.LogInfo(message);
            else log.LogWarning(message);
            return success;
        }
    }
}
