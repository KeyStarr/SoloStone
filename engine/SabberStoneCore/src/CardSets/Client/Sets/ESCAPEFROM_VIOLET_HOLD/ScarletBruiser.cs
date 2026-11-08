using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScarletBruiser
    {
        internal const int DbfId = 126618;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.DeckZone.Any(entity => entity.Card.Class == CardClass.NEUTRAL)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.PaladinCard(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = Entity.FromCard(c, chosen);
                generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                generated.Cost -= 2;
                Generic.AddHandPhase(c, generated);
            }) };
        }
    }
}
