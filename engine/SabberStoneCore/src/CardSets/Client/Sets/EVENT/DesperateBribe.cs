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
    internal static class DesperateBribe
    {
        [StandardCard("JAIL_EVENT_102", 130087)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var owner in new[] { c, c.Opponent })
                    for (int i = 0; i < 2; i++)
                    {
                        var chosen = EventCardPools.PickMinion(g, c, s, 2, PoolDestination.Summon);
                        if (chosen == null || owner.BoardZone.IsFull) continue;
                        var body = (Minion)Entity.FromCard(owner, chosen);
                        body[GameTag.DISPLAYED_CREATOR] = s.Id;
                        Generic.SummonBlock(g, body, owner.BoardZone.SlotCount, s);
                        g.DeathProcessingAndAuraUpdate();
                    }
                foreach (var minion in c.BoardZone.Where(m => !m.IsDormant && !m.ToBeDestroyed).ToArray())
                {
                    if (minion.Zone != c.BoardZone || minion.ToBeDestroyed) continue;
                    var chosen = EventCardPools.PickMinion(g, c, s, minion.Card.Cost + 1, PoolDestination.Transform);
                    if (chosen != null) Generic.TransformBlock(c, chosen, minion);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
