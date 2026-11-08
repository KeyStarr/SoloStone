using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MalignantHorror
    {
        internal const int DbfId = 120876;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var source = (Minion)s;
                    if (source.Zone != c.BoardZone || source.IsDead || c.BoardZone.IsFull || !CorpseRules.TrySpend(c, 4)) return;
                    new SummonCopyTask(EntityType.SOURCE, side: SummonSide.NUMBER)
                        .Process(g, c, source, null, new TaskStack { Number = source.ZonePosition + 2 });
                })
            } };
        }
    }
}
