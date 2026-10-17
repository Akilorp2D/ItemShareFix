using System;

namespace ItemShareFix.Core
{
    public static class MarkerSceneVisibilityPolicy
    {
        // Exact SceneDef pairs verified in the installed DLC4 trial assets. Never match by substring or SceneType.
        public static bool ShouldSuppress(bool hideInTrials, string? sceneName, string? nameToken)
            => hideInTrials && (
                Matches(sceneName, nameToken, "surv2", "MAP_SURV2_TITLE")
                || Matches(sceneName, nameToken, "sad1", "MAP_SAD1_TITLE")
                || Matches(sceneName, nameToken, "soul1", "MAP_SOUL1_TITLE"));

        private static bool Matches(string? name, string? token, string expectedName, string expectedToken)
            => string.Equals(name, expectedName, StringComparison.Ordinal)
                && string.Equals(token, expectedToken, StringComparison.Ordinal);
    }
}
