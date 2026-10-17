using System;
using RoR2;

namespace ItemShareFix
{
    /// <summary>
    /// Exact-byte compatibility boundary for character-progression pickups that must retain
    /// vanilla/provider-local collection semantics instead of entering ItemShare ownership.
    /// The identities below are proven against the exact provider assemblies documented in
    /// the Phase C2-A Correction1 handoff. Matching intentionally requires BOTH the internal
    /// ItemDef.name and nameToken and never falls back to localized/display text or item tier.
    /// </summary>
    internal static class KnownProgressionPickupCompatibility
    {
        private readonly struct ExactItemIdentity
        {
            public ExactItemIdentity(string name, string nameToken)
            {
                Name = name;
                NameToken = nameToken;
            }

            public string Name { get; }
            public string NameToken { get; }
        }

        private static readonly ExactItemIdentity[] ExcludedItems =
        {
            // rob-Belmont 1.0.8
            new ExactItemIdentity("BelmontKeyFragment", "ROB_BELMONT_KEYFRAGMENT_NAME"),

            // rob-HUNK 1.5.1 virus samples
            new ExactItemIdentity("GVirusSample", "ROB_HUNK_G_VIRUS_SAMPLE_NAME"),
            new ExactItemIdentity("TVirusSample", "ROB_HUNK_T_VIRUS_SAMPLE_NAME"),
            new ExactItemIdentity("CVirusSample", "ROB_HUNK_C_VIRUS_SAMPLE_NAME"),
            new ExactItemIdentity("UroborosSample", "ROB_HUNK_URO_SAMPLE_NAME"),
            new ExactItemIdentity("PlagasSample", "ROB_HUNK_PLAGAS_SAMPLE_NAME"),

            // rob-HUNK 1.5.1 U.C. keycards. These are world pickups when depositKeycards=false.
            new ExactItemIdentity("SpadeKeycard", "ROB_HUNK_SPADE_KEYCARD_NAME"),
            new ExactItemIdentity("ClubKeycard", "ROB_HUNK_CLUB_KEYCARD_NAME"),
            new ExactItemIdentity("HeartKeycard", "ROB_HUNK_HEART_KEYCARD_NAME"),
            new ExactItemIdentity("DiamondKeycard", "ROB_HUNK_DIAMOND_KEYCARD_NAME"),
            new ExactItemIdentity("GoldKeycard", "ROB_HUNK_GOLD_KEYCARD_NAME"),
            new ExactItemIdentity("MasterKeycard", "ROB_HUNK_MASTER_KEYCARD_NAME"),
        };

        public static bool IsExcluded(GenericPickupController pickup)
        {
            if (pickup == null) return false;
            var pickupDef = PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
            return IsExcluded(pickupDef);
        }

        public static bool IsExcluded(PickupDef pickupDef)
        {
            if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None) return false;
            return IsExcluded(ItemCatalog.GetItemDef(pickupDef.itemIndex));
        }

        private static bool IsExcluded(ItemDef itemDef)
        {
            if (itemDef == null) return false;
            var name = itemDef.name;
            var nameToken = itemDef.nameToken;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(nameToken)) return false;

            for (var i = 0; i < ExcludedItems.Length; i++)
            {
                var identity = ExcludedItems[i];
                if (string.Equals(name, identity.Name, StringComparison.Ordinal)
                    && string.Equals(nameToken, identity.NameToken, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
