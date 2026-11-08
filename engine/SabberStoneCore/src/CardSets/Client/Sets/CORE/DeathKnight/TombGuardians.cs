using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TombGuardians
    {
        internal const int DbfId = 117028;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpses);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var summoned = new List<int>();
                for (int i = 0; i < 2; i++)
                {
                    int entity = ctx.Summon("RLK_118t3");
                    if (entity != 0) summoned.Add(entity);
                }
                if (!CanSpendCorpses((IPlayable)s) || !CorpseRules.TrySpend(c, 4)) return;
                foreach (int entity in summoned)
                    new ApplyEffectTask(EntityType.TARGET, new Effect(GameTag.REBORN, EffectOperator.SET, 1))
                        .Process(g, c, s, g.IdEntityDic[entity], stack);
            }) };
        }
        private static bool CanSpendCorpses(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 4;
        internal static void ConfigureToken(Card card) { card.Implemented = true; }
    }
}
