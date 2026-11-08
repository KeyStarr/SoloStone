using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForebodingFreeze
    {
        internal const int DbfId = 131200;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                ctx.RunLegacy(new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET));
                var chosen = ctx.PickRandomCard(BEPools.FiveCostMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                int summoned = ctx.Summon(chosen.Id, s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount);
                if (summoned != 0) ctx.RunLegacy(new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET), summoned);
            }) };
        }
    }
}
