using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class EitherSideRules
    {
        public static bool Supports(Card card) => card[(GameTag)4690] == 1;
        public static Controller Destination(Controller player, int branch) => branch == 2 ? player.Opponent : player;
        public static bool Valid(Controller player, IPlayable source, IPlayable target, int position, int branch)
        {
            if (source.Controller != player || target != null || branch < 1 || branch > 2) return false;
            var board = Destination(player, branch).BoardZone;
            return !board.IsFull && position >= 0 && position <= board.SlotCount;
        }
        internal static void Place(Controller player, Minion minion, int position, int branch)
        {
            var destination = Destination(player, branch);
            if (minion.Controller != destination)
            {
                minion.Controller = destination;
                minion[GameTag.CONTROLLER] = destination.PlayerId;
            }
            destination.BoardZone.Add(minion, position, applyEntrySetters: true);
        }
        internal static void SyncController(Minion minion)
        {
            if (!Supports(minion.Card)) return;
            var destination = minion.Controller;
            if (minion.ChooseOnePlayables != null)
                foreach (var option in minion.ChooseOnePlayables)
                {
                    if (option.Controller == destination && option.Zone == destination.SetasideZone
                        && option[Generic.PrivateDecisionOfferTag] == destination.PlayerId) continue;
                    option.Zone?.Remove(option);
                    option.Controller = destination;
                    option[GameTag.CONTROLLER] = destination.PlayerId;
                    option[Generic.PrivateDecisionOfferTag] = destination.PlayerId;
                    destination.SetasideZone.Add(option);
                }
            if (minion.AppliedEnchantments != null)
                foreach (var enchantment in minion.AppliedEnchantments)
                { enchantment.Controller = destination; enchantment[GameTag.CONTROLLER] = destination.PlayerId; }
        }
    }
}
