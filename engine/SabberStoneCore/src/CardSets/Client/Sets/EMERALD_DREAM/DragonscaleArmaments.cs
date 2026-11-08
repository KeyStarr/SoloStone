using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DragonscaleArmaments
    {
        internal const int DbfId = 114511;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: history-facts R2/v1 uses physical entity origin relative to caster; missing partitions do not draw.
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.DrawWhere(candidate => candidate.Card.Type == CardType.SPELL
                    && ctx.History.StartedInDeck(candidate.Id, ctx.ControllerId));
                ctx.DrawWhere(candidate => candidate.Card.Type == CardType.SPELL
                    && !ctx.History.StartedInDeck(candidate.Id, ctx.ControllerId));
                return TaskState.COMPLETE;
            }) };
        }
    }
}
