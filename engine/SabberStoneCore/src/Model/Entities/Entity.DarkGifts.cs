using SabberStoneCore.Enums;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCore.Model.Entities
{
    public partial class Entity
    {
        private IZone _zone;
        // Remove/add are separate calls; remember the origin even with history off or between zones.
        private Zone _darkGiftPreviousZone;
        internal bool PreserveDarkGiftsOnNextBackwardMove { get; set; }
        // Game-only replay restriction uses the entity clone/snapshot path, without changing canonical tags.
        internal bool UnversionedDarkGiftReplay { get; set; }

        private void SetZoneWithDarkGifts(IZone destination)
        {
            if (destination != null)
            {
                var from = _zone?.Type ?? _darkGiftPreviousZone;
                // Both ordinary and mixed location/minion deaths pass through this zone boundary.
                if (destination.Type == Enums.Zone.GRAVEYARD && from == Enums.Zone.PLAY && this is Minion minion)
                {
                    var rebirth = DarkGifts.CaptureRebirth(minion);
                    if (rebirth != null) Game.DarkGiftRebirths[Id] = rebirth;
                }
                if (this is IPlayable playable && DarkGifts.Has(playable) &&
                    DarkGifts.RemoveForBackwardMove(playable, from, destination.Type, PreserveDarkGiftsOnNextBackwardMove))
                    PreserveDarkGiftsOnNextBackwardMove = false;
                _darkGiftPreviousZone = destination.Type;
            }
            _zone = destination;
        }
    }
}
