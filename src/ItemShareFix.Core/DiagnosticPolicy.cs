using System;

namespace ItemShareFix.Core
{
    public enum DiagnosticSeverity { Error, Warning, Info, Debug }
    public static class DiagnosticPolicy
    {
        public static bool Allows(bool enabled, string? level, DiagnosticSeverity severity)
        {
            if (!enabled) return false;
            var threshold = string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Error
                : string.Equals(level, "Warning", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Warning
                : string.Equals(level, "Debug", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Debug : DiagnosticSeverity.Info;
            return severity <= threshold;
        }
        public static string ExactRemoteOperation(string? evidence)
        {
            const string marker = "exactRemoteOperationPreset=";
            if (string.IsNullOrEmpty(evidence)) return "not-probed";
            var index = evidence!.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return "unavailable-or-not-probed";
            var start = index + marker.Length;
            if (Matches(evidence, start, "true")) return "true";
            if (Matches(evidence, start, "false")) return "false";
            return "unavailable-fail-closed";
        }

        private static bool Matches(string evidence, int start, string value)
        {
            if (evidence.IndexOf(value, start, StringComparison.Ordinal) != start) return false;
            var end = start + value.Length;
            return end == evidence.Length || evidence[end] == ';' || char.IsWhiteSpace(evidence[end]);
        }
    }
}
