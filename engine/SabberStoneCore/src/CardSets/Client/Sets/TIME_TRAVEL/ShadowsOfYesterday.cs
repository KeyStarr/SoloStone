using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowsOfYesterday
    {
        internal const int DbfId = 120530;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                for (int i=0;i<4;i++)
                {
                    int summoned = new EffectContext(g,c.Id,s.Id).Summon("TIME_610t2");
                    if (summoned != 0) BonusEffects.GiveRandom((Minion)g.IdEntityDic[summoned], (IPlayable)s, 2);
                }
            }) };
        }
    }
}
