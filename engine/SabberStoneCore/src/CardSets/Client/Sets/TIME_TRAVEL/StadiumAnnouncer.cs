using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StadiumAnnouncer
    {
        internal const int DbfId = 120503;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (bool opponent in new[] { false, true })
                {
                    var chosen = ctx.PickRandomCard(TimeTravelPools.AnyWeapon(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.RunLegacy(new WeaponTask(chosen.Id, opponent));
                }
                if (c.Hero.Weapon != null) ctx.GiveBuff(c.Hero.Weapon.Id, "TIME_034e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1),
                new Effect(GameTag.DURABILITY, EffectOperator.ADD, 1)) };
        }
    }
}
