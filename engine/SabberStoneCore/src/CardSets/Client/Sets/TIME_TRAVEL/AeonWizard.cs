using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AeonWizard
    {
        internal const int DbfId = 119312;
        internal static OfflinePoolFilter ClassSpells(CardClass heroClass) =>
            new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: heroClass, type: CardType.SPELL);

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2; i++)
                {
                    var spell = ctx.PickRandomCard(ClassSpells(c.HeroClass), recipeDbfId: DbfId);
                    if (spell != null) ctx.AddToHand(spell.Id);
                }
            }) };
        }
    }
}
