using SabberStoneCore.Actions;
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
    internal static class EscapeArtist
    {
        internal const int DbfId = 125643;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF,
                Condition = SelfCondition.IsNotDead,
                SingleTask = ComplexTask.Create(new DrawTask(), new CustomTask((g, c, s, t, stack) => { if (s is Minion m && m.Zone == m.Controller.BoardZone) Generic.RemoveMinionFromGame(m); }))
            } };
        }
    }
}
