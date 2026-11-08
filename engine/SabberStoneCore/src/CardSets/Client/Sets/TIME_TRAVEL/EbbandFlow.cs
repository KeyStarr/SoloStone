using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EbbandFlow
    {
        internal const int DbfId = 120748;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(PlayedMinionWhileHolding);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power {
                // PlayCard.PlayMinion raises this event for actual plays, never for summons.
                Trigger = new Trigger(TriggerType.PLAY_MINION) {
                    TriggerActivation = TriggerActivation.HAND,
                    TriggerSource = TriggerSource.FRIENDLY,
                    SingleTask = new SetGameTagTask(GameTag.TAG_SCRIPT_DATA_NUM_1, 1, EntityType.SOURCE)
                },
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                    if (t != null) ctx.Damage(t.Id, 3, spellDamage: true);
                    if (PlayedMinionWhileHolding((IPlayable)s)) ctx.RunLegacy(new ArmorTask(5));
                })
            };
        }

        private static bool PlayedMinionWhileHolding(IPlayable source) => source[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0;
    }
}
