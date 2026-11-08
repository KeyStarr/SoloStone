using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoneGauntlet
    {
        internal const int DbfId = 129938;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    int health = c.Hero.Health;
                    int converted = Math.Min(5, Math.Max(0, health - 1));
                    c.Hero.Damage = c.Hero.BaseHealth - Math.Max(1, health - converted);
                    c.Hero.ToBeDestroyed = false;
                    if (converted > 0) new ArmorTask(converted).Process(g, c, s, t, stack);
                })
            } };
        }
    }
}
