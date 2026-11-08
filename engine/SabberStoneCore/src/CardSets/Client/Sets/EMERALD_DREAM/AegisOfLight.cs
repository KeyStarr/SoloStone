using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AegisOfLight
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);
        [OfflinePool("EDR_264", 116675)]
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("aegis-two-cost-minion", Pool(), new[] { "PALADIN" }, destination: PoolDestination.Summon) };
        [StandardCard("EDR_264", 116675)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(Pool(), recipeDbfId: 116675);
                int summoned = chosen == null ? 0 : ctx.Summon(chosen.Id);
                if (summoned != 0) ctx.RunLegacy(new SetGameTagTask(GameTag.TAUNT, 1, EntityType.TARGET), summoned);
                ctx.Imbue();
                return TaskState.COMPLETE;
            }) };
        }
    }
}
