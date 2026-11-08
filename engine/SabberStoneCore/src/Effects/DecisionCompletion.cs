namespace SabberStoneCore.Model.Effects
{
    internal enum DecisionCompletionStage { Recipe, CastTail, PlayTail, CardTail }

    /// <summary>Value-only hand-play completion; no caller stack survives a player decision.</summary>
    internal sealed class DecisionCompletion
    {
        internal int ControllerId, SourceId, TargetId, DefinitionDbfId, ChooseOne;
        internal bool Echo;
        internal bool ThroughPower;
        internal (int EntityId, int ResidenceGeneration)[] HandProgress;
        internal DecisionCompletionStage Stage;
        internal DecisionCompletion Clone()
        {
            var copy = (DecisionCompletion)MemberwiseClone();
            copy.HandProgress = HandProgress == null ? null : ((int, int)[])HandProgress.Clone();
            return copy;
        }
    }
}

namespace SabberStoneCore.Model
{
    public partial class Game
    {
        internal Effects.DecisionCompletion SpellCompletion;
    }
}
