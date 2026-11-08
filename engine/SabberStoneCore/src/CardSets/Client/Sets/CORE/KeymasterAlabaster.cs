using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KeymasterAlabaster
    {
        [StandardCard("CORE_SCH_717", 128337)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DRAW) {
                TriggerSource = TriggerSource.ENEMY,
                SingleTask = new CustomTask((g, c, s, t, stack) =>
                    Generic.Copy(c, s, t, Zone.HAND, initialize: copy => copy.Cost = 1))
            } };
        }
    }
}
