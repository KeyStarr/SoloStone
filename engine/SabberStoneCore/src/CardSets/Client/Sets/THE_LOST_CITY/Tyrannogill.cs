using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Tyrannogill
    {
        internal const int DbfId = 117949;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                int position = EffectContext.DeathrattlePosition(c,s);
                foreach (var id in new[] { "TLC_240t", "TLC_240t2", "TLC_240t3" })
                {
                    int summoned = new EffectContext(g,c.Id,s.Id).Summon(id, position);
                    if (summoned == 0) continue;
                    BonusEffects.GiveRandom((Minion)g.IdEntityDic[summoned], (IPlayable)s, 1);
                    position++;
                }
            }) };
        }
    }
}
