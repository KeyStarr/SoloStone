using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreBoomkin
    {
        [StandardCard("CORE_ONY_018", 129216)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
        }
        [StandardCard("ONY_018t", 71264)]
        internal static void ConfigureHeal(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new HealTask(8, EntityType.HERO) };
        }
        [StandardCard("ONY_018t2", 71265)]
        internal static void ConfigureDamage(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.TARGET) };
        }
    }
}
