using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeTravelHandConditions
    {
        internal static bool HoldingAnotherDragon(IPlayable source) =>
            source.Controller.HandZone.Any(other => other.Id != source.Id && other.Card.IsRace(Race.DRAGON));
    }
}
