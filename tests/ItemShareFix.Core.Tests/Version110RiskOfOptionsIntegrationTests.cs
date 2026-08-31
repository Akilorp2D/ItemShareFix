using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version110RiskOfOptionsIntegrationTests
    {
        private static DirectoryInfo RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ItemShareFix.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory);
            return directory!;
        }

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(RepositoryRoot().FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        [TestMethod]
        public void ISF_V110_ROO_01_PublicVersionIsOnePointOnePointZero()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            var props = ReadSource("Directory.Build.props");
            var manifest = ReadSource("manifest.json");
            StringAssert.Contains(plugin, "public const string PluginVersion = \"1.1.0\";");
            StringAssert.Contains(props, "<Version>1.1.0</Version>");
            StringAssert.Contains(manifest, "\"version_number\": \"1.1.0\"");
        }

        [TestMethod]
        public void ISF_V110_ROO_02_PublicGuidRemainsCanonical()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            StringAssert.Contains(plugin, "public const string PluginGuid = \"com.itemsharefix\";");
        }

        [TestMethod]
        public void ISF_V110_ROO_03_RiskOfOptionsRemainsSoftDependencyAndNotManifestDependency()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            var manifest = ReadSource("manifest.json");
            StringAssert.Contains(plugin, "BepInDependency.DependencyFlags.SoftDependency");
            Assert.IsFalse(manifest.Contains("riskofoptions", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_V110_ROO_04_NoCompileTimeRiskOfOptionsReferenceExists()
        {
            var project = ReadSource("src/ItemShareFix.Plugin/ItemShareFix.Plugin.csproj");
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            Assert.IsFalse(project.Contains("RiskOfOptions.dll", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(source.Contains("using RiskOfOptions", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_05_IntegrationTargetsOnlyItemShareFixPageIdentity()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName");
            StringAssert.Contains(source, "public const int TotalOptionCount = ItemShareFixOptionCount + RequiredUpstreamOptionCount;");
            Assert.IsFalse(source.Contains("AddOption(option, UpstreamPluginGuid", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_06_PageDescriptionIsApprovedAndNonEmpty()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "Fixes and extends ItemShare with personal pickup markers, temporary-item sharing, multiplayer fixes, and convenient in-game configuration.");
            StringAssert.Contains(source, "SetModDescription");
        }

        [TestMethod]
        public void ISF_V110_ROO_07_ApprovedIconBytesArePreservedInRootAndEmbeddedResource()
        {
            var root = RepositoryRoot().FullName;
            var paths = new[] { "icon.png", "src/ItemShareFix.Plugin/Resources/ItemShareFixIcon.png" };
            foreach (var relative in paths)
            {
                var bytes = File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                Assert.AreEqual("F77ECBAF5D3C37411E536E8D0DDEB2D9385E04F5C1D678C0DD63AFF1F0D1ED1C", hash);
            }
            var project = ReadSource("src/ItemShareFix.Plugin/ItemShareFix.Plugin.csproj");
            StringAssert.Contains(project, "EmbeddedResource Include=\"Resources/ItemShareFixIcon.png\"");
        }

        [TestMethod]
        public void ISF_V110_ROO_08_AllFifteenRequiredUpstreamKeysAreDeclared()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var keys = new[] { "PickupMode", "ShareEquipment", "ShareToDead", "AnnounceProgress", "ShareCommandPicks", "PingShowsPending", "HideCollectedOrbs", "SilenceRemoteNotificationErrors", "White", "Green", "Red", "Boss", "Lunar", "Void", "Food" };
            foreach (var key in keys) StringAssert.Contains(source, "\"" + key + "\"");
            StringAssert.Contains(source, "public const int RequiredUpstreamOptionCount = 15;");
        }

        [TestMethod]
        public void ISF_V110_ROO_09_UpstreamEntriesComeFromExactItemSharePluginConfig()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "Chainloader.PluginInfos.TryGetValue(UpstreamPluginGuid");
            StringAssert.Contains(source, "info.Instance.Config");
            StringAssert.Contains(source, "Path.GetFileName(config.ConfigFilePath)");
            StringAssert.Contains(source, "UpstreamPluginGuid + \".cfg\"");
        }

        [TestMethod]
        public void ISF_V110_ROO_10_NoUpstreamShadowConfigBindIsCreated()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            Assert.IsFalse(source.Contains(".Bind(\"General\", \"PickupMode\"", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".Bind(\"Tiers\",", StringComparison.Ordinal));
            StringAssert.Contains(source, "ConfigEntryBase entry");
        }

        [TestMethod]
        public void ISF_V110_ROO_11_SimpleApprovedSectionsAreUsedWithoutOwnershipPrefixes()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            foreach (var category in new[] { "Sharing", "Item Tiers", "Markers", "Off-screen Indicators", "Marker Colors" })
                StringAssert.Contains(source, "\"" + category + "\"");
            StringAssert.Contains(source, "public const string SharingCategory = \"Sharing\";");
            StringAssert.Contains(source, "public const string ItemTiersCategory = \"Item Tiers\";");
            StringAssert.Contains(source, "public const string MarkersCategory = \"Markers\";");
            StringAssert.Contains(source, "public const string OffscreenIndicatorsCategory = \"Off-screen Indicators\";");
            StringAssert.Contains(source, "public const string MarkerColorsCategory = \"Marker Colors\";");
            Assert.IsFalse(source.Contains("ItemShare —", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ItemShareFix —", StringComparison.Ordinal));

            var resolverStart = source.IndexOf("private static string ResolveItemShareFixCategory", StringComparison.Ordinal);
            var resolverEnd = source.IndexOf("private static MarkerOptionLocalizedText ResolveEnglishLocalOptionText", resolverStart, StringComparison.Ordinal);
            Assert.IsTrue(resolverStart >= 0 && resolverEnd > resolverStart);
            var resolver = source.Substring(resolverStart, resolverEnd - resolverStart);
            Assert.IsFalse(resolver.Contains("Temporary Items", StringComparison.Ordinal));
            Assert.IsFalse(resolver.Contains("return \"General\"", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_12_ModePredicatesCoverMarkersIndividualOnlyAndShareEquipment()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "Func<bool> instantMode = () => IsPickupMode(pickupMode, \"Instant\")");
            StringAssert.Contains(source, "Func<bool> individualMode = () => IsPickupMode(pickupMode, \"Individual\")");
            foreach (var key in new[] { "AnnounceProgress", "ShareCommandPicks", "PingShowsPending", "HideCollectedOrbs" })
                StringAssert.Contains(source, "\"" + key + "\"");
            StringAssert.Contains(source, "string.Equals(spec.Key, \"ShareEquipment\"");
        }

        [TestMethod]
        public void ISF_V110_ROO_13_DisabledPredicatesDoNotWriteConfigValues()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "public bool Invoke() => _predicate();");
            StringAssert.Contains(source, "pickupMode.BoxedValue?.ToString()");
            Assert.IsFalse(source.Contains("BoxedValue =", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".Value =", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_14_MarkerDetailRowsUsesIntegerSliderPath()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "RiskOfOptions.Options.IntSliderOption\", config.MarkerDetailRows");
            Assert.IsFalse(source.Contains("StepSliderOption\", config.MarkerDetailRows", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_15_AllTwentySevenIntendedItemShareFixOptionsRemainPlanned()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "public const int CurrentMarkerOptionCount = 27;");
            StringAssert.Contains(source, "public const int ItemShareFixOptionCount = CurrentMarkerOptionCount;");
            var methodStart = source.IndexOf("private static bool TryBuildItemShareFixPlans", StringComparison.Ordinal);
            var methodEnd = source.IndexOf("private static bool AddLocalPlan", methodStart, StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0 && methodEnd > methodStart);
            var body = source.Substring(methodStart, methodEnd - methodStart);
            Assert.AreEqual(27, body.Split(new[] { "AddLocalPlan(" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_V110_ROO_16_RegistrationHasPreflightCompletenessAndDuplicateGuard()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "if (_registrationComplete || _registrationAttempted) return;");
            StringAssert.Contains(source, "if (plans.Count != TotalOptionCount)");
            StringAssert.Contains(source, "duplicateProtection=True");
        }

        [TestMethod]
        public void ISF_V110_ROO_17_ReadmesAreApprovedConciseAndKeepRequirementsOptionalRooAndLicense()
        {
            foreach (var relative in new[] { "README.md", "docs/README_RU.md" })
            {
                var text = ReadSource(relative);
                Assert.IsTrue(text.Contains("Risk Of Options", StringComparison.Ordinal));
                Assert.IsTrue(text.Contains("MIT", StringComparison.Ordinal));
            }
            var english = ReadSource("README.md");
            StringAssert.Contains(english, "**Optional:**");
            StringAssert.Contains(english, "## Requirements");
            StringAssert.Contains(english, "## License");
        }

        [TestMethod]
        public void ISF_V110_ROO_18_PublicTreeHasNoInternalEvidenceDirectories()
        {
            var root = RepositoryRoot().FullName;
            foreach (var forbidden in new[] { "evidence", "qa", "manager", "handoff", "testresults" })
                Assert.IsFalse(Directory.Exists(Path.Combine(root, forbidden)), "Internal directory present: " + forbidden);
        }

        [TestMethod]
        public void ISF_V110_ROO_19_InitialRegistrationAppliesLocalChoiceTokensBeforeLanguageKeyBecomesCurrent()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var addOptionIndex = source.IndexOf("addOption.Invoke(null, new object?[] { plan.Option, ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName });", StringComparison.Ordinal);
            var englishIndex = source.IndexOf("var english = ResolveEnglishLocalOptionText(plan.Entry);", addOptionIndex, StringComparison.Ordinal);
            var applyIndex = source.IndexOf("ApplyRegisteredEnglishTokens(assembly, plan.Option, plan.Entry, english);", englishIndex, StringComparison.Ordinal);
            var bindingIndex = source.IndexOf("RegisteredOptions.Add(new RegisteredOptionBinding(assembly, plan.Option, plan.Entry, plan.RefreshLocalization));", applyIndex, StringComparison.Ordinal);
            var languageKeyIndex = source.IndexOf("_appliedLanguageKey = MarkerRiskOfOptionsLocalization.CurrentLanguageKey();", bindingIndex, StringComparison.Ordinal);

            Assert.IsTrue(addOptionIndex >= 0);
            Assert.IsTrue(englishIndex > addOptionIndex);
            Assert.IsTrue(applyIndex > englishIndex);
            Assert.IsTrue(bindingIndex > applyIndex);
            Assert.IsTrue(languageKeyIndex > bindingIndex);
            StringAssert.Contains(source, "if (plan.RefreshLocalization)");
            StringAssert.Contains(source, "private static readonly string[] EnglishPresentationModeChoices = { \"Detailed\", \"Compact\" };");
            StringAssert.Contains(source, "private static readonly string[] EnglishSortOrderChoices = { \"High to low\", \"Low to high\" };");
            Assert.IsFalse(source.Contains("MarkerRiskOfOptionsLocalization.PresentationModeChoices()", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("MarkerRiskOfOptionsLocalization.SortChoices()", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_20_ExactlyFiveApprovedCategoriesAreDeclaredInExactOrder()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var orderStart = source.IndexOf("private static readonly string[] ApprovedCategoryOrder", StringComparison.Ordinal);
            var orderEnd = source.IndexOf("private static readonly string[] EnglishPresentationModeChoices", orderStart, StringComparison.Ordinal);
            Assert.IsTrue(orderStart >= 0 && orderEnd > orderStart);
            var order = source.Substring(orderStart, orderEnd - orderStart);
            var expected = new[] { "SharingCategory", "ItemTiersCategory", "MarkersCategory", "OffscreenIndicatorsCategory", "MarkerColorsCategory" };
            var cursor = 0;
            foreach (var token in expected)
            {
                var next = order.IndexOf(token, cursor, StringComparison.Ordinal);
                Assert.IsTrue(next >= cursor, "Category missing/out of order: " + token);
                cursor = next + token.Length;
            }
            Assert.AreEqual(5, order.Split(new[] { "Category," }, StringSplitOptions.None).Length - 1 + (order.Contains("MarkerColorsCategory", StringComparison.Ordinal) ? 1 : 0));
        }

        [TestMethod]
        public void ISF_V110_ROO_21_ApprovedPlanOrderIsExactNineSevenTenSixTenAndTotalsFortyTwo()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var start = source.IndexOf("private static bool TryComposeApprovedFiveTabPlanOrder", StringComparison.Ordinal);
            var end = source.IndexOf("private static bool TryMovePlan", start, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0 && end > start);
            var body = source.Substring(start, end - start);

            var sharing = new[] {
                "upstreamPlans, plans, \"PickupMode\"", "upstreamPlans, plans, \"ShareEquipment\"", "upstreamPlans, plans, \"ShareToDead\"",
                "localPlans, plans, \"ShareTemporaryItems\"", "upstreamPlans, plans, \"AnnounceProgress\"", "upstreamPlans, plans, \"ShareCommandPicks\"",
                "upstreamPlans, plans, \"PingShowsPending\"", "upstreamPlans, plans, \"HideCollectedOrbs\"", "upstreamPlans, plans, \"SilenceRemoteNotificationErrors\"" };
            var tiers = new[] { "White", "Green", "Red", "Boss", "Lunar", "Void", "Food" }.Select(x => "upstreamPlans, plans, \"" + x + "\"").ToArray();
            var markers = new[] { "PersonalMarkersEnabled", "MarkerPresentationMode", "ShowMarkerDistance", "MarkerScale", "MarkerOpacity", "MarkerBackgroundOpacity", "ShowMarkerCategoryDiamond", "MarkerDetailRows", "MarkerCategorySortOrder", "MarkerCompactShowCount" }.Select(x => "localPlans, plans, \"" + x + "\"").ToArray();
            var offscreen = new[] { "EnableOffscreenIndicators", "ShowOffscreenDistance", "ShowOffscreenTotalCount", "OffscreenIndicatorScale", "OffscreenIndicatorOpacity", "OffscreenEdgePadding" }.Select(x => "localPlans, plans, \"" + x + "\"").ToArray();
            var colors = new[] { "Common", "Uncommon", "Legendary", "Boss", "Lunar", "Void", "Equipment", "Command", "Neutral", "OffscreenIndicator" }.Select(x => "localPlans, plans, \"" + x + "\"").ToArray();

            Assert.AreEqual(9, sharing.Length);
            Assert.AreEqual(7, tiers.Length);
            Assert.AreEqual(10, markers.Length);
            Assert.AreEqual(6, offscreen.Length);
            Assert.AreEqual(10, colors.Length);
            var expected = sharing.Concat(tiers).Concat(markers).Concat(offscreen).Concat(colors).ToArray();
            Assert.AreEqual(42, expected.Length);
            Assert.AreEqual(42, body.Split(new[] { "TryMovePlan(" }, StringSplitOptions.None).Length - 1);

            var cursor = 0;
            foreach (var move in expected)
            {
                var next = body.IndexOf(move, cursor, StringComparison.Ordinal);
                Assert.IsTrue(next >= cursor, "Plan missing/out of order: " + move);
                cursor = next + move.Length;
            }
        }

        [TestMethod]
        public void ISF_V110_ROO_22_CategoryMembershipHasSharingTemporaryNoGeneralAndNoTemporaryTab()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var resolverStart = source.IndexOf("private static string ResolveItemShareFixCategory", StringComparison.Ordinal);
            var resolverEnd = source.IndexOf("private static MarkerOptionLocalizedText ResolveEnglishLocalOptionText", resolverStart, StringComparison.Ordinal);
            var resolver = source.Substring(resolverStart, resolverEnd - resolverStart);
            StringAssert.Contains(resolver, "string.Equals(key, \"ShareTemporaryItems\", StringComparison.Ordinal)) return SharingCategory;");
            StringAssert.Contains(resolver, "return OffscreenIndicatorsCategory;");
            StringAssert.Contains(resolver, "return MarkerColorsCategory;");
            StringAssert.Contains(resolver, "return MarkersCategory;");
            Assert.IsFalse(resolver.Contains("Temporary Items", StringComparison.Ordinal));
            Assert.IsFalse(resolver.Contains("GeneralCategory", StringComparison.Ordinal));

            var specsStart = source.IndexOf("private static IEnumerable<UpstreamEntrySpec> RequiredUpstreamSpecs", StringComparison.Ordinal);
            var specsEnd = source.IndexOf("private static bool TryResolveUpstreamEntries", specsStart, StringComparison.Ordinal);
            var specs = source.Substring(specsStart, specsEnd - specsStart);
            Assert.AreEqual(8, specs.Split(new[] { "SharingCategory" }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(7, specs.Split(new[] { "ItemTiersCategory" }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void ISF_V110_ROO_23_LocalRiskOfOptionsTextAndChoicesStayEnglishAcrossLanguageChanges()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "MarkerRiskOfOptionsLocalization.ResolveForLanguage(entry.Definition.Key, 0)");
            foreach (var label in new[] {
                "Enable Markers", "Marker Mode", "Share Temporary Items", "Show Distance", "Marker Scale", "Marker Opacity", "Background Opacity",
                "Show Category Diamond", "Item Rows", "Category Sort Order", "Compact Counts", "Enable Off-screen Indicators", "Show Off-screen Distance",
                "Show Off-screen Total Count", "Off-screen Scale", "Off-screen Opacity", "Edge Padding", "Common Color", "Uncommon Color", "Legendary Color",
                "Boss Color", "Lunar Color", "Void Color", "Equipment Color", "Command Color", "Neutral Color", "Off-screen Indicator Color" })
                StringAssert.Contains(source, "return \"" + label + "\";");
            StringAssert.Contains(source, "EnglishPresentationModeChoices = { \"Detailed\", \"Compact\" }");
            StringAssert.Contains(source, "EnglishSortOrderChoices = { \"High to low\", \"Low to high\" }");

            var refreshStart = source.IndexOf("public static void TryRefreshLocalization", StringComparison.Ordinal);
            var refreshEnd = source.IndexOf("private static bool TryBuildItemShareFixPlans", refreshStart, StringComparison.Ordinal);
            var refresh = source.Substring(refreshStart, refreshEnd - refreshStart);
            StringAssert.Contains(refresh, "ResolveEnglishLocalOptionText(binding.Entry)");
            StringAssert.Contains(refresh, "ApplyRegisteredEnglishTokens(binding.Assembly, binding.Option, binding.Entry, english)");
            Assert.IsFalse(refresh.Contains("MarkerRiskOfOptionsLocalization.Resolve(binding.Entry)", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("MarkerRiskOfOptionsLocalization.PresentationModeChoices()", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("MarkerRiskOfOptionsLocalization.SortChoices()", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V110_ROO_24_TooltipsContainBehaviorOnlyWhileCanonicalConfigOwnershipRemainsUnchanged()
        {
            var integration = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            var localLocalization = ReadSource("src/ItemShareFix.Plugin/MarkerRiskOfOptionsLocalization.cs");
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");

            var userVisibleDescriptionSources = integration + "\n" + localLocalization;
            Assert.IsFalse(userVisibleDescriptionSources.Contains("com.majai.itemshare.cfg", StringComparison.Ordinal));
            Assert.IsFalse(userVisibleDescriptionSources.Contains("com.itemsharefix.cfg", StringComparison.Ordinal));
            Assert.IsFalse(userVisibleDescriptionSources.Contains("ItemShare setting; saved to", StringComparison.Ordinal));
            Assert.IsFalse(userVisibleDescriptionSources.Contains("ItemShareFix setting; saved to", StringComparison.Ordinal));

            StringAssert.Contains(integration, "Chainloader.PluginInfos.TryGetValue(UpstreamPluginGuid");
            StringAssert.Contains(integration, "info.Instance.Config");
            StringAssert.Contains(integration, "Path.GetFileName(config.ConfigFilePath)");
            StringAssert.Contains(integration, "UpstreamPluginGuid + \".cfg\"");
            StringAssert.Contains(plugin, "public const string PluginGuid = \"com.itemsharefix\";");
            StringAssert.Contains(plugin, "_config = new PluginConfig(Config);");
            StringAssert.Contains(integration, "AddLocalPlan(assembly, plans, \"RiskOfOptions.Options.CheckBoxOption\", config.ShareTemporaryItems");
        }

    }
}
