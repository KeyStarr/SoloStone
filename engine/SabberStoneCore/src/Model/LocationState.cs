namespace SabberStoneCore.Model
{
    public sealed class LocationState
    {
        public int UsesCompleted;
        public int Stage;
        public bool ConditionLocked;
        public LocationState Clone() => (LocationState)MemberwiseClone();
    }
}
