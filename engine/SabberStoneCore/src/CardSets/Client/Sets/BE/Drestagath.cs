using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Drestagath
    {
        internal const int DbfId = 129945;
        private static readonly GameTag OldGod = (GameTag)4754;
        private static OfflinePoolFilter Filter() => new OfflinePoolFilter(type: CardType.MINION,
            cardIds: new[] { "BE_013", "BE_017", "BE_784", "BE_009" });
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("four-old-gods", Filter(), new[] { "ROGUE" }) };
        [StandardCard("BE_703", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var context = new EffectContext(g, c.Id, s.Id);
                // A masked deck must use DrawWhere's Unknown boundary, never infer absence from hidden definitions.
                if (g.HistoryFacts.IsProjected && c.DeckZone.Any(p => p is Unknown) || c.DeckZone.Any(p => p.Card[OldGod] > 0))
                    context.DrawWhere(p => p.Card[OldGod] > 0);
                else
                {
                    var chosen = context.PickRandomCard(Filter(), recipeDbfId: DbfId);
                    if (chosen != null) context.AddToHand(chosen.Id);
                }
            }) };
        }
    }
}
