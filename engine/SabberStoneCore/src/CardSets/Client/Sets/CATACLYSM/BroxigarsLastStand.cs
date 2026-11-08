using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BroxigarsLastStand
    {
        internal const int DbfId = 122770;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var minions = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).ToArray();
                new DamageTask(1, EntityType.ALLMINIONS, true).Process(g, c, s, t, stack);
                int deaths = minions.Count(m => m.IsDead);
                // Count this wave's casualties before resolving Deathrattles and any secondary deaths.
                g.DeathProcessingAndAuraUpdate();
                for (int i = 0; i < deaths; i++) Generic.Draw(c);
            }) };
        }
    }
}
