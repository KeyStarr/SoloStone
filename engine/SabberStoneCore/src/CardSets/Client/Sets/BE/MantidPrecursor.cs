using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MantidPrecursor
    {
        internal const int DbfId = 130634;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasWeapon);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HasWeapon((IPlayable)s)) new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, "BE_169e");
            }) };
        }

        private static bool HasWeapon(IPlayable source) => source.Controller.Hero.Weapon != null;

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1), Effects.Rush) };
        }
    }
}
