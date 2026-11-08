using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EndangeredDodo
    {
        internal const int DbfId = 120749;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HeroHasTenOrLessHealth);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!HeroHasTenOrLessHealth((IPlayable)s)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.GiveBuff(s.Id, "TIME_703e");
                ctx.RunLegacy(new SummonCopyTask(EntityType.SOURCE,
                    side: s.Zone == c.BoardZone ? SummonSide.RIGHT : SummonSide.DEFAULT));
            }) };
        }

        private static bool HeroHasTenOrLessHealth(IPlayable source) => source.Controller.Hero.Health <= 10;

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(5)) };
        }
    }
}
