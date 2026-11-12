using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1EmeraldDreamProof
{
    internal static void Run()=>StandardSetsProof.Case("EDR_781",HarbingerOfTheBlighted);
    private static void HarbingerOfTheBlighted(Game g, Controller p, string id)
    {
        SeededSummons(id);
        var body=(Minion)Play(g,p,id);Core2026Proof.Check(p.BoardZone.Count==1,"ordinary play does not summon");
        Generic.ReturnToHandBlock(p,body);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Zone==p.HandZone && p.BoardZone.Count==2 && p.BoardZone.All(m=>m.Card.Cost==2),"battlefield bounce summons two 2-Cost minions");
        var another=Summon(p,id);int before=p.BoardZone.Count;
        Generic.Copy(p,p.Hero,another,Zone.HAND);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count==before,"board-to-hand copy is not a battlefield bounce");
        while(!p.BoardZone.IsFull)Summon(p,"CS2_231");
        Generic.ReturnToHandBlock(p,another);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.IsFull,"only one free physical slot is filled");
        var empty=HistoryFactsProof.Fixture();var own=empty.Player1;var source=Summon(own,id);
        LostCityPoolProof.WithPool(empty,id,Array.Empty<string>(),()=>{
            var rng=empty.Random.CaptureState();Generic.ReturnToHandBlock(own,source);empty.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(own.BoardZone.Count==0 && empty.Random.CaptureState().Equals(rng),"empty exact-Cost bucket gives nothing, no RNG");
        });
    }
    private static void SeededSummons(string id)
    {
        // Independent xoroshiro vectors: indices 2,0 and 0,2 in the dbf-sorted three-card pool.
        var vectors=new[] {
            (Start:new RngState(1,-2,0), First:"CORE_EX1_082", Second:"CORE_EX1_012",
                Once:new RngState(36028797018980351,-1,1), Twice:new RngState(-71987225025331169,-1125831321583617,2)),
            (Start:new RngState(-1,4611686018427387904,0), First:"CORE_EX1_012", Second:"CORE_EX1_082",
                Once:new RngState(-4611686018427404288,-17179869185,1), Twice:new RngState(4638426123766595615,1125848367235136,2))
        };
        foreach(var vector in vectors)
        foreach(bool reverse in new[]{false,true})
        foreach(int free in new[]{1,2})
        {
            var game=HistoryFactsProof.Fixture();var player=game.Player1;
            // Reverse unrelated board/history order as well as fixture input order.
            foreach(string filler in reverse?new[]{"CS2_120","CS2_182"}:new[]{"CS2_182","CS2_120"})Summon(player,filler);
            while(player.BoardZone.Count<7-free)Summon(player,"CS2_231");
            var source=Summon(player,id);int before=player.BoardZone.Count-1;
            var members=new[]{"CORE_EX1_012","CORE_EX1_059","CORE_EX1_082"};
            LostCityPoolProof.WithPool(game,id,reverse?members.Reverse().ToArray():members,()=>{
                game.Random.RestoreState(vector.Start);
                var snapshot=game.SaveSnapshot();
                foreach(var copy in new[]{game.CloneExact(),Game.LoadSnapshotWithPools(snapshot,game.OfflinePools),game})
                {
                    Generic.ReturnToHandBlock(copy.Player1,(Minion)copy.IdEntityDic[source.Id]);copy.DeathProcessingAndAuraUpdate();
                    var summons=copy.Player1.BoardZone.Skip(before).ToArray();
                    var expected=free==1?new[]{vector.First}:new[]{vector.First,vector.Second};
                    Core2026Proof.Check(summons.Select(m=>m.Card.Id).SequenceEqual(expected),"Harbinger seeded identities and summon order");
                    Core2026Proof.Check(copy.Random.CaptureState().Equals(free==1?vector.Once:vector.Twice),
                        "Harbinger consumes exactly one draw per available slot, clone/snapshot agree");
                }
            });
        }
    }

}
