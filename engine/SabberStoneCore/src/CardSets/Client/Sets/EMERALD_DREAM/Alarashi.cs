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
    internal static class Alarashi
    {
        internal const int DbfId = 115928;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON);
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var context = new EffectContext(g,c.Id,s.Id);
                foreach (var minion in c.HandZone.OfType<Minion>().ToArray())
                {
                    var demon = context.PickRandomCard(Filter, recipeDbfId: DbfId);
                    if (demon != null) CostRules.TransformHandKeepingValues(minion, demon, (IPlayable)s, "EDR_493e2", "EDR_493e");
                }
            }) };
        }
        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(0), Effects.SetMaxHealth(0)) { UseScriptTag = true } };
        }
        internal static void ConfigureCost(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetCost(0)) { UseScriptTag = true } };
        }
    }
}
