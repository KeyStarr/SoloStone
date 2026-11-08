namespace SabberStoneCore.Model.Rewind
{
    public enum RewindStage { Attempt, Offered }

    /// <summary>Only action values and immutable snapshot bytes; never references into an attempt's graph.</summary>
    public sealed class RewindTransaction
    {
        public RewindStage Stage { get; internal set; }
        public byte[] Baseline { get; internal set; }
        public int ControllerId { get; internal set; }
        public int SourceId { get; internal set; }
        public int TargetId { get; internal set; }
        public int ZonePosition { get; internal set; }
        public int ChooseOne { get; internal set; }
        public string SourceCardId { get; internal set; }
        public int Budget { get; internal set; }
        public int Used { get; internal set; }
        public bool IsHeroPowerActivation { get; internal set; }
        public bool KeepBoth { get; internal set; }
        public long OfferChoiceId { get; internal set; }
        public int RewindEntityId { get; internal set; }
        public int KeepEntityId { get; internal set; }

        internal RewindTransaction Clone() => (RewindTransaction)MemberwiseClone();
    }
}
