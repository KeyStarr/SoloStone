using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisguisedExecutioner
    {
        [StandardCard("JAIL_461", 128052)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var body = (Minion)s;
                if (body.Zone != c.BoardZone) return;
                var adjacent = body.GetAdjacentMinions().Where(m => !m.Untouchable && !m.ToBeDestroyed).ToArray();
                if (adjacent.Length == 0) return;
                g.OnRandomHappened(true);
                adjacent[g.Random.Next(adjacent.Length)].ToBeDestroyed = true;
            }) };
        }
        [StandardCard("JAIL_461a", 130026)]
        [StandardCard("JAIL_461b", 130027)]
        internal static void Side(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
