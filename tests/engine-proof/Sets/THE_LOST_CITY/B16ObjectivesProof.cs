using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16ObjectivesLostCityProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("DINO_405",(g,p,id)=>{
            Play(g,p,id);var body=Summon(p,"CS2_231");End(g);
            Core2026Proof.Check(body.AttackDamage==1,"Hatching skips current end");End(g);
            foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
                End(branch);var m=(Minion)branch.IdEntityDic[body.Id];
                Core2026Proof.Check(m.AttackDamage==3 && m.Health==3,"Hatching next owner end +2/+2");
                m.Silence();Core2026Proof.Check(m.AttackDamage==1 && m.Health==1,"Hatching buff removable");
                Core2026Proof.Check(ObjectiveRules.CompletedRituals(branch.Player1)==0,"ordinary objective is not Ritual");
            }
        });
        StandardSetsProof.Case("TLC_466",Pinned_LakkariDuplicates);
    }
    private static void Pinned_LakkariDuplicates(Game g,Controller p,string id)
    {
        Play(g,p,id);Play(g,p,id);var a=Add(p,"CS2_182");var b=Add(p,"CS2_231");var c=Add(p,"CS2_200");
        var rng=g.Random.Clone();var ids=p.HandZone.Select(x=>x.Id).ToList();
        ids.RemoveAt(rng.Next(ids.Count));ids.RemoveAt(rng.Next(ids.Count));
        End(g);
        Core2026Proof.Check(p.HandZone.Select(x=>x.Id).SequenceEqual(ids),"duplicate Lakkari discards use seeded order");
        Core2026Proof.Check(p.BoardZone.Count==7 && p.BoardZone.All(m=>m.Card.Id=="UNG_829t3" && m.AttackDamage==3 && m.Health==2),"pinned 3/2 Imp identity and full board");
        End(g);End(g);End(g);End(g);
        Core2026Proof.Check(!p.SecretZone.Any(ObjectiveRules.IsActive) && ObjectiveRules.CompletedRituals(p)==0,"three owner ends expire independently without Ritual ledger");
    }
}
