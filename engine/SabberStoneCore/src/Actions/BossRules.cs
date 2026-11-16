using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Rules an adventure boss brings with its hero (Dungeon Run), registered by hero card: a snapshot or clone keeps them with
    // the hero, and no other game is touched (both tables stay empty unless such a hero card is defined).
    public static class BossRules
    {
        private static readonly Dictionary<int, int> HoldUntilTurn = new Dictionary<int, int>();
        private static readonly HashSet<int> NoDraws = new HashSet<int>();
        private static readonly HashSet<int> StandingTriggers = new HashSet<int>();
        private static readonly Dictionary<int, string> Partners = new Dictionary<int, string>();

        // Tag-team partners (George and Karl), on the controller: the partner off the board keeps his Damage and Armor here,
        // and PartnerDown is set once he has been defeated. Private: never sent to the client.
        private const GameTag PartnerDamage = (GameTag)34700, PartnerArmor = (GameTag)34701, PartnerDown = (GameTag)34702;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 34700 && (int)tag <= 34702;

        // The hero plays no cards before its Nth turn (A. F. Kay: none before her sixth; Tad: never).
        internal static void HoldCardsUntilTurn(Card hero, int turn) => HoldUntilTurn[hero.AssetId] = turn;

        // The hero draws nothing: no opening hand, no draw at the start of its turns (Tad, Treasure Vault).
        internal static void MarkDrawsNothing(Card hero) => NoDraws.Add(hero.AssetId);

        // A usable Hero Power whose trigger is live from the start of the game too (Tad's Catch and Release notes damage to
        // him); passive powers get theirs anyway.
        internal static void MarkStandingTrigger(Card heroPower) => StandingTriggers.Add(heroPower.AssetId);
        public static bool HasStandingTrigger(Card heroPower) => StandingTriggers.Count != 0 && StandingTriggers.Contains(heroPower.AssetId);

        // The hero has a partner who takes over when he is defeated, and with whom a Tag Team swaps him.
        internal static void MarkPartner(Card hero, string partnerHeroId) => Partners[hero.AssetId] = partnerHeroId;

        private static string PartnerOf(Controller c) =>
            Partners.Count != 0 && c.Hero != null && c[PartnerDown] == 0 && Partners.TryGetValue(c.Hero.Card.AssetId, out string id) ? id : null;

        public static bool CanSwapPartner(Controller c) => PartnerOf(c) != null;

        // Tag Team: the partner comes in with the Damage and Armor he had; the hero going out keeps his for later.
        internal static void SwapPartner(Controller c)
        {
            string partner = PartnerOf(c);
            if (partner == null) return;
            int damage = c[PartnerDamage], armor = c[PartnerArmor];
            c[PartnerDamage] = c.Hero.Damage;
            c[PartnerArmor] = c.Hero.Armor;
            BringIn(c, partner, damage, armor);
        }

        // A defeated hero with a partner still standing: the partner takes over the board (CheckDeadHeroes, before the game
        // can end), once.
        internal static void TagInPartners(Game game)
        {
            if (Partners.Count == 0 || game.State == State.COMPLETE) return;
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                if (!owner.Hero.ToBeDestroyed || owner.PlayState == PlayState.CONCEDED) continue;
                string partner = PartnerOf(owner);
                if (partner == null) continue;
                owner[PartnerDown] = 1;
                BringIn(owner, partner, owner[PartnerDamage], owner[PartnerArmor]);
                owner.PlayState = PlayState.PLAYING;
            }
        }

        private static void BringIn(Controller c, string heroId, int damage, int armor)
        {
            c.AddHeroAndPower(Cards.FromId(heroId));
            c.Hero.Damage = damage;
            c.Hero.Armor = armor;
        }

        internal static bool HoldsCards(IPlayable target)
        {
            if (HoldUntilTurn.Count == 0) return false;
            var hero = target.Controller.Hero;
            // TurnsTaken counts the turn under way: on the hero's first turn it is 1.
            return hero != null && HoldUntilTurn.TryGetValue(hero.Card.AssetId, out int turn)
                && target.Game.HistoryFacts.Query.TurnsTaken(target.Controller.Id) < turn;
        }

        public static bool DrawsNothing(Controller controller) =>
            NoDraws.Count != 0 && controller.Hero != null && NoDraws.Contains(controller.Hero.Card.AssetId);
    }
}
