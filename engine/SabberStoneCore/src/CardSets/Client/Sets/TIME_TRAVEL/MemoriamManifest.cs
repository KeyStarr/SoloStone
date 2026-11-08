using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MemoriamManifest
    {
        internal const int DbfId = 120209;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var deaths = ctx.History.Deaths().Where(f => f.ControllerId == c.Id && (f.Races.Contains(Race.UNDEAD) || f.Races.Contains(Race.ALL))).ToArray();
                if (deaths.Length == 0 || c.BoardZone.IsFull) return;
                int cost = deaths.Max(f => f.Definition.ReadDefinition().Cost);
                var chosen = ctx.SampleDistinct(deaths.Where(f => f.Definition.ReadDefinition().Cost == cost).ToArray(), 1)[0];
                var minion = (Minion)Entity.FromCard(c, chosen.Definition.Materialize());
                minion[GameTag.DISPLAYED_CREATOR] = s.Id;
                Generic.SummonBlock(g, minion, -1, s);
            }) };
        }
    }
}
