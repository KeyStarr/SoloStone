using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Counts hand plays while the experimental Kael'thas is on the board.</summary>
    public static class RepentantPrinceRules
    {
        public const string CardId = "SOLO_KAELTHAS";
        private const GameTag PlayCount = GameTag.TAG_SCRIPT_DATA_NUM_1;

        private static bool Active(Minion minion) =>
            minion.Card.Id == CardId && !minion.IsSilenced && !minion.ToBeDestroyed;

        internal static bool ChangesPayment(Controller owner) => owner.Game.EnableCorpses
            && owner.BoardZone.AnyInteractable(minion => Active(minion) && minion[PlayCount] == 2);

        internal static void HandCardPlayed(Controller owner)
        {
            if (!owner.BoardZone.AnyInteractable(Active)) return;
            foreach (var minion in owner.BoardZone.Interactable)
                if (Active(minion)) minion[PlayCount] = (minion[PlayCount] + 1) % 3;
        }

        internal static void Reset(Minion minion)
        {
            if (minion.Card.Id == CardId) minion[PlayCount] = 0;
        }
    }
}
