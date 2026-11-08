using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Liferender
    {
        internal const int DbfId = 121015;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HeroHealthChangedThisTurn);
            card.ConsumesHistory = true;
            // Rule: any actual friendly hero Health change this global turn qualifies; armor alone does not (design R5).
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (c, _) => HeroHealthChangedThisTurn(c);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t != null && HeroHealthChangedThisTurn((IPlayable)s))
                    new DamageTask(6, EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }

        private static bool HeroHealthChangedThisTurn(IPlayable source) => HeroHealthChangedThisTurn(source.Controller);

        private static bool HeroHealthChangedThisTurn(Controller controller)
        {
            var history = controller.Game.HistoryFacts.Query;
            return history.HeroHealthChanged(controller.Id, history.ThisTurn);
        }
    }
}
