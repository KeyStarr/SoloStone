using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LavaFlow
    {
        internal const int DbfId = 117536;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = SplitHits.Inline(3, new CustomTask((g, c, s, t, stack) => {
                var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable).Cast<ICharacter>().Append(c.Opponent.Hero)
                    .Where(e => !e.ToBeDestroyed && e.Health > 0).ToArray();
                if (enemies.Length == 0) return;
                int lowest = enemies.Min(e => e.Health);
                var target = enemies.Where(e => e.Health == lowest).ToArray().Choose(g.Random);
                new EffectContext(g, c.Id, s.Id, stack: stack).Damage(target.Id, 2, spellDamage: true);
            }), deathProcessing: false) };
        }
    }
}
