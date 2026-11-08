using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RenewingFlames
    {
        internal const int DbfId = 114543;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                for (int i = 0; i < 2; i++)
                {
                    var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable).Cast<ICharacter>()
                        .Append(c.Opponent.Hero).Where(e => !e.IsDead).ToArray();
                    if (enemies.Length == 0) break;
                    int health = enemies.Min(e => e.Health);
                    var lowest = enemies.Where(e => e.Health == health).ToArray();
                    var chosen = lowest[g.Random.Next(lowest.Length)];
                    g.OnRandomHappened(true);
                    // Unpinned: reselect per hit, but leave deaths/Deathrattles to the spell's normal completion.
                    ctx.Damage(chosen.Id, 5, true);
                }
            }) };
        }
    }
}
