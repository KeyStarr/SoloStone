using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FelInfusion
    {
        internal const int DbfId = 122848;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, source, target, stack) => {
                new EffectContext(g, c.Id, source.Id, stack: stack).Herald();
                var enchantment = Enchantment.GetInstance(c, (IPlayable)source, c.Hero, Cards.FromId("CATA_530e"));
                enchantment.ExpireAtEndOfTurn();
                enchantment.Card.Power.Enchant.ActivateTo(c.Hero);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.LIFESTEAL, EffectOperator.ADD, 1)) { IsOneTurnEffect = true } };
        }
    }
}
