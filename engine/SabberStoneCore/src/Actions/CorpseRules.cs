using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Opt-in cumulative player counters; no separate balance or ledger.</summary>
    public static class CorpseRules
    {
        // Pinned RLK_008t/RLK_061t restriction; the client supplies no trustworthy enum name.
        private const GameTag NoCorpse = (GameTag)2217;
        /// <summary>Hero tag PLAYER_DEATHRATTLE: entity id of the card the client shows beside the hero's Deathrattle skull.</summary>
        internal const GameTag PlayerDeathrattle = (GameTag)4398;
        public static int Available(Controller controller) =>
            Math.Max(0, checked(controller[GameTag.CORPSES] - controller[GameTag.CORPSES_SPENT_THIS_GAME]));

        public static void Gain(Controller controller, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !controller.Game.EnableCorpses) return;
            // Dying sources are excluded for the entire batch, including before their removal.
            if (controller.BoardZone.Any(m => m.Card.Id == "CORE_EDR_003" && !m.IsSilenced && !m.Untouchable && !m.ToBeDestroyed))
                amount = checked(amount * 2);
            controller[GameTag.CORPSES] = checked(controller[GameTag.CORPSES] + amount);
        }

        public static bool TrySpend(Controller controller, int amount,IEntity source=null)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;
            if (!controller.Game.EnableCorpses || Available(controller) < amount) return false;
            controller[GameTag.CORPSES_SPENT_THIS_GAME] = checked(controller[GameTag.CORPSES_SPENT_THIS_GAME] + amount);
            controller.Game.Facts.CorpsesSpent(controller,amount,source);
            QuestRules.CorpsesSpent(controller, amount);
            return true;
        }

        public static int SpendUpTo(Controller controller, int limit)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            int amount = Math.Min(Available(controller), limit);
            return TrySpend(controller, amount) ? amount : 0;
        }

        /// <summary>The skull follows the player-level grant, so a replacement hero takes it over from the old one.</summary>
        internal static void CarryPlayerDeathrattle(Hero oldHero, Hero newHero)
        {
            if (oldHero == null || oldHero == newHero) return;
            int linked = oldHero[PlayerDeathrattle];
            if (linked == 0) return;
            oldHero[PlayerDeathrattle] = 0;
            newHero[PlayerDeathrattle] = linked;
        }

        internal static void ResurrectHeroes(Model.Game game)
        {
            if (game.State == State.COMPLETE) return;
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                if (!owner.Hero.ToBeDestroyed || owner.PlayState == PlayState.CONCEDED) continue;
                var grant = owner.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "TIME_618e");
                if (grant == null) continue;
                grant.Remove();
                owner.Hero[PlayerDeathrattle] = 0;
                int paid = SpendUpTo(owner, 20);
                if (paid == 0) continue;
                owner.Hero.ToBeDestroyed = false;
                owner.Hero.Health = paid;
                owner.PlayState = PlayState.PLAYING;
                Enchantment.GetInstance(owner, grant.Creator, owner.Hero, Model.Cards.FromId("TIME_618e2"));
            }
        }

        /// <summary>Called once by death creation, before graveyard cleanup and queued reactions.</summary>
        public static void RecordDeath(Minion minion)
        {
            if (minion.Game.EnableCorpses && minion[NoCorpse] == 0) Gain(minion.Controller, 1);
        }
    }
}
