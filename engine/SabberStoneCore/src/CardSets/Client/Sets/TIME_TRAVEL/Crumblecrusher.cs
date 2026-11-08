using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Crumblecrusher
    {
        internal const int DbfId = 121197;
        [StandardCard("END_034", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var minions = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).ToArray();
                var locations = c.Opponent.BoardZone.GetLocations().ToArray();
                if (minions.Length > 0) {
                    int selected = g.Random.Next(minions.Length); g.OnRandomHappened(true);
                    ctx.Destroy(minions[selected].Id);
                }
                if (locations.Length > 0) {
                    int selected = g.Random.Next(locations.Length); g.OnRandomHappened(true);
                    ctx.Destroy(locations[selected].Id);
                }
                if (c.Opponent.Hero.Weapon != null) ctx.Destroy(c.Opponent.Hero.Weapon.Id);
            }) };
        }
    }
}
