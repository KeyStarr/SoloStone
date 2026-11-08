using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Vyranoth
    {
        internal const int DbfId = 123151;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(Condition);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (CostRules.StartingMinionCost(c) != 100) return;
                var minions = c.DeckZone.OfType<Minion>().ToArray();
                if (minions.Length == 0) return;
                int basic = 50 / minions.Length, remainder = 50 % minions.Length;
                var context = new EffectContext(g,c.Id,s.Id);
                var attack = remainder == 0 ? Array.Empty<Minion>() : context.SampleDistinct(minions,remainder);
                var health = remainder == 0 ? Array.Empty<Minion>() : context.SampleDistinct(minions,remainder);
                foreach (var minion in minions)
                {
                    Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_213e"), (IPlayable)s, minion, basic + (attack.Contains(minion) ? 1 : 0));
                    Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_213e2"), (IPlayable)s, minion, basic + (health.Contains(minion) ? 1 : 0));
                }
            }) };
        }
        private static bool Condition(IPlayable source) => CostRules.StartingMinionCost(source.Controller) == 100;
        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }
        internal static void ConfigureHealth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
