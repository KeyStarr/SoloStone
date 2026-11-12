using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16C16Proof
{
    [GroupProof("b16-c16-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_509", Unpinned_GodfreySetup);
    }
    private static void Unpinned_GodfreySetup(Game unused, Controller unusedOwner, string id)
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 29).Append(Cards.FromId(id)).ToList();
        var game = new Game(new GameConfig { RandomSeed=1, StartPlayer=1, Shuffle=false,
            SkipMulligan=false, History=true, Logging=false, FormatType=FormatType.FT_STANDARD,
            Player1Deck=deck, Player2Deck=deck });
        game.StartGame();
        foreach (var p in new[] {game.Player1, game.Player2})
            Core2026Proof.Check(p.AppliedEnchantments?.Any(e=>e.Card.Id=="JAIL_509e")!=true,
                "Unpinned_Godfrey: no effect during mulligan");
        B15GameSetupProof.Kept(game);
        foreach (var p in new[] {game.Player1, game.Player2})
            Core2026Proof.Check(p.AppliedEnchantments.Count(e=>e.Card.Id=="JAIL_509e")==1,
                "Unpinned_Godfrey: original deck installs effect after mulligan in both seats");
        Pinned_GodfreyBacklogRandomOrder();
        Unpinned_CompletedAction();
    }
    private static void Unpinned_CompletedAction()
    {
        var g=B15DrawReplacementProof.NewGame(); var p=g.Player1;
        Enchantment.GetInstance(p,p.Hero,p,Cards.FromId("JAIL_509e"));
        var draw=Add(p,"EX1_015");
        while(!p.HandZone.IsFull) Add(p,"CS2_182");
        var retained=Entity.FromCard(p,Cards.FromId("CS2_029"),zone:p.DeckZone);
        Generic.Draw(p,retained);
        int before=p.DeckZone.Count;
        PlayEntity(g,p,draw);
        Core2026Proof.Check(p.HandZone.IsFull && retained.Zone==p.SetasideZone && p.DeckZone.Count==before-1,
            "Unpinned_Godfrey: draw Battlecry fills freed slot before backlog");
        var body=Summon(p.Opponent,"JAIL_509");
        body.Silence(); Kill(g,body);
        Core2026Proof.Check(p.Opponent.AppliedEnchantments?.Any(e=>e.Card.Id=="JAIL_509e")!=true,
            "generated/played/Silenced Godfrey never installs start-of-game effect");
    }
    private static void Pinned_GodfreyBacklogRandomOrder()
    {
        for(int seed=1;seed<=6;seed++)
        {
            var g=B15DrawReplacementProof.NewGame(); var p=g.Player1;
            Enchantment.GetInstance(p,p.Hero,p,Cards.FromId("JAIL_509e"));
            while(!p.HandZone.IsFull) Add(p,"CS2_182");
            var retained=new[]{"CS2_029","CS2_231","CS2_029"}.Select(id=>Entity.FromCard(p,Cards.FromId(id),zone:p.DeckZone)).ToArray();
            foreach(var card in retained) Generic.Draw(p,card);
            g.SetRandomSeed(seed);
            foreach(var branch in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot()),g})
            {
                var owner=branch.Player1;
                var remaining=retained.Select(c=>c.Id).ToList();
                var rng=new SabberStoneCore.Model.Util.DeepCloneableRandom(seed);
                int draws=owner.NumCardsDrawnThisTurn;
                while(remaining.Count>0)
                {
                    int index=rng.Next(remaining.Count), expected=remaining[index]; remaining.RemoveAt(index);
                    owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
                    var delivered=branch.IdEntityDic[expected];
                    Core2026Proof.Check(delivered.Zone==owner.HandZone && delivered.Cost==Math.Max(0,delivered.Card.Cost-1),
                        "Pinned_GodfreyBacklogRandomOrder: seeded physical entries, duplicates and discount");
                    Core2026Proof.Check(owner.NumCardsDrawnThisTurn==draws,"return is not a draw");
                }
                Core2026Proof.Check(branch.Random.CaptureState().Equals(rng.CaptureState()),"exact random consumption across clone/load");
            }
        }
    }
}
