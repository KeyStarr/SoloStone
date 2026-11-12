using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Triggers;
using static Core2026Proof;

internal static class DarkBribeProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_206", (g, p, id) => {
        DecisionSpellProof.DarkBribe(g, p, id);
        GiftedDraw(++_seed);
        GiftedWallow(_seed);
    });

    private static int _seed;

    private static readonly FieldInfo TriggerOwner = typeof(Trigger).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "Dark Bribe gift transfer: " + message);

    private static Game Fixture(int seed, params string[] deck)
    {
        var g = PlayerDecisionProof.Fixture(seed);
        var p = g.Player1;
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in deck.Reverse()) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
        return g;
    }

    // Answers the pending Dark Bribe in the original, an exact clone and a loaded snapshot; all three must agree.
    private static Game[] AnswerEverywhere(Game g, int pick)
    {
        var bytes = g.SaveSnapshot();
        var games = new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) };
        Check(bytes.SequenceEqual(games[2].SaveSnapshot()), "pending snapshot byte round trip");
        foreach (var game in games) PlayerDecisionProof.Answer(game, pick);
        Same(games, "answer");
        return games;
    }

    private static void Same(Game[] games, string label)
    {
        foreach (var other in games.Skip(1))
            Check(Canonical.State(games[0]) == Canonical.State(other) && games[0].Random.CaptureState().Equals(other.Random.CaptureState()),
                "clone/snapshot parity after " + label);
        var after = games[0].SaveSnapshot();
        Check(after.SequenceEqual(Game.LoadSnapshot(after).SaveSnapshot()), "snapshot byte round trip after " + label);
    }

    private static int OwnedTriggers(Game g, IEntity owner) => g.Triggers.Count(t => ReferenceEquals(TriggerOwner.GetValue(t), owner));

    private static int Hides(Game g, int id) => g.PowerHistory.Full.OfType<PowerHistoryHideEntity>().Count(h => h.EntityID == id);

    // (a) A card drawn by Dark Bribe already carries Dark Gifts (one with a trigger, one with cost/stat effects).
    private static void GiftedDraw(int seed)
    {
        var g = Fixture(seed, "CS2_182", "CS2_182", "CS2_182");
        var p = g.Player1;
        var card = p.DeckZone.TopCard;
        DarkGifts.Mirror(card, "EDR_100t5", p.Hero);
        DarkGifts.Mirror(card, "EDR_100t2", p.Hero);
        var gifts = DarkGifts.GiftsOf(card).ToArray();
        var expected = gifts.Select(e => (Id: e.Id, CardId: e.Card.Id, Creator: e[GameTag.CREATOR], Spell: e.Creator.Id)).ToArray();
        Check(gifts.Length == 2 && card[GameTag.HAS_DARK_GIFT] == 1 && card.Cost == 2, "fixture gifts attached in deck");
        Play(g, p, "JAIL_206");
        Check(p.Choice != null && p.Choice.Choices.Contains(card.Id) && card.Zone == p.HandZone && DarkGifts.GiftsOf(card).Count == 2,
            "gifted card drawn by Dark Bribe and offered with gifts intact");
        int cardId = card.Id, nightmareId = expected[0].Id;
        var games = AnswerEverywhere(g, cardId);
        foreach (var game in games)
        {
            var me = game.Player1; var opp = game.Player2;
            var given = game.IdEntityDic[cardId];
            var now = DarkGifts.GiftsOf(given);
            Check(given.Controller == opp && given[GameTag.CONTROLLER] == opp.PlayerId && given.Zone == opp.HandZone, "card controller and zone");
            Check(now.Select(e => (Id: e.Id, CardId: e.Card.Id, Creator: e[GameTag.CREATOR], Spell: e.Creator.Id)).SequenceEqual(expected),
                "same gift attachments, creators and gift spells after transfer");
            Check(now.All(e => e.Controller == opp && e[GameTag.CONTROLLER] == opp.PlayerId && e[GameTag.IS_NIGHTMARE_BONUS] == 1 &&
                e[GameTag.ATTACHED] == cardId && e.Target == given && e[GameTag.ZONE] == (int)Zone.PLAY), "gift tags rebound to receiver, attachment kept");
            Check(given[GameTag.HAS_DARK_GIFT] == 1 && given.AppliedEnchantments.Count == 2 && given.Cost == 2 &&
                ((Minion)given).AttackDamage == 2 && ((Minion)given).Health == 5, "HAS_DARK_GIFT, cost and stats unchanged");
            var nightmare = (Enchantment)game.IdEntityDic.Values.OfType<Enchantment>().Single(e => e.Id == nightmareId);
            Check(nightmare.ActivatedTrigger != null && OwnedTriggers(game, nightmare) == 1 &&
                ReferenceEquals(TriggerOwner.GetValue(nightmare.ActivatedTrigger), nightmare), "exactly one live Living Nightmare trigger");
            Check(Hides(game, nightmareId) == 0 && Hides(game, expected[1].Id) == 0, "no gift cleanup during transfer");
            Check(me.HandZone.All(c => c.Id != cardId) && me.SetasideZone.All(c => c.Id != cardId), "giver keeps no copy");
        }
        // Branch: the receiver returns the given card to its deck; ordinary backward cleanup runs exactly once.
        var bounced = games.Select(game => game.CloneExact(history: true)).ToArray();
        foreach (var game in bounced)
        {
            var opp = game.Player2; var given = game.IdEntityDic[cardId];
            opp.DeckZone.Add(opp.HandZone.Remove(given));
            Check(!DarkGifts.Has(given) && given[GameTag.HAS_DARK_GIFT] == 0 && given.AppliedEnchantments.Count == 0 &&
                given.Cost == 4 && ((Minion)given).AttackDamage == 4 && expected.All(e => Hides(game, e.Id) == 1),
                "backward move removes each gift exactly once");
            Check(OwnedTriggers(game, game.IdEntityDic.Values.OfType<Enchantment>().Single(e => e.Id == nightmareId)) == 0, "removed gift leaves no trigger");
        }
        Same(bounced, "backward cleanup");
        foreach (var game in games)
        {
            End(game); var opp = game.Player2; opp.BaseMana = 10; opp.UsedMana = 0;
            int mine = game.Player1.BoardZone.Count;
            Check(game.Process(PlayCardTask.Any(opp, game.IdEntityDic[cardId])), "receiver plays the gifted card");
            var copies = opp.BoardZone.Where(m => m.Id != cardId && m.Card.Id == "CS2_182").ToArray();
            Check(opp.BoardZone.Count == 2 && copies.Length == 1 && copies[0].AttackDamage == 2 && copies[0].Health == 2 &&
                copies[0].Controller == opp && game.Player1.BoardZone.Count == mine, "Living Nightmare fires once, for the receiver only");
            Check(expected.All(e => Hides(game, e.Id) == 0), "no gift cleanup on the forward play");
        }
        Same(games, "receiver play");
    }

    // (b) Wallow, whose own trigger lives in hand or deck, carrying a gift, is given away.
    private static void GiftedWallow(int seed)
    {
        var g = Fixture(seed, "EDR_487", "CS2_182", "CS2_182");
        var p = g.Player1;
        var wallow = p.DeckZone.TopCard;
        DarkGifts.Mirror(wallow, "EDR_100t", p.Hero);
        Check(wallow.ActivatedTrigger?.Type == TriggerType.DARK_GIFT_GIVEN && DarkGifts.GiftsOf(wallow).Count == 1, "Wallow armed in deck with one gift");
        Play(g, p, "JAIL_206");
        int wallowId = wallow.Id, giftId = DarkGifts.GiftsOf(wallow).Single().Id;
        var games = AnswerEverywhere(g, wallowId);
        foreach (var game in games)
        {
            var me = game.Player1; var opp = game.Player2;
            var given = game.IdEntityDic[wallowId];
            var gift = DarkGifts.GiftsOf(given).Single();
            Check(given.Controller == opp && given.Zone == opp.HandZone && gift.Id == giftId && gift.Controller == opp &&
                gift[GameTag.CONTROLLER] == opp.PlayerId && given[GameTag.HAS_DARK_GIFT] == 1 && Hides(game, giftId) == 0,
                "Wallow and its gift move to the receiver without cleanup");
            Check(given.ActivatedTrigger?.Type == TriggerType.DARK_GIFT_GIVEN && ReferenceEquals(TriggerOwner.GetValue(given.ActivatedTrigger), given),
                "Wallow's own hand trigger re-armed once in the receiver's hand");
            // A gift for the giver's minion is no longer friendly to Wallow.
            var mine = (Minion)Entity.FromCard(me, Cards.FromId("CS2_182"), zone: me.HandZone);
            Check(DarkGifts.Give(mine, me.Hero), "giver gets a fresh gift");
            Check(DarkGifts.GiftsOf(given).Count == 1, "giver's gift does not reach the given Wallow");
            // A gift for the receiver's minion mirrors exactly once onto Wallow, owned by the receiver.
            var theirs = (Minion)Entity.FromCard(opp, Cards.FromId("CS2_182"), zone: opp.HandZone);
            Check(DarkGifts.Give(theirs, opp.Hero), "receiver gets a fresh gift");
            var mirrored = DarkGifts.GiftsOf(given);
            Check(mirrored.Count == 2 && mirrored.Last().Controller == opp && mirrored.Last()[GameTag.ATTACHED] == wallowId,
                "receiver's gift mirrors once onto Wallow: " + mirrored.Count);
        }
        Same(games, "Wallow mirror");
    }
}
