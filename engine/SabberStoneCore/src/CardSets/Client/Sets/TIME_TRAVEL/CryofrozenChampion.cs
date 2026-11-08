using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CryofrozenChampion
    {
        internal const int DbfId = 120481;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                    .PickRandomCard(OfflinePoolDeclarations.LegendaryMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = Entity.FromCard(c, chosen);
                generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                generated.Cost -= 1;
                Generic.AddHandPhase(c, generated);
            }) };
        }
    }
}
