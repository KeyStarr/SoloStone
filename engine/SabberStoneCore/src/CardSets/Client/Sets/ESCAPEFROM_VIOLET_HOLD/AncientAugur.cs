using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AncientAugur
    {
        internal const int DbfId = 125878;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_303/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.EnemyHand),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables?.LastOrDefault();
                    if (chosen == null) return;
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, s, Cards.FromId("JAIL_303e"));
                    actor[DiscoverRealRules.BoundEntity] = chosen.Id;
                    actor[DiscoverRealRules.WatchedController] = chosen.Controller.Id;
                    actor[DiscoverRealRules.WatchedTurn] = g.HistoryFacts.Query.Residences(chosen.Id, Zone.HAND).Count();
                })) };
        }
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var actor = t as Enchantment ?? s as Enchantment;
                if (actor == null || !g.IdEntityDic.TryGetValue(actor[DiscoverRealRules.BoundEntity], out var chosen)) return;
                var owner = g.ControllerById(actor[DiscoverRealRules.WatchedController]);
                if (chosen.Zone == owner.HandZone && g.HistoryFacts.Query.Residences(chosen.Id, Zone.HAND).Count() == actor[DiscoverRealRules.WatchedTurn])
                    Generic.DiscardBlock(owner, chosen);
            }) };
        }
    }
}
