using System;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Model
{
    public partial class Game
    {
        public RewindTransaction Rewind { get; internal set; }
        public bool RewindDisabled { get; private set; }

        internal void BeginRewindAnswer()
        {
            _optionsDecisionId = NextDecisionId;
            if (!Actions.AutonomousTurnRules.IsActive(this)) PowerHistory?.Last.Clear();
        }

        /// <summary>validated: the same bytes already decoded and validated (RewindRules.Answer); null decodes and validates
        /// a detached copy first. Either way an invalid baseline throws before `this` is touched; the in-place load itself has
        /// no rollback (only ID counters are re-maxed). Restores in place,
        /// then keeps counters monotonic and the match's evidence verdict sticky.</summary>
        internal void RestoreRewindBaseline(byte[] baseline, Game validated = null)
        {
            validated = validated ?? LoadRewindBaseline(baseline, OfflinePools, strictEvidence: false);
            long next = _nextDecisionId, options = _optionsDecisionId;
            int entity = _idIndex, oop = _oopIndex;
            string evidence = HistoryFacts.EvidenceFailure ?? validated.HistoryFacts.EvidenceFailure;
            try
            {
                // Bind carried services before decoding delegate targets, never swap them after hydration.
                new Snapshots.SnapshotCodec(this).Load(baseline, this);
                ValidateLoadedSnapshot(strictEvidence: false);
            }
            finally
            {
                // Even an unexpected failure must not reuse decision or entity IDs already sent to the client.
                _nextDecisionId = Math.Max(_nextDecisionId, next);
                _optionsDecisionId = Math.Max(_optionsDecisionId, options);
                _idIndex = Math.Max(_idIndex, entity);
                _oopIndex = Math.Max(_oopIndex, oop);
            }
            if (evidence != null) HistoryFacts.InvalidateEvidence(evidence);
            else if (!HistoryFacts.EvidenceValid) InvalidateHistoryEvidence(HistoryFacts.EvidenceFailure, alreadyMarked: true);
        }

        /// <summary>Marks this match ineligible for achievements and logs one warning; gameplay is unaffected.</summary>
        internal void InvalidateHistoryEvidence(string reason, bool alreadyMarked = false)
        {
            if (HistoryFacts.InvalidateEvidence(reason) || alreadyMarked)
                Log(LogLevel.WARNING, Enums.BlockType.PLAY, "HistoryFacts",
                    "Achievement evidence invalidated; match is ineligible for achievements: " + HistoryFacts.EvidenceFailure);
        }

        internal bool ReplayRewind(PlayerTask task) => Process(task, preserveHistory: true);
    }
}
