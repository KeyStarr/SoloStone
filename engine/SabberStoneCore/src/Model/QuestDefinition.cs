using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    public enum QuestRewardKind { CardToHand, PlayerEffect }

    /// <summary>Immutable card-module recipe. Unregistered quests use the legacy path.</summary>
    public sealed class QuestDefinition
    {
        private static readonly Dictionary<int, QuestDefinition> Definitions = new Dictionary<int, QuestDefinition>();
        public Action<Spell, HandCountFact> HandCountChanged { get; }
        public int DbfId { get; }
        public int RequiredProgress { get; }
        public bool Repeatable { get; }
        public QuestRewardKind RewardKind { get; }
        public string RewardCardId { get; }
        public bool CountsFullBoardTurns { get; }

        public QuestDefinition(int dbfId, int requiredProgress, QuestRewardKind rewardKind,
            string rewardCardId, bool repeatable = false, bool countsFullBoardTurns = false,
            Action<Spell, HandCountFact> handCountChanged = null)
        {
            if (dbfId <= 0 || requiredProgress <= 0) throw new ArgumentOutOfRangeException();
            if (string.IsNullOrEmpty(rewardCardId)) throw new ArgumentException(nameof(rewardCardId));
            if (!Enum.IsDefined(typeof(QuestRewardKind), rewardKind)) throw new ArgumentOutOfRangeException(nameof(rewardKind));
            DbfId = dbfId;
            RequiredProgress = requiredProgress;
            RewardKind = rewardKind;
            RewardCardId = rewardCardId;
            Repeatable = repeatable;
            CountsFullBoardTurns = countsFullBoardTurns;
            HandCountChanged = handCountChanged;
        }

        public static void Register(QuestDefinition definition) => Definitions.Add(definition.DbfId, definition);
        public static bool TryGet(int dbfId, out QuestDefinition definition) => Definitions.TryGetValue(dbfId, out definition);
        internal static void Unregister(int dbfId) => Definitions.Remove(dbfId);
    }
}
