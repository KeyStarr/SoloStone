using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Snapshots;

namespace SabberStoneCore.Model
{
    public partial class Game
    {
        private int _snapshotOperationDepth;

        internal void RestoreSnapshotCorpses(bool enabled)
        {
            EnableCorpses = enabled;
            _gameConfig.EnableCorpses = enabled;
        }

        /// <summary>
        /// Saves a quiescent legacy decision boundary, including private offers and generated cards.
        /// Unknown callbacks and in-flight work fail explicitly. Diagnostic bus observers are not saved.
        /// The host must serialize access to the game, as for Process and CloneExact.
        /// </summary>
        public byte[] SaveSnapshot()
        {
            // Saving never throws on achievement evidence: inconsistent extra-turn provenance marks this game's evidence
            // invalid (a side effect on the live game) so live Rewind keeps playing; loaders judge the rest.
            RequireSnapshotBoundary(strictEvidence: false);
            return new SnapshotCodec().Save(this);
        }

        /// <summary>
        /// Restores without replaying actions or advancing RNG. Null config uses the saved configuration;
        /// a supplied config must agree on runtime format/history/logging. Construction settings remain saved.
        /// </summary>
        public static Game LoadSnapshot(byte[] data, GameConfig config = null)
            => LoadSnapshotWithPools(data, null, config);

        internal static Game LoadSnapshotWithPools(byte[] data, OfflinePoolManifest pools, GameConfig config = null)
        {
            Game game = new SnapshotCodec().Load(data, pools: pools);
            if (config != null && (config.FormatType != game.FormatType || config.History != game.History || config.Logging != game.Logging || config.EnableCorpses != game.EnableCorpses))
                throw new SerializationException("Snapshot configuration mismatch (format/history/logging/corpses).");
            game.ValidateLoadedSnapshot();
            return game;
        }

        /// <summary>strictEvidence=false only for live Rewind/Sands bytes this process saved: an achievement-evidence
        /// failure marks <see cref="HistoryFacts.EvidenceValid"/> false instead of blocking the decision.</summary>
        internal static Game LoadRewindBaseline(byte[] data, OfflinePoolManifest pools, bool strictEvidence = true)
        {
            var game = new SnapshotCodec().Load(data, pools: pools);
            // Reject nested transactions before validation can recursively decode another baseline.
            if (game.Rewind != null) throw new SerializationException("Rewind baseline contains a transaction.");
            game.ValidateLoadedSnapshot(strictEvidence);
            return game;
        }

        internal void RestoreSnapshotInPlace(byte[] data)
        {
            new SnapshotCodec().Load(data, this);
            ValidateLoadedSnapshot();
        }

        private void ValidateLoadedSnapshot(bool strictEvidence = true)
        {
            var game = this;
            game.RequireSnapshotBoundary(strictEvidence);
            game.Random.RestoreState(game.Random.CaptureState());
            if (game.Game != game || game.Player1.Game != game || game.Player2.Game != game ||
                game.Player1.Id != 2 || game.Player2.Id != 3 || game._nextDecisionId <= game._optionsDecisionId)
                throw new SerializationException("Invalid snapshot game identity/counters.");
            foreach (var pair in game.IdEntityDic)
            {
                if (pair.Key != pair.Value.Id || pair.Value.Game != game || pair.Value.Controller.Game != game)
                    throw new SerializationException("Invalid snapshot entity ownership.");
                try { pair.Value.Card.ValidateHistoryDisplays(); }
                catch (ArgumentException e) { throw new SerializationException("Invalid history display metadata.", e); }
            }
            if (game.HistoryFacts == null) throw new SerializationException("Missing history facts.");
            try { game.HistoryFacts.Validate(game, strictEvidence); }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidOperationException || e is NullReferenceException || e is OverflowException)
            { throw new SerializationException("Invalid history facts snapshot.", e); }
            var baseline = Model.Rewind.RewindRules.Validate(this, strictEvidence);
            // A nested baseline is a prefix of this ledger: its evidence verdict applies to this match too.
            if (baseline != null && !baseline.HistoryFacts.EvidenceValid) HistoryFacts.InvalidateEvidence(baseline.HistoryFacts.EvidenceFailure);
        }

