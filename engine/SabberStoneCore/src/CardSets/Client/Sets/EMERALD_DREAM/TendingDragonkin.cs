using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TendingDragonkin
    {
        internal const int DbfId = 115960;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var beasts = c.HandZone.Where(x => x.Card.IsRace(Race.BEAST)).ToArray();
                if (beasts.Length == 0) return;
                int lowest = beasts.Min(x => x.Cost);
                var chosen = beasts.Where(x => x.Cost == lowest).ToArray().Choose(g.Random);
                g.OnRandomHappened(true);
                Generic.Copy(c, s, chosen, Zone.HAND);
            }) };
        }
    }
}
