using SabberStoneCore.Conditions;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HandCannon
    {
        internal const int DbfId = 127015;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                OwnerCondition = new SelfCondition(owner => owner.Controller.BoardZone.GetAll().Any(m => m.Card.Id == "CAP_107t" && !m.IsSilenced && !m.IsDormant && !m.IsDead)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    foreach (var cannon in c.BoardZone.GetAll().Where(m => m.Card.Id == "CAP_107t").ToArray())
                        if (cannon.Zone == c.BoardZone && !cannon.IsSilenced && !cannon.IsDormant && !cannon.IsDead)
                            Cannonmaster.Fire(cannon);
                })
            } };
        }
    }
}
