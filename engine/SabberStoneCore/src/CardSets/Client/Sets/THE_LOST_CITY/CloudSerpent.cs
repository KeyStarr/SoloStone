using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CloudSerpent
    {
        internal const int DbfId = 122318;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var pool = c.HandZone.Where(p => p.Id != s.Id &&
                    (p.Card.IsRace(Race.ELEMENTAL) || p.Card.IsRace(Race.DRAGON))).ToArray();
                if (pool.Length == 0 || c.HandZone.IsFull) return;
                var original = pool[g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                Generic.Copy(c, s, original, Zone.HAND);
            }) };
        }
    }
}
