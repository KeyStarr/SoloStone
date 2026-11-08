using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EndtimeSurvivor
    {
        internal const int DbfId = 121166;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HeroTookDamageThisTurn);
            card.ConsumesHistory = true;
            // Rule: positive friendly-hero damage this global turn includes armor; payment/Health setting does not (design R5).
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                if (HeroTookDamageThisTurn((IPlayable)s)) ctx.GiveBuff(s.Id, "END_019e");
            }) };
        }

        private static bool HeroTookDamageThisTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return history.HeroTookDamage(source.Controller.Id, history.ThisTurn);
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(3)) };
        }
    }
}
