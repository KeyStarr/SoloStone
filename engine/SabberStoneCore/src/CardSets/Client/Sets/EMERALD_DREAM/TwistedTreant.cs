using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwistedTreant
    {
        internal const int DbfId = 115027;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var owner in new[] { c, c.Opponent }) {
                    var minions = owner.HandZone.OfType<Minion>().ToArray();
                    if (minions.Length == 0) continue;
                    var chosen = minions[g.Random.Next(minions.Length)];
                    g.OnRandomHappened(true);
                    ctx.GiveBuff(chosen.Id, "EDR_495e");
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(-2)) };
        }
    }
}
