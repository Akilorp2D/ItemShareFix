using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ItemShareFix.Core.Tests
{
    [TestClass]
    public class LiveSharingAndDlcMarkerTests
    {
        [TestMethod]
        public void UntouchedPickupFollowsOnThenOffWithinTheSameRun()
        {
            var item = new object();
            var policy = new SharedOptionPolicy<object>();
            policy.Observe(item, false, false);
            Assert.IsFalse(policy.Contains(item));
            policy.Observe(item, false, true);
            Assert.IsTrue(policy.Contains(item));
            policy.Observe(item, false, false);
            Assert.IsFalse(policy.Contains(item));
            Assert.IsFalse(DlcPickupSharingPolicy.CanShare(DlcPickupKind.Hex, default, false, false, false, false, false, false));
            Assert.IsTrue(DlcPickupSharingPolicy.CanShare(DlcPickupKind.Hex, new DlcSharingSettings(false, true, false), false, false, false, false, false, false));
        }

        [TestMethod]
        public void StartedSharingRetainsOnlyPreviouslyEligibleOptionsAfterOff()
        {
            var allowed = new object(); var neverEnabled = new object();
            var policy = new SharedOptionPolicy<object>();
            policy.Observe(allowed, false, true);
            policy.Observe(neverEnabled, false, false);
            policy.Observe(allowed, true, false);
            policy.Observe(neverEnabled, true, false);
            Assert.IsTrue(policy.Contains(allowed));
            Assert.IsFalse(policy.Contains(neverEnabled));
            var untouched = new SharedOptionPolicy<object>();
            untouched.Observe(allowed, false, false);
            Assert.IsFalse(untouched.Contains(allowed), "A retained pickup cannot authorize another pickup of the same item.");
        }

        [TestMethod]
        public void EnablingAnotherOptionDuringStartedSharingRequiresActualOptIn()
        {
            var item = new object(); var policy = new SharedOptionPolicy<object>();
            policy.Observe(item, true, false);
            Assert.IsFalse(policy.Contains(item));
            policy.Observe(item, true, true);
            Assert.IsTrue(policy.Contains(item));
            policy.Observe(item, true, false);
            Assert.IsTrue(policy.Contains(item));
        }

        [TestMethod]
        public void NestedUnownedPickupShadowsAndRestoresOwnedScopeEvenForTheSameItem()
        {
            var item = new object(); var other = new object();
            var context = new SharedInteractionContext<object>();
            using (context.Enter(item, true))
            {
                Assert.IsTrue(context.IsOwned(item));
                Assert.IsFalse(context.IsOwned(other));
                using (context.Enter(item, false)) Assert.IsFalse(context.IsOwned(item));
                Assert.IsTrue(context.IsOwned(item));
            }
            Assert.IsFalse(context.IsOwned(item));
        }

        [TestMethod]
        public void ScopeDisposalAfterExceptionAndRepeatedCleanupCannotLeakAuthorization()
        {
            var item = new object(); var context = new SharedInteractionContext<object>();
            IDisposable? scope = null;
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                using (scope = context.Enter(item, true)) throw new InvalidOperationException("upstream failure");
            });
            scope!.Dispose();
            Assert.IsFalse(context.IsOwned(item));
            using (context.Enter(item, false)) Assert.IsFalse(context.IsOwned(item));
        }

        [TestMethod]
        public void OffDuringPartialCollectionPreservesPendingButNotCollectedRights()
        {
            var item = new object(); var policy = new SharedOptionPolicy<object>();
            var pickup = new SharedPickupKey(51);
            var a = new ParticipantKey(new StableUserKey("a"), "master:a");
            var b = new ParticipantKey(new StableUserKey("b"), "master:b");
            var ledger = new ClaimLedger();
            ledger.Ensure(pickup, a, ParticipantState.Alive, 1);
            ledger.Ensure(pickup, b, ParticipantState.Alive, 1);
            policy.Observe(item, false, true);
            Assert.IsTrue(ledger.MarkCollected(pickup, a, 1));
            policy.Observe(item, true, false);
            Assert.IsTrue(policy.Contains(item));
            Assert.IsFalse(ledger.MarkCollected(pickup, a, 1));
            Assert.IsTrue(ledger.MarkCollected(pickup, b, 1));
            Assert.IsFalse(ledger.MarkCollected(pickup, b, 1));
        }

        [DataTestMethod]
        [DataRow("Cursed", MarkerClassKind.Hallowed, MarkerSemanticCategory.Hallowed, "Освящённые")]
        [DataRow("Hex", MarkerClassKind.Hex, MarkerSemanticCategory.Hex, "Проклятия")]
        [DataRow("Page", MarkerClassKind.Page, MarkerSemanticCategory.Page, "Страницы Н'тормата")]
        public void HomogeneousDlcCommandHasExactTypeInsteadOfOther(string tier, MarkerClassKind kind, MarkerSemanticCategory category, string label)
        {
            Assert.AreEqual(kind, MarkerClassPolicy.Classify(tier, false, false));
            var decision = MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { kind, kind }, MarkerLanguage.Russian);
            Assert.IsTrue(decision.ExactClass);
            Assert.AreEqual(kind, decision.Kind);
            Assert.AreEqual(label, decision.Label);
            Assert.AreEqual(category, MarkerSemanticCategoryPolicy.From(PersonalMarkerKind.CommandPicker, kind));
            Assert.AreEqual(label, MarkerTextLocalization.CategoryPlural(category, MarkerLanguage.Russian));
            Assert.AreEqual(label, MarkerTextLocalization.CategorySummaryLabel(category, MarkerLanguage.Russian));
        }

        [TestMethod]
        public void MixedOrUnknownCommandDoesNotInventAHomogeneousDlcType()
        {
            Assert.IsFalse(MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Hallowed, MarkerClassKind.Hex }, false).ExactClass);
            Assert.IsFalse(MarkerClassPolicy.ResolveCommandClassForReadablePresentation(new[] { MarkerClassKind.Page, MarkerClassKind.Unknown }, false).ExactClass);
            Assert.AreEqual(MarkerClassKind.Other, MarkerClassPolicy.Classify("UnverifiedModTier", false, false));
        }

        [DataTestMethod]
        [DataRow(12)] [DataRow(13)] [DataRow(14)]
        public void ExtendedCompactLayoutDoesNotDropOrOverlapNewCategories(int count)
        {
            var slots = MarkerCategorySummaryPolicy.BuildCompactLayout(count);
            Assert.AreEqual(count, slots.Length);
            Assert.AreEqual(count, slots.Select(slot => (slot.Row, slot.Column)).Distinct().Count());
            Assert.IsTrue(slots.All(slot => slot.RowSize > 0 && !float.IsNaN(slot.XUnits) && !float.IsNaN(slot.YUnits)));
        }

        [TestMethod]
        public void RetentionHooksUseExactObjectPolicyAndCleanUpOnFailure()
        {
            var source = File.ReadAllText(C2HistoricalFreezeReconciliation.ResolvePath("src/ItemShareFix.Plugin/RuntimePatches.cs"));
            StringAssert.Contains(source, "_server.CanRetainSharedPickup(self, interactionDef)");
            StringAssert.Contains(source, "_server.CanRetainSharedPickup(picker, selectedDef)");
            StringAssert.Contains(source, "finalizer: nameof(SharingScopeFinalizer)");
            StringAssert.Contains(source, "finalizer: nameof(CommandSharingScopeFinalizer)");
            StringAssert.Contains(source, "_server?.ReleaseSharingPolicy(_instanceId)");
        }
    }
}
