using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Cindersword
    {
        internal const int DbfId = 115649;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingDarkGift);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (EmeraldDreamConditions.HoldingDarkGift((IPlayable)s))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_922e"), (IPlayable)s, s);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }
    }
}
