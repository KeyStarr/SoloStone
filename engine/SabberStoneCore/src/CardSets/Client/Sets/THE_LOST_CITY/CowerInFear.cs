using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CowerInFear
    {
        internal const int DbfId = 117580;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = ComplexTask.Create(new DamageTask(3, EntityType.TARGET, true),
                new AddEnchantmentTask("TLC_823e1", EntityType.CONTROLLER)) };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2)) {
                    Condition = new SelfCondition(p => p is Minion m && m.IsRace(Race.BEAST))
                },
                Trigger = new Trigger(TriggerType.PLAY_MINION) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SelfCondition(p => p.Card.IsRace(Race.BEAST)),
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}
