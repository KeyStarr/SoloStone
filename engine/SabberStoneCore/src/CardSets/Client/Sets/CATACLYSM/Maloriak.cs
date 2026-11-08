using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Maloriak
    {
        internal const int DbfId = 122714;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DISCARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p is Minion),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // DiscardBlock moves the target to the graveyard before resolving this trigger;
                    // graveyard copies have base stats (Actions/Copy.cs).
                    int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    new EffectContext(g, c.Id, s.Id, stack: stack).Summon(t.Card.Id, position);
                })
            } };
        }
    }
}
