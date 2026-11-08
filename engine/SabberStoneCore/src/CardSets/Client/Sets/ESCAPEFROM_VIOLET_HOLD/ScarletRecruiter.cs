using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScarletRecruiter
    {
        internal const int DbfId = 127319;
        // Two distinct random deck minions with current Cost <= 2, summoned right of the Recruiter in creation order
        // (appended if it left play), limited by free physical slots; then each one still in play gets JAIL_516e Rush.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int amount = System.Math.Min(2, c.BoardZone.FreeSpace);
                if (amount == 0) return;
                var pool = c.DeckZone.GetAll().OfType<Minion>().Where(m => m.Cost <= 2).ToArray();
                if (pool.Length == 0) return;
                var picks = pool.ChooseNElements(amount, g.Random);
                if (pool.Length > amount) g.OnRandomHappened(true);
                var summoned = new List<Minion>();
                foreach (var minion in picks)
                {
                    if (c.BoardZone.IsFull) break;
                    // Anchor: the previous output still in play, else the Recruiter; append if neither is in play.
                    IPlayable anchor = summoned.LastOrDefault(m => m.Zone == c.BoardZone) ?? (s.Zone == c.BoardZone ? (IPlayable)s : null);
                    int position = anchor == null ? c.BoardZone.SlotCount
                        : Enumerable.Range(0, c.BoardZone.SlotCount).First(i => c.BoardZone.GetSlot(i) == anchor) + 1;
                    c.DeckZone.Remove(minion);
                    Generic.SummonBlock(g, minion, position, s);
                    summoned.Add(minion);
                }
                foreach (var minion in summoned.Where(m => m.Zone == c.BoardZone))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_516e"), (IPlayable)s, minion);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Rush) };
        }
    }
}
