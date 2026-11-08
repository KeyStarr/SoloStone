using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Daze
    {
        [StandardCard("CATA_215", 123315)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t is Minion minion && Generic.ReturnToHandBlock(minion.Controller, minion))
                    PlayLockRules.LockHand(minion, (IPlayable)s, "CATA_215e", g.CurrentPlayer == minion.Controller ? 2 : 1);
            }) };
        }

        [StandardCard("CATA_215e", 123378)]
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = PlayLockRules.Expiry() };
        }
    }
}
