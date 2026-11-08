using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArrivalRules
    {
        internal static bool BlocksPlay(IPlayable card)
        {
            if (card.Zone != card.Controller.HandZone) return false;
            return card.Controller.AppliedEnchantments?.Any(e =>
                e.Card.Id == "BE_EVENT_100e" && card.Card.Type != CardType.MINION
                || e.Card.Id == "BE_EVENT_101e" && card.Card.Type != CardType.SPELL) == true;
        }

        internal static void Resolve(Game game, Controller player, IPlayable source, CardType type, int refresh, string watch)
        {
            var context = new EffectContext(game, player.Id, source.Id);
            for (int i = 0; i < 2; i++) context.DrawWhere(card => card.Card.Type == type);
            // Same convention as every other Refresh: lower RESOURCES_USED; never unlocks Overload or adds crystals.
            context.RefreshMana(refresh);
            Enchantment.GetInstance(player, source, player, Cards.FromId(watch));
        }
    }
}
