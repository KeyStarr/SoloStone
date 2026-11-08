using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BattlefieldNecromancer
    {
        internal const int DbfId = 78353;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var source = (Minion)s;
                    // Unpinned raise-fits-board: a full board spends nothing.
                    if (source.Zone != c.BoardZone || source.IsDead || c.BoardZone.IsFull || !CorpseRules.TrySpend(c, 1)) return;
                    new EffectContext(g, c.Id, s.Id, stack: stack).Summon("RLK_061t");
                })
            } };
        }
    }
}
