using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScalehideKodo
    {
        internal const int DbfId = 118182;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                for (int i = 0; i < (repeats > 0 ? repeats : 1); i++)
                {
                    var enemies = c.Opponent.BoardZone.GetAll().Where(m => i == 0 || !m.IsDead).ToArray();
                    if (enemies.Length == 0) return;
                    int attack = repeats > 0 ? enemies.Max(m => m.AttackDamage) : enemies.Min(m => m.AttackDamage);
                    var tied = enemies.Where(m => m.AttackDamage == attack).ToArray();
                    g.OnRandomHappened(true);
                    new EffectContext(g, c.Id, s.Id, stack: stack).Destroy(tied[g.Random.Next(tied.Length)].Id);
                }
            }) };
        }
    }
}
