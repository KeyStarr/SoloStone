using System;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Adventures.DungeonRun
{
    // Dungeon Run round 1 bosses (https://hearthstone.wiki.gg/wiki/Dungeon_Run). Names, Health and text come from the facts
    // file; these modules add the behaviour.
    internal static class DungeonRunRound1
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Giant Rat: 10 Health. Rat Race (2): Summon two 1/1 Rats.
            register(47366, "LOOTA_BOSS_18h", DungeonRunCards.Plain);
            register(45894, "LOOTA_BOSS_18p", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                card.Power = new Enchants.Power { PowerTask = new SummonTask("LOOTA_BOSS_18t", 2) };
                DungeonRunCards.Plain(card);
            });
            register(47621, "LOOTA_BOSS_18t", DungeonRunCards.Plain);

            // Wee Whelp: 10 Health. Baby Breath (2): Deal 2 damage.
            register(47309, "LOOTA_BOSS_44h", DungeonRunCards.Plain);
            register(47308, "LOOTA_BOSS_44p", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
                card.Power = new Enchants.Power { PowerTask = new DamageTask(2, EntityType.TARGET) };
                DungeonRunCards.Plain(card);
            });

            // Bink the Burglar: 10 Health. Coin (0, autocast: AI_MUST_PLAY): Gain 1 Mana Crystal this turn only.
            register(47316, "LOOTA_BOSS_45h", DungeonRunCards.Plain);
            register(47315, "LOOTA_BOSS_45p", card => {
                card.Power = new Enchants.Power { PowerTask = new TempManaTask(1) };
                DungeonRunCards.Plain(card);
            });
        }
    }
}
