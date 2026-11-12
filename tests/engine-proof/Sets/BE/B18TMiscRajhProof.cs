using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using static Core2026Proof;
internal static class B18TMiscRajhProof
{
    [GroupProof("b18-t-misc-rajh-cards",ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("BE_004",(g,p,id)=>{
        Play(g,p,id);
        int choices=0;
        while(p.Choice!=null && choices++<3) DiscoverRealProof.Answer(g,DiscoverRealProof.Offers(g)[0].Id);
        Core2026Proof.Check(choices==2 && p.Hero.HeroPower.Card.Id=="BE_004p" && p.HandZone.Count==0,"Rajh two choices install immediately without hand card");
        var pool=AssembleRules.Candidates(g,p,id,132353);
        Core2026Proof.Check(pool.All(c=>!SabberStoneCore.Loader.AssembleCatalog.HasBattlecry(c) && !SabberStoneCore.Loader.AssembleCatalog.HasDeathrattle(c)),"passive-only curated pool");
        Core2026Proof.Check(pool.Any(a=>pool.Any(b=>Math.Abs(a.Cost-b.Cost)>2 && AssembleRules.DonorMatches(a,b,id,p.HeroClass))),"no ordinary donor cost band");
        Console.WriteLine("Rajh component bucket="+pool.Length);
    });
}
