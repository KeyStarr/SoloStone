using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AetherialKindling
    {
        internal const int DbfId = 129892;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target) ctx.Damage(target, 3, spellDamage: true);
                var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.ThreeCostMinion(), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }
    }
}
