using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DivineAugur
    {
        internal const int DbfId = 120471;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.HandZone.OfType<Minion>().ToArray())
                {
                    int value = Math.Max(minion.AttackDamage, minion.Health);
                    if (minion.AttackDamage < value)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_429e1"), (IPlayable)s, minion, value, 0, 0);
                    if (minion.Health < value)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_429e2"), (IPlayable)s, minion, value, 0, 0);
                }
            }) };
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(0)) { UseScriptTag = true } };
        }

        internal static void ConfigureHealth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetMaxHealth(0)) { UseScriptTag = true } };
        }
    }
}
