using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LightoftheNewMoon
    {
        internal const int DbfId = 115642;
        internal static void Configure(Card card)
        {
            ConfigureBuffSpell(card);
            card.ExcludeFromLegacyRandomPools = true;
            card.HandProgress = new HandProgressDefinition("FIR_918t", 3, HandProgressMode.OwnerSpells);
        }

        internal static void ConfigureFullMoon(Card card)
        {
            ConfigureBuffSpell(card);
            card.Power.PowerTask = ComplexTask.Create(card.Power.PowerTask, new AddCardTo("FIR_918", EntityType.HAND));
        }

        private static void ConfigureBuffSpell(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("FIR_918e1", EntityType.TARGET) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(3)) };
        }
    }
}
