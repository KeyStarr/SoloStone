using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BrassknuckleBrawler
    {
        internal const int DbfId = 129946;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasTenArmor);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HasTenArmor((IPlayable)s)) new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, "BE_560e");
            }) };
        }

        private static bool HasTenArmor(IPlayable source) => source.Controller.Hero.Armor >= 10;

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(4)) };
        }
    }
}
