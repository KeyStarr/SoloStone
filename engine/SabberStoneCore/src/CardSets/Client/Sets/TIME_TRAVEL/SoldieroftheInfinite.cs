using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SoldieroftheInfinite
    {
        internal const int DbfId = 120949;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.GiveBuff(ctx.SourceId, "TIME_051e")) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.MUL, 2)) };
        }
    }
}
