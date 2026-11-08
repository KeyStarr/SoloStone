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
    internal static class MysticMisdirection
    {
        [StandardCard("JAIL_315", 126113)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.OP_MINIONS,
                SingleTask = ComplexTask.Create(new ConditionTask(EntityType.SOURCE, SelfCondition.IsOpTurn),
                    new FlagTask(true, ComplexTask.Secret(new CustomTask((g, c, s, t, stack) => {
                        if (!(t is Minion attacker) || attacker.Zone != attacker.Controller.BoardZone) return;
                        if (attacker.Card.Id == "REV_925" && !attacker.IsSilenced)
                            new SummonTask("CS2_tk1").Process(g, attacker.Controller, attacker, null, stack);
                        else
                        {
                            new TransformTask("CS2_tk1", EntityType.TARGET, clearNativeKeywords: true)
                                .Process(g, attacker.Controller, s, attacker, stack);
                            attacker[DeathContextRules.CancelAttack] = 1;
                        }
                    }))))
            } };
        }
    }
}
