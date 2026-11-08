using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RecklessDetective
    {
        internal const int DbfId = 126479;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new AddCardTo("JAIL_447t", EntityType.HAND) };
        }

        // Detective's Clothes JAIL_447t: "Give a minion +4/+4 and Rush." Any minion; export has no play requirements.
        internal static void ConfigureClothes(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("JAIL_447e", EntityType.TARGET) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4), Effects.Health_N(4), Effects.Rush) };
        }
    }
}
