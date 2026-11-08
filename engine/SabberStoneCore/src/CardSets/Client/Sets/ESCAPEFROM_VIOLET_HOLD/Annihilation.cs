using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Annihilation
    {
        internal const int DbfId = 126128;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new DestroyTask(EntityType.ALLMINIONS).Process(g, c, s, t, stack);
                g.DeathProcessingAndAuraUpdate();
                // DeckZone draws from Count - 1, so indices 0..2 are the bottom three.
                var demons = c.DeckZone.Take(3).OfType<Minion>().Where(m => m.IsRace(Race.DEMON)).ToArray();
                foreach (var demon in demons)
                {
                    if (c.BoardZone.IsFull) break;
                    if (demon.Zone != c.DeckZone) continue;
                    c.DeckZone.Remove(demon);
                    Generic.SummonBlock(g, demon, -1, s);
                }
            }) };
        }
    }
}
