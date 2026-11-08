namespace SabberStoneCore.Model
{
    /// <summary>Modern-only state. Progress and total remain authoritative on the spell's tags.</summary>
    public sealed class QuestState
    {
        public int CompletionGeneration { get; internal set; }
        public int RewardedGeneration { get; internal set; } = -1;
        public int LastCountedTurn { get; set; } = -1;
        public int Stage { get; set; }
        public bool Completing { get; internal set; }

        public QuestState Clone() => (QuestState)MemberwiseClone();
    }
}
