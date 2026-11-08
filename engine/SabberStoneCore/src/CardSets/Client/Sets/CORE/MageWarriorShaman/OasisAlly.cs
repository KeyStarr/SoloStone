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
    internal static class OasisAlly
    {
        internal const int DbfId = 120392;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.ALL,
                OwnerCondition = new SelfCondition(secret => secret.Controller != secret.Game.CurrentPlayer
                    && secret.Game.CurrentEventData?.EventTarget is Minion defender
                    && defender.Controller == secret.Controller && !secret.Controller.BoardZone.IsFull),
                SingleTask = ComplexTask.Secret(new SummonTask("CS2_033"))
            } };
        }
    }
}
