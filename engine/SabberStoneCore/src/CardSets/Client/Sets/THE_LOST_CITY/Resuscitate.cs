using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Resuscitate
    {
        internal const int DbfId = 117557;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // Burned/discarded minions have no death marker; each actual death is one pool entry.
                var dead = c.GraveyardZone.OfType<Minion>().Where(m => m.ToBeDestroyed).Select(m => m.Card).ToArray();
                var resurrected = new List<Minion>();
                for (int cost = 1; cost <= 3 && !c.BoardZone.IsFull; cost++)
                {
                    var pool = dead.Where(card => card.Cost == cost).ToArray();
                    if (pool.Length == 0) continue;
                    if (pool.Length > 1) g.OnRandomHappened(true);
                    var cardToRaise = pool[pool.Length == 1 ? 0 : g.Random.Next(pool.Length)];
                    var raised = (Minion)Entity.FromCard(c, cardToRaise);
                    Generic.SummonBlock(g, raised, -1, s);
                    resurrected.Add(raised);
                }
                foreach (var raised in resurrected) raised[GameTag.REBORN] = 1;
            }) };
        }
    }
}
