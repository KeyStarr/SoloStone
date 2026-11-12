using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15DrawReplacementProof
{
    [GroupProof("b15-draw-replacement")]
    internal static void Run()
    {
        var game = NewGame(); var p = game.Player1;
        Summon(p, "BRM_031");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        // A missing burn DRAW event also loses Chromaggus's copy when its recipient has space.
        var keymaster = Cards.FromId("CORE_SCH_717");
        Core2026Proof.Check(keymaster != null && keymaster.Implemented, "draw replacement: Keymaster definition installed");
        Summon(p.Opponent, keymaster.Id);
        var burned = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Generic.Draw(p);
        Core2026Proof.Check(burned.Zone == p.GraveyardZone && p.Opponent.HandZone.Count == 1
            && p.Opponent.HandZone[0].Card.Id == "CS2_029" && p.Opponent.HandZone[0].Cost == 1,
            "burn is a draw event for the other controller");
        Unpinned_NormalDrawPrecedenceAndPermutation();
        BurnedDeathrattleSettlesBeforeNextDraw();
        Unpinned_CrossControllerAndFatigue();
        Unpinned_BacklogCloneAndRewind();
        Console.WriteLine("PASS draw replacement: burn, cross-controller draw/cast/fatigue, backlog clone/load/Rewind");
    }

    private static void Unpinned_NormalDrawPrecedenceAndPermutation()
    {
        for (int size = 0; size <= 3; size++)
        {
            var game = NewGame(); var owner = game.Player1; EmptyDeck(owner);
            var bottom = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            var spells = new[] { "CS2_029", "CS2_024", "CS1_130" }.Take(size)
                .Select(id => Entity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone)).ToArray();
            game.SetRandomSeed(17);
            DrawReplacementRules.MoveSpellsToTop(owner);
            Core2026Proof.Check(owner.DeckZone[0] == bottom
                && owner.DeckZone.Skip(1).Select(c => c.Id).Order().SequenceEqual(spells.Select(c => c.Id).Order()),
                "Discs preserves exact physical membership for zero/one/all spell buckets");
            if (size <= 1)
                Core2026Proof.Check(game.Random.Next(1000) == new SabberStoneCore.Model.Util.DeepCloneableRandom(17).Next(1000),
                    "zero/one spell does not consume permutation RNG");
        }
        var orders = new HashSet<string>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var game = NewGame(); var owner = game.Player1; EmptyDeck(owner);
            foreach (string id in new[] { "CS2_029", "CS2_024", "CS1_130" })
                Entity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            game.SetRandomSeed(seed); DrawReplacementRules.MoveSpellsToTop(owner);
            orders.Add(string.Join(",", owner.DeckZone.Select(c => c.Card.Id)));
        }
        Core2026Proof.Check(orders.Count > 1, "Unpinned_Discs spell group is randomized rather than stable sorted");

        var g = NewGame(); var p = g.Player1;
        Play(g, p, "BE_250"); var geddon = (Minion)Play(g, p, "CATA_591"); Kill(g, geddon);
        var chiller = Summon(p, "TIME_617");
        int before = p.DeckZone.Count; g.MainDraw();
        Core2026Proof.Check(p.Choice == null && p.DeckZone.Count == before,
            "Chronochiller suppresses normal draw even with saved Geddon/Discs effects");
        chiller.Silence(); g.MainDraw();
        Core2026Proof.Check(p.Choice != null && g.IdEntityDic[p.Choice.SourceId].Card.Id == "CATA_591e",
            "Geddon survives source death and has priority over Discs");
        var decision = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
            decision.Id, decision.ControllerId, new[] { p.Choice.Choices[0] }), out _), "resolve saved Geddon replacement");
    }

    private static void BurnedDeathrattleSettlesBeforeNextDraw()
    {
        var g = NewGame(); var p = g.Player1;
        var listener = Summon(p.Opponent, "CORE_SCH_717"); listener.Damage = 5;
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var imp = Entity.FromCard(p, Cards.FromId("JAIL_398"), zone: p.DeckZone);
        Generic.Draw(p, imp);
        Core2026Proof.Check(listener.Zone == p.Opponent.GraveyardZone,
            "overdraw Deathrattle completes its deaths before another draw can trigger the dead listener");
        int hand = p.Opponent.HandZone.Count;
        Generic.Draw(p);
        Core2026Proof.Check(p.Opponent.HandZone.Count == hand, "dead Alabaster cannot copy a later draw");
    }

    private static void Unpinned_CrossControllerAndFatigue()
    {
        var g = NewGame(); var p = g.Player1; var enemy = p.Opponent;
        EmptyDeck(enemy);
        var card = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        card.Cost = 2;
        Generic.DrawFrom(enemy, p, card);
        Core2026Proof.Check(card.Zone == enemy.HandZone && card.Controller == enemy
            && card[GameTag.CONTROLLER] == enemy.PlayerId && card.Cost == 2 && enemy.Hero.Damage == 0,
            "foreign selected draw ignores empty recipient deck and retains modified cost/controller");
        int drawn = enemy.NumCardsDrawnThisTurn;
        var bomb = Entity.FromCard(p, Cards.FromId("BOT_511t"), zone: p.DeckZone);
        Generic.DrawFrom(enemy, p, bomb);
        Core2026Proof.Check(enemy.Hero.Damage == 6 && p.Hero.Damage == 0
            && enemy.NumCardsDrawnThisTurn == drawn + 1, "foreign Casts When Drawn damages recipient; replacement fatigues its own deck");
        EmptyDeck(p);
        Generic.DrawFrom(enemy, p);
        Core2026Proof.Check(enemy.Hero.Damage == 8 && p.Hero.Damage == 0, "empty foreign source increments only recipient fatigue");
        int count = g.IdEntityDic.Count;
        bool rejected = false;
        try { Generic.DrawFrom(p, p, card); } catch (ArgumentException) { rejected = true; }
        Core2026Proof.Check(rejected && g.IdEntityDic.Count == count && card.Zone == enemy.HandZone,
            "invalid source entity rejected before state changes");
        while (!enemy.HandZone.IsFull) Add(enemy, "CS2_182");
        bomb = Entity.FromCard(p, Cards.FromId("BOT_511t"), zone: p.DeckZone);
        Generic.DrawFrom(enemy, p, bomb);
        Core2026Proof.Check(bomb.Zone == enemy.GraveyardZone && enemy.Hero.Damage == 8,
            "full recipient burns foreign automatic card without casting/replacement draw");
    }

    private static void Unpinned_BacklogCloneAndRewind()
    {
        var g = NewGame(); var p = g.Player1;
        var effect = Enchantment.GetInstance(p, p.Hero, p, Cards.FromId("JAIL_509e"));
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var second = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        var first = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var nested = PayloadRules.StoreCard(first, 77, Cards.FromId("CS2_200"));
        Generic.Draw(p, first); Generic.Draw(p, second);
        Core2026Proof.Check(first.Zone == p.SetasideZone && second.Zone == p.SetasideZone
            && PayloadRules.Entries(effect).Length == 2, "overdraw retains original entities in arrival order");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1;
            int draws = owner.NumCardsDrawnThisTurn;
            var entries = new[] { first, second }.OrderBy(x => x.Id).ToArray();
            int selected = next.Random.Clone().Next(entries.Length);
            int expected = entries[selected].Id, other = entries[1-selected].Id;
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
            Core2026Proof.Check(next.IdEntityDic[expected].Zone == owner.HandZone
                && next.IdEntityDic[other].Zone == owner.SetasideZone && owner.NumCardsDrawnThisTurn == draws,
                "pinned random backlog delivery without a new DRAW after clone/load");
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
            Core2026Proof.Check(next.IdEntityDic[other].Zone == owner.HandZone
                && next.IdEntityDic[first.Id].Cost == 3 && next.IdEntityDic[second.Id].Cost == 0
                && PayloadRules.Entries(next.IdEntityDic[first.Id], 77).Single().Id == nested.Id,
                "backlog discounts both original cards once, retains nested payload and floors at zero");
        }
        // A genuine Rewind restores hand-exit delivery and replays it once, without duplicating the queue.
        var rewind = NewGame(); p = rewind.Player1; EmptyDeck(p);
        effect = Enchantment.GetInstance(p, p.Hero, p, Cards.FromId("JAIL_509e"));
        var portal = Add(p, "TIME_003");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var retained = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Generic.Draw(p, retained);
        Core2026Proof.Check(rewind.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, portal)), "Rewind backlog play");
        var transaction = rewind.Rewind;
        Core2026Proof.Check(transaction != null, "Portal Vanguard opens Rewind");
        Core2026Proof.Check(rewind.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
            transaction.OfferChoiceId, transaction.ControllerId, new[] { transaction.RewindEntityId }), out var error), "backlog Rewind: " + error);
        p = rewind.Player1;
        Core2026Proof.Check(rewind.IdEntityDic[retained.Id].Zone == p.HandZone && rewind.IdEntityDic[retained.Id].Cost == 3
            && PayloadRules.Entries(rewind.IdEntityDic[effect.Id]).Length == 0, "Rewind restores and replays backlog delivery exactly once");
    }

    internal static Game NewGame(int seed = 42)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            player.BaseMana = 10;
        }
        return game;
    }

    internal static void EmptyDeck(Controller p)
    { foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card)); }
}
