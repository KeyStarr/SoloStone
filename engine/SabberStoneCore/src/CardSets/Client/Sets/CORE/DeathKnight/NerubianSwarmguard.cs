using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NerubianSwarmguard
    {
        internal const int DbfId = 111319;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < 2; i++)
                    new SummonCopyTask(EntityType.SOURCE, side: SummonSide.NUMBER)
                        .Process(g, c, s, null, new TaskStack { Number = position + i + 1 });
            }) };
        }
    }
}