        internal void ValidateSelectorState()
        {
            EffectFrame parent = null;
            foreach (var frame in Resolution.EffectFrames)
            {
                if (parent != null && !Actions.CthunRules.IsAttackContinuation(parent, frame) && !Actions.AfterAttackRules.IsContinuation(parent, frame) && (!(EffectFrameRecipes.Resolve(parent.RecipeKey)[parent.NextStep - 1]
                    is Tasks.SuspendableTask nested) || nested.RecipeKey != frame.RecipeKey ||
                    parent.ControllerId != frame.ControllerId || parent.SourceId != frame.SourceId || parent.TargetId != frame.TargetId))
                    throw new SerializationException("Selector nested recipe does not match parent cursor.");
                parent = frame;
                var steps = EffectFrameRecipes.Resolve(frame.RecipeKey);
                var selectors = steps.Take(frame.NextStep).OfType<SelectEntityThenContinueTask>().Where(s => s.Spec != null).ToArray();
                foreach (var selector in selectors)
                    try { selector.Spec.Validate(); }
                    catch (ArgumentException e) { throw new SerializationException("Invalid selector descriptor.", e); }
                ValidateSelectorHistory(frame, steps);
                if (frame.CapturedCandidateIds == null || frame.CapturedCandidateIds.Distinct().Count() != frame.CapturedCandidateIds.Length ||
                    frame.CapturedCandidateIds.Any(id => !IdEntityDic.ContainsKey(id)) ||
                    frame.SelectedInGroup == null || frame.OfferEntityIds == null ||
                    frame.SelectedInGroup.Distinct().Count() != frame.SelectedInGroup.Length ||
                    frame.OfferEntityIds.Distinct().Count() != frame.OfferEntityIds.Length ||
                    frame.SelectedInGroup.Any(id => !IdEntityDic.ContainsKey(id) || !frame.ChosenEntities.Contains(id)) ||
                    frame.OfferEntityIds.Any(id => !IdEntityDic.ContainsKey(id)) ||
                    frame.SourceId != 0 && frame.SourceId != Id && frame.SourceId != Player1.Id && frame.SourceId != Player2.Id && !IdEntityDic.ContainsKey(frame.SourceId) ||
                    frame.TargetId != 0 && !IdEntityDic.ContainsKey(frame.TargetId) ||
                    frame.SelectionGroupKey != null && !selectors.Any(s => s.Spec.GroupKey == frame.SelectionGroupKey) ||
                    frame.SelectedInGroup.Length != 0 && frame.SelectionGroupKey == null)
                    throw new SerializationException("Invalid selector frame state.");
                if (frame.DecisionId == 0) continue;
                if (steps[frame.NextStep - 1] is SelectEntityThenContinueTask current && current.Spec != null)
                {
                    var controller = ControllerById(frame.ControllerId);
                    var choice = controller.Choice;
                    if (frame.SelectionGroupKey != current.Spec.GroupKey || frame.OfferEntityIds.Length == 0 ||
                        choice.ChoiceType != ChoiceType.TARGET || !frame.OfferEntityIds.SequenceEqual(choice.Choices) ||
                        (current.Spec.Source == SelectorSource.Hand || current.Spec.Source == SelectorSource.CapturedEntities || current.Spec.Source == SelectorSource.HandOrBoard) &&
                            frame.OfferEntityIds.Any(id => IdEntityDic[id].Controller != controller) ||
                        current.Spec.Source == SelectorSource.CapturedEntities && frame.OfferEntityIds.Any(id => !frame.CapturedCandidateIds.Contains(id)) ||
                        current.Spec.ExcludePlayTarget && frame.OfferEntityIds.Contains(frame.TargetId) ||
                        current.Spec.ExcludeEarlierPicks && frame.OfferEntityIds.Intersect(frame.SelectedInGroup).Any())
                        throw new SerializationException("Selector frozen offers do not match pending choice.");
                }
                else if (frame.OfferEntityIds.Length != 0)
                    throw new SerializationException("Unexpected selector offers.");
            }
        }

