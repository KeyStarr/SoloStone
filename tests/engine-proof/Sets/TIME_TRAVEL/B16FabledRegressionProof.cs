using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16FabledRegressionProof
{
    private static Game New(long seed, bool secondPlayer)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        if (secondPlayer) End(game);
        return game;
    }

    internal static void Unpinned_PortalLifecycle(long seed)
    {
        // broxigar-chain-return: the printed Deathrattle advances independently of its killing source.
        foreach (bool secondPlayer in new[] { false, true })
        foreach (int stage in new[] { 2, 3, 4, 5 })
        {
            var game = New(seed, secondPlayer); var owner = game.CurrentPlayer;
            var original = Add(owner, "TIME_020"); FabledRules.Vanish(original);
            string portal = "TIME_020t" + stage;
            Play(game, owner, portal); var demon = owner.Opponent.BoardZone.Single();
            Play(game, owner, "EX1_581", demon); End(game);
            PlayEntity(game, game.CurrentPlayer, demon);
            Core2026Proof.Check(demon.HasDeathrattle && demon.Card.Deathrattle && demon.Card[GameTag.DEATHRATTLE] == 1,
                "Portal Demon retains printed Deathrattle after Sap/replay: " + portal);
            foreach (var next in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var recipient = next.ControllerById(owner.Id);
                var replayed = (Minion)next.IdEntityDic[demon.Id];
                Kill(next, replayed);
                CheckReward(next, recipient, original.Id, stage, 1);
                Play(next, recipient.Opponent, "BRM_017");
                var risen = recipient.Opponent.BoardZone.Single();
                Core2026Proof.Check(risen.Card.Id == portal + "t" && risen.HasDeathrattle && !risen.IsSilenced,
                    "Resurrect creates fresh Demon with printed Deathrattle");
                Kill(next, risen); CheckReward(next, recipient, original.Id, stage, 2);
                var silenced = Summon(recipient.Opponent, portal + "t"); silenced.Silence(); Kill(next, silenced);
                CheckReward(next, recipient, original.Id, stage, 2);
            }
        }
    }

    private static void CheckReward(Game game, Controller recipient, int originalId, int stage, int deaths)
    {
        if (stage == 5)
            Core2026Proof.Check(game.IdEntityDic[originalId].Zone == recipient.HandZone
                && recipient.HandZone.Count(c => c.Card.Id == "TIME_020") == 1, "final returns original once");
        else
            Core2026Proof.Check(recipient.DeckZone.Concat(recipient.HandZone).Count(c => c.Card.Id == "TIME_020t" + (stage + 1)) == deaths
                && !recipient.Opponent.DeckZone.Any(c => c.Card.Id == "TIME_020t" + (stage + 1)),
                "only unsilenced deaths shuffle next Portal for opposing controller");
    }

    internal static void Unpinned_Oathstone(long seed)
    {
        foreach (bool secondPlayer in new[] { false, true })
        {
            var game = New(seed, secondPlayer); var owner = game.CurrentPlayer;
            Play(game, owner, "TIME_852t3");
            Kill(game, Summon(owner, "CS2_182"));
            Kill(game, Summon(owner.Opponent, "TIME_852t1"));
            Play(game, owner, "TIME_852t3");
            Core2026Proof.Check(owner.BoardZone.IsEmpty, "empty, non-Dragon and enemy death histories summon nothing");
            string[] deaths = { "TIME_852", "TIME_852t1", "GIL_681", "TIME_852" };
            foreach (string id in deaths)
            {
                var body = Summon(owner, id); body.AttackDamage += 10; body.Damage = 1;
                body.Silence(); Kill(game, body);
            }
            game.SetRandomSeed(seed);
            string[] expected = deaths.ChooseNElements(deaths.Length, game.CloneExact(history: true).Random);
            foreach (int occupied in new[] { 0, 6, 7 })
            foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var player = next.ControllerById(owner.Id);
                for (int i = 0; i < occupied; i++) Summon(player, "CS2_231");
                Play(next, player, "TIME_852t3");
                var risen = player.BoardZone.Skip(occupied).ToArray();
                Core2026Proof.Check(risen.Select(m => m.Card.Id).SequenceEqual(expected.Take(7 - occupied)),
                    "Oathstone seeded ordered friendly Dragon/ALL death events, duplicates and board capacity");
                Core2026Proof.Check(risen.All(m => !m.IsSilenced && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health),
                    "Oathstone resurrects fresh printed stats and abilities after clone/snapshot");
            }
            var all = New(seed, secondPlayer); Kill(all, Summon(all.CurrentPlayer, "GIL_681"));
            Play(all, all.CurrentPlayer, "TIME_852t3");
            Core2026Proof.Check(all.CurrentPlayer.BoardZone.Single().Card.Id == "GIL_681", "ALL counts as Dragon");
        }
    }
}
