using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MoltenGold
    {
        internal const int DbfId = 126407;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.HandProgress = new HandProgressDefinition("JAIL_801t", 3, HandProgressMode.OwnerSpells);
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.TARGET, true) };
        }
        internal static void ConfigureElemental(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.TARGET) };
        }
    }
}
