using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
internal static partial class B14H4IdentityProof
{
    private static void Cataclysm()=>StandardSetsProof.Case("CATA_140",MerithraOfTheDream);
    private static void MerithraOfTheDream(Game g,Controller p,string id)
    {
        Pools(g,id);KnownMetaProofFixture.UseBlackEmpire(g);var source=Add(p,id);
        for(int i=0;i<3;i++)Play(g,p,"CS2_171");
        Core2026Proof.Check(!PoweredUp.IsMet(source) && source[GameTag.TAG_SCRIPT_DATA_NUM_1]==22,"three actual Mana spent, not printed threshold");
        for(int i=0;i<3;i++){Play(g,p,"CS2_200");ParkBoard(p);}
        Play(g,p,"CS2_171");Play(g,p,"CS2_171");Play(g,p,"CS2_171");
        Core2026Proof.Check(!PoweredUp.IsMet(source),"24 Mana not ready");Play(g,p,"CS2_171");
        Core2026Proof.Check(PoweredUp.IsMet(source) && source[GameTag.TAG_SCRIPT_DATA_NUM_1]==0,"25 Mana ready");
        Future(g,next=>{
            var candidates=OfflineCardPools.Candidates(next,next.Player1.HeroClass,
                OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id).Pools.Single().Filter,Cards.FromId(id).AssetId);
            var random=next.Random.Clone();
            var expected=Enumerable.Range(0,10).Select(_=>candidates[random.Next(candidates.Count)].Id).ToArray();
            PlayEntity(next,next.Player1,next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.HandZone.Count==10 && next.Player1.HandZone.All(c=>c.Card.IsRace(Race.DRAGON) && c.Cost==1),"ready fills hand with one-Cost Dragons");
            Core2026Proof.Check(next.Player1.HandZone.Select(c=>c.Card.Id).SequenceEqual(expected),"ordered seeded Dragon identities");
            Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState())
                && next.Random.Clone().Next()==random.Next(),"one RNG draw per Dragon and exact continuation");
            End(next);End(next);
            // Merithra activates Windpeak Wyrm's separate Kindred discount next turn.
            Core2026Proof.Check(next.Player1.HandZone.All(c=>c.Cost==(c.Card.Id=="TLC_600" ? 0 : 1)),
                "persistent one-Cost Dragons retain their own Kindred discount");
            End(next);End(next);
            Core2026Proof.Check(next.Player1.HandZone.All(c=>c.Cost==1),
                "Merithra discount persists after Kindred expires");
        });
        EmptyHand(p);ParkBoard(p);
        B14K3BEProof.Pool(g,new[]{"CORE_EX1_189","EDR_890","END_030"},()=>{ Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==10 && p.HandZone.All(c=>c.Card.IsRace(Race.DRAGON) && c.Cost==c.Card.Cost),$"fresh copy costs, old spend ignored: {string.Join(",",p.HandZone.Select(c=>c.Card.Id+":"+c.Cost+"/"+c.Card.Cost))}"); });
    }
}
