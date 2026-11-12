using System;
using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Adventures
{
    // Dungeon Run (Kobolds & Catacombs adventure, LOOTA_*) boss cards. Proof of concept: the first boss only.
    // None of these are collectible, and all stay out of the legacy random pools, so no random effect or recorded
    // game draws them.
    internal static class DungeonRunCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Giant Rat: 10 Health, Hero Power "Rat Race" (2): Summon two 1/1 Rats.
            register(47366, "LOOTA_BOSS_18h", Plain);
            register(45894, "LOOTA_BOSS_18p", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                card.Power = new Power { PowerTask = new SummonTask("LOOTA_BOSS_18t", 2) };
                Plain(card);
            });
            register(47621, "LOOTA_BOSS_18t", Plain);
        }

        private static void Plain(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
