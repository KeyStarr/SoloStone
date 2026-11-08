using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FrostStrike
    {
        internal const int DbfId = 78325;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, target.Id, stack);
                ctx.Damage(target.Id, 3, spellDamage: true);
                // frost-strike-timing.yaml: retail build 175913 creates offers before death cleanup.
                if (target.IsDead) ctx.Discover(OfflinePoolDeclarations.FrostRuneCard(), recipeDbfId: DbfId);
            }) };
        }
    }
}
