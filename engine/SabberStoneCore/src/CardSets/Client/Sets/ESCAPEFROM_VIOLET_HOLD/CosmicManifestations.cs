using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CosmicManifestations
    {
        internal const int DbfId = 127159;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                int repeats = ctx.PlayedFromEdge ? 2 : 1;
                for (int i = 0; i < repeats; i++)
                {
                    if (ctx.TargetId is int target) ctx.Damage(target, 2, spellDamage: true);
                    var chosen = ctx.PickRandomCard(CosmicManifestationsPools.DemonHunterSpell(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.RunLegacy(new AddCardTo(chosen, EntityType.DECK));
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}
