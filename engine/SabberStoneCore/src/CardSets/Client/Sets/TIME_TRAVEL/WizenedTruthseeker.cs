using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WizenedTruthseeker
    {
        internal const int DbfId = 120962;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var target in c.HandZone.Concat(c.Opponent.HandZone).Cast<Playable>().ToArray())
                {
                    CostRules.RestorePrintedCost(target);
                    Enchantment.GetInstance(c, (IPlayable)s, target, Cards.FromId("TIME_057e"));
                }
            }) };
        }
    }
}
