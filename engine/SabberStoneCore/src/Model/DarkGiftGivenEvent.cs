using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Model
{
    /// <summary>Original grant fact on the existing event bus. Legacy callbacks read the gift spell,
    /// recipient and spell dbfId from CurrentEventData; attachment copies never raise this event.
    /// Power.Trigger binds DARK_GIFT_GIVEN and resolves immediately: task source is its owner,
    /// task target is the recipient. HAND_OR_DECK activation supports Wallow.</summary>
    public readonly record struct DarkGiftGivenEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public int ControllerId { get; }
        public int TargetEntityId { get; }
        public string GiftId { get; }
        public string Channel => "DarkGiftGiven";
        public DarkGiftGivenEvent(IPlayable target, string giftId)
        { Sender = target; ControllerId = target.Controller.Id; TargetEntityId = target.Id; GiftId = giftId; }
    }
}
