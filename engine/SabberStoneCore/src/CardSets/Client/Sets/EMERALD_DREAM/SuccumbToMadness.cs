using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SuccumbToMadness
    {
        internal const int DbfId = 114217;
        [StandardCard("EDR_455", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                var dragons = ctx.History.Deaths().Where(f => f.ControllerId == c.Id
                    && (f.Races.Contains(Race.DRAGON) || f.Races.Contains(Race.ALL)))
                    .GroupBy(f => f.DbfId).Select(group => group.First().Definition.Materialize()).ToArray();
                if (ctx.Discover(ctx.SampleDistinct(dragons, 3), ChoiceAction.SUMMON))
                    foreach (int offer in c.Choice.Choices)
                        g.IdEntityDic[offer][Generic.PrivateDecisionOfferTag] = c.PlayerId;
            }) };
        }
    }
}
