using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WizenedWildspeaker
    {
        internal const int DbfId = 123478;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(NoMinionPlayedPreviousTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (NoMinionPlayedPreviousTurn((IPlayable)s))
                    new EffectContext(g, c.Id, s.Id, stack: stack).RefreshMana(3);
            }) };
        }

        private static bool NoMinionPlayedPreviousTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            // The previous-turn window excludes this play, but still counts this entity's earlier plays after a bounce.
            return !history.Plays(history.PreviousTurnOf(source.Controller.Id))
                .Any(f => f.ControllerId == source.Controller.Id && f.CardType == CardType.MINION);
        }
    }
}
