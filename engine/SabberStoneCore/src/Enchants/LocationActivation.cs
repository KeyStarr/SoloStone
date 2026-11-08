using SabberStoneCore.Tasks;
namespace SabberStoneCore.Enchants
{
    public enum LocationTargetKind { None, Minion, EnemyLocation, Character, Location }
    public sealed class LocationActivation
    {
        public ISimpleTask Task;
        public LocationTargetKind Target;
        public bool RequiresFreeSlot;
        public System.Func<SabberStoneCore.Model.Entities.Controller, SabberStoneCore.Model.Entities.IPlayable, bool> TargetCondition;
    }
}
