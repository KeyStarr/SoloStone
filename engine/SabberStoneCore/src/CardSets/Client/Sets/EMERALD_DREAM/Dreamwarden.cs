using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Dreamwarden
    {
        private static IPlayable[] Eligible(IPlayable source) => source.Controller.DeckZone
            .Where(p => p is Unknown || !source.Game.HistoryFacts.Query.StartedInDeck(p.Id, source.Controller.Id)).ToArray();
        private static bool HasNonoriginal(IPlayable source) => Eligible(source).Length != 0;
        [StandardCard("EDR_256", 116674)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasNonoriginal);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var eligible = Eligible((IPlayable)s);
                if (eligible.Length == 0) return;
                Generic.Draw(c, cardToDraw: eligible[g.Random.Next(eligible.Length)]);
                Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_256e"), (IPlayable)s, (IPlayable)s);
            }) };
        }
        [StandardCard("EDR_256e", 116981)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
