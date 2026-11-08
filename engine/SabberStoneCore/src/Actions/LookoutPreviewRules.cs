using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class LookoutPreviewRules
    {
        public static void HandExit(IPlayable card)
        {
            if (card.Card.Id != "EDR_950") return;
            // Clear before a public play/discard snapshot can reveal the owner's deck top.
            card[GameTag.TAG_SCRIPT_DATA_NUM_6] = 0;
            card[(GameTag)955] = 0;
        }
    }
}
