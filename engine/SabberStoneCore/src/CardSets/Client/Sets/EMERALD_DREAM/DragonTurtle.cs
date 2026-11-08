using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DragonTurtle
    {
        internal const int DbfId = 115755;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingDarkGift);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!EmeraldDreamConditions.HoldingDarkGift((IPlayable)s)) return;
                Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_956e"), (IPlayable)s, c.Hero);
                new ArmorTask(6).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) { IsOneTurnEffect = true } };
        }
    }
}
