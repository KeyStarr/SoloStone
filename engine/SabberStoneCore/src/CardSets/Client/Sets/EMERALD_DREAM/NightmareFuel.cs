using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NightmareFuel
    {
        internal const int DbfId = 115088;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Discover(false), ComboTask = Discover(true) };
        }

        private static CustomTask Discover(bool gifted) => new CustomTask((g, c, s, t, stack) => {
            // Canonical definitions preserve full-deck weighting while GetChoices rejects repeated card IDs.
            var candidates = c.Opponent.DeckZone.Where(p => p is Minion).Select(p => Cards.FromId(p.Card.Id)).ToArray();
            var offers = DiscoverTask.GetChoices(new[] { candidates }, 3, g.Random);
            if (offers.Length == 0) return;
            var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
            if (gifted) ctx.DiscoverWithDarkGift(offers);
            else
            {
                ctx.Discover(offers);
                var choice = c.Choice;
                while (choice.NextChoice != null) choice = choice.NextChoice;
                foreach (int offer in choice.Choices)
                    g.IdEntityDic[offer][Generic.PrivateDecisionOfferTag] = c.PlayerId;
            }
            var copies = c.Choice;
            while (copies.NextChoice != null) copies = copies.NextChoice;
            foreach (int offer in copies.Choices) PayloadRules.MarkCopy(c.Opponent, g.IdEntityDic[offer]);
        });
    }
}
