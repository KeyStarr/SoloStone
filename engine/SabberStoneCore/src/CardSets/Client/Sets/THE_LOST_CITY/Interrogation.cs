using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Interrogation
    {
        internal const int DbfId = 117700;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddCardTo("TLC_513t2", EntityType.DECK, 3) };
        }

        internal static void ConfigureNinja(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                var ninja = (Minion)s;
                ninja.Zone.Remove(ninja);
                if (!c.BoardZone.IsFull) Generic.SummonBlock(g, ninja, -1, s);
                else c.GraveyardZone.Add(ninja);
                new DrawTask().Process(g, c, s, null);
            }) };
        }
    }
}
