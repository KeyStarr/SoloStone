using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hexmarshal
    {
        internal const int DbfId = 126415;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(StartedWithoutSpells);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                    .PickRandomCard(EscapeFromVioletHoldPools.ExpensiveSpell(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = Entity.FromCard(c, chosen);
                generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                if (StartedWithoutSpells((IPlayable)s)) generated.Cost -= 5;
                Generic.AddHandPhase(c, generated);
            }) };
        }
        private static bool StartedWithoutSpells(IPlayable source) =>
            !source.Controller.DeckCards.Any(card => card.Type == CardType.SPELL);

    }
}
