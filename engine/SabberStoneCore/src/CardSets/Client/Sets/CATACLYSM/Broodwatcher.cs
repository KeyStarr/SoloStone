using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Broodwatcher
    {
        internal const int DbfId = 122968;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(IsReady);
            // Rule: R6 counts this hand residence's Mana, excluding own play; copies/bounces reset, ready overflow summons are lost.
            card.Power = new Power {
                Trigger = new MultiTrigger(Display(TriggerType.ZONE), Display(TriggerType.AFTER_PLAY_CARD),
                    Display(TriggerType.AFTER_CAST), Display(TriggerType.INSPIRE), Display(TriggerType.CARD_PREPARED)),
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    if (!IsReady((IPlayable)s))
                    {
                        ctx.AddToHand("CATA_132t", 2);
                        return;
                    }
                    int position = s is Minion source && source.Zone == c.BoardZone
                        ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    ctx.Summon("CATA_132t", position);
                    ctx.Summon("CATA_132t", position + 1);
                })
            };
        }

        private static bool IsReady(IPlayable source) =>
            source.Game.HistoryFacts.Query.ManaSpentWhileHolding(source.Id, source.Controller.Id) >= 8;

        private static Trigger Display(TriggerType type) => new Trigger(type) {
            TriggerActivation = TriggerActivation.HAND,
            TriggerSource = TriggerSource.FRIENDLY,
            FastExecution = true,
            SingleTask = new CustomTask((g, c, s, t, stack) =>
                s[GameTag.TAG_SCRIPT_DATA_NUM_1] = Math.Max(0, 8 - g.HistoryFacts.Query.ManaSpentWhileHolding(s.Id, c.Id)))
        };
    }
}
