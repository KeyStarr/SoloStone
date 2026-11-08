using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class MarkoftheWild
    {
        internal const int DbfId = 69511;
        internal static void Configure(Card card)
        {
            var enchantment = ClientOverlay.Snapshot("CS2_009e");
            enchantment.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(3), new Effect(GameTag.TAUNT, EffectOperator.SET, 1)) };
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) =>
                Generic.AddEnchantmentBlock(g, enchantment, (IPlayable)s, t, 0, 0, 0)) };
        }
    }
}
