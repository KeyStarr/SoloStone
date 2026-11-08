using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowcloakedAssailant
    {
        internal const int DbfId = 114514;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasSharedCard);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var matches = MatchingEnemies((IPlayable)s).ToArray();
                if (matches.Length == 0) return;
                var chosen = matches[g.Random.Next(matches.Length)];
                if (matches.Length > 1) g.OnRandomHappened(true);
                new MoveToDeck(EntityType.TARGET, opponent: true).Process(g, c, s, chosen, stack);
                c.Opponent.DeckZone.Shuffle();
            }) };
        }

        private static IEnumerable<IPlayable> MatchingEnemies(IPlayable source) => source.Controller.Opponent.DeckZone.IsFull
            ? Enumerable.Empty<IPlayable>()
            : source.Controller.Opponent.HandZone
                .Where(enemy => source.Controller.HandZone.Any(own => own.Id != source.Id && own.Card.Id == enemy.Card.Id));
        private static bool HasSharedCard(IPlayable source) => MatchingEnemies(source).Any();
    }
}
