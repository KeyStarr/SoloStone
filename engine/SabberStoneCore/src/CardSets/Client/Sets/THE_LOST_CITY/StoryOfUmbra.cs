using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoryOfUmbra
    {
        internal const int DbfId = 118555;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/118555/story-of-umbra/v1",
                new DiscoverThenContinueTask(OfflinePoolDeclarations.ExpensiveDeathrattleMinion()),
                new CustomTask((g, c, s, t, stack) => {
                    var selected = stack.Playables?.LastOrDefault();
                    if (selected == null) return;
                    int summoned = new EffectContext(g, c.Id, s.Id, stack: stack).Summon(selected.Card.Id);
                    if (summoned == 0) return;
                    var minion = (Minion)g.IdEntityDic[summoned];
                    // Legacy Deathrattle summons use this cursor even while their source is alive.
                    minion.LastBoardPosition = minion.ZonePosition + 1;
                    new ActivateDeathrattleTask(EntityType.TARGET).Process(g, c, s, minion, stack);
                })) };
        }
    }
}
