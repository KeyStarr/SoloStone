using System.Collections.Generic;
using SabberStoneCore.Model;

namespace SabberStoneCore.Actions
{
    /// <summary>Passive Hero Powers the card data marks only in their text (adventure bosses, e.g. Dungeon Run's
    /// Charrrrrge!): never used; a starting one's trigger is live from the start of the game. Kept outside Card, whose
    /// definition snapshots are part of the recorded game state.</summary>
    public static class PassiveHeroPowerRules
    {
        private static readonly HashSet<string> Ids = new HashSet<string>();

        internal static void Mark(Card card) => Ids.Add(card.Id);

        public static bool IsMarked(Card card) => card != null && Ids.Contains(card.Id);
    }
}
