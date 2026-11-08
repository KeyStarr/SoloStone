using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Model
{
    public partial class Game
    {
        // Kettle indexes are process-global packet IDs, allocated by readers. Never use them as state IDs.
        private long _nextDecisionId = 2;
        private long _optionsDecisionId = 1;
        internal long NextDecisionId => _nextDecisionId++;

        /// <summary>
        /// Projects the existing external decision boundary without changing it. Includes both mulligans.
        /// Call only between engine operations; this is not an in-flight snapshot API.
        /// </summary>
        public IReadOnlyList<PendingDecision> GetPendingDecisions()
        {
            if (State != State.RUNNING) return Array.Empty<PendingDecision>();
            var open = new List<PendingDecision>(2);
            foreach (var player in new[] { Player1, Player2 })
            {
                Choice choice = player.Choice;
                if (choice == null) continue;
                if (choice.ChoiceType != ChoiceType.MULLIGAN && choice.ChoiceType != ChoiceType.GENERAL && choice.ChoiceType != ChoiceType.TARGET)
                    throw new NotSupportedException("Unsupported legacy choice type.");
                var kind = Resolution.SuspendedEffect?.DecisionId == choice.DecisionId ? DecisionKind.SuspendedEffect :
                    choice.ChoiceType == ChoiceType.MULLIGAN ? DecisionKind.Mulligan : DecisionKind.GeneralChoice;
                open.Add(new PendingDecision(choice.DecisionId, player.Id, choice.SourceId, kind,
                    kind == DecisionKind.Mulligan ? 0 : 1, kind == DecisionKind.Mulligan ? choice.Choices.Count : 1,
                    choice.Choices.Select(id => new DecisionOption(id, kind, id))));
            }
            if (open.Count != 0) return open.AsReadOnly();
            if (Step != Step.MAIN_ACTION) return Array.Empty<PendingDecision>();
            var tasks = DecisionTasks();
            open.Add(new PendingDecision(_optionsDecisionId, CurrentPlayer.Id, 0, DecisionKind.Options, 1, 1,
                tasks.Select((task, index) => new DecisionOption(index,
                    task.ChooseOne > 0 ? DecisionKind.ChooseOne : task.HasTarget ? DecisionKind.Target : DecisionKind.Options,
                    task.Source?.Id ?? (task.PlayerTaskType == PlayerTaskType.HERO_ATTACK ? task.Controller.Hero.Id :
                        task.PlayerTaskType == PlayerTaskType.HERO_POWER ? task.Controller.Hero.HeroPower.Id : 0),
                    task.ActionTarget?.Id ?? 0, task.ChooseOne,
                    task is PlayCardTask play ? play.ZonePosition : -1, task.PlayerTaskType))));
            return open.AsReadOnly();
        }

        /// <summary>Deterministic visible-offer policy for descriptor selectors; null preserves legacy policy.</summary>
        public DecisionAnswer GetSelectorAnswer()
        {
            var frame = Resolution.SuspendedEffect;
            if (frame == null || !(Effects.EffectFrameRecipes.Resolve(frame.RecipeKey)[frame.NextStep - 1]
                is Tasks.SimpleTasks.SelectEntityThenContinueTask selector) || selector.Spec == null) return null;
            foreach (int id in frame.OfferEntityIds)
                if (Resolution.ValidateSelection(this, id))
                    return new DecisionAnswer(frame.DecisionId, frame.ControllerId, new[] { id });
            return null;
        }

        // Options reads lazy cost caches and card predicates. Isolate all enumeration from authoritative state.
        // shortcut: one exact clone per option query; optimize only with a proven pure legal-action enumerator.
        private List<PlayerTask> DecisionTasks() => CloneExact().CurrentPlayer.Options();

        /// <summary>
        /// Validates identity, owner, cardinality, uniqueness and membership before invoking legacy Process.
        /// False means no state/history/RNG mutation. Engine execution failures still throw.
        /// As with ChooseTask, the host calls MainBegin after both mulligans finish.
        /// </summary>
        public bool TryAnswer(DecisionAnswer answer, out string error)
        {
            if (Actions.AutonomousTurnRules.IsActive(this)) { error = "Autonomous turn controls input."; return false; }
            bool result = TryAnswerCore(answer,out error,false);
            if (result) Actions.AutonomousTurnRules.Run(this);
            return result;
        }

        internal bool AnswerAutonomous(DecisionAnswer answer, out string error) => TryAnswerCore(answer,out error,true);

        private bool TryAnswerCore(DecisionAnswer answer, out string error, bool autonomous)
        {
            error = null;
            if (answer == null || answer.SchemaVersion != 1)
            {
                error = "Missing answer or unsupported schema version.";
                return false;
            }
            if (State != State.RUNNING)
            {
                error = "No pending decision.";
                return false;
            }
            var owner = answer.ControllerId == Player1.Id ? Player1 : answer.ControllerId == Player2.Id ? Player2 : null;
            if (owner == null)
            {
                error = "Wrong decision controller.";
                return false;
            }
            Choice choice = owner.Choice;
            if (Resolution.HasSuspendedEffect)
            {
                var frame = Resolution.SuspendedEffect;
                if (frame.ControllerId != answer.ControllerId || frame.DecisionId != answer.DecisionId ||
                    answer.SelectedOptionIds.Count != 1 || choice == null || !choice.Choices.Contains(answer.SelectedOptionIds[0]))
                {
                    error = "Stale or invalid suspended effect answer.";
                    return false;
                }
                if (!Resolution.ValidateSelection(this, answer.SelectedOptionIds[0]))
                {
                    error = "Selected entity is no longer eligible.";
                    return false;
                }
                Resolution.ValidateEffects(this);
                ValidateSelectorState();
                Actions.Generic.ValidateDecisionSpell(this);
                _snapshotOperationDepth++;
                try
                {
                    _optionsDecisionId = NextDecisionId;
                    if (!autonomous) PowerHistory?.Last.Clear();
                    owner.NumOptionsPlayedThisTurn++;
                    if (SpellCompletion != null)
                        Actions.Generic.ResumeDecisionSpell(ControllerById(SpellCompletion.ControllerId), answer.SelectedOptionIds[0]);
                    else
                    {
                        Actions.Generic.RestoreDecisionPlayEvent(owner);
                        Resolution.ResumeEffect(this, answer.SelectedOptionIds[0]);
                        Actions.Generic.ResumeDecisionPlay(owner);
                        Actions.LocationRules.Resume(owner);
                        Actions.RepeatRules.ContinueHeroPower(owner);
                    }
                    // A resumed real effect can summon/destroy before opening its next prompt.
                    // Settle that work so the exposed decision is a valid snapshot boundary.
                    // Decision Battlecries retain their phase (including pending deaths) across prompts.
                    if (!Actions.Generic.HasDecisionPlay(owner) && !Actions.Generic.HasDecisionSpell(owner) && (!Resolution.HasSuspendedEffect || !TaskQueue.IsQuiescent ||
                        SummonedMinions.Count != 0 || DeadMinions.Count != 0 || ClearWeapons != null || _rebornMinions.Count != 0))
                    {
                        ProcessTasks();
                        DeathProcessingAndAuraUpdate();
                    }
                }
                finally { _snapshotOperationDepth--; }
                Actions.AutoPlayRules.CompleteTurn(this);
                CheckDeadHeroes();
                SettleTerminalState();
                Actions.ObjectiveRules.Resume(this);
                RefreshHistoryDisplays();
                if (_snapshotOperationDepth == 0)
                {
                    Actions.DrawReplacementRules.DeliverBacklog(Player1);
                    Actions.DrawReplacementRules.DeliverBacklog(Player2);
                    Model.Rewind.RewindRules.TrySettle(this);
                }
                return true;
            }
            bool choosing = choice != null && choice.DecisionId == answer.DecisionId;
            bool options = Player1.Choice == null && Player2.Choice == null && owner == CurrentPlayer &&
                Step == Step.MAIN_ACTION && answer.DecisionId == _optionsDecisionId;
            if (!choosing && !options)
            {
                error = "Stale, unknown or cross-controller decision.";
                return false;
            }
            bool mulligan = choosing && choice.ChoiceType == ChoiceType.MULLIGAN;
            if (choosing && !mulligan && choice.ChoiceType != ChoiceType.GENERAL)
            {
                error = "Unsupported legacy choice type.";
                return false;
            }
            var selected = answer.SelectedOptionIds;
            if (selected.Count < (mulligan ? 0 : 1) || selected.Count > (mulligan ? choice.Choices.Count : 1) ||
                selected.Distinct().Count() != selected.Count)
            {
                error = "Invalid selection count or duplicate selection.";
                return false;
            }
            PlayerTask task;
            if (choosing)
            {
                if (selected.Any(id => !choice.Choices.Contains(id)))
                {
                    error = "Selection was not offered.";
                    return false;
                }
                task = mulligan ? ChooseTask.Mulligan(owner, selected.ToList()) : ChooseTask.Pick(owner, selected[0]);
            }
            else
            {
                var tasks = DecisionTasks();
                if (selected[0] < 0 || selected[0] >= tasks.Count)
                {
                    error = "Option was not offered.";
                    return false;
                }
                // Process already rebinds a foreign task's controller/source/target to this game.
                task = tasks[selected[0]];
                if (task is ActivateOldGodTask)
                    task = new ActivateOldGodTask(owner, (Entities.Minion)IdEntityDic[task.Source.Id]);
                if (task is ActivateLocationTask)
                    task = new ActivateLocationTask(owner, IdEntityDic[task.Source.Id],
                        task.ActionTarget == null ? null : IdEntityDic[task.ActionTarget.Id]);
            }
            if (!(autonomous ? ProcessAutonomous(task) : Process(task))) throw new InvalidOperationException("Legacy engine rejected an offered decision answer.");
            return true;
        }
    }
}
