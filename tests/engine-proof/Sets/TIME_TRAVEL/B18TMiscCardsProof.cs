using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class B18TMiscCardsProof
{
    [GroupProof("b18-t-misc-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (var id in new[] { "TIME_432", "END_018" })
            Core2026Proof.Check(Cards.FromId(id)?.Implemented == true, id + " registered");
        StandardSetsProof.Case("TIME_432", (g,p,id) => Unpinned_FateCopies());
        StandardSetsProof.Case("END_018", (g,p,id) => B18TMiscProof.Unpinned_InfinityLayers());
        Unpinned_FateCapacity();
    }
    private static void Unpinned_FateCapacity()
    {
        foreach(bool empty in new[]{false,true}) {
            var g=DiscoverRealProof.New();var p=g.Player1;
            DiscoverRealProof.Deck(p, empty?Array.Empty<string>():new[]{"CS2_200","CS2_182","CS2_231","CS2_029"});
            DiscoverRealProof.Deck(p.Opponent,empty?Array.Empty<string>():new[]{"CS2_029"});
            var fate=Add(p,"TIME_432");for(int i=0;i<9;i++)Add(p,"CS2_231");
            PlayEntity(g,p,fate);
            if(!empty) {
                var offers=DiscoverRealProof.Offers(g);
                Core2026Proof.Check(offers.Length==3 && offers.Select(c=>c.Card.Id).Distinct().Count()==3,"three distinct offers");
                DiscoverRealProof.Answer(g,offers[0].Id);
                DiscoverRealProof.Answer(g,DiscoverRealProof.Offers(g)[0].Id);
            }
            Core2026Proof.Check(p.Choice==null && p.HandZone.Count==(empty?9:10),"empty decks complete and overflow burns only the extra copy");
        }
    }
    internal static void Unpinned_FateCopies()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        g[PayloadRules.Tracking]=2;
        var own = DiscoverRealProof.Deck(p, "CS2_200", "CS2_200");
        var enemy = DiscoverRealProof.Deck(p.Opponent, "CS2_029", "CS2_029");
        Play(g, p, "TIME_432");
        Core2026Proof.Check(DiscoverRealProof.Offers(g).Length == 1, "own duplicate definitions collapse");
        DiscoverRealProof.Saved(g, next => {
            DiscoverRealProof.Answer(next, DiscoverRealProof.Offers(next).Single().Id);
            Core2026Proof.Check(next.Player1.HandZone.Count == 1 && next.Player1.DeckZone.Count == 2, "first copy leaves own deck intact");
            Core2026Proof.Check(DiscoverRealProof.Offers(next).Length == 1, "enemy duplicate definitions collapse");
            DiscoverRealProof.Answer(next, DiscoverRealProof.Offers(next).Single().Id);
            Core2026Proof.Check(next.Player1.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { "CS2_200", "CS2_029" }), "two copies delivered in order");
            Core2026Proof.Check(!PayloadRules.CopiedFromOpponent(next.Player1.HandZone[0]) && PayloadRules.CopiedFromOpponent(next.Player1.HandZone[1]), "copy lineage follows the actual deck controller");
            Core2026Proof.Check(next.Player2.DeckZone.Count == 2 && next.Player1.Choice == null, "enemy deck intact; choice completes");
        });
    }
}
