using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OminousNightmares
    {
        internal const int DbfId = 116660;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Choose Both keeps the area damage playable when no buff target exists.
            SetTargeting(card, PlayReq.REQ_TARGET_IF_AVAILABLE);
        }
        private static void SetTargeting(Card card, PlayReq requirement)
        {
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { requirement, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_DAMAGED_TARGET, 0 } });
        }
        internal static void ConfigureDamage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new DamageTask(1, EntityType.ALLMINIONS, true) };
        }
        internal static void ConfigureBuffOption(Card card)
        {
            card.Implemented = true;
            SetTargeting(card, PlayReq.REQ_TARGET_TO_PLAY);
            card.Power = new Power { PowerTask = new AddEnchantmentTask("EDR_570e", EntityType.TARGET) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
