using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Frostshatter
    {
        internal const int DbfId = 126410;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.HandProgress = new HandProgressDefinition("JAIL_803t", 3, HandProgressMode.OwnerSpells);
            card.Power = new Power { PowerTask = ComplexTask.Create(ComplexTask.Freeze(EntityType.TARGET), new DrawTask(), new DrawTask()) };
        }
        internal static void ConfigureElemental(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = ComplexTask.Create(ComplexTask.Freeze(EntityType.TARGET), new DrawTask(), new DrawTask()) };
        }
    }
}
