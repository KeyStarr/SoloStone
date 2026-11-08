using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KragwatheFrog
    {
        internal const int DbfId = 87920;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var history = g.HistoryFacts.Query;
                var plays = history.Plays(history.PreviousTurnOf(c.Id))
                    .Where(f => f.ControllerId == c.Id && f.CardType == CardType.SPELL && f.DefinitionKnown).ToList();
                plays.Shuffle(g.Random);
                foreach (var play in plays)
                {
                    var copy = Entity.FromCard(c, play.Definition.Materialize(),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    Generic.AddHandPhase(c, copy);
                }
            }) };
        }
    }
}
