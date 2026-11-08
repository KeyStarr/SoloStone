using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Model.Events
{
    public enum ResolutionKind
    {
        PlayCard, PlayMinion, SpellCast, Summon, Attack,
        OnPlay, SpellText, Battlecry, AfterPlay, SummonOn, SummonAfter,
        AttackPreparation, AttackDamage, AttackAfter, Cleanup, DeathCreation,
        DeathResolution, AuraUpdate, Event, TaskDrain, TriggerResolution, TaskResolution, EffectResolution
    }

    // Value snapshots are passed by copy; mutable positional accessors also support netstandard2.1.
    public record struct ResolutionFrame(long Id, long ParentId, long SequenceId,
        int Depth, ResolutionKind Kind, int SourceId);

    public enum ShadowDivergence { None, OrderOfPlay, ActivationNotEligible, ControllerChanged, NestedValidation }

    // Eligibility is metadata only. Null means the legacy predicate result was not observable.
    public record struct ShadowResponse(long Id, long FrameId, long SequenceId,
        string Channel, long Ordinal, int BusOwnerId, int EventSourceId, int OwnerId, int ControllerId, int OrderOfPlay,
        bool? V2Eligible, ShadowDivergence Divergence);

    public record struct ResolutionObservation(string Stage, ResolutionFrame Frame,
        ShadowResponse Response, bool LegacyQueued);

    /// <summary>
    /// Observes LegacyV1 helpers without changing their ordering or eligibility.
    /// Explicitly registered effect recipes use a separate persisted stack of resumable cursors.
    /// </summary>
    public sealed class ResolutionScheduler
    {
        // Legacy observation scopes are stack-local; registered effect cursors persist separately.
        [NonSerialized] private List<FrameState> _frames;
        [NonSerialized] private Candidate _candidate;
        [field: NonSerialized] public Action<ResolutionObservation> Trace { get; set; }
        private long _nextFrameId;
        private long _nextResponseId;
        public long FramesStarted { get; private set; }
        public long FramesEnded { get; private set; }
        public long OrderingDivergences { get; private set; }
        public long ActivationDivergences { get; private set; }
        public long ControllerDivergences { get; private set; }
        public long ValidationDivergences { get; private set; }
        private List<EffectFrame> _effects;
        public bool HasSuspendedEffect => _effects != null && _effects.Count != 0 && _effects[_effects.Count - 1].DecisionId != 0;
        internal EffectFrame SuspendedEffect => HasSuspendedEffect ? _effects[_effects.Count - 1] : null;
        internal System.Collections.Generic.IEnumerable<EffectFrame> EffectFrames => _effects ?? Enumerable.Empty<EffectFrame>();
        internal EffectFrame CurrentEffect => _effects != null && _effects.Count != 0 ? _effects[_effects.Count - 1] : null;
        public int Depth => _frames?.Count ?? 0;
        public ResolutionFrame Current => Depth == 0 ? default : _frames[Depth - 1].Frame;
        internal bool IsResolvingDeaths => _frames != null && _frames.Any(frame => frame.Frame.Kind == ResolutionKind.DeathResolution);

        private struct Member
        {
            internal GameEventSubscription Subscription;
            internal int Controller;
            internal bool? Validated;
        }
        private struct FrameState
        {
            internal ResolutionFrame Frame;
            internal List<Member> Members;
        }
        internal struct Candidate
        {
            internal GameEventBus Bus;
            internal GameEventSubscription Subscription;
            internal ShadowResponse Response;
            internal bool Queued;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly ResolutionScheduler _scheduler;
            private readonly long _id;
            internal Scope(ResolutionScheduler scheduler, long id) { _scheduler = scheduler; _id = id; }
            public void Dispose() => _scheduler?.End(_id);
        }

        public Scope Enter(ResolutionKind kind, int sourceId = 0)
            => Push(kind, sourceId, false, null);

        public Scope Sequence(ResolutionKind kind, Game game, IEntity source)
        {
            List<Member> members = null;
            Capture(game.TriggerManager.Events, kind, ref members);
            if (kind == ResolutionKind.Attack && source is Character character)
                Capture(character.Events, kind, ref members);
            return Push(kind, source?.Id ?? 0, true, members);
        }

        private Scope Push(ResolutionKind kind, int sourceId, bool sequence, List<Member> members)
        {
            var parent = Current;
            long id = ++_nextFrameId;
            var frame = new ResolutionFrame(id, parent.Id, sequence ? id : parent.SequenceId, Depth, kind, sourceId);
            if (_frames == null) _frames = new List<FrameState>(16);
            _frames.Add(new FrameState { Frame = frame, Members = members });
            FramesStarted++;
            try { Trace?.Invoke(new ResolutionObservation("start", frame, default, false)); }
            catch { _frames.RemoveAt(Depth - 1); FramesEnded++; throw; }
            return new Scope(this, id);
        }

        private void End(long id)
        {
            var frame = Current;
            if (frame.Id != id) throw new InvalidOperationException("Resolution frames must close in stack order.");
            _frames.RemoveAt(Depth - 1);
            FramesEnded++;
            Trace?.Invoke(new ResolutionObservation("end", frame, default, false));
        }

        internal bool IsQuiescent => Depth == 0 && _candidate.Subscription == null;

        public void RequireQuiescent()
        {
            if (!IsQuiescent)
                throw new NotSupportedException("Legacy resolution scopes are in flight; only registered effect boundaries can be saved.");
        }

        internal ResolutionScheduler CloneBoundary()
        {
            RequireQuiescent();
            return new ResolutionScheduler {
                _nextFrameId = _nextFrameId, _nextResponseId = _nextResponseId,
                _effects = _effects?.Select(f => f.Clone()).ToList(),
                FramesStarted = FramesStarted, FramesEnded = FramesEnded,
                OrderingDivergences = OrderingDivergences, ActivationDivergences = ActivationDivergences,
                ControllerDivergences = ControllerDivergences, ValidationDivergences = ValidationDivergences
            };
        }

        internal TaskState RunEffect(Game game, string recipeKey, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (_effects != null && _effects.Count != 0)
                throw new InvalidOperationException("An effect is already running or suspended; nest recipes as explicit steps.");
            EffectFrameRecipes.Resolve(recipeKey);
            if (game.Player1.Choice != null || game.Player2.Choice != null)
                throw new InvalidOperationException("Cannot start a suspendable effect over an existing choice.");
            if (_effects == null) _effects = new List<EffectFrame>();
            _effects.Add(new EffectFrame(recipeKey, controller, source, target, stack));
            return ContinueEffect(game);
        }

        // Indirect casts finish synchronously, isolating their cursors from the calling recipe.
        internal void RunAutomaticEffect(Game game, string key, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (HasSuspendedEffect) throw new InvalidOperationException("Cannot cast over a pending player decision.");
            var caller = _effects;
            var player1Choice = game.Player1.Choice;
            var player2Choice = game.Player2.Choice;
            _effects = null;
            game.Player1.Choice = null;
            game.Player2.Choice = null;
            try
            {
                RunEffect(game, key, controller, source, target, stack);
                while (HasSuspendedEffect)
                {
                    var frame = SuspendedEffect;
                    var choice = controller.Choice;
                    bool ordered = EffectFrameRecipes.Resolve(frame.RecipeKey)[frame.NextStep - 1]
                        is SelectEntityThenContinueTask selector && selector.Spec != null;
                    ResumeEffect(game, ordered && controller[Actions.AutoPlayRules.Active] == 0
                        ? choice.Choices[0] : choice.Choices.Choose(game.Random));
                }
            }
            finally
            {
                _effects = caller;
                game.Player1.Choice = player1Choice;
                game.Player2.Choice = player2Choice;
            }
        }

        internal void ResumeEffect(Game game, int selected)
        {
            var frame = SuspendedEffect ?? throw new InvalidOperationException("No suspended effect.");
            game.Facts.AdmitDefinition(game.IdEntityDic[selected].Card);
            var selectedChoice = game.ControllerById(frame.ControllerId).Choice;
            bool completedDiscover = selectedChoice.HistoryDiscoverId != 0 && !Actions.DiscoverRealRules.IsRefresh(selectedChoice, selected);
            game.Facts.Discover(selectedChoice, selected);
            // Consume identity before executing any continuation. A repeated/reentrant answer cannot replay it.
            frame.DecisionId = 0;
            var choice = game.ControllerById(frame.ControllerId).Choice;
            DarkGifts.ApplyChosen(choice, game.IdEntityDic[selected]);
            if (completedDiscover) Actions.DiscoverRealRules.Completed(choice, selected);
            game.ControllerById(frame.ControllerId).Choice = null;
            bool repeat = false;
            if (EffectFrameRecipes.Resolve(frame.RecipeKey)[frame.NextStep - 1] is DiscoverThenContinueTask discover)
                selected = discover.Resolve(game.ControllerById(frame.ControllerId), frame.SourceId,
                    choice, selected, out repeat);
            if (EffectFrameRecipes.Resolve(frame.RecipeKey)[frame.NextStep - 1] is DiscoverEntitiesWithDarkGiftThenContinueTask)
            {
                var chosen = game.IdEntityDic[selected];
                Actions.Generic.Draw(game.ControllerById(frame.ControllerId), chosen);
                DarkGifts.ResolveSweetDreams(chosen);
            }
            frame.Choose(game, selected);
            if (repeat) frame.NextStep--;
            ContinueEffect(game);
            Actions.AutoPlayRules.DrainDiscover(game.ControllerById(frame.ControllerId));
            // Nested frames share the root's source; a parked Outcast play also owns the work it queued.
            Actions.OutcastRules.AfterResume(game, frame.SourceId);
            Actions.AfterAttackRules.Flush(game);
        }

        internal void CancelSuspendedEffect(Game game)
        {
            RequireQuiescent();
            var frame = SuspendedEffect ?? throw new InvalidOperationException("No suspended effect.");
            game.ControllerById(frame.ControllerId).Choice = null;
            frame.DecisionId = 0;
            // Concession discards every parent cursor without running another step or drawing RNG.
            Actions.CthunRules.CancelAbilities(game);
            Actions.AfterAttackRules.Cancel(game);
            foreach (var pending in _effects)
                if (game.IdEntityDic.TryGetValue(pending.SourceId, out var source)) source[Actions.DiscoverRealRules.Budget] = 0;
            _effects.Clear();
            Actions.OutcastRules.EndContinuation(game, frame.SourceId);
            game.SpellCompletion = null;
            Actions.Generic.CancelDecisionPlay(game.Player1);
            Actions.Generic.CancelDecisionPlay(game.Player2);
        }

        private TaskState ContinueEffect(Game game)
        {
            using var scope = Enter(ResolutionKind.EffectResolution, _effects[_effects.Count - 1].SourceId);
            while (_effects.Count != 0)
            {
                var frame = _effects[_effects.Count - 1];
                var steps = EffectFrameRecipes.Resolve(frame.RecipeKey);
                var stack = frame.Restore(game);
                if (frame.NextStep == steps.Length)
                {
                    _effects.RemoveAt(_effects.Count - 1);
                    bool attackChild = frame.RecipeKey == Actions.CthunRules.AttackRecipeKey || Actions.AfterAttackRules.IsRecipe(frame.RecipeKey);
                    if (_effects.Count != 0 && attackChild && Actions.AfterAttackRules.TakeNext(game) is EffectFrame nextAttack)
                    { _effects.Add(nextAttack); continue; }
                    if (_effects.Count != 0 && !attackChild)
                    {
                        var parent = _effects[_effects.Count - 1];
                        parent.Capture(stack);
                        parent.CapturedCandidateIds = frame.CapturedCandidateIds.ToArray();
                        parent.ChosenEntities = frame.ChosenEntities.ToArray();
                        parent.LastSelectionEntityId = frame.LastSelectionEntityId;
                    }
                    continue;
                }
                var controller = game.ControllerById(frame.ControllerId);
                IEntity source = frame.SourceId == 0 ? null : frame.SourceId == game.Id ? game :
                    frame.SourceId == game.Player1.Id ? game.Player1 : frame.SourceId == game.Player2.Id ? game.Player2 : game.IdEntityDic[frame.SourceId];
                var target = frame.TargetId == 0 ? null : game.IdEntityDic[frame.TargetId];
                target = Actions.GeneratedDefinitionRules.RecipeTarget(frame.RecipeKey, source, target);
                var task = steps[frame.NextStep++];
                if (task is SuspendableTask nested)
                {
                    _effects.Add(new EffectFrame(nested.RecipeKey, controller, source, target, stack) {
                        CapturedCandidateIds = frame.CapturedCandidateIds.ToArray(),
                        ChosenEntities = frame.ChosenEntities.ToArray(), LastSelectionEntityId = frame.LastSelectionEntityId
                    });
                    continue;
                }
                var result = task is SelectEntityThenContinueTask select ? select.Open(game, controller, source, frame) :
                    task is DiscoverThenContinueTask discover ? discover.Open(game, controller, source, target, stack) :
                    task is DiscoverWithDarkGiftThenContinueTask darkGift ? darkGift.Open(game, controller, source, target, stack) :
                    task is DiscoverEntitiesWithDarkGiftThenContinueTask deckGift ? deckGift.Open(game, controller, source, target, stack) :
                    task.Process(game, controller, source, target, stack);
                frame.Capture(stack);
                int attackSource = controller[Actions.CthunRules.PendingAttackSource];
                if (attackSource != 0)
                {
                    if (!(task is AttackTask)) throw new InvalidOperationException("Hero attacks with decisions require registered AttackTask steps.");
                    controller[Actions.CthunRules.PendingAttackSource] = 0;
                    _effects.Add(new EffectFrame(Actions.CthunRules.AttackRecipeKey, controller,
                        game.IdEntityDic[attackSource], null, new TaskStack()));
                    continue;
                }
                if (task is AttackTask && Actions.AfterAttackRules.TakeNext(game) is EffectFrame afterAttack)
                { _effects.Add(afterAttack); continue; }
                if (result == TaskState.SUSPENDED)
                {
                    if (!(task is DiscoverThenContinueTask) && !(task is DiscoverWithDarkGiftThenContinueTask) &&
                        !(task is DiscoverEntitiesWithDarkGiftThenContinueTask) && !(task is SelectEntityThenContinueTask) || controller.Choice == null)
                        throw new InvalidOperationException("Suspension requires a registered decision step.");
                    frame.DecisionId = controller.Choice.DecisionId;
                    frame.RngAtSuspension = game.Random.CaptureState();
                    return TaskState.SUSPENDED;
                }
                if (result != TaskState.COMPLETE) frame.NextStep = steps.Length;
            }
            return TaskState.COMPLETE;
        }

        internal bool ValidateSelection(Game game, int selected)
        {
            var frame = SuspendedEffect;
            var task = EffectFrameRecipes.Resolve(frame.RecipeKey)[frame.NextStep - 1];
            var controller = game.ControllerById(frame.ControllerId);
            return task is SelectEntityThenContinueTask select ? select.IsEligible(game, controller, frame, selected) :
                task is DiscoverThenContinueTask discover ? discover.Eligible(controller, selected) :
                !(task is DiscoverEntitiesWithDarkGiftThenContinueTask deckGift) || deckGift.IsEligible(game, controller, selected);
        }

        internal void ValidateEffects(Game game)
        {
            if (_effects == null || _effects.Count == 0) return;
            if (!HasSuspendedEffect) throw new NotSupportedException("Effect recipe is executing, not suspended.");
            foreach (var frame in _effects)
            {
                var steps = EffectFrameRecipes.Resolve(frame.RecipeKey);
                if (frame.NextStep < 1 || frame.NextStep > steps.Length ||
                    frame.ControllerId != game.Player1.Id && frame.ControllerId != game.Player2.Id)
                    throw new System.Runtime.Serialization.SerializationException("Invalid suspended effect cursor/owner.");
                frame.Restore(game);
            }
            var suspended = SuspendedEffect;
            var choice = game.ControllerById(suspended.ControllerId).Choice;
            if (choice == null || choice.DecisionId != suspended.DecisionId || DarkGifts.RecipeSource(game, choice.SourceId) != suspended.SourceId ||
                choice.ChoiceAction != ChoiceAction.STACK || choice.AfterChooseTask != null || choice.NextChoice != null)
                throw new System.Runtime.Serialization.SerializationException("Suspended effect decision does not match its frame.");
        }

        private static bool Belongs(string channel, ResolutionKind kind)
        {
            switch (channel)
            {
                case "PlayCardTrigger": case "AfterPlayCardTrigger": case "TargetTrigger":
                    return kind == ResolutionKind.PlayCard || (channel == "TargetTrigger" && kind == ResolutionKind.Attack);
                case "PlayMinionTrigger": case "AfterPlayMinionTrigger": return kind == ResolutionKind.PlayMinion;
                case "CastSpellTrigger": case "AfterCastTrigger": return kind == ResolutionKind.SpellCast;
                case "SummonTrigger": case "AfterSummonTrigger": return kind == ResolutionKind.Summon || kind == ResolutionKind.PlayMinion;
                case "AttackTrigger": case "AfterAttackTrigger": return kind == ResolutionKind.Attack;
                default: return false;
            }
        }

        private static void Capture(GameEventBus bus, ResolutionKind kind, ref List<Member> members)
        {
            foreach (var channel in bus.Subscriptions)
            {
                if (!Belongs(channel.Key, kind)) continue;
                foreach (var subscription in channel.Value)
                {
                    if (!(subscription.Owner is Trigger) || subscription.EntityOwner == null) continue;
                    if (members == null) members = new List<Member>();
                    members.Add(new Member { Subscription = subscription, Controller = subscription.EntityOwner.Controller.Id });
                }
            }
        }

        // Called immediately AFTER the existing ValidateTriggers call; never invokes a condition.
        public void CaptureValidation(SequenceType type)
        {
            for (int i = Depth - 1; i >= 0; i--)
            {
                var state = _frames[i];
                var kind = state.Frame.Kind;
                if (!((type == SequenceType.PlayCard || type == SequenceType.Target) && kind == ResolutionKind.PlayCard ||
                    type == SequenceType.Target && kind == ResolutionKind.Attack ||
                    type == SequenceType.PlayMinion && kind == ResolutionKind.PlayMinion ||
                    type == SequenceType.PlaySpell && kind == ResolutionKind.SpellCast)) continue;
                if (state.Members != null)
                    for (int n = 0; n < state.Members.Count; n++)
                    {
                        var member = state.Members[n];
                        var trigger = (Trigger)member.Subscription.Owner;
                        bool matches = type == SequenceType.Target ? trigger.Type == TriggerType.TARGET :
                            type == SequenceType.PlayCard ? trigger.Type == TriggerType.PLAY_CARD || trigger.Type == TriggerType.AFTER_PLAY_CARD :
                            type == SequenceType.PlayMinion ? trigger.Type == TriggerType.PLAY_MINION || trigger.Type == TriggerType.AFTER_PLAY_MINION :
                            trigger.Type == TriggerType.CAST_SPELL || trigger.Type == TriggerType.AFTER_CAST;
                        if (matches) { member.Validated = trigger.Validated; state.Members[n] = member; }
                    }
                return;
            }
        }

        internal Candidate BeginCallback(GameEventBus bus, GameEventSubscription subscription, string channel, IEntity sender)
        {
            var previous = _candidate;
            _candidate = default;
            if (!(subscription.Owner is Trigger) || subscription.EntityOwner == null) return previous;
            var owner = subscription.EntityOwner;
            bool? eligible = null;
            var divergence = ShadowDivergence.None;
            long sequence = Current.SequenceId;
            for (int i = Depth - 1; i >= 0; i--)
            {
                var state = _frames[i];
                if (state.Frame.SequenceId != state.Frame.Id || !Belongs(channel, state.Frame.Kind)) continue;
                sequence = state.Frame.Id;
                eligible = false;
                divergence = ShadowDivergence.ActivationNotEligible;
                if (state.Members != null)
                    foreach (var member in state.Members)
                    {
                        if (!ReferenceEquals(member.Subscription, subscription)) continue;
                        bool sameController = member.Controller == owner.Controller.Id;
                        eligible = sameController ? member.Validated : false;
                        divergence = sameController ? ShadowDivergence.None : ShadowDivergence.ControllerChanged;
                        break;
                    }
                break;
            }
            _candidate = new Candidate { Bus = bus, Subscription = subscription,
                Response = new ShadowResponse(++_nextResponseId, Current.Id, sequence, channel,
                    subscription.Ordinal, ReferenceEquals(bus, owner.Game.TriggerManager.Events) ? 0 : sender?.Id ?? 0,
                    sender?.Id ?? 0, owner.Id, owner.Controller.Id, owner.OrderOfPlay, eligible, divergence) };
            return previous;
        }

        internal void EndCallback(Candidate previous, bool completed)
        {
            var candidate = _candidate;
            _candidate = previous;
            if (!completed || candidate.Subscription == null || candidate.Queued) return;
            var response = candidate.Response;
            var owner = candidate.Subscription.EntityOwner;
            // A removed/dormant listener is a liveness veto, not nested validation corruption.
            if (!candidate.Bus.Contains(response.Channel, candidate.Subscription) || owner is Minion m && m.IsDormant)
                response = response with { V2Eligible = false, Divergence = ShadowDivergence.None };
            else if (response.V2Eligible == true)
                response = response with { Divergence = ShadowDivergence.NestedValidation };
            else response = response with { Divergence = ShadowDivergence.None };
            Record(response, "candidate", false);
        }

        internal ShadowResponse Queued(Tasks.ISimpleTask task)
        {
            if (!(_candidate.Subscription?.Owner is Trigger trigger) || !ReferenceEquals(trigger.SingleTask, task)) return default;
            var response = _candidate.Response;
            if (_candidate.Queued) response = response with { Id = ++_nextResponseId };
            _candidate.Queued = true;
            if (!response.V2Eligible.HasValue) response = response with { V2Eligible = true };
            if (response.V2Eligible == false && response.Divergence == ShadowDivergence.None)
                response = response with { Divergence = ShadowDivergence.NestedValidation };
            Record(response, "queued", true);
            return response;
        }

        internal readonly struct CallbackSuspension : IDisposable
        {
            private readonly ResolutionScheduler _scheduler;
            private readonly Candidate _previous;
            internal CallbackSuspension(ResolutionScheduler scheduler) { _scheduler = scheduler; _previous = scheduler.SuspendCallback(); }
            public void Dispose() => _scheduler.RestoreCallback(_previous);
        }
        internal CallbackSuspension Suspend() => new CallbackSuspension(this);
        private Candidate SuspendCallback() { var old = _candidate; _candidate = default; return old; }
        internal void RestoreCallback(Candidate candidate) => _candidate = candidate;

        internal void Resolved(ShadowResponse response)
        {
            if (response.Id != 0) Trace?.Invoke(new ResolutionObservation("resolved", Current, response, true));
        }

        internal void CompareOrder(ShadowResponse previous, ShadowResponse current)
        {
            if (previous.Id == 0 || current.Id == 0 || previous.V2Eligible != true || current.V2Eligible != true || previous.EventSourceId != current.EventSourceId) return;
            // Adjacent inversions in one frozen legacy FIFO are evidence, never a replacement queue.
            if (previous.OrderOfPlay > current.OrderOfPlay || previous.OrderOfPlay == current.OrderOfPlay &&
                previous.BusOwnerId == current.BusOwnerId && previous.Ordinal > current.Ordinal)
                Record(current with { Divergence = ShadowDivergence.OrderOfPlay }, "order", true);
        }

        private void Record(ShadowResponse response, string stage, bool queued)
        {
            switch (response.Divergence)
            {
                case ShadowDivergence.OrderOfPlay: OrderingDivergences++; break;
                case ShadowDivergence.ActivationNotEligible: ActivationDivergences++; break;
                case ShadowDivergence.ControllerChanged: ControllerDivergences++; break;
                case ShadowDivergence.NestedValidation: ValidationDivergences++; break;
            }
            Trace?.Invoke(new ResolutionObservation(stage, Current, response, queued));
        }

        // Resolve ownership once per activation, outside the hot dispatch path. Trigger remains untouched.
        private static readonly FieldInfo TriggerOwner = typeof(Trigger).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static IPlayable OwnerOf(object owner) => owner is Trigger ? (IPlayable)TriggerOwner.GetValue(owner) : null;
    }
}
