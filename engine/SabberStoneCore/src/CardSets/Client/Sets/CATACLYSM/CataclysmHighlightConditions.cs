using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CataclysmHighlightConditions
    {
        internal static bool HoldingAnotherDragon(IPlayable source) => source.Controller.HandZone
            .Any(card => card.Id != source.Id && card.Card.IsRace(Race.DRAGON));
    }
}
