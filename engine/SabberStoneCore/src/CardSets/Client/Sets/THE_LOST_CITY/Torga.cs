using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Torga
    {
        internal const int DbfId = 117848;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // 4028 is the pinned Kindred mechanic tag, distinct from referencedTagLists.
                var pool = c.DeckZone.Where(p => p.Card[(GameTag)4028] == 1).ToArray();
                if (pool.Length == 0) return;
                var kindred = pool[g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                Generic.Draw(c, kindred);
                pool = c.DeckZone.Where(p => KindredRules.SharesKindredType(p.Card, kindred.Card)).ToArray();
                if (pool.Length == 0) return;
                var activator = pool[g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                Generic.Draw(c, activator);
            }) };
        }
    }
}
