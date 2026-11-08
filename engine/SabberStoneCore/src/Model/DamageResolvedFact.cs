using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    /// <summary>Committed damage, including armor and overkill, before damage reactions run.</summary>
    public readonly struct DamageResolvedFact
    {
        public int SourceControllerId { get; }
        public int SourceDbfId { get; }
        public int TargetId { get; }
        public int TargetControllerId { get; }
        public int TurnControllerId { get; }
        public int Amount { get; }

        public DamageResolvedFact(IPlayable source, Character target, int amount)
        {
            SourceControllerId = source.Controller.Id;
            SourceDbfId = source.Card.AssetId;
            TargetId = target.Id;
            TargetControllerId = target.Controller.Id;
            TurnControllerId = target.Game.CurrentPlayer.Id;
            Amount = amount;
        }
    }
}
