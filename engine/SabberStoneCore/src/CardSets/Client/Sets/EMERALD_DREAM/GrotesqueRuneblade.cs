using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GrotesqueRuneblade
    {
        internal const int DbfId = 114304;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasRuneBonus);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!HasRuneBonus((IPlayable)s)) return;
                var previous = PreviousCard((IPlayable)s);
                if (previous[(GameTag)2198] > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_812e"), (IPlayable)s, s);
                if (previous[(GameTag)2196] > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_812e1"), (IPlayable)s, s);
            }) };
        }

        // PlayCard appends the source before its Battlecry; ignore its entity in either phase.
        private static Card PreviousCard(IPlayable source) => source.Controller.PlayHistory
            .LastOrDefault(play => play.SourceId != source.Id).SourceCard;

        private static bool HasRuneBonus(IPlayable source)
        {
            var previous = PreviousCard(source);
            return previous != null && (previous[(GameTag)2198] > 0 || previous[(GameTag)2196] > 0);
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }
        internal static void ConfigureDurability(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(GameTag.DURABILITY, EffectOperator.ADD, 1) };
        }
    }
}
