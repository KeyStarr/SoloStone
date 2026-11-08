using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BeastTripwire
    {
        internal const int DbfId = 126419;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                SummonBeast(g, c, s, stack);
                new AddCardTo("JAIL_879t", EntityType.DECK, 2).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureTripped(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => SummonBeast(g, c, s, stack)),
                TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                    SummonBeast(g, c, s, stack);
                    new DrawTask().Process(g, c, s, t, stack);
                })
            };
        }

        private static void SummonBeast(Game game, Controller controller, IEntity source, TaskStack stack)
        {
            var ctx = new EffectContext(game, controller.Id, source.Id, stack: stack);
            var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.FiveCostBeast(), recipeDbfId: DbfId);
            if (chosen == null) return;
            int position = source is Minion minion && minion.Zone == controller.BoardZone
                ? minion.ZonePosition + 1 : controller.BoardZone.SlotCount;
            ctx.Summon(chosen.Id, position);
        }
    }
}
