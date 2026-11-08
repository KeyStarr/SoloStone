using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DreadRaptor
    {
        internal const int DbfId = 117423;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = SelfCondition.Kindred.Eval((IPlayable)s);
                var candidates = c.DeckZone.Where(p => p is Minion && p.HasDeathrattle && p.Cost <= 3).ToArray();
                if (candidates.Length == 0) { KindredRules.BeginMatchedClause((IPlayable)s); return; }
                var drawn = candidates[g.Random.Next(candidates.Length)];
                if (candidates.Length > 1) g.OnRandomHappened(true);
                Generic.Draw(c, drawn);
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats && drawn.Zone == c.HandZone; i++)
                    new ApplyEffectTask(EntityType.TARGET, Effects.SetCost(0)).Process(g, c, s, drawn, stack);
            }) };
        }
    }
}
