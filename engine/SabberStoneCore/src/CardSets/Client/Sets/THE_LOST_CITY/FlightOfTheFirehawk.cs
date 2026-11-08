using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlightOfTheFirehawk
    {
        internal const int DbfId = 117606;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                Card first = null;
                for (int i = 0; i < 2; i++)
                {
                    var pool = c.DeckZone.Where(p => p is Minion && p.Card.Races.Count > 0 &&
                        (first == null || !p.Card.SharesRaceWith(first))).ToArray();
                    if (pool.Length == 0) break;
                    var drawn = pool[g.Random.Next(pool.Length)];
                    if (pool.Length > 1) g.OnRandomHappened(true);
                    first = drawn.Card;
                    Generic.Draw(c, drawn);
                    if (drawn.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_222e"), (IPlayable)s, drawn);
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
