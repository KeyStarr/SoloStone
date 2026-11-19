using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystalsmithCultist
    {
        internal const int DbfId = 129166;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HoldingShadowSpell);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HoldingShadowSpell((IPlayable)s))
                    new AddEnchantmentTask("RLK_814e", EntityType.SOURCE).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }

        private static bool HoldingShadowSpell(IPlayable source) =>
            source.Controller.HandZone.Any(p => p.Id != source.Id && p is Spell
                && p.Card[GameTag.SPELL_SCHOOL] == 6);

    }
}
