using SabberStoneCore.Model.Entities;
namespace SabberStoneCore.Model.Events
{
    public readonly record struct LastManaSpentEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(LastManaSpentEvent);
        public LastManaSpentEvent(IEntity sender) => Sender=sender;
    }
}
