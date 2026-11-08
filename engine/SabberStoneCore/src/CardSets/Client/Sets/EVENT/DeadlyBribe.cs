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
    internal static class DeadlyBribe
    {
        [StandardCard("CATA_EVENT_402", 126097)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = Effect(false), ComboTask = Effect(true) };
        }
        private static CustomTask Effect(bool combo) => new CustomTask((g, c, s, t, stack) => {
            new DestroyTask(EntityType.TARGET, true).Process(g, c, s, t, stack);
            new AddCardTo("GAME_005", EntityType.OP_HAND).Process(g, c, s, null, stack);
            if (combo) new AddCardTo("GAME_005", EntityType.HAND).Process(g, c, s, null, stack);
        });
    }
}
