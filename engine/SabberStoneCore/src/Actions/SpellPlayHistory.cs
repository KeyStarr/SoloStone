using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class SpellPlayHistory
    {
        // PlayCard records paid plays before countering; automatic casts and Prepare never enter this list.
        public static bool PlayedSchoolThisTurn(Controller controller, int school) =>
            controller.CardsPlayedThisTurn.Any(card => card.Type == CardType.SPELL && card[GameTag.SPELL_SCHOOL] == school);
    }
}
