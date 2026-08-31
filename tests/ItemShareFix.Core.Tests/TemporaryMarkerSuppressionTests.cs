using System;
using System.IO;
using System.Linq;
using ItemShareFix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public sealed class C21B1Correction10TemporaryMarkerSuppressionTests
    {
        private static string ReadSource(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            Assert.Fail(relativePath + " was not found while walking up from the MSTest base directory.");
            return string.Empty;
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_01_OrdinaryTemporaryOffIsMarkerIneligible()
            => Assert.IsFalse(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimePolicy.FromTemporaryFlag(true), false));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_02_OrdinaryTemporaryOnIsMarkerEligible()
            => Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimePolicy.FromTemporaryFlag(true), true));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_03_OrdinaryPermanentOffRemainsMarkerEligible()
            => Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimePolicy.FromTemporaryFlag(false), false));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_04_ExactTemporaryCommandOffIsMarkerIneligible()
            => Assert.IsFalse(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Temporary, false));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_05_ExactTemporaryCommandOnIsMarkerEligible()
            => Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Temporary, true));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_06_PermanentCommandOffRemainsMarkerEligible()
            => Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Permanent, false));

        [TestMethod]
        public void ISF_R1_C21_B1_C10_07_MixedAndUnknownCommandOffAreNotGloballyHidden()
        {
            Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Mixed, false));
            Assert.IsTrue(MarkerLifetimePolicy.IsMarkerEligible(MarkerLifetimeKind.Unknown, false));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_08_MixedPhysicalPileOffFiltersOnlyTemporaryMembers()
        {
            var lifetimes = new[]
            {
                MarkerLifetimeKind.Permanent,
                MarkerLifetimeKind.Temporary,
                MarkerLifetimeKind.Permanent,
                MarkerLifetimeKind.Temporary,
            };
            var eligible = lifetimes.Where(x => MarkerLifetimePolicy.IsMarkerEligible(x, false)).ToArray();
            CollectionAssert.AreEqual(new[] { MarkerLifetimeKind.Permanent, MarkerLifetimeKind.Permanent }, eligible);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_09_AllTemporaryPileOffYieldsZeroMarkerMembers()
        {
            var eligibleCount = new[] { MarkerLifetimeKind.Temporary, MarkerLifetimeKind.Temporary, MarkerLifetimeKind.Temporary }
                .Count(x => MarkerLifetimePolicy.IsMarkerEligible(x, false));
            Assert.AreEqual(0, eligibleCount);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_10_ClientSweepGatesOrdinaryAndCommandBeforeMarkerRegistryEntry()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var ordinaryGate = source.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(lifetime, _config.ShareTemporaryItems.Value)", StringComparison.Ordinal);
            var ordinaryMark = source.IndexOf("_markerRegistry.MarkPending(PersonalMarkerKind.OrdinaryPickup", StringComparison.Ordinal);
            var commandGate = source.IndexOf("MarkerLifetimePolicy.IsMarkerEligible(commandLifetime, _config.ShareTemporaryItems.Value)", StringComparison.Ordinal);
            var commandMark = source.IndexOf("_markerRegistry.MarkPending(PersonalMarkerKind.CommandPicker", StringComparison.Ordinal);
            Assert.IsTrue(ordinaryGate >= 0 && ordinaryGate < ordinaryMark);
            Assert.IsTrue(commandGate >= 0 && commandGate < commandMark);
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_11_ShareTemporaryChangeRequestsLivePresentationRefresh()
        {
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            var presentation = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            StringAssert.Contains(config, "BindPresentationInvalidation(ShareTemporaryItems)");
            StringAssert.Contains(presentation, "ReferenceEquals(sender, _config.ShareTemporaryItems)");
            StringAssert.Contains(presentation, "RequestRefresh();");
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_12_PersonalMarkersAndTemporarySharingRemainIndependent()
        {
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            Assert.IsFalse(config.Contains("ShareTemporaryItems = PersonalMarkersEnabled", StringComparison.Ordinal));
            Assert.IsFalse(config.Contains("PersonalMarkersEnabled = ShareTemporaryItems", StringComparison.Ordinal));
            Assert.IsFalse(config.Contains("ShareTemporaryItems = Enabled", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_13_NoNewNetworkProtocolOrGameplayMutationWasAdded()
        {
            var lifetime = ReadSource("src/ItemShareFix.Core/MarkerLifetimePolicy.cs");
            var config = ReadSource("src/ItemShareFix.Plugin/PluginConfig.cs");
            var presentation = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var combined = lifetime + config + presentation;
            Assert.IsFalse(combined.Contains("NetworkMessage", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("MessageBase", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("RegisterHandler", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("NetworkWriter", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("GiveItemPermanent", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ISF_R1_C21_B1_C10_14_LocalCollectedVisibilityOrderingRemainsBeforeMarkerEligibility()
        {
            var source = ReadSource("src/ItemShareFix.Plugin/ClientPresentation.cs");
            var suppression = source.IndexOf("LocalPickupSuppressionPolicy.ShouldSuppressProcessVisual", StringComparison.Ordinal);
            var applyHidden = source.IndexOf("gate.ApplyHidden();", suppression, StringComparison.Ordinal);
            var visualLog = source.IndexOf("LogLocalVisualGateTransition", suppression, StringComparison.Ordinal);
            var lifetime = source.IndexOf("var lifetime = MarkerLifetimePolicy.FromTemporaryFlag", visualLog, StringComparison.Ordinal);
            Assert.IsTrue(suppression >= 0 && applyHidden > suppression && visualLog > applyHidden && lifetime > visualLog);
            StringAssert.Contains(source, "ApplyLocalCommandPresentation(picker, commandPresentation);");
        }
    }
}
