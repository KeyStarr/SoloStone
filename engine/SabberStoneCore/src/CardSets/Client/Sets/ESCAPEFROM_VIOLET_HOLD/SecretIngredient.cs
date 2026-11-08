using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SecretIngredient
    {
        internal const int DbfId = 127932;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("JAIL_201ae1", EntityType.HERO) };
        }

        internal static void ConfigureRandom(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.DruidCard(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect = true } };
        }
    }
}
