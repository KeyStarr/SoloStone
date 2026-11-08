using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChronoDaggers
    {
        internal const int DbfId = 119270;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var context = new EffectContext(g, c.Id, s.Id, stack: stack);
                g.TaskQueue.StartEvent();
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var enemies = c.Opponent.BoardZone.GetAll().Cast<ICharacter>().Prepend(c.Opponent.Hero)
                            .Where(e => !e.ToBeDestroyed && e.Health > 0).ToArray();
                        if (enemies.Length == 0) break;
                        context.Damage(enemies[g.Random.Next(enemies.Length)].Id, 2, spellDamage: true);
                        g.ProcessTasks();
                        g.DeathProcessingAndAuraUpdate();
                    }
                }
                finally { g.TaskQueue.EndEvent(); }
            }) };
        }
    }
}
