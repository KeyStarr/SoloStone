using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FranticForger
    {
        internal const int DbfId = 126937;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Random(type: CardType.SPELL);
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var pick = new EffectContext(g,c.Id,s.Id).PickAffordableSpell(Filter, recipeDbfId: DbfId);
                if (pick == null) return;
                var spell = Entity.FromCard(c, pick, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                TemporaryRules.Make(spell);
                Generic.AddHandPhase(c, spell);
            }) };
        }
    }
}
