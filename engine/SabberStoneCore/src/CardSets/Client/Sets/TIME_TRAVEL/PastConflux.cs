using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PastConflux
    {
        internal static OfflinePoolFilter RandomPool() => OfflinePoolFilter.Random(CardType.MINION, minCost: 5, race: Race.DRAGON);
        internal static OfflinePoolFilter DiscoverPool() => OfflinePoolFilter.Discover(CardType.MINION, minCost: 5, race: Race.DRAGON);
        [StandardCard("TIME_436", 120544)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                RequiresFreeSlot = true,
                Task = EffectContext.Task(ctx => {
                    var picked = ctx.PickRandomCard(RandomPool(), recipeDbfId: 120544);
                    if (picked != null) ctx.SummonRightOfLocation(picked.Id);
                    ctx.AdvanceLocationStage("TIME_436t1");
                    return ctx.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                })
            } };
        }
        [StandardCard("TIME_436t1", 122262)]
        internal static void Present(Card card) => Stage(card, false);
        [StandardCard("TIME_436t2", 122263)]
        internal static void Future(Card card) => Stage(card, true);
        private static void Stage(Card card, bool future)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                RequiresFreeSlot = true,
                Task = StateTaskList.Suspendable(future ? "b16.conflux.future.v1" : "b16.conflux.present.v1",
                    new DiscoverThenContinueTask(DiscoverPool(), recipeDbfId: future ? 122263 : 122262),
                    new CustomTask((g, c, s, t, stack) => {
                        var chosen = stack.Playables.LastOrDefault()?.Card;
                        var context = new EffectContext(g, c.Id, s.Id, stack: stack);
                        if (chosen != null)
                        {
                            context.SummonRightOfLocation(chosen.Id);
                            g.DeathProcessingAndAuraUpdate();
                            if (future) context.AddToHand(chosen.Id);
                        }
                        if (!future) context.AdvanceLocationStage("TIME_436t2");
                        s[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                    }))
            } };
        }
    }
}
