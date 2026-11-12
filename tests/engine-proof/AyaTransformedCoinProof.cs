using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class AyaTransformedCoinProof
{
    [GroupProof("aya-transformed-coin")]
    internal static void Unpinned_TransformedCoins()
    {
        foreach (bool aya in new[] { false, true })
        foreach (string targetId in new[] { "CS2_182", "CS2_029", "CS2_106" })
        {
            var game = ImbueProof.Make(CardClass.ROGUE);
            var player = game.Player1;
            if (aya) ChooseJade(game);
            var left = Add(player, "CS2_171");
            var selected = Add(player, targetId);
            var right = Add(player, "CS2_171");
            selected.Cost = 1;
            int position = selected.ZonePosition;
            Play(game, player, "CATA_200");
            foreach (var next in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                int cursor = next.PowerHistory.Full.Count;
                PlayerDecisionProof.Answer(next, selected.Id);
                var owner = next.Player1;
                var coin = next.IdEntityDic[selected.Id];
                string expected = aya ? "JAIL_504t" : "GAME_005";
                Core2026Proof.Check(coin is Spell && coin.Card.Id == expected && coin.Cost == 0,
                    "Agent after Aya=" + aya + " transforms " + targetId + " into " + expected);
                Core2026Proof.Check(owner.HandZone.Contains(coin) && coin.ZonePosition == position
                    && next.IdEntityDic[left.Id].Card.Id == "CS2_171" && next.IdEntityDic[right.Id].Card.Id == "CS2_171",
                    "Agent keeps selected identity, hand slot and neighbors");
                AssertChange(next, cursor, selected.Id, expected);
                Core2026Proof.Check(owner.Choice == null, "Agent choice finishes");
            }
        }

        var g = ImbueProof.Make(CardClass.ROGUE);
        var p = g.Player1;
        ChooseJade(g);
        // ChangeEntityTask can copy a prototype definition without re-entering the hand.
        var prototype = Entity.FromCard(p, Cards.FromId("GAME_005"), zone: p.DeckZone);
        var held = Add(p, "CS2_029");
        int start = g.PowerHistory.Full.Count;
        new ChangeEntityTask(EntityType.SOURCE, EntityType.TARGET).Process(g, p, held, prototype);
        Core2026Proof.Check(held.Card.Id == "JAIL_504t", "prototype-copy ChangeEntityTask also upgrades held Coin");
        AssertChange(g, start, held.Id, "JAIL_504t");
        Core2026Proof.Check(prototype.Card.Id == "GAME_005", "copy prototype stays a plain Coin in deck");

        var copied = Generic.Copy(p, p.Hero, prototype, Zone.HAND);
        Core2026Proof.Check(copied.Card.Id == "JAIL_504t", "copy into hand uses existing Coin admission policy");
        var bookie = (Minion)Play(g, p, "JAIL_720");
        int before = p.HandZone.Count(c => c.Card.Id == "JAIL_504t");
        Kill(g, bookie);
        Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "JAIL_504t") == before + 1,
            "Lotus Bookie deathrattle still generates Jade Coin");

        var deckCard = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var deckCoin = Generic.ChangeEntity(p, deckCard, Cards.FromId("GAME_005"), true);
        Core2026Proof.Check(deckCoin.Card.Id == "GAME_005" && deckCoin.Zone == p.DeckZone,
            "deck transformation does not apply hand policy");
        var enemy = Add(p.Opponent, "CS2_029");
        var enemyCoin = Generic.ChangeEntity(p.Opponent, enemy, Cards.FromId("GAME_005"), true);
        Core2026Proof.Check(enemyCoin.Card.Id == "GAME_005", "opponent without Aya keeps plain Coin");
        var plain = ImbueProof.Make(CardClass.ROGUE);
        Core2026Proof.Check(Add(plain.Player1, "GAME_005").Card.Id == "GAME_005", "no-Aya generated Coin stays plain");
        Console.WriteLine("PASS Aya transformed Coins: Agent, prototype change, copy, generation, history, clone/snapshot, deck/opponent/no-Aya boundaries");
    }

    private static void ChooseJade(Game game)
    {
        Play(game, game.Player1, "JAIL_504");
        B18GeneratedProof.Answer(game, game.Player1.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "JAIL_504t"));
        Core2026Proof.Check(game.Player1.HandZone.Count(c => c.Card.Id == "JAIL_504t") == 3, "Aya resolves Jade choice and grants three");
    }

    private static void AssertChange(Game game, int cursor, int entityId, string expected)
    {
        var changes = game.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryChangeEntity>()
            .Where(change => change.Entity.Id == entityId).ToArray();
        Core2026Proof.Check(changes.Length == 1 && changes[0].CardId == expected,
            "native CHANGE_ENTITY publishes final Coin identity once: " + expected);
    }
}
