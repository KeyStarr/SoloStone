using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScavengingFlytrap
    {
        [StandardCard("EDR_484", 115037)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.ObserveDeathAttack] = 1;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.ALL_MINIONS,
                OrderDeathByPlay = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (DeathContextRules.Active((Minion)s) && t is Minion)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_484e"), (IPlayable)s, s,
                            DeathContextRules.AttackAtDeath(t));
                })
            } };
        }
        [StandardCard("EDR_484e", 115038)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }
    }
}
