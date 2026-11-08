using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EntropicRift
    {
        internal const int DbfId = 129256;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.ShadowSpell(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}
