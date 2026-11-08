using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ankylodon
    {
        internal const int DbfId = 118620;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id); var created = new List<int>();
                int position = EffectContext.DeathrattlePosition(c, s);
                int summons = Math.Min(2, c.BoardZone.FreeSpace);
                for (int i = 0; i < summons && !c.BoardZone.IsFull; i++)
                {
                    var selected = ctx.PickRandomCard(TheLostCityPools.ThreeCostBeast(), recipeDbfId: DbfId);
                    if (selected == null) continue;
                    int id = ctx.Summon(selected.Id, position);
                    if (id != 0) { created.Add(id); position++; }
                    g.DeathProcessingAndAuraUpdate();
                }
                foreach (int id in created)
                {
                    var attacker = (Minion)g.IdEntityDic[id];
                    if (attacker.IsDead || attacker.Zone != c.BoardZone || attacker.Untouchable) continue;
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).Cast<ICharacter>()
                        .Append(c.Opponent.Hero).Where(e => !e.IsDead).ToArray();
                    if (enemies.Length == 0) break;
                    var defender = enemies[g.Random.Next(enemies.Length)]; g.OnRandomHappened(true);
                    new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, defender,
                        new TaskStack { Playables = new[] { attacker } });
                }
            }) };
        }
    }
}
