using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public class DlcSharingSettingsTests
    {
        private static string Current(string path) => File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath(path));
        private static bool Share(DlcPickupKind kind, DlcSharingSettings settings, bool activePage = true)
            => DlcPickupSharingPolicy.CanShare(kind, settings, false, false, false, false, false, activePage);

        [TestMethod]
        public void NewDlcSharingDefaultsLeaveAllThreeTypesVanilla()
        {
            foreach (var kind in new[] { DlcPickupKind.Hallowed, DlcPickupKind.Hex, DlcPickupKind.Page })
                Assert.IsFalse(Share(kind, default));
        }

        [DataTestMethod]
        [DataRow(DlcPickupKind.Hallowed)]
        [DataRow(DlcPickupKind.Hex)]
        [DataRow(DlcPickupKind.Page)]
        public void EachToggleEnablesOnlyItsOwnType(DlcPickupKind enabled)
        {
            var settings = new DlcSharingSettings(enabled == DlcPickupKind.Hallowed, enabled == DlcPickupKind.Hex, enabled == DlcPickupKind.Page);
            foreach (var kind in new[] { DlcPickupKind.Hallowed, DlcPickupKind.Hex, DlcPickupKind.Page, DlcPickupKind.Other })
                Assert.AreEqual(kind == enabled, Share(kind, settings), "Independent toggle: " + kind);
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, false)]
        [DataRow(false, true, false, false, false)]
        [DataRow(false, false, true, false, false)]
        [DataRow(false, false, false, true, false)]
        [DataRow(false, false, false, false, true)]
        public void OptInNeverSharesHiddenConsumedUniqueObjectiveOrProgression(bool hidden, bool consumed, bool unique, bool objective, bool progression)
        {
            var all = new DlcSharingSettings(true, true, true);
            foreach (var kind in new[] { DlcPickupKind.Hallowed, DlcPickupKind.Hex, DlcPickupKind.Page })
                Assert.IsFalse(DlcPickupSharingPolicy.CanShare(kind, all, hidden, consumed, unique, objective, progression, true));
        }

        [TestMethod]
        public void PagesRequireAnActiveSpellAndUnknownTypesStayExcluded()
        {
            var all = new DlcSharingSettings(true, true, true);
            Assert.IsTrue(Share(DlcPickupKind.Hallowed, all));
            Assert.IsTrue(Share(DlcPickupKind.Hex, all));
            Assert.IsFalse(Share(DlcPickupKind.Page, all, false));
            Assert.IsTrue(Share(DlcPickupKind.Page, all, true));
            Assert.IsFalse(Share(DlcPickupKind.Other, all));
            Assert.IsFalse(Share((DlcPickupKind)99, all));
        }

        [TestMethod]
        public void RuntimeExtendsFalseUpstreamShareabilityAndPreservesCommandBoundaries()
        {
            var source = Current("src/ItemShareFix.Plugin/RuntimePatches.cs");
            var postfix = C2HistoricalFreezeReconciliation.MethodBody(source, "private static void DlcShareabilityPostfix(");
            StringAssert.Contains(postfix, "if (__result || _server == null");
            StringAssert.Contains(postfix, "_server.IsDlcPickupShareable(def)");
            Assert.IsFalse(postfix.Contains("Give", StringComparison.Ordinal));
            var selection = C2HistoricalFreezeReconciliation.MethodBody(source, "private static bool ItemShareCommandSelectionPrefix(");
            StringAssert.Contains(selection, "KnownProgressionPickupCompatibility.IsExcluded");
            StringAssert.Contains(selection, "TemporarySharingPolicy.ShouldUseVanillaBypass");
        }

        [TestMethod]
        public void RuntimeReadsCurrentFlagsAndInvalidatesPresentationWithoutRunRestart()
        {
            var plugin = Current("src/ItemShareFix.Plugin/ItemShareFixPlugin.cs");
            Assert.IsFalse(plugin.Contains("CaptureDlcSharingSettings", StringComparison.Ordinal));
            var server = Current("src/ItemShareFix.Plugin/ServerCoordinator.cs");
            StringAssert.Contains(server, "=> IsDlcPickupShareable(def, ReadDlcSharingSettings());");
            Assert.IsFalse(server.Contains("_dlcSharingSettings", StringComparison.Ordinal));
            var config = Current("src/ItemShareFix.Plugin/PluginConfig.cs");
            foreach (var key in new[] { "ShareHallowedItems", "ShareHexItems", "ShareNTormatPages" })
                StringAssert.Contains(config, "BindPresentationInvalidation(" + key + ");");
            StringAssert.Contains(server, "spell.itemDef.itemIndex == item.itemIndex");
            StringAssert.Contains(server, "KnownProgressionPickupCompatibility.IsExcluded(def)");
        }

        [TestMethod]
        public void ThreeCanonicalCheckboxesArePresentInBothPickupModesAndTierTab()
        {
            var config = Current("src/ItemShareFix.Plugin/PluginConfig.cs");
            var roo = Current("src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs");
            foreach (var key in new[] { "ShareHallowedItems", "ShareHexItems", "ShareNTormatPages" })
            {
                StringAssert.Contains(config, "config.Bind(\"DLC Sharing\", \"" + key + "\", false,");
                StringAssert.Contains(roo, "config." + key + ", null, out failure)");
                StringAssert.Contains(roo, "TryMovePlan(localPlans, plans, \"" + key + "\", out failure)");
            }
            StringAssert.Contains(roo, "public const int ItemShareFixOptionCount = CurrentMarkerOptionCount + 3;");
            StringAssert.Contains(roo, "return ItemTiersCategory;");
        }

        [TestMethod]
        public void ReviewedDlcDeltasRejectSourceDrift()
        {
            const string path = "src/ItemShareFix.Plugin/OptionalRiskOfOptionsIntegration.cs";
            var source = Current(path);
            Assert.ThrowsException<AssertFailedException>(() => DlcSettingsReconciliation.Normalize(path, source + "// drift"));
            Assert.ThrowsException<AssertFailedException>(() => DlcSettingsReconciliation.Normalize(path,
                source.Replace("CurrentMarkerOptionCount + 3", "CurrentMarkerOptionCount + 2", StringComparison.Ordinal)));
        }
    }
}
