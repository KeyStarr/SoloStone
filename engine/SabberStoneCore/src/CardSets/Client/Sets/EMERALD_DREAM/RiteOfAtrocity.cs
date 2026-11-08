using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RiteOfAtrocity
    {
        internal const int DbfId = 114303;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.UNDEAD);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanGiveDarkGift);
            card.Power = new Power { PowerTask = new CustomTask(Resolve) };
        }
        private static void Resolve(Game game, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (target != null) { CorpseRules.TrySpend(controller, 2); return; }
            var ctx = new EffectContext(game, controller.Id, source.Id, stack: stack);
            if (CanGiveDarkGift((IPlayable)source))
                ctx.DiscoverWithDarkGift(Filter, afterChoose: new CustomTask(Resolve), recipeDbfId: DbfId);
            else ctx.Discover(Filter, recipeDbfId: DbfId);
        }

        private static bool CanGiveDarkGift(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 2;
    }
}
