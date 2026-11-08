using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShokkJungleTyrant
    {
        internal static OfflinePoolFilter Filter(int attack) => new OfflinePoolFilter(
            classMode: PoolClassMode.Any, type: CardType.MINION, race: Race.BEAST, exactAttack: attack);
        [StandardCard("TLC_830t", 117673)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var steps = new System.Collections.Generic.List<ISimpleTask>();
            foreach (int attack in new[] {8,6,4})
            {
                steps.Add(new DiscoverThenContinueTask(Filter(attack), recipeDbfId: 117673));
                steps.Add(new CustomTask((g,c,s,t,stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    chosen.Cost = 2;
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                }));
            }
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TLC_830t/v1", steps.ToArray()) };
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B16C16Pools
    {
        [OfflinePool("TLC_830t", 117673)]
        internal static PoolRequirement[] Declare() => new[] {8,6,4}.Select(attack =>
            new PoolRequirement("shokk-attack-" + attack, Sets.ShokkJungleTyrant.Filter(attack), new[] {"HUNTER"})).ToArray();
    }
}
