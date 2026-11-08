using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class BuildABeast
    {
        internal const int DbfId = 43183;
        // The legacy choice adapter composes actual beast cards, not these display templates.
        internal static readonly IReadOnlyDictionary<int, string> Components = new Dictionary<int, string>
        {
            [46120] = "ICC_828e", [46119] = "ICC_828t", [46204] = "ICC_828t2", [46672] = "ICC_828t3",
            [50141] = "ICC_828t4", [50142] = "ICC_828t5", [50143] = "ICC_828t6", [50144] = "ICC_828t7"
        };

        internal static void Configure(Card card)
        {
            card.Power = new Power
            {
                // Retain the legacy stack output as well as offer/history ordering.
                PowerTask = ComplexTask.Create(new IncludeTask(EntityType.SOURCE), EffectContext.Task(Begin))
            };
        }

        internal static void BeginStorm(Spell source)
        {
            var c = source.Controller; var g = source.Game;
            var standard = c.Game.FormatType == FormatType.FT_STANDARD;
            var first = StormOffers(g, c, standard ? PoolScope.Standard : PoolScope.Wild, true);
            var second = StormOffers(g, c, PoolScope.Wild, false);
            if (first.Length == 0 || second.Length == 0) return;
            var ctx = new EffectContext(g, c.Id, source.Id);
            ctx.Discover(first, ChoiceAction.BUILDABEAST);
            ctx.Discover(second, ChoiceAction.BUILDABEAST);
            for (var choice = c.Choice; choice != null; choice = choice.NextChoice)
                foreach (int id in choice.Choices) g.IdEntityDic[id][Actions.Generic.PrivateDecisionOfferTag] = c.PlayerId;
        }
        internal static Card[] StormCandidates(Game g, Controller c, PoolScope scope, bool text)
        {
            var banned = new[] { "Shockspitter", "Elwynn Boar", "Ravenous Kraken", "King of Beasts" };
            return OfflineCardPools.Candidates(g,c.HeroClass,StormTheGatesPools.Beasts(scope),120648)
                .Where(card => !banned.Contains(card.Name) && !card.Echo && !new[] { "Corrupt", "Infuse", "Forge" }.Any(word => card.Text.Contains("<b>" + word))
                    && (card.Power?.PowerTask != null || card.Power?.DeathrattleTask != null || card.Power?.Trigger != null || card.Power?.Aura != null) == text)
                .ToArray();
        }
        private static Card[] StormOffers(Game g, Controller c, PoolScope scope, bool text)
        {
            var remaining = StormCandidates(g,c,scope,text).ToList();
            var result = new List<Card>();
            while (result.Count < 3 && remaining.Count != 0) {
                var hunter = remaining.Where(x => x.Class == CardClass.HUNTER).ToArray();
                var neutral = remaining.Where(x => x.Class == CardClass.NEUTRAL).ToArray();
                var bucket = hunter.Length == 0 ? neutral : neutral.Length == 0 ? hunter : g.Random.Next(2) == 0 ? hunter : neutral;
                var pick = bucket[g.Random.Next(bucket.Length)]; result.Add(pick); remaining.Remove(pick);
            }
            return result.ToArray();
        }

        private static TaskState Begin(EffectContext ctx)
        {
            // Pools follow this game's format; LegacyGenerationV1 memoizes them by name.
            string format = LegacyGenerationV1.Format(ctx.Format);
            var first = ctx.SampleDistinct(LegacyGenerationV1.Get(format + "/Beasts/First"), 3);
            var second = ctx.SampleDistinct(LegacyGenerationV1.Get(format + "/Beasts/Second"), 3);
            ctx.Discover(first, ChoiceAction.BUILDABEAST);
            ctx.Discover(second, ChoiceAction.BUILDABEAST);
            // ChoicePick retains the clone/composition/creator/AddHandPhase path for both answers.
            return TaskState.COMPLETE;
        }
    }
}
