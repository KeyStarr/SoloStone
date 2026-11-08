using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HollowDirehorn
    {
        internal const int DbfId = 118558;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.MINIONS,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var source = (Minion)s;
                    if (source.Zone != c.BoardZone || source.IsDead || !CorpseRules.TrySpend(c, 3)) return;
                    source[GameTag.REBORN] = 1;
                })
            } };
        }
    }
}
