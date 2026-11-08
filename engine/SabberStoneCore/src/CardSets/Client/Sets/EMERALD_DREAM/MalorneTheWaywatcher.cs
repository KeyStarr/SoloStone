using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MalorneTheWaywatcher
    {
        internal const int DbfId = 115186;
        private static OfflinePoolFilter Filter() => new OfflinePoolFilter(type: CardType.MINION,
            cardIds: new[] { "EDR_031", "EDR_209", "EDR_421", "EDR_430", "EDR_480", "EDR_471", "EDR_527", "EDR_259", "EDR_489", "EDR_895", "EDR_819" });
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("legendary-wild-gods", Filter(), new[] { "NEUTRAL" }) };
        [StandardCard("EDR_888", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(Ready);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_888/v1",
                new DiscoverThenContinueTask(Filter(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    if (Ready((IPlayable)s)) Effects.SetCost(1).ApplyTo(chosen);
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                })) };
        }
        private static bool Ready(IPlayable source) => ImbueRules.Count(source.Controller) >= 4;
    }
}
