using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // "Add it to your Dungeon Deck": cards a run fight adds to its player's run deck (Dungeon Run's Magic Mirror, Amulet of
    // Domination, Treasure Coffer). The game only records them; the run mode reads them when the fight is settled. Kept beside
    // the game, not in it, so game state, snapshots and histories are unchanged; a clone (AI search) records nothing.
    public static class RunDeckRules
    {
        private static readonly ConditionalWeakTable<Game, Dictionary<int, List<string>>> Additions =
            new ConditionalWeakTable<Game, Dictionary<int, List<string>>>();

        internal static void Add(Controller owner, Card card)
        {
            var byPlayer = Additions.GetOrCreateValue(owner.Game);
            if (!byPlayer.TryGetValue(owner.PlayerId, out var cards)) byPlayer[owner.PlayerId] = cards = new List<string>();
            cards.Add(card.Id);
        }

        public static IReadOnlyList<string> Of(Controller owner) =>
            Additions.TryGetValue(owner.Game, out var byPlayer) && byPlayer.TryGetValue(owner.PlayerId, out var cards)
                ? cards.ToArray() : new string[0];
    }
}
