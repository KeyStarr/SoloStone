using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MorbidSwarm
    {
        [StandardCard("EDR_813", 114967)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanFundDamage);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.TargetingAvailabilityPredicate = (c, _) => c.ChooseBoth && HasDamageBranch(c);
            card.PlayAvailabilityPredicate = (c, _) => !c.BoardZone.IsFull || HasDamageBranch(c);
            card.Power = new Power();
        }
        private static bool HasDamageBranch(Controller owner) => owner.Game.EnableCorpses && CorpseRules.Available(owner) >= 2
            && Cards.FromId("EDR_813b").GetValidPlayTargets(owner).Count > 0;
        private static bool CanFundDamage(IPlayable source) => HasDamageBranch(source.Controller);
        [StandardCard("EDR_813a", 115797)]
        internal static void ConfigureAnts(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new SummonTask("EDR_813at", 2) };
        }
        [StandardCard("EDR_813b", 117525)]
        internal static void ConfigureDamage(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.PlayAvailabilityPredicate = (c, _) => c.Game.EnableCorpses && CorpseRules.Available(c) >= 2;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t != null && CorpseRules.TrySpend(c, 2)) new DamageTask(4, EntityType.TARGET, true).Process(g, c, s, t, stack);
            }) };
        }
        [StandardCard("EDR_813at", 117526)]
        internal static void ConfigureToken(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
