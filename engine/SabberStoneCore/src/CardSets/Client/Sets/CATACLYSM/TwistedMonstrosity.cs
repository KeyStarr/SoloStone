using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwistedMonstrosity
    {
        internal const int DbfId = 123119;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerActivation = TriggerActivation.HAND,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (s is Minion m && g.CurrentPlayer == c && m.Zone == c.HandZone) BonusEffects.ReplacePair(m);
                })
            } };
        }
    }
}
