using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ZakajzTheCorruptor
    {
        internal const int DbfId = 130285;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var target in c.HandZone.Concat(c.DeckZone).ToArray())
                    if (target.Card.Type == CardType.MINION || target.Card.Type == CardType.SPELL)
                        CostRules.AddModifier((Playable)target, target.Card.Type == CardType.MINION ? EffectOperator.SUB : EffectOperator.ADD, 1);
            }) };
        }
    }
}
