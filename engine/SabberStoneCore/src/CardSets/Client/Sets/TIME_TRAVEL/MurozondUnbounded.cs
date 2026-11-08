using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MurozondUnbounded
    {
        [StandardCard("TIME_024", 120266)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("TIME_024e2", EntityType.SOURCE) };
        }

        [StandardCard("TIME_024e2", 121170)]
        internal static void ConfigurePending(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                RemoveAfterTriggered = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var actor = (Enchantment)s;
                    var target = actor.Target as Minion;
                    actor.Remove();
                    if (target == null || target.Zone != target.Controller.BoardZone || target.IsSilenced) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_024e"), target, target);
                })
            } };
        }

        [StandardCard("TIME_024e", 120267)]
        internal static void ConfigureSetter(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(InfinityRules.Value)) };
        }
    }
}
