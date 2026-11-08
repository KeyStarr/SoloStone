using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RehgarEarthfury
    {
        [StandardCard("CORE_CATA_004", 127407)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.AttackObserver] = 1;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.MINIONS,
                OwnerCondition = new SelfCondition(owner => owner.Zone == owner.Controller.BoardZone
                    && !((Minion)owner).Untouchable && !((Minion)owner).IsSilenced
                    && owner.Game.CurrentEventData?.EventSource is Minion attacker
                    && (attacker == owner || attacker[DeathContextRules.LeftObserver] == owner.Id || attacker[DeathContextRules.RightObserver] == owner.Id)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // Combat death does not cancel an already triggered after-attack reward.
                    Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("CORE_EX1_238"), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
                })
            } };
        }
    }
}
