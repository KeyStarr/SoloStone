using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
namespace SabberStoneCore.Actions
{
    public static class SpellDamageRules
    {
        public static bool PreventsHealing(ICharacter target) => target is Hero
            && (target.Controller.Opponent.AppliedEnchantments?.Any(e => e.Card.Id == "TLC_250e") ?? false);

        public static int Bonus(IPlayable source) => source is Spell spell
            ? DamageArithmetic.Double(DamageArithmetic.Add(source.Controller.CurrentSpellPower, source[GameTag.SPELLPOWER]), spell.ReceveivesDoubleSpellDamage ? 1 : 0)
            : source.Controller.CurrentSpellPower;
        public static int Amount(IPlayable source, int amount) => DamageArithmetic.Double(DamageArithmetic.Add(amount, Bonus(source)),
            source.Controller.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
    }
}
