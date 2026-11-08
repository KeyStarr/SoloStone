using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArcaneFlow
    {
        internal const int DbfId = 123692;
        internal static void Configure(Card card)
        {
            ConfigureFamily(card);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = ComplexTask.Create(new DamageTask(4, EntityType.TARGET, true),
                new IncludeTask(EntityType.ENEMIES), new DamageTask(2, EntityType.STACK, true)) };
        }
        internal static void ConfigureFirst(Card card)
        {
            ConfigureFamily(card);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.TARGET, true) };
        }
        internal static void ConfigureSecond(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(new IncludeTask(EntityType.ENEMIES), new DamageTask(2, EntityType.STACK, true)) };
        }
        private static void ConfigureFamily(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Shatter = new ShatterDefinition("CATA_489", "CATA_489t", "CATA_489t2");
        }
    }
}
