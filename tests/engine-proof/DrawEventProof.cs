using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DrawEventProof
{
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
                SkipMulligan = true, Logging = false, History = true, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            var owner = game.Player1;
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            Summon(owner, "BRM_031"); // Chromaggus copies each friendly draw.
            var egg = Entity.FromCard(owner, Cards.FromId("EX1_557")); owner.DeckZone.Add(egg);
            var top = Entity.FromCard(owner, Cards.FromId("CS2_029")); owner.DeckZone.Add(top);
            Play(game, owner, "DINO_411");
            Core2026Proof.Check(owner.HandZone.Count == 2 && owner.HandZone.All(c => c.Card.Id == "EX1_557")
                && owner.HandZone.Contains(egg) && top.Zone == owner.DeckZone,
                "filtered draw fires DRAW: Chromaggus copies Holy Eggbearer's selected egg exactly once");

            int hand = owner.HandZone.Count;
            Add(owner, "CS2_182");
            Core2026Proof.Check(owner.HandZone.Count == hand + 1, "created card added to hand is not a draw");
            Generic.Draw(owner, top);
            Core2026Proof.Check(owner.HandZone.Count == hand + 3 && owner.HandZone.Count(c => c.Card.Id == "CS2_029") == 2,
                "direct selected draw fires DRAW exactly once");
            Generic.Draw(owner.Opponent);
            Core2026Proof.Check(owner.HandZone.Count == hand + 3, "opponent draw does not trigger friendly Chromaggus");

            while (!owner.HandZone.IsFull) Add(owner, "CS2_182");
            foreach (bool selected in new[] { false, true })
            {
                var burned = Entity.FromCard(owner, Cards.FromId("CS2_182")); owner.DeckZone.Add(burned);
                int drawn = owner.NumCardsDrawnThisTurn;
                Generic.Draw(owner, selected ? burned : null);
                Core2026Proof.Check(burned.Zone == owner.GraveyardZone && owner.HandZone.Count == 10
                    && owner.NumCardsDrawnThisTurn == drawn + 1, "burned draw behavior preserved");
            }
            foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            int damage = owner.Hero.Damage;
            Core2026Proof.Check(Generic.Draw(owner) == null && owner.Hero.Damage == damage + 1 && owner.HandZone.Count == 10,
                "empty deck still fatigues without a draw");
        }
        BwonsamdiStopsWhenHandFills();
        Console.WriteLine("PASS filtered/direct DRAW events, generated cards, burn and fatigue boundaries");
    }

    private static void BwonsamdiStopsWhenHandFills()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool copyDraw in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
                SkipMulligan = true, Logging = false, History = true, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            var owner = game.Player1;
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            if (copyDraw) Summon(owner, "BRM_031");
            var eligible = Enumerable.Range(0, 2).Select(_ => Entity.FromCard(owner, Cards.FromId("CS2_171"))).ToArray();
            foreach (var card in eligible) owner.DeckZone.Add(card);
            for (int i = 0; i < 8; i++) Add(owner, "CS2_182");
            int deck = owner.DeckZone.Count, drawn = owner.NumCardsDrawnThisTurn;
            Play(game, owner, "TRL_260");
            int expectedDraws = copyDraw ? 1 : 2;
            Core2026Proof.Check(owner.HandZone.IsFull && owner.HandZone.Count(c => c.Card.Id == "CS2_171") == 2,
                "Bwonsamdi fills eight-card hand with eligible draws and any Chromaggus copy");
            Core2026Proof.Check(owner.DeckZone.Count == deck - expectedDraws
                && eligible.Count(c => c.Zone == owner.DeckZone) == 2 - expectedDraws
                && eligible.All(c => c.Zone != owner.GraveyardZone)
                && owner.NumCardsDrawnThisTurn == drawn + expectedDraws,
                "Bwonsamdi stops when Chromaggus fills hand; remaining eligible minion stays in deck, never burns");
        }
        Console.WriteLine("PASS Bwonsamdi stops at full hand after DRAW triggers; ordinary draws still fill hand");
    }
}
