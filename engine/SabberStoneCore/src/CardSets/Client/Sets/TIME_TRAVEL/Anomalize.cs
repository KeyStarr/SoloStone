using System.Collections.Generic;
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
    internal static class Anomalize
    {
        internal const int DbfId = 119691;
        [StandardCard("TIME_859", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var participants = new List<Minion>();
                var context = new EffectContext(g, c.Id, s.Id);
                foreach (int cost in new[] { 10, 1 })
                {
                    var chosen = context.PickRandomCard(B14K3TimeTravelPools.Minion(cost), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    g.TaskQueue.StartEvent();
                    try
                    {
                        int summoned = context.Summon(chosen.Id);
                        if (summoned != 0) participants.Add((Minion)g.IdEntityDic[summoned]);
                        g.ProcessTasks();
                        g.DeathProcessingAndAuraUpdate();
                    }
                    finally { g.TaskQueue.EndEvent(); }
                }
                participants.RemoveAll(m => m.Zone != m.Controller.BoardZone || m.IsDead);
                if (participants.Count == 0) return;
                int[] original = participants.SelectMany(m => new[] { m.AttackDamage, m.BaseHealth }).ToArray();
                var assignments = new List<int[]>();
                Permute(original.ToArray(), 0, assignments);
                var distinct = assignments.GroupBy(values => string.Join(",", values)).Select(group => group.First()).ToArray();
                int BadHealth(int[] values) => participants.Select((m, i) => !m.IsDormant && values[2 * i + 1] <= 0 ? 1 : 0).Sum();
                int Unchanged(int[] values) => values.Where((value, i) => value == original[i]).Count();
                int bestHealth = distinct.Min(BadHealth);
                var living = distinct.Where(values => BadHealth(values) == bestHealth).ToArray();
                int bestChanged = living.Min(Unchanged);
                var candidates = living.Where(values => Unchanged(values) == bestChanged).ToArray();
                var result = candidates[g.Random.Next(candidates.Length)];
                for (int i = 0; i < participants.Count; i++)
                    if (!participants[i].IsDormant)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_859e"), (IPlayable)s, participants[i], result[2 * i], result[2 * i + 1]);
                g.DeathProcessingAndAuraUpdate();
            }) };
        }
        private static void Permute(int[] values, int index, List<int[]> result)
        {
            if (index == values.Length) { result.Add(values.ToArray()); return; }
            for (int i = index; i < values.Length; i++)
            {
                (values[index], values[i]) = (values[i], values[index]);
                Permute(values, index + 1, result);
                (values[index], values[i]) = (values[i], values[index]);
            }
        }
        [StandardCard("TIME_859e", 119692)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(0), Effects.SetMaxHealth(0)) { UseScriptTag = true } };
        }
    }
}
