using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.EffectSystem
{
    /// <summary>LegacyV1 operations in the caller's task frame, with no additional history blocks.
    /// A context lives for one task invocation; persist entity IDs in ordinary choices, not this object.</summary>
    public sealed class EffectContext
    {
        private readonly Game _game;
        private readonly TaskStack _stack;
        public int SourceId { get; }
        public int ControllerId { get; }
        public int? TargetId { get; }
        public HistoryQuery History => _game.HistoryFacts.Query;
        public FormatType Format => _game.FormatType;
        public int? EventSourceId { get; }
        public int? EventTargetId { get; }
        public int EventNumber { get; }
        public bool PlayedFromEdge => OutcastRules.PlayedFromEdge(Source as IPlayable);
        public int LastChosenEntityId => _game.Resolution.CurrentEffect?.LastSelectionEntityId ?? 0;
        public string LastChosenCardId => _stack?.Playables?.LastOrDefault()?.Card.Id;
        public int[] FriendlyLocationIds => Controller.BoardZone.GetLocations().Select(location => location.Id).ToArray();
        public int[] FriendlyMinionIds => Controller.BoardZone.GetAll().Select(m => m.Id).ToArray();

        public EffectContext(Game game, int controllerId, int sourceId, int? targetId = null, TaskStack stack = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            ControllerId = controllerId;
            SourceId = sourceId;
            TargetId = targetId;
            _stack = stack;
            EventSourceId = game.CurrentEventData?.EventSource?.Id;
            EventTargetId = game.CurrentEventData?.EventTarget?.Id;
            EventNumber = game.CurrentEventData?.EventNumber ?? 0;
        }

        private Controller Controller => _game.ControllerById(ControllerId);
        private IEntity Source => _game.IdEntityDic[SourceId];
        private IPlayable Target(int id) => _game.IdEntityDic[id];

        /// <summary>Adapt a card recipe to the existing runner. Return STOP explicitly when the recipe needs it.</summary>
        public static ISimpleTask Task(Func<EffectContext, TaskState> resolve) => new EffectContextTask(resolve);

        /// <summary>Execute inline, retaining the current stack and target. Never drain or open a task block here.</summary>
        public TaskState RunLegacy(ISimpleTask task, int? targetId = null) =>
            task.Process(_game, Controller, Source, (targetId ?? TargetId) is int id ? Target(id) : null, _stack);

        public TaskState Damage(int targetId, int amount, bool spellDamage = false) =>
            RunLegacy(new DamageTask(amount, EntityType.TARGET, spellDamage), targetId);

        /// <summary>Preserve DamageTask's ordered traversal, including duplicate targets and deferred deaths.</summary>
        public TaskState DamageBatch(IEnumerable<int> targetIds, int amount, bool spellDamage = false) =>
            new DamageTask(amount, EntityType.STACK, spellDamage).Process(_game, Controller, Source, null,
                new TaskStack { Playables = targetIds.Select(Target).ToList() });

        public TaskState Heal(int targetId, int amount) => RunLegacy(new HealTask(amount, EntityType.TARGET), targetId);

        /// <summary>Zero-based board position; -1 appends. Returns 0 when the legacy task cannot summon.</summary>
        public int Summon(string cardId, int position = -1)
        {
            var stack = new TaskStack { Playables = new List<IPlayable>(), Number = position + 1 };
            var task = new SummonTask(position < 0 ? SummonSide.DEFAULT : SummonSide.NUMBER,
                Cards.FromId(cardId), addToStack: true);
            task.Process(_game, Controller, Source, null, stack);
            return stack.Playables.FirstOrDefault()?.Id ?? 0;
        }

        /// <summary>Physical board index for a Deathrattle summon: the dying minion's last slot (as SummonTask DEATHRATTLE),
        /// or right of a source that is still in play (a triggered Deathrattle). Enchantment sources resolve their target.</summary>
        public static int DeathrattlePosition(Controller controller, IEntity source)
        {
            var minion = source as Minion ?? (source as Enchantment)?.Target as Minion;
            if (minion == null) return controller.BoardZone.SlotCount;
            int position = minion.Zone == controller.BoardZone ? minion.ZonePosition + 1 : minion.LastBoardPosition;
            return Math.Clamp(position, 0, controller.BoardZone.SlotCount);
        }

        public bool AdvanceQuest(int amount = 1) => QuestRules.Advance(Source as Spell, amount);
        public bool HasPlayedQuest() => QuestRules.HasPlayedQuest(Controller);
        public int PlayerEffectStacks(string enchantmentId) => QuestRules.PlayerEffectStacks(Controller, enchantmentId);
        public void AddPlayerEffect(string enchantmentId) => QuestRules.AddPlayerEffect(Controller, (IPlayable)Source, enchantmentId);

        public void GiveBuff(int targetId, string enchantmentId, int amount) =>
            Generic.AddEnchantmentBlock(_game, Cards.FromId(enchantmentId), (IPlayable)Source, Target(targetId), amount, amount);
        public int SummonRightOfLocation(string cardId)
        {
            var source = (BoardObject)Source;
            int position = source.Zone == Controller.BoardZone ? source.ZonePosition + 1 : source.LastBoardPosition;
            return Summon(cardId, Math.Min(position, Controller.BoardZone.SlotCount));
        }

        public void AdvanceLocationStage(string nextCardId) => LocationRules.AdvanceStage((BoardObject)Source, nextCardId);

        public IPlayable DrawEntity()
        {
            bool burned = Controller.HandZone.IsFull;
            var drawn = Generic.Draw(Controller);
            return burned ? null : drawn;
        }

        public TaskState RefreshMana(int amount) =>
            RunLegacy(new SetControllerGameTagTask(GameTag.RESOURCES_USED, Math.Max(0, Controller.UsedMana - amount)));

        public TaskState DestroyAllMinionsAndLocations()
        {
            RunLegacy(new DestroyTask(EntityType.ALLMINIONS));
            foreach (var location in Controller.BoardZone.GetLocations()) location.Destroy();
            foreach (var location in Controller.Opponent.BoardZone.GetLocations()) location.Destroy();
            return TaskState.COMPLETE;
        }

        public int Herald() => HeraldRules.Resolve(Controller, Source);
        public void GrantPrepare(int targetId) => PrepareRules.Grant(Target(targetId));

        public int ImbueCount => ImbueRules.Count(Controller);

        public void Imbue(int times = 1) => ImbueRules.Imbue(Controller, Source, times);

        // Direct resolution is not a player use: no payment, exhaustion or Inspire.
        public TaskState TriggerCurrentHeroPower(int? targetId = null) =>
            ImbueRules.TriggerCurrentHeroPower(Controller, targetId);

        public void CastSpell(int entityId, HistoryCastKind kind, int? targetId = null, int chooseOne = 0)
        {
            if (kind == HistoryCastKind.Player) throw new ArgumentException("Player casts require a committed hand play.", nameof(kind));
            var spell = (Spell)Target(entityId);
            var target = targetId.HasValue ? (ICharacter)Target(targetId.Value) : null;
            if (!HeraldRules.CanCast(spell.Controller, spell, target)) return;
            if (!ImbueRules.CanCast(spell.Controller, spell, target)) return;
            using (var cast = _game.Facts.Cast(spell, kind))
            {
                cast.AllowBodyClaim = true;
                Generic.CastSpell(spell.Controller, _game, spell, target, chooseOne);
                cast.Complete();
            }
        }

        /// <summary>Opt-in random targeting with entity-specific requirements (including Elusive).
        /// Explicit candidates narrow the card's targets; nontargeting effects supply their own set.
        /// Returns null without consuming RNG when no target is legal. Does not cast or change CardTarget.</summary>
        public int? RandomLegalTarget(int entityId, IEnumerable<int> candidateIds = null)
        {
            var source = (Playable)Target(entityId);
            // Repeats retain spell targets; untargeted random effects still select independently.
            if (source.Card.TargetingType != TargetingType.None && RepeatRules.IsRepeating(source)) return null;
            var candidates = candidateIds == null ? source.Card.GetValidPlayTargets(source.Controller)
                : candidateIds.Distinct().Select(id => Target(id)).OfType<ICharacter>();
            var legal = candidates.Where(t => (t == t.Controller.Hero || t.Zone == t.Controller.BoardZone)
                && (source.Card.TargetingType == TargetingType.None
                    ? source.TargetingRequirements(t) : source.IsValidPlayTarget(t))).ToArray();
            return legal.Length == 0 ? (int?)null : legal[_game.Random.Next(legal.Length)].Id;
        }

        /// <summary>Draw a random matching physical card, or nothing (no fatigue) if no match exists.
        /// Fair search cannot inspect masked deck identities/origins: draw an Unknown slot without evaluating
        /// the predicate, then FairBotState's existing unknown-draw boundary stops expansion. A partly
        /// sampled deck also holds known cards; the top card may fail the predicate (Portal Vanguard
        /// drawing a spell, then +2/+2 on it), so only Unknown slots are candidates.</summary>
        public IPlayable DrawWhere(Func<IPlayable, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var hidden = _game.HistoryFacts.IsProjected ? Controller.DeckZone.Where(card => card is Unknown).ToArray() : null;
            var matches = hidden?.Length > 0 ? hidden : Controller.DeckZone.Where(predicate).ToArray();
            if (matches.Length == 0) return null;
            bool burned = Controller.HandZone.IsFull;
            var drawn = Generic.Draw(Controller, matches[_game.Random.Next(matches.Length)]);
            return burned ? null : drawn;
        }

        public TaskState Draw(int count = 1) => RunLegacy(new DrawTask(count));

        public TaskState DrawUntilHandFull()
        {
            while (!Controller.HandZone.IsFull && !Controller.DeckZone.IsEmpty)
                Generic.Draw(Controller);
            return TaskState.COMPLETE;
        }

        public TaskState EmptyOpponentHand()
        {
            var opponent = Controller.Opponent;
            foreach (var card in opponent.HandZone.ToArray())
                if (!DrawReplacementRules.DestroyOffboard(card)) opponent.GraveyardZone.Add(opponent.HandZone.Remove(card));
            return TaskState.COMPLETE;
        }

        /// <summary>Install pre-sampled offers through the existing serializable choice mechanism.
        /// Appends to an existing chain; the caller still completes before either answer is exposed.</summary>
        public bool Discover(Card[] offers, ChoiceAction action = ChoiceAction.HAND, ISimpleTask afterChoose = null)
        {
            if (offers.Length == 0 || !Generic.CreateChoiceCards(Controller, Source, null, ChoiceType.GENERAL, action, offers, afterChoose)) return false;
            var choice = Controller.Choice;
            while (choice.NextChoice != null) choice = choice.NextChoice;
            _game.Facts.MarkDiscover(choice);
            return true;
        }

        /// <summary>Sample the pinned offline pool, then use the existing choice chain.</summary>
        public bool Discover(OfflinePoolFilter filter, ChoiceAction action = ChoiceAction.HAND, ISimpleTask afterChoose = null,
            PoolCostBinding costBinding = PoolCostBinding.None, int? recipeDbfId = null)
        {
            filter = ResolvePoolFilter(filter, costBinding, recipeDbfId, out var destination);
            if (action == ChoiceAction.STACK && _stack != null) _stack.Playables = new List<IPlayable>();
            var offers = OfflineCardPools.SampleDistinct(_game, filter, Controller.HeroClass, Source.Card.AssetId, action == ChoiceAction.SUMMON ? PoolDestination.Summon :
                action == ChoiceAction.HAND || action == ChoiceAction.STACK ? destination : PoolDestination.Other, OfflineCardPools.PrintedClassMask(Source.Card));
            if (offers.Length == 0 || !Discover(offers, action, afterChoose)) return false;
            var choice = Controller.Choice;
            while (choice.NextChoice != null) choice = choice.NextChoice;
            foreach (int id in choice.Choices)
                _game.IdEntityDic[id][Generic.PrivateDecisionOfferTag] = Controller.PlayerId;
            return true;
        }

        public Card PickRandomCard(OfflinePoolFilter filter, PoolCostBinding costBinding = PoolCostBinding.None, int? recipeDbfId = null)
        {
            filter = ResolvePoolFilter(filter, costBinding, recipeDbfId, out var destination);
            return OfflineCardPools.Pick(_game, filter, Controller.HeroClass, Source.Card.AssetId, destination);
        }

        public Card PickAffordableSpell(OfflinePoolFilter filter, int? recipeDbfId = null)
        {
            filter = ResolvePoolFilter(filter, PoolCostBinding.None, recipeDbfId, out var destination);
            var candidates = SabberStoneCore.Actions.CostRules.AffordableSpells(Controller,
                OfflineCardPools.Candidates(_game, Controller.HeroClass, filter, Source.Card.AssetId, destination));
            return candidates.Length == 0 ? null : candidates[_game.Random.Next(candidates.Length)];
        }

        public bool DiscoverWithDarkGift(OfflinePoolFilter filter, ChoiceAction action = ChoiceAction.HAND,
            ISimpleTask afterChoose = null, PoolCostBinding costBinding = PoolCostBinding.None, int? recipeDbfId = null)
        {
            filter = ResolvePoolFilter(filter, costBinding, recipeDbfId, out var destination);
            if (action == ChoiceAction.STACK && _stack != null) _stack.Playables = new List<IPlayable>();
            return DiscoverWithDarkGift(OfflineCardPools.SampleDistinct(_game, filter, Controller.HeroClass, Source.Card.AssetId, action == ChoiceAction.SUMMON ? PoolDestination.Summon :
                action == ChoiceAction.HAND || action == ChoiceAction.STACK ? destination : PoolDestination.Other, OfflineCardPools.PrintedClassMask(Source.Card)), action, afterChoose);
        }

        /// <summary>Fixed card definitions become private SETASIDE offers; the original deck stays untouched.</summary>
        public bool DiscoverWithDarkGift(Card[] offers, ChoiceAction action = ChoiceAction.HAND, ISimpleTask afterChoose = null)
        {
            DarkGifts.EnsureCompatible(_game);
            if (offers == null) throw new ArgumentNullException(nameof(offers));
            if (offers.Any(card => card == null || card.Type != CardType.MINION))
                throw new ArgumentException("Dark Gift offers must be minions.", nameof(offers));
            var entities = offers.Select(card => Entity.FromCard(Controller, card,
                new EntityData { [GameTag.CREATOR] = SourceId, [GameTag.DISPLAYED_CREATOR] = SourceId }, Controller.SetasideZone)).ToArray();
            return DiscoverEntitiesWithDarkGift(entities, action, afterChoose);
        }

        /// <summary>Existing own-deck entities stay in DECK; HAND selection grants the stored gift then draws that ID.</summary>
        public bool DiscoverEntitiesWithDarkGift(IReadOnlyList<IPlayable> existing, ChoiceAction action = ChoiceAction.HAND, ISimpleTask afterChoose = null)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (existing.Any(p => p == null || !(p is Minion) || p.Game != _game || p.Controller != Controller ||
                    p.Zone != Controller.DeckZone && p.Zone != Controller.SetasideZone) || existing.Select(p => p.Id).Distinct().Count() != existing.Count)
                throw new ArgumentException("Dark Gift offers require distinct own-deck or SETASIDE minions.", nameof(existing));
            if (!DarkGifts.Open(Controller, Source, existing, action, afterChoose)) return false;
            // A gifted Discover is a semantic Discover for history (the executor choice is the chain tail).
            _game.Facts.MarkDiscover(Controller);
            return true;
        }

        public bool GiveDarkGift(int entityId) => DarkGifts.Give(Target(entityId), (IPlayable)Source);
        public bool HasDarkGift(int entityId) => DarkGifts.Has(Target(entityId));

        private OfflinePoolFilter ResolvePoolFilter(OfflinePoolFilter template, PoolCostBinding binding, int? recipeDbfId, out PoolDestination destination)
        {
            if (_game.LegacyPoolReplay)
                throw new InvalidOperationException("Offline pools are unavailable in schema-2 legacy replay mode.");
            // Copied recipes keep their declaration identity; exclusion and creator still use the runtime source.
            var pool = OfflinePoolDeclarations.Cards.SingleOrDefault(card => card.DbfId == (recipeDbfId ?? Source.Card.AssetId))?
                .Pools.FirstOrDefault(requirement => requirement.Filter == template && requirement.CostBinding == binding);
            if (pool == null)
                throw new InvalidOperationException("Effect requires a declared pool for its recipe, filter template and cost binding.");
            destination = pool.Destination;
            if (binding == PoolCostBinding.None) return template;
            if (_stack == null) throw new InvalidOperationException("StackNumber pool binding requires a task stack.");
            return binding == PoolCostBinding.StackNumberMaxCost
                ? template.WithMaxCost(_stack.Number) : template.WithCost(_stack.Number);
        }

        /// <summary>Use the existing DiscoverTask's pool, sampling and choice policy.</summary>
        public TaskState Discover(DiscoverType type, int numberOfChoices = 3) =>
            RunLegacy(new DiscoverTask(type, numberOfChoices));

        public TaskState GiveBuff(int targetId, string enchantmentId) =>
            RunLegacy(new AddEnchantmentTask(enchantmentId, EntityType.TARGET), targetId);

        public TaskState Transform(int targetId, string cardId) =>
            RunLegacy(new TransformTask(cardId, EntityType.TARGET), targetId);

        public TaskState AddToHand(string cardId, int count = 1) =>
            RunLegacy(new AddCardTo(cardId, EntityType.HAND, count));

        public TaskState Destroy(int targetId) => RunLegacy(new DestroyTask(EntityType.TARGET), targetId);

        /// <summary>Exact ChooseNElements consumption, including rejection draws. Like BuildABeast,
        /// this helper does not introduce a RandomHappened notification.</summary>
        public T[] SampleDistinct<T>(IReadOnlyList<T> pool, int count) => pool.ChooseNElements(count, _game.Random);

    }
}
