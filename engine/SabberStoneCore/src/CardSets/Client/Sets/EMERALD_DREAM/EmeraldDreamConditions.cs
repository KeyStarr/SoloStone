using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmeraldDreamConditions
    {
        internal static bool HoldingDragon(IPlayable source) => source.Controller.HandZone
            .Any(p => p.Id != source.Id && p is ICharacter character && character.IsRace(Race.DRAGON));

        internal static bool HoldingExpensiveSpell(IPlayable source) => HoldingExpensiveSpell(source.Controller, source.Id);

        // Target availability receives a definition, not an entity; its minion source cannot be a spell.
        internal static bool HoldingExpensiveSpell(Controller controller, int sourceId = 0) => controller.HandZone
            .Any(p => p.Id != sourceId && p.Card.Type == CardType.SPELL && p.Cost >= 5);

        internal static bool HoldingDarkGift(IPlayable source) => source.Controller.HandZone
            .Any(p => p.Id != source.Id && p is Minion && DarkGifts.Has(p));
    }
}
