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
        // The client mission script's events for a hero coming in (MISSION_EVENT on the game entity): its voice line, and the
        // name banner refreshed to the hero now in play.
        private static readonly Dictionary<string, (int TagIn, int TakeOver)> PartnerEvents = new Dictionary<string, (int, int)>();

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

        // missionEvents: the events the client's mission script plays when this hero is tagged in, and when it takes over
        // from its defeated partner.
        internal static void MarkPartnerEvents(Card hero, int tagIn, int takeOver) => PartnerEvents[hero.Id] = (tagIn, takeOver);

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
            BringIn(c, partner, damage, armor, takingOver: false);
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
                BringIn(owner, partner, owner[PartnerDamage], owner[PartnerArmor], takingOver: true);
                owner.PlayState = PlayState.PLAYING;
            }
        }

        private static void BringIn(Controller c, string heroId, int damage, int armor, bool takingOver)
        {
            // Created in PLAY (as retail's hero swaps): the client's swap spell (TagTeamSwapSpell, a HeroSwapSpell) applies only
            // the new hero's FULL_ENTITY before it looks up the hero's zone; created zoneless and moved after, the hero had no
            // zone there, the spell threw and the match froze.
            // He also arrives with the Damage and Armor he left with in that FULL_ENTITY, not as a later hit (30 -> 19).
            var tags = new EntityData { [GameTag.ZONE] = (int)Zone.PLAY };
            if (damage != 0) tags[GameTag.DAMAGE] = damage;
            if (armor != 0) tags[GameTag.ARMOR] = armor;
            c.AddHeroAndPower(Cards.FromId(heroId), null, tags);
            if (PartnerEvents.TryGetValue(heroId, out var events))
            {
                int missionEvent = takingOver ? events.TakeOver : events.TagIn;
                if (missionEvent != 0) c.Game[GameTag.MISSION_EVENT] = missionEvent;
            }
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
