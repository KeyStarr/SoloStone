using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChromaticBroodmother
    {
        internal const int DbfId = 122771;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Darkscale Broodmother already refreshes Mana through RESOURCES_USED.
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    new SetControllerGameTagTask(GameTag.RESOURCES_USED, Math.Max(0, c.UsedMana - ((Minion)s).AttackDamage)).Process(g, c, s, t, stack);
                })
            } };
        }
    }
}
