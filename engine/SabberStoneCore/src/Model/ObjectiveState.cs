using SabberStoneCore.Enums;
namespace SabberStoneCore.Model.Objectives
{
    // Keep constants outside the legacy namespace-discovered snapshot type table.
    public static class ObjectiveState
    {
        public const GameTag LastTick = (GameTag)32900;
        public const GameTag CompletedRituals = (GameTag)32901;
        internal const GameTag PendingStart = (GameTag)32902;
    }
}
