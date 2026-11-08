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
    internal static class PrimordialLord
    {
        [StandardCard("CATA_EVENT_000", 126698)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(EventCardPools.PastColossal(), recipeDbfId: 126698);
                if (chosen != null) new EffectContext(g, c.Id, s.Id).AddToHand(chosen.Id);
            }) };
        }
        // Exported annotation, not attached: printed Battlecry promises no discount.
        [StandardCard("CATA_EVENT_000e", 126699)]
        internal static void Annotation(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power();
        }
    }
}
