using System;
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
    internal static class Setesh
    {
        internal const int DbfId = 129935;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new ArmorTask(20),
                new CustomTask((g, c, s, t, stack) =>
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_557e2"), (IPlayable)s, c, 2))) };
        }

        internal static void ConfigureTimer(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (--s[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0) return;
                    int armor = c.Hero.Armor;
                    c.Hero.Armor = 0;
                    ((Enchantment)s).Remove();
                    // The owner-approved zero-Armor policy emits no summon or death event.
                    if (armor <= 0 || c.BoardZone.IsFull) return;
                    var golem = (Minion)Entity.FromCard(c, Cards.FromId("BE_561t"));
                    golem.AttackDamage = armor;
                    golem.BaseHealth = armor;
                    Generic.SummonBlock(g, golem, -1, s);
                })
            } };
        }
    }
}
