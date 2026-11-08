using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AirSupport
    {
        internal const int DbfId = 122952;
        [StandardCard("CATA_564", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_564e", EntityType.TARGET) };
        }

        [StandardCard("CATA_564e", 128591)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.MEGA_WINDFURY, EffectOperator.SET, 1),
                new Effect(GameTag.CANNOT_ATTACK_HEROES, EffectOperator.SET, 1)) };
        }
    }
}
