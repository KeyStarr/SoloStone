using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Select a frozen offer of existing hand cards or board characters without moving entities.</summary>
    public sealed class SelectEntityThenContinueTask : SimpleTask
    {
        internal SelectorSpec Spec { get; }
        public SelectEntityThenContinueTask() { }
        public SelectEntityThenContinueTask(SelectorSpec spec) => Spec = spec ?? throw new ArgumentNullException(nameof(spec));

        internal TaskState Open(Game game, Controller controller, IEntity source, EffectFrame frame)
        {
            if (game.Player1.Choice != null || game.Player2.Choice != null)
                throw new InvalidOperationException("Cannot select over an existing choice.");
            frame.LastSelectionEntityId = 0;
            // Settle queued reactions before rebuilding each fresh descriptor offer; deaths keep the Battlecry boundary.
            if (Spec != null) game.ProcessTasks();
            if (Spec != null)
                frame.SelectorHistory = frame.SelectorHistory.Concat(new[] {
                    new SelectorStepHistory { StepIndex = frame.NextStep - 1 }
                }).ToArray();
            if (Spec != null && frame.SelectionGroupKey != Spec.GroupKey)
            {
                frame.SelectionGroupKey = Spec.GroupKey;
                frame.SelectedInGroup = Array.Empty<int>();
            }
            var candidates = Spec == null || Spec.Source == SelectorSource.Hand
                ? controller.HandZone.Select(p => p.Id)
                : Spec.Source == SelectorSource.CapturedEntities ? frame.CapturedCandidateIds
                : new[] { controller, controller.Opponent }.SelectMany(c =>
                    c.BoardZone.Select(m => m.Id).Concat(Spec.IncludeHeroes ? new[] { c.Hero.Id } : Array.Empty<int>())).OrderBy(id => id);
            if (Spec?.Source == SelectorSource.HandOrBoard)
                candidates = controller.HandZone.Select(p => p.Id).Concat(candidates);
            var offers = candidates.Where(id => Spec == null || SelectorSpec.Matches(game, controller, frame, Spec, id)).ToList();
            if (Spec != null) frame.OfferEntityIds = offers.ToArray();
            if (offers.Count == 0) return Spec?.IfEmpty == EmptySelectorAction.StopRecipe ? TaskState.STOP : TaskState.COMPLETE;
            if (Spec?.UsePlayTarget == true && offers.Contains(frame.TargetId))
            {
                frame.Choose(game, frame.TargetId);
                return TaskState.COMPLETE;
            }
            controller.Choice = new Choice(controller) { ChoiceType = ChoiceType.TARGET,
                ChoiceAction = ChoiceAction.STACK, SourceId = source.Id, Choices = offers };
            return TaskState.SUSPENDED;
        }

        internal bool IsEligible(Game game, Controller controller, EffectFrame frame, int id)
        {
            if (Spec == null) return game.IdEntityDic.TryGetValue(id, out var entity) &&
                entity.Controller == controller && entity.Zone == controller.HandZone;
            if (!frame.OfferEntityIds.Contains(id)) return false;
            // Cost and character getters warm caches. Isolate validation of untrusted answers.
            if (Spec.IsBoard || Spec.MaximumCurrentCost.HasValue)
            {
                var copy = game.CloneExact();
                return SelectorSpec.Matches(copy, copy.ControllerById(controller.Id), frame, Spec, id);
            }
            return SelectorSpec.Matches(game, controller, frame, Spec, id);
        }

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null) =>
            throw new NotSupportedException("Entity selection requires a registered suspendable recipe.");
    }
}
