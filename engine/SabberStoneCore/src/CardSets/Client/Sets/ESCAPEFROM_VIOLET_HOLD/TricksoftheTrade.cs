using System.Collections.Generic;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TricksoftheTrade
    {
        internal const int DbfId = 127073;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(StealthedMinionAttacked);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.ATTACK) {
                    TriggerActivation = TriggerActivation.HAND,
                    TriggerSource = TriggerSource.ALL_MINIONS,
                    Condition = new SelfCondition(p => ((Minion)p).HasStealth),
                    SingleTask = new SetGameTagTask(GameTag.TAG_SCRIPT_DATA_NUM_1, 1, EntityType.SOURCE)
                },
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    new DamageTask(StealthedMinionAttacked((IPlayable)s) ? 3 : 1, EntityType.TARGET, spellDmg: true)
                        .Process(g, c, s, t, stack);
                })
            };
        }

        private static bool StealthedMinionAttacked(IPlayable source) =>
            source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1;

    }
}
