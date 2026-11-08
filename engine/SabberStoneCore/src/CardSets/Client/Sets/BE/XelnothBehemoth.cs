using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class XelnothBehemoth
    {
        internal const int DbfId = 131157;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int alternate = 0;
                int position = SummonTask.GetPosition(s, SummonSide.RIGHT, 0, ref alternate);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.OneCostMinion(), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    int summoned = ctx.Summon(chosen.Id, Math.Min(position, c.BoardZone.SlotCount));
                    if (summoned != 0 && g.IdEntityDic[summoned].Zone == c.BoardZone)
                        position = g.IdEntityDic[summoned].ZonePosition + 1;
                }
                foreach (int minion in ctx.FriendlyMinionIds) ctx.GiveBuff(minion, "BE_749e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }
    }
}
