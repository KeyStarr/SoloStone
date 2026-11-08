using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MarkofUrsol
    {
        internal const int DbfId = 114630;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t != null) new AddEnchantmentTask(t.Controller == c ? "EDR_252e1" : "EDR_252e", EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureEnemyBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(1), Effects.SetMaxHealth(1)) };
        }
        internal static void ConfigureFriendlyBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(3), Effects.SetMaxHealth(3)) };
        }
    }
}
