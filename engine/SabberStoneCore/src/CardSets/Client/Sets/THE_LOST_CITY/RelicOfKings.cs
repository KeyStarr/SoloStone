using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RelicOfKings
    {
        internal const int DbfId = 118616;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TLC_334/v1",
                new DiscoverThenContinueTask(TheLostCityPools.ExpensiveSpell()),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen == null) return;
                    c.SetasideZone.Remove(chosen);
                    new Enchant(Effects.SetCost(1)).ActivateTo(chosen);
                    Generic.AddHandPhase.Invoke(c, chosen);
                })) };
        }
    }
}
