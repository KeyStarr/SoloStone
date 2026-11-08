using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ReinforcementAura
    {
        [StandardCard("JAIL_327", 126617)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card, effect:
            new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var eligible = c.DeckZone.OfType<Minion>().Where(m => m.Cost <= 2).ToArray();
                if (eligible.Length == 0) return;
                var chosen = eligible[g.Random.Next(eligible.Length)];
                c.DeckZone.Remove(chosen);
                Generic.SummonBlock(g, chosen, -1, s);
            }));
    }
}
