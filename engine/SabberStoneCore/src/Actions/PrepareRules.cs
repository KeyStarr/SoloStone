using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class PrepareRules
    {
        // Keep legacy CANT_PLAY handling unchanged outside this family.
        internal static bool BlocksPlay(IPlayable source) => source[GameTag.PREPARING] != 0
            || (source[GameTag.PREPARED] != 0 && source[GameTag.CANT_PLAY] != 0);

        public static int ManaToSpend(IPlayable source) => source == null || source.Cost < 1 ? 0
            : Math.Min(source.Controller.RemainingMana, Math.Max(1, source.Cost - 1));

        public static void Grant(IPlayable target)
        {
            if (target[GameTag.PREPARE] != 0) return;
            Enchantment.GetInstance(target.Controller, target, target, Cards.FromId("CAP_407e"));
            target[GameTag.PREPARE] = 1;
            target[GameTag.DECK_ACTION_COST] = 1;
        }

        internal static void Prepare(IPlayable source, int discount)
        {
            var enchant = Cards.FromId("JAIL_907e");
            Enchantment.GetInstance(source.Controller, source, source, enchant, discount);
            enchant.Power.Enchant.ActivateTo(source, discount);
            Enchantment.GetInstance(source.Controller, source, source, Cards.FromId("JAIL_907e02"), source.Game.Turn);
            source[GameTag.PREPARED] = 1;
            source[GameTag.PREPARING] = 1;
            PublishRestriction(source);
        }

        internal static void CopyState(IPlayable source, IPlayable copy)
        {
            if (source[GameTag.PREPARE] == 0 && source[GameTag.PREPARED] == 0) return;
            foreach (var tag in new[] { GameTag.PREPARE, GameTag.DECK_ACTION_COST, GameTag.PREPARED, GameTag.PREPARING })
                copy[tag] = source[tag];
            PublishRestriction(copy);
        }

        internal static void Reset(IPlayable source)
        {
            if (source[GameTag.PREPARED] == 0) return;
            source[GameTag.PREPARED] = 0;
            foreach (var enchant in source.AppliedEnchantments?.Where(e => e.Card.Id == "JAIL_907e" || e.Card.Id == "JAIL_907e02").ToArray()
                ?? Array.Empty<Enchantment>()) enchant.Remove();
        }

        internal static void Removing(Enchantment enchant)
        {
            if (enchant.Card.Id == "JAIL_907e02")
            {
                enchant.Target[GameTag.PREPARING] = 0;
                PublishRestriction((IPlayable)enchant.Target);
            }
            else if (enchant.Card.Id == "CAP_407e")
            {
                enchant.Target[GameTag.PREPARE] = enchant.Target.Card[GameTag.PREPARE];
                enchant.Target[GameTag.DECK_ACTION_COST] = enchant.Target.Card[GameTag.DECK_ACTION_COST];
            }
        }

        private static void PublishRestriction(IPlayable source)
        {
            if (source.Game.History)
                source.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.CANT_PLAY, source[GameTag.CANT_PLAY]));
        }
    }
}
