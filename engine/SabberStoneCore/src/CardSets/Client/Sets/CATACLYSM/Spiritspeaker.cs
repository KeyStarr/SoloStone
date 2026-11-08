using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Spiritspeaker
    {
        internal const int DbfId = 123488;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/MEND_301/v1",
                DiscoverThenContinueTask.CompanionRoster(),
                new CustomTask((g, c, s, t, stack) => {
                    int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    new EffectContext(g, c.Id, s.Id, stack: stack).Summon(stack.Playables[0].Card.Id, position);
                    CompanionRules.SummonExtras(c, s);
                })) };
        }
    }
}
