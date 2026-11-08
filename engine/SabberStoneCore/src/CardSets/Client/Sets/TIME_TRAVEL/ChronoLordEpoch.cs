using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChronoLordEpoch
    {
        internal const int DbfId = 120817;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: destroy live incarnations from opponent's last begun turn's plays, on either side; new bodies do not inherit plays (design R3).
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var history = g.HistoryFacts.Query;
                var plays = history.Plays(history.PreviousTurnOf(c.Opponent.Id))
                    .Where(f => f.ControllerId == c.Opponent.Id && f.CardType == CardType.MINION).Select(f => f.PlayId).ToHashSet();
                var victims = c.BoardZone.Concat(c.Opponent.BoardZone)
                    .Where(m => !m.Untouchable && plays.Contains(g.HistoryFacts.Board(m.Id)?.PlayId ?? 0)).ToArray();
                foreach (var victim in victims) new DestroyTask(EntityType.TARGET).Process(g, c, s, victim, stack);
            }) };
        }
    }
}
