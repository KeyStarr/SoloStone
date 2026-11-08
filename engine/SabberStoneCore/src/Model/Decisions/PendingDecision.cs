using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Model.Decisions
{
    // Numeric values are part of schema version 1.
    public enum DecisionKind { Mulligan = 0, GeneralChoice = 1, Options = 2, ChooseOne = 3, Target = 4, SuspendedEffect = 5 }

    /// <summary>An ordered, atomic legacy option. Branch and target are committed together.</summary>
    public sealed class DecisionOption
    {
        public int OptionId { get; private set; }
        public DecisionKind Kind { get; private set; }
        public int EntityId { get; private set; }
        public int TargetId { get; private set; }
        public int BranchIndex { get; private set; }
        public int ZonePosition { get; private set; }
        public PlayerTaskType TaskType { get; private set; }

        internal DecisionOption(int optionId, DecisionKind kind, int entityId, int targetId = 0,
            int branchIndex = 0, int zonePosition = -1, PlayerTaskType taskType = PlayerTaskType.CHOOSE)
        {
            OptionId = optionId;
            Kind = kind;
            EntityId = entityId;
            TargetId = targetId;
            BranchIndex = branchIndex;
            ZonePosition = zonePosition;
            TaskType = taskType;
        }
    }

    /// <summary>
    /// Immutable authoritative view, including private offers. Hosts must filter by ControllerId.
    /// This envelope is not a saved game or a serialized effect continuation.
    /// </summary>
    public sealed class PendingDecision
    {
        public int SchemaVersion { get; private set; }
        public long Id { get; private set; }
        public int ControllerId { get; private set; }
        public int SourceId { get; private set; }
        public DecisionKind Kind { get; private set; }
        public int MinSelections { get; private set; }
        public int MaxSelections { get; private set; }
        private DecisionOption[] _options;
        /// <summary>Choice option IDs are entity IDs; action option IDs index Controller.Options().</summary>
        public IReadOnlyList<DecisionOption> Options => Array.AsReadOnly(_options);
        /// <summary>Mulligan selections are cards to KEEP, including the empty keep list.</summary>
        public bool SelectionsAreKeepList => Kind == DecisionKind.Mulligan;

        internal PendingDecision(long id, int controllerId, int sourceId, DecisionKind kind,
            int minSelections, int maxSelections, IEnumerable<DecisionOption> options)
        {
            SchemaVersion = 1;
            Id = id;
            ControllerId = controllerId;
            SourceId = sourceId;
            Kind = kind;
            MinSelections = minSelections;
            MaxSelections = maxSelections;
            _options = options.ToArray();
        }

        public string ToJson() => DecisionJson.Write(this);
        public static PendingDecision FromJson(string json)
        {
            var view = DecisionJson.Read<PendingDecision>(json);
            if (view.SchemaVersion != 1 || view.Id <= 0 || view.ControllerId <= 0 ||
                !Enum.IsDefined(typeof(DecisionKind), view.Kind) || view._options == null ||
                view.MinSelections < 0 || view.MaxSelections < view.MinSelections ||
                view.MaxSelections > view._options.Length || view._options.Any(o => o == null ||
                    !Enum.IsDefined(typeof(DecisionKind), o.Kind) || !Enum.IsDefined(typeof(PlayerTaskType), o.TaskType)) ||
                view._options.Select(o => o.OptionId).Distinct().Count() != view._options.Length)
                throw new SerializationException("Invalid pending decision or unsupported schema version.");
            return view;
        }
    }

    /// <summary>An untrusted answer; Game.TryAnswer validates it against current authoritative offers.</summary>
    public sealed class DecisionAnswer
    {
        public int SchemaVersion { get; private set; }
        public long DecisionId { get; private set; }
        public int ControllerId { get; private set; }
        private int[] _selectedOptionIds;
        public IReadOnlyList<int> SelectedOptionIds => Array.AsReadOnly(_selectedOptionIds);

        public DecisionAnswer(long decisionId, int controllerId, IEnumerable<int> selectedOptionIds)
        {
            SchemaVersion = 1;
            DecisionId = decisionId;
            ControllerId = controllerId;
            _selectedOptionIds = (selectedOptionIds ?? throw new ArgumentNullException(nameof(selectedOptionIds))).ToArray();
        }

        public string ToJson() => DecisionJson.Write(this);
        public static DecisionAnswer FromJson(string json)
        {
            var answer = DecisionJson.Read<DecisionAnswer>(json);
            if (answer.SchemaVersion != 1 || answer._selectedOptionIds == null)
                throw new SerializationException("Invalid decision answer or unsupported schema version.");
            return answer;
        }
    }

}
