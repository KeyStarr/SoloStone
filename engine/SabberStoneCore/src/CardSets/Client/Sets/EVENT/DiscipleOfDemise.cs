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
    internal static class DiscipleOfDemise
    {
        [StandardCard("TIME_EVENT_301", 126697)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int attempts = 1 + c.HandZone.Count(p => p.Card.Type == CardType.MINION && p.Card.IsRace(Race.DRAGON));
                for (int i = 0; i < attempts; i++)
                {
                    var victims = c.BoardZone.Concat(c.Opponent.BoardZone)
                        .Where(m => m.Id != s.Id && !m.IsDormant && !m.ToBeDestroyed).ToArray();
                    if (victims.Length == 0) break;
                    new DestroyTask(EntityType.TARGET, true).Process(g, c, s, victims[g.Random.Next(victims.Length)], stack);
                }
            }) };
        }
    }
}
