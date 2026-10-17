using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class Version120ReleaseMetadataTests
    {
        private static string Read(string path) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));

        [TestMethod]
        public void ReleaseMetadataIsConsistentAt120()
        {
            using var manifest = JsonDocument.Parse(Read("manifest.json"));
            Assert.AreEqual("1.2.0", manifest.RootElement.GetProperty("version_number").GetString());
            var properties = XDocument.Parse(Read("Directory.Build.props")).Root!.Element("PropertyGroup")!;
            Assert.AreEqual("1.2.0", properties.Element("Version")!.Value);
            Assert.AreEqual("1.2.0.0", properties.Element("AssemblyVersion")!.Value);
            Assert.AreEqual("1.2.0.0", properties.Element("FileVersion")!.Value);
            Assert.AreEqual("1.2.0", properties.Element("InformationalVersion")!.Value);
            var plugin = Read("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            StringAssert.Contains(plugin, "public const string PluginVersion = \"1.2.0\";");
            StringAssert.Contains(plugin, "public const string PluginGuid = \"com.itemsharefix\";");
        }

        [TestMethod]
        public void CompiledCoreAssemblyMetadataIs120()
        {
            var assembly = Assembly.Load("ItemShareFix.Core");
            Assert.AreEqual(new Version(1, 2, 0, 0), assembly.GetName().Version);
            Assert.AreEqual("1.2.0.0", assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
            Assert.AreEqual("1.2.0", assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        }

        [TestMethod]
        public void PublicChangelogMatchesApproved120EntryAndRetainsHistory()
        {
            const string approved = "## Version 1.2.0\n\n"
                + "- Updated compatibility with Risk of Rain 2 and the Hallowed Concepts DLC.\n"
                + "- Reworked the item marker system, improving performance and the display of large numbers of markers.\n"
                + "- Added item sharing support for Hallowed, Hex, and N'Tormat Pages. Sharing is disabled by default for these new item types and can be enabled individually through the mod configuration or Risk of Options.\n"
                + "- Fixed compatibility with Belmont and HUNK: special items required for character progression are no longer shared with other players.\n";
            var changelog = Read("CHANGELOG.md").Replace("\r\n", "\n");
            StringAssert.StartsWith(changelog, "# Changelog\n\n" + approved + "\n## 1.1.1\n");
            StringAssert.Contains(changelog, "\n## 1.1.0\n");
            StringAssert.Contains(changelog, "\n## 1.0.0\n");
            foreach (var path in new[] { "README.md", "docs/README_RU.md", "BUILD.md" })
                StringAssert.Contains(Read(path), "1.2.0");
        }

        [TestMethod]
        public void VersionDeltaRejectsUnrelatedAndWithinMetadataDrift()
        {
            const string path = "src/ItemShareFix.Plugin/ItemShareFixPlugin.cs";
            var current = Read(path);
            var parent = Version120Reconciliation.Normalize(path, current);
            Assert.AreEqual(current.Replace("PluginVersion = \"1.2.0\"", "PluginVersion = \"1.1.1\""), parent);
            Assert.ThrowsException<AssertFailedException>(() => Version120Reconciliation.Normalize(path, current + "// unexpected change"));
            Assert.ThrowsException<AssertFailedException>(() => Version120Reconciliation.Normalize(path, current.Replace("1.2.0", "1.2.1")));
        }
    }
}
