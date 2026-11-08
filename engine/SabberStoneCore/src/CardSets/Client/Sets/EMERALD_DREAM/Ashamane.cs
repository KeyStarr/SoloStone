using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ashamane
    {
        internal const int DbfId = 114527;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var pool = c.Opponent.DeckZone.ToArray();
                if (pool.Length == 0) return;
                while (!c.HandZone.IsFull) {
                    var original = pool[g.Random.Next(pool.Length)];
                    if (pool.Length > 1) g.OnRandomHappened(true);
                    var copy = Generic.Copy(c, s, original, Zone.HAND);
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(3)).Process(g, c, s, copy, stack);
                }
            }) };
        }
    }
}
