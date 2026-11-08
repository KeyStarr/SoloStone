using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DrakeadonMongrel
    {
        internal const int DbfId = 122641;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source ? System.Math.Clamp(source.LastBoardPosition, 0, c.BoardZone.SlotCount) : c.BoardZone.SlotCount;
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.FourCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id, position++);
                }
            }) };
        }
    }
}
