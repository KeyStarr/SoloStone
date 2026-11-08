using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Gifted own-deck Discover. The caller samples ordered own-deck minion offers with
    /// distinct card IDs each time this step opens, using only controller.Game.Random for randomness.
    /// Use DarkGifts.SampleDeckOffers for copy-weighted sampling with duplicate-ID rejection.
    /// Only entity IDs and gift pairings survive suspension; selectors must use the supplied controller,
    /// never capture live game entities. The optional recipe ID is declaration metadata, since deck
    /// selectors have no offline pool filter to validate.</summary>
    public sealed class DiscoverEntitiesWithDarkGiftThenContinueTask : SimpleTask
    {
        private readonly Func<Controller, IReadOnlyList<IPlayable>> _selector;
        internal readonly int? RecipeDbfId;

        public DiscoverEntitiesWithDarkGiftThenContinueTask(Func<Controller, IReadOnlyList<IPlayable>> selector, int? recipeDbfId = null)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            RecipeDbfId = recipeDbfId;
        }

        internal TaskState Open(Game game, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (game.Player1.Choice != null || game.Player2.Choice != null)
                throw new InvalidOperationException("Cannot suspend an effect over an existing choice.");
            DarkGifts.EnsureCompatible(game);
            var offers = _selector(controller);
            if (offers == null || offers.Any(p => !(p is Minion) || p.Game != game ||
                    p.Controller != controller || p.Zone != controller.DeckZone) ||
                offers.Select(p => p.Card.Id).Distinct(StringComparer.Ordinal).Count() != offers.Count)
                throw new ArgumentException("Dark Gift deck offers require own-deck minions with distinct card IDs.");
            stack.Playables?.Clear();
            new EffectContext(game, controller.Id, source.Id, target?.Id, stack)
                .DiscoverEntitiesWithDarkGift(offers, ChoiceAction.STACK);
            return controller.Choice != null ? TaskState.SUSPENDED : TaskState.COMPLETE;
        }

        internal bool IsEligible(Game game, Controller controller, int id) =>
            game.IdEntityDic.TryGetValue(id, out var entity) && entity is Minion &&
            entity.Controller == controller && entity.Zone == controller.DeckZone;

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null) =>
            throw new NotSupportedException("DiscoverEntitiesWithDarkGiftThenContinueTask requires a registered suspendable recipe.");
    }
}
