using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class V100A2R1ReturnCorrection2PublicPluginIdentityTests
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

        private static string OldPrereleaseGuid()
            => string.Concat("com.", "multi", "tweaker.", "itemsharefix");

        private static IEnumerable<string> PublicTextFiles()
        {
            var root = RepositoryRoot();
            foreach (var file in Directory.GetFiles(root.FullName, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root.FullName, file).Replace('\\', '/');
                var directory = "/" + (Path.GetDirectoryName(relative) ?? string.Empty).Replace('\\', '/') + "/";
                if (directory.IndexOf("/evidence/", StringComparison.OrdinalIgnoreCase) >= 0
                    || directory.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0
                    || directory.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0
                    || directory.IndexOf("/TestResults/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var extension = Path.GetExtension(relative);
                if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(relative, ".gitignore", StringComparison.OrdinalIgnoreCase))
                    yield return file;
            }
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_01_CanonicalPluginGuidIsPublicStandaloneIdentity()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            const string definition = "public const string PluginGuid = \"com.itemsharefix\";";
            Assert.AreEqual(1, plugin.Split(new[] { definition }, StringSplitOptions.None).Length - 1);
            Assert.IsFalse(plugin.Contains(OldPrereleaseGuid(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_02_PublicTreeContainsNoPrereleaseItemShareFixIdentity()
        {
            var oldGuid = OldPrereleaseGuid();
            foreach (var file in PublicTextFiles())
            {
                var text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains(oldGuid, StringComparison.OrdinalIgnoreCase), "Prerelease GUID leaked into public file: " + file);
                Assert.IsFalse(text.Contains(string.Concat("multi", "tweaker"), StringComparison.OrdinalIgnoreCase), "Prerelease identity namespace leaked into public file: " + file);
            }
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_03_BepInPluginAndHarmonyUseCanonicalPluginGuidConstant()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            StringAssert.Contains(plugin, "[BepInPlugin(PluginGuid, PluginName, PluginVersion)]");
            StringAssert.Contains(plugin, "new Harmony(PluginGuid)");
            Assert.IsFalse(plugin.Contains("BepInPlugin(\"com.itemsharefix\"", StringComparison.Ordinal));
            Assert.IsFalse(plugin.Contains("new Harmony(\"com.itemsharefix\")", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_04_RiskOfOptionsRegistrationUsesCanonicalItemShareFixIdentity()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            StringAssert.Contains(source, "ItemShareFixPlugin.PluginGuid, ItemShareFixPlugin.PluginName");
            Assert.IsFalse(source.Contains("com.itemsharefix", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(OldPrereleaseGuid(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_05_DependencyGuidsAndThunderstoreDependenciesRemainUnchanged()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            StringAssert.Contains(plugin, "[BepInDependency(\"com.majai.pickupshareapi\", BepInDependency.DependencyFlags.HardDependency)]");
            StringAssert.Contains(plugin, "[BepInDependency(\"com.majai.itemshare\", BepInDependency.DependencyFlags.HardDependency)]");
            StringAssert.Contains(plugin, "[BepInDependency(\"com.rune580.riskofoptions\", BepInDependency.DependencyFlags.SoftDependency)]");

            var manifest = ReadSource("manifest.json");
            StringAssert.Contains(manifest, "bbepis-BepInExPack-5.4.2121");
            StringAssert.Contains(manifest, "Vibecodeguy-PickupShareApi-1.0.0");
            StringAssert.Contains(manifest, "Vibecodeguy-ItemShare-1.7.1");
            Assert.IsFalse(manifest.Contains("riskofoptions", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_06_PluginNameAndVersionRemainPublicOnePointZeroPointZero()
        {
            var plugin = ReadSource("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            StringAssert.Contains(plugin, "public const string PluginName = \"ItemShareFix\";");
            StringAssert.Contains(plugin, "public const string PluginVersion = \"1.0.0\";");
            var manifest = ReadSource("manifest.json");
            StringAssert.Contains(manifest, "\"name\": \"ItemShareFix\"");
            StringAssert.Contains(manifest, "\"version_number\": \"1.0.0\"");
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_07_NewGuidHasSingleProductionDefinitionAndNoAliasOrMigrationLiteral()
        {
            var root = RepositoryRoot();
            var sourceFiles = Directory.GetFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories);
            var combined = string.Join("\n", sourceFiles.Select(File.ReadAllText));
            const string definition = "public const string PluginGuid = \"com.itemsharefix\";";
            Assert.AreEqual(1, combined.Split(new[] { definition }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, combined.Split(new[] { "com.itemsharefix" }, StringSplitOptions.None).Length - 1);
            Assert.IsFalse(combined.Contains(OldPrereleaseGuid(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_V100_A2R1_RC2_ID_08_FreshTemporarySharingDefaultRemainsOffWithoutForcedSavedValueWrite()
        {
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            StringAssert.Contains(config, "ShareTemporaryItems = config.Bind(\"General\", \"ShareTemporaryItems\", false,");
            Assert.IsFalse(config.Contains("ShareTemporaryItems.Value = false", StringComparison.Ordinal));
            Assert.IsFalse(config.Contains("ShareTemporaryItems.Value=false", StringComparison.Ordinal));
        }
    }
}
