using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CaliaMenethil
    {
        internal const int DbfId = 126888;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (c.BoardZone.IsFull) return;
                var deaths = g.HistoryFacts.Query.Deaths().Where(f => f.ControllerId == c.Id).ToArray();
                if (deaths.Length == 0) return;
                int cost = deaths.Max(f => f.CostAtDeath);
                var tied = deaths.Where(f => f.CostAtDeath == cost).ToArray();
                var definition = tied[g.Random.Next(tied.Length)].Definition.Materialize();
                Generic.SummonBlock(g, (Minion)Entity.FromCard(c, definition), -1, s);
            }) };
        }
    }
}
