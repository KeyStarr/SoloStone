using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ResistanceAura
    {
        [StandardCard("TTN_851", 98053)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card, 2, AuraSpellClock.EnemyEnd,
            aura: new Aura(AuraType.OP_HAND, "TTN_851e") {
                Condition = SelfCondition.IsSpell
            });
        [StandardCard("TTN_851e", 98065)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.COST, EffectOperator.ADD, 1)) };
        }
    }
}
