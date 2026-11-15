using System;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets.Legacy
{
    // Legacy (Wild) cards whose legacy definition was left empty, completed where run content needs them (strict admission:
    // every card a run mode offers must be implemented).
    internal static class LegacyWildCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Power of the Wild (Dungeon Run's Druid start deck): Choose One - its two halves (EX1_160a/b, shared with the
            // Core reprint CORE_EX1_160) carry the effects, as for the reprint.
            register(503, "EX1_160", card => card.Implemented = true);

            // One-eyed Cheat (Bink the Burglar's deck): Whenever you summon a Pirate, gain Stealth.
            register(1990, "GVG_025", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                        // Another friendly minion: summoning the Cheat itself (a Pirate) gives no Stealth.
                        TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
                        Condition = SelfCondition.IsRace(Race.PIRATE),
                        SingleTask = new SetGameTagTask(GameTag.STEALTH, 1, EntityType.SOURCE)
                    }
                };
                card.Implemented = true;
            });
        }
    }
}
