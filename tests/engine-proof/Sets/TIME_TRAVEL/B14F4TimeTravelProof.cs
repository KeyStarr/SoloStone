using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B14F4TimeTravelProof
{
    internal static void Run()=>StandardSetsProof.Case("TIME_890",Unpinned_Medivh);
    private static void Unpinned_Medivh(Game g, Controller p,string id)
    {
        var staff=Add(p,"TIME_890t");
        var enemy=Summon(p.Opponent,"FP1_002"); var friendly=Summon(p,"FP1_002");
        Play(g,p,id);
        Core2026Proof.Check(enemy.Zone==p.Opponent.GraveyardZone && friendly.Zone==p.GraveyardZone
            && p.BoardZone.Count==1 && p.Opponent.BoardZone.Count==0,"all others silenced before destruction; no Deathrattle summons");
        Core2026Proof.Check(staff.Cost==0,"Medivh enables staff zero cost"); PlayEntity(g,p,staff);
        var fire=Add(p,"CORE_CS2_029"); int before=p.Opponent.Hero.Damage;
        Generic.DamageCharFunc(fire,p.Opponent.Hero,3,true);
        Core2026Proof.Check(p.Opponent.Hero.Damage==before+6,"Atiesh doubles spell damage");
        foreach(var next in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var owner=next.Player1;
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("CS1h_001")).Process(next,owner,owner.Hero,null);
            owner.Hero.Damage=20; owner.UsedMana=0;
            Core2026Proof.Check(next.Process(HeroPowerTask.Any(owner,owner.Hero)) && owner.Hero.Damage==18,
                "Atiesh leaves Lesser Heal at two after clone/snapshot");
            var velen=Summon(owner,"EX1_350");
            owner.Hero.HeroPower.IsExhausted=false; owner.UsedMana=0;
            Core2026Proof.Check(next.Process(HeroPowerTask.Any(owner,owner.Hero)) && owner.Hero.Damage==14,
                "Velen still doubles Lesser Heal alongside Atiesh");
            owner.Hero.TakeHeal(Add(owner,"CS1_130"),3);
            Core2026Proof.Check(owner.Hero.Damage==2,"Atiesh and Velen stack on spell healing");
            owner.Hero.RemoveWeapon(); next.DeathProcessingAndAuraUpdate();
            owner.Hero.Damage=20; owner.Hero.HeroPower.IsExhausted=false; owner.UsedMana=0;
            Core2026Proof.Check(next.Process(HeroPowerTask.Any(owner,owner.Hero)) && owner.Hero.Damage==16,
                "removing Atiesh preserves Velen Hero Power healing");
        }
        Play(g,p,"CATA_216"); Play(g,p,"CATA_216");
        p.Hero.Damage=25; p.Hero.TakeHeal(Add(p,"CS1_130"),3);
        Core2026Proof.Check(p.Hero.Damage==11,"Unpinned_Atiesh: additive healing before multiplier gives (3+2+2)*2");
        StandardSetsProof.ParkBoard(p);
        Summon(p,id);
        var sanctum=Add(p,"TIME_890t2"); Core2026Proof.Check(sanctum.Cost==0,"Atiesh enables Karazhan zero cost");
        PlayEntity(g,p,sanctum);
        var second=Add(p,id); Core2026Proof.Check(second.Cost==0,"controlled Karazhan enables Medivh zero cost");
        while(p.BoardZone.SlotCount<7) Summon(p,"CS2_182");
        sanctum[GameTag.DAMAGE]=1;
        TimeTravelS2Proof.WithCards(g,new[]{"CORE_EX1_414"},()=> {
            Core2026Proof.Check(g.Process(new ActivateLocationTask(p,(BoardObject)sanctum)),"final full-board Karazhan activation accepted");
            Core2026Proof.Check(sanctum.Zone==p.GraveyardZone && p.BoardZone.SlotCount==7
                && p.BoardZone.Count(m=>m.Card.Id=="CORE_EX1_414")==1,"last charge clears first and summons only available 8-Cost minion");
        });
        Core2026Proof.Check(second.Cost==10,"last charge removes the Karazhan cost condition");
        p.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate();
        p.Hero.Damage=25; p.Hero.TakeHeal(Add(p,"CS1_130"),3);
        Core2026Proof.Check(p.Hero.Damage==18,"unequipping Atiesh removes spell multiplier, retaining Cleric grants");

    }
}
