using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HoundsOfFury
    {
        internal const int DbfId = 120990;
        private static bool NoMinions(IPlayable source) => !source.Controller.DeckZone.Any(p => p.Card.Type == CardType.MINION);
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(NoMinions);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool attack = NoMinions((IPlayable)s);
                var ctx = new EffectContext(g, c.Id, s.Id); var created = new List<int>();
                foreach (string token in new[] { "TIME_443t", "TIME_443t2" })
                {
                    int id = ctx.Summon(token); if (id != 0) created.Add(id);
                    g.DeathProcessingAndAuraUpdate();
                }
                if (!attack) return;
                foreach (int id in created)
                {
                    var attacker = (Minion)g.IdEntityDic[id];
                    if (attacker.IsDead || attacker.Zone != c.BoardZone || attacker.Untouchable) continue;
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).Cast<ICharacter>()
                        .Append(c.Opponent.Hero).Where(e => !e.IsDead).ToArray();
                    if (enemies.Length == 0) break;
                    int health = enemies.Min(e => e.Health);
                    var lowest = enemies.Where(e => e.Health == health).ToArray();
                    var defender = lowest[g.Random.Next(lowest.Length)]; g.OnRandomHappened(true);
                    new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, defender,
                        new TaskStack { Playables = new[] { attacker } });
                }
            }) };
        }
        internal static void ConfigureToken(Card card) { card.Implemented = true; }
    }
}
