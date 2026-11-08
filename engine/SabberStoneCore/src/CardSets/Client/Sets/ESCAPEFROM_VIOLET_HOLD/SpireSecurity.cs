using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpireSecurity
    {
        internal const int DbfId = 126355;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var spells = c.DeckZone.Where(p => p is Spell).ToArray();
                if (spells.Length == 0) return;
                var chosen = spells[g.Random.Next(spells.Length)];
                g.OnRandomHappened(true);
                Generic.RevealCardBlock((IPlayable)s, chosen);
                if (chosen.Cost < 5) return;
                new EffectContext(g, c.Id, s.Id, stack: stack).RunLegacy(
                    SplitHits.Inline(5, ComplexTask.DamageRandomTargets(1, EntityType.OP_MINIONS, 1), deathProcessing: false));
            }) };
        }
    }
}
