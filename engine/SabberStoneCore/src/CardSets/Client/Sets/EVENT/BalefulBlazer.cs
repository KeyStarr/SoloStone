using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BalefulBlazer
    {
        [StandardCard("CATA_EVENT_002", 126847)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(FirePlayed);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (c, _) => SpellPlayHistory.PlayedSchoolThisTurn(c, 2);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t != null && t.Zone == t.Controller.BoardZone && !t.ToBeDestroyed && FirePlayed((IPlayable)s))
                    new DestroyTask(EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }
        private static bool FirePlayed(IPlayable source) => SpellPlayHistory.PlayedSchoolThisTurn(source.Controller, 2);
    }
}
