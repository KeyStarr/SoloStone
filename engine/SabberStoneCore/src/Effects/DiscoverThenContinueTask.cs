using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    public enum PoolCostBinding { None, StackNumber, StackNumberMaxCost }

    /// <summary>Discover for a registered recipe. Pool choices replace stack playables; zero offers leave an empty list.</summary>
    public sealed class DiscoverThenContinueTask : SimpleTask
    {
        private readonly DiscoverTask _discover;
        private readonly Func<Game, Controller, IEntity, TaskStack, Card[]> _offers;

        /// <summary>Registered recipe computes offers once; any continuation values belong in its saved stack.</summary>
        public DiscoverThenContinueTask(Func<Game, Controller, IEntity, TaskStack, Card[]> offers,
            Action<IEntity,IPlayable> prepareOffer = null)
        {
            _offers = offers ?? throw new ArgumentNullException(nameof(offers));
            if (prepareOffer != null) OfferPreparations[offers] = prepareOffer;
        }
        // Definition callbacks are installed recipes; they must not alter the historical task field layout.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Func<Game, Controller, IEntity, TaskStack, Card[]>, Action<IEntity,IPlayable>>
            OfferPreparations = new System.Collections.Concurrent.ConcurrentDictionary<Func<Game, Controller, IEntity, TaskStack, Card[]>, Action<IEntity,IPlayable>>();
        private readonly bool _companionRoster;
        private DiscoverThenContinueTask(bool companionRoster) { _companionRoster = companionRoster; }
        public static DiscoverThenContinueTask CompanionRoster() => new DiscoverThenContinueTask(true);
        private readonly string[] _fixedOffers;
        private readonly OfflinePoolFilter? _poolFilter;
        private readonly PoolCostBinding _costBinding;
        private readonly int? _recipeDbfId;
        internal readonly DiscoverEntitySource? EntitySource;
        private readonly DiscoverEntityAction _entityAction;
        private readonly RejectedOfferAction _rejected;
        private readonly int _offerCount;
        private readonly string _discount;
        private readonly int _reductionBudget;

        public DiscoverThenContinueTask(DiscoverEntitySource source, DiscoverEntityAction action = DiscoverEntityAction.Select,
            RejectedOfferAction rejected = RejectedOfferAction.Keep, int count = 3, string discount = null,
            OfflinePoolFilter? fallback = null, int? recipeDbfId = null)
        {
            if (!Enum.IsDefined(typeof(DiscoverEntitySource), source) || !Enum.IsDefined(typeof(DiscoverEntityAction), action)
                || !Enum.IsDefined(typeof(RejectedOfferAction), rejected) || count < 1 || count > 4)
                throw new ArgumentOutOfRangeException(nameof(source));
            EntitySource = source; _entityAction = action; _rejected = rejected; _offerCount = count;
            _discount = discount; _poolFilter = fallback; _recipeDbfId = recipeDbfId;
        }

        public DiscoverThenContinueTask(OfflinePoolFilter filter, PoolCostBinding costBinding = PoolCostBinding.None,
            int? recipeDbfId = null, int reductionBudget = 0, string discount = null)
        {
            if (!Enum.IsDefined(typeof(PoolCostBinding), costBinding))
                throw new ArgumentOutOfRangeException(nameof(costBinding));
            _poolFilter = filter;
            _costBinding = costBinding;
            _recipeDbfId = recipeDbfId;
            if (reductionBudget < 0 || reductionBudget > 0 && string.IsNullOrWhiteSpace(discount))
                throw new ArgumentOutOfRangeException(nameof(reductionBudget));
            _reductionBudget = reductionBudget; _discount = discount;
        }

        /// <summary>Ordered fixed branches; no random sampling. Repetition is owned by the recipe.</summary>
        public DiscoverThenContinueTask(string firstCardId, params string[] otherCardIds)
        {
            var cardIds = new[] { firstCardId }.Concat(otherCardIds ?? Array.Empty<string>()).ToArray();
            if (cardIds.Any(string.IsNullOrWhiteSpace) ||
                cardIds.Distinct().Count() != cardIds.Length)
                throw new ArgumentException("Fixed choices require distinct card IDs.", nameof(cardIds));
            _fixedOffers = cardIds;
        }
        public DiscoverThenContinueTask(CardType cardType = CardType.INVALID, CardClass cardClass = CardClass.INVALID)
            => _discover = new DiscoverTask(cardType, cardClass, choiceAction: ChoiceAction.STACK);

        internal TaskState Open(Game game, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (game.Player1.Choice != null || game.Player2.Choice != null)
                throw new InvalidOperationException("Cannot suspend an effect over an existing choice.");
            TaskState result;
            if (_companionRoster)
                result = Actions.Generic.CreateChoiceCards(controller, source, null, ChoiceType.GENERAL, ChoiceAction.STACK,
                    Actions.CompanionRules.Roster(controller).Select(Cards.FromId).ToArray(), null) ? TaskState.COMPLETE : TaskState.STOP;
            else if (_offers != null)
            {
                var offers = _offers(game, controller, source, stack);
                stack.Playables = Array.Empty<IPlayable>();
                if (offers.Length > 0)
                    Actions.Generic.CreateChoiceCards(controller, source, null, ChoiceType.GENERAL, ChoiceAction.STACK, offers, null);
                if (controller.Choice != null && OfferPreparations.TryGetValue(_offers,out var prepareOffer))
                    foreach (int id in controller.Choice.Choices) prepareOffer(source,game.IdEntityDic[id]);
                result = TaskState.COMPLETE;
            }
            else if (EntitySource.HasValue)
            {
                stack.Playables?.Clear();
                DiscoverRealRules.Open(controller, source, EntitySource.Value, _offerCount, _poolFilter, _recipeDbfId);
                result = TaskState.COMPLETE;
            }
            else if (_poolFilter.HasValue)
            {
                if (_reductionBudget > 0)
                {
                    if (source[DiscoverRealRules.Budget] == 0) source[DiscoverRealRules.Budget] = _reductionBudget;
                    if (controller.HandZone.IsFull) { source[DiscoverRealRules.Budget] = 0; return TaskState.COMPLETE; }
                    stack.Playables?.Clear();
                }
                new EffectContext(game, controller.Id, source.Id, target?.Id, stack)
                    .Discover(_poolFilter.Value, ChoiceAction.STACK, costBinding: _costBinding, recipeDbfId: _recipeDbfId);
                // An empty pool does nothing; the remaining recipe still runs.
                result = TaskState.COMPLETE;
            }
            else
                result = _fixedOffers == null ? _discover.Process(game, controller, source, target, stack) :
                    Actions.Generic.CreateChoiceCards(controller, source, null, ChoiceType.GENERAL, ChoiceAction.STACK,
                        _fixedOffers.Select(Cards.FromId).ToArray(), null) ? TaskState.COMPLETE : TaskState.STOP;
            if ((_companionRoster || _offers != null || _fixedOffers != null || _poolFilter.HasValue) && controller.Choice != null &&
                (Actions.Generic.IsDecisionRecipe(controller) || _reductionBudget > 0))
                foreach (int id in controller.Choice.Choices)
                    game.IdEntityDic[id][Actions.Generic.PrivateDecisionOfferTag] = controller.PlayerId;
            if (controller.Choice == null && _reductionBudget > 0) source[DiscoverRealRules.Budget] = 0;
            return controller.Choice != null ? TaskState.SUSPENDED : result;
        }

        internal bool Eligible(Controller controller, int selected) => !EntitySource.HasValue ||
            DiscoverRealRules.Eligible(controller, EntitySource.Value, selected);

        internal int Resolve(Controller controller, int sourceId, Choice choice, int selected, out bool repeat)
        {
            repeat = false;
            if (DiscoverRealRules.IsRefresh(choice, selected)) {
                var game = controller.Game;
                if (game.Random.Next(5) == 0) controller.Hero.TakeDamage(game.IdEntityDic[sourceId], 5, outgoingApplied: true);
                game.DeathProcessingAndAuraUpdate();
                game.CheckDeadHeroes();
                repeat = game.State != Enums.State.COMPLETE;
                return selected;
            }
            if (!EntitySource.HasValue && _reductionBudget == 0) {
                if (controller.Game.IdEntityDic.TryGetValue(sourceId, out var mapSource) && mapSource.Card.Id == "TLC_464") {
                    var mapChosen = controller.Game.IdEntityDic[selected];
                    Generic.AddHandPhase(controller, controller.SetasideZone.Remove(mapChosen));
                    DiscoverRealRules.RetainMap(controller.Game.IdEntityDic[sourceId], choice, selected, mapChosen);
                }
                return selected;
            }
            var source = controller.Game.IdEntityDic[sourceId];
            if (EntitySource.HasValue)
                return DiscoverRealRules.Resolve(controller, source, choice, selected, EntitySource.Value, _entityAction, _rejected, _discount);
            var chosen = controller.Game.IdEntityDic[selected];
            bool entered;
            using (controller.HandZone.DeferCardStateEntry())
            {
                Generic.AddHandPhase(controller, controller.SetasideZone.Remove(chosen));
                entered = chosen.Zone == controller.HandZone;
                if (entered)
                {
                    controller.Game.AuraUpdate(); // Sample the current hand cost, including newly attached cost auras.
                    int reduction = Math.Min(Math.Max(0, chosen.Cost), source[DiscoverRealRules.Budget]);
                    if (reduction > 0) Generic.AddEnchantmentBlock(controller.Game, Cards.FromId(_discount), (IPlayable)source, chosen, reduction);
                    source[DiscoverRealRules.Budget] -= reduction;
                }
            }
            if (entered)
            {
                controller.Game.ProcessTasks();
                repeat = source[DiscoverRealRules.Budget] > 0 && !controller.HandZone.IsFull && controller.Game.State != Enums.State.COMPLETE;
            }
            if (!repeat) source[DiscoverRealRules.Budget] = 0;
            return selected;
        }

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
            => throw new NotSupportedException("DiscoverThenContinueTask requires a registered suspendable recipe.");
    }
}