        private void ValidateSelectorHistory(EffectFrame frame, Tasks.ISimpleTask[] steps)
        {
            var executed = Enumerable.Range(0, frame.NextStep)
                .Where(i => steps[i] is SelectEntityThenContinueTask s && s.Spec != null).ToArray();
            if (frame.SelectorHistory == null || frame.ChosenEntities == null ||
                frame.SelectorHistory.Any(p => p == null) ||
                !frame.SelectorHistory.Select(p => p.StepIndex).SequenceEqual(executed))
                throw new SerializationException("Selector history does not match recipe cursor.");
            string group = null;
            var selected = new List<int>();
            int previousChosen = -1;
            var controller = ControllerById(frame.ControllerId);
            foreach (var pick in frame.SelectorHistory)
            {
                var spec = ((SelectEntityThenContinueTask)steps[pick.StepIndex]).Spec;
                if (group != spec.GroupKey) { group = spec.GroupKey; selected.Clear(); }
                bool pending = frame.DecisionId != 0 && pick.StepIndex == frame.NextStep - 1;
                if (pick.EntityId == 0)
                {
                    if (pick.ChosenIndex != -1 || pick.ControllerId != 0 || pick.Zone != 0 || pick.CardType != 0)
                        throw new SerializationException("Invalid empty selector history.");
                    continue;
                }
                // Continuations may move, transform or take control of a pick; validate its saved origin.
                if (pending || pick.ChosenIndex <= previousChosen || pick.ChosenIndex >= frame.ChosenEntities.Length ||
                    frame.ChosenEntities[pick.ChosenIndex] != pick.EntityId || !IdEntityDic.ContainsKey(pick.EntityId) ||
                    pick.ControllerId != controller.Id && pick.ControllerId != controller.Opponent.Id ||
                    (spec.Source == SelectorSource.Hand || spec.Source == SelectorSource.CapturedEntities) && (pick.ControllerId != controller.Id || pick.Zone != Enums.Zone.HAND) ||
                    spec.Source == SelectorSource.HandOrBoard && pick.Zone != Enums.Zone.PLAY &&
                        (pick.Zone != Enums.Zone.HAND || pick.ControllerId != controller.Id || pick.CardType != CardType.MINION) ||
                    spec.IsBoard && !(spec.Source == SelectorSource.HandOrBoard && pick.Zone == Enums.Zone.HAND) && (pick.Zone != Enums.Zone.PLAY ||
                        pick.CardType != CardType.MINION && !(spec.IncludeHeroes && pick.CardType == CardType.HERO) ||
                        spec.Side == SelectorSide.Friendly && pick.ControllerId != controller.Id ||
                        spec.Side == SelectorSide.Enemy && pick.ControllerId != controller.Opponent.Id) ||
                    spec.ExcludePlayTarget && pick.EntityId == frame.TargetId ||
                    spec.ExcludeEarlierPicks && selected.Contains(pick.EntityId))
                    throw new SerializationException("Invalid completed selector pick.");
                previousChosen = pick.ChosenIndex;
                if (group != null && !selected.Contains(pick.EntityId)) selected.Add(pick.EntityId);
            }
            if (frame.SelectionGroupKey != group || frame.SelectedInGroup == null || !selected.SequenceEqual(frame.SelectedInGroup))
                throw new SerializationException("Selector group history does not match completed picks.");
        }

        internal bool IsSnapshotBoundary => SnapshotBoundaryError == null;

        private string SnapshotBoundaryError
        {
            get
            {
                bool decisionBattlecry = Actions.Generic.HasDecisionPlay(Player1) || Actions.Generic.HasDecisionPlay(Player2) || SpellCompletion != null;
                if (!Resolution.IsQuiescent || _snapshotOperationDepth != 0 || !TaskQueue.IsQuiescent ||
                    (!decisionBattlecry && (DeadMinions.Count != 0 || SummonedMinions.Count != 0)) ||
                    DeadBoardObjects.Count != 0 || (ClearWeapons != null && SpellCompletion == null) || _rebornMinions.Count != 0)
                    return "Unsupported snapshot: in-flight legacy work is not quiescent.";
                if (State != State.RUNNING || (Player1.Choice == null && Player2.Choice == null && Step != Step.MAIN_ACTION))
                    return "Snapshot requires a running decision boundary.";
                return null;
            }
        }

        private void RequireSnapshotBoundary(bool strictEvidence = true)
        {
            Resolution.RequireQuiescent();
            Resolution.ValidateEffects(this);
            ValidateSelectorState();
            Actions.Generic.ValidateDecisionPlay(this);
            Actions.Generic.ValidateDecisionSpell(this);
            Actions.TurnQueueRules.Validate(this, strictEvidence);
            if (ClearWeapons != null && ClearWeapons.Any(id => !IdEntityDic.TryGetValue(id, out var hero) || !(hero is Entities.Hero)))
                throw new SerializationException("Invalid pending weapon removal.");
            string boundaryError = SnapshotBoundaryError;
            if (boundaryError != null) throw new NotSupportedException(boundaryError);
        }
    }
}
