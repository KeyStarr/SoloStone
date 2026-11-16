using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // "Your minions have permanent Stealth" (Dungeon Run's Cloak of Invisibility): a player enchantment registered here keeps
    // its owner's minions stealthed when they attack. A registry, not a Card property, so card-definition snapshots stay
    // unchanged.
    public static class PermanentStealthRules
    {
        private static readonly HashSet<int> PlayerEnchantments = new HashSet<int>();

        internal static void Mark(Card playerEnchantment) => PlayerEnchantments.Add(playerEnchantment.AssetId);

        public static bool Keeps(Minion minion) => PlayerEnchantments.Count > 0
            && minion.Controller.AppliedEnchantments?.Any(e => PlayerEnchantments.Contains(e.Card.AssetId)) == true;
    }
}
