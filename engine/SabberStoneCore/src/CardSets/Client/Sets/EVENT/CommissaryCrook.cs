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
    internal static class CommissaryCrook
    {
        [StandardCard("CATA_EVENT_400", 130005)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int spent = Math.Max(0, c.RemainingMana);
                Generic.SpendMana(c, (IPlayable)s, spent, HistoryPaymentKind.Other);
                var chosen = EventCardPools.PickMinion(g, c, s, spent, PoolDestination.Summon);
                if (chosen != null) new EffectContext(g, c.Id, s.Id).Summon(chosen.Id);
            }) };
        }
    }
}
