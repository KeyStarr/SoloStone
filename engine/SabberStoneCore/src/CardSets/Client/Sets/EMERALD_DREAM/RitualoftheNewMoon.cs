using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RitualoftheNewMoon
    {
        internal const int DbfId = 114300;
        internal static void Configure(Card card)
        {
            card.ExcludeFromLegacyRandomPools = true;
            card.HandProgress = new HandProgressDefinition("EDR_461t", 3, HandProgressMode.OwnerSpells);
            ConfigureSummons(card, EmeraldDreamPools.ThreeCostMinion());
        }

        internal static void ConfigureFullMoon(Card card) => ConfigureSummons(card, EmeraldDreamPools.SixCostMinion());

        private static void ConfigureSummons(Card card, OfflinePoolFilter filter)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(filter, recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}
