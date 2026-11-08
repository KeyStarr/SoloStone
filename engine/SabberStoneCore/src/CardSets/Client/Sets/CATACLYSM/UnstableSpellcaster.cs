using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnstableSpellcaster
    {
        internal const int DbfId = 123351;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(DealtSpellDamageThisTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!DealtSpellDamageThisTurn((IPlayable)s)) return;
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                new SummonCopyTask(EntityType.SOURCE, side: SummonSide.NUMBER)
                    .Process(g, c, s, t, new TaskStack { Number = position + 1 });
            }) };
        }

        private static bool DealtSpellDamageThisTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return history.AnySpellDamage(source.Controller.Id, history.ThisTurn);
        }
    }
}
