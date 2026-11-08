using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrecursoryStrike
    {
        internal const int DbfId = 120975;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HoldingExpensiveMinion);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                if (t != null) ctx.Damage(t.Id, 3, spellDamage: true);
                if (HoldingExpensiveMinion((IPlayable)s))
                    ctx.RunLegacy(ComplexTask.DrawFromDeck(1, SelfCondition.IsMinion));
            }) };
        }

        private static bool HoldingExpensiveMinion(IPlayable source) => source.Controller.HandZone
            .Any(e => e.Id != source.Id && e.Card.Type == CardType.MINION && e.Cost >= 5);
    }
}
