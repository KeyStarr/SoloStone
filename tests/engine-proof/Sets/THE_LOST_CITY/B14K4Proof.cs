using System;
using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14K4LostCityProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_624", Nablya);
    private static void Nablya(Game g, Controller p, string id)
    {
        var baseline=g.CloneExact(history:true); var location=LocationProofs.Location(p);
        var a=Summon(p,"CS2_182"); a.AttackDamage=8; a.BaseHealth=9; a.Damage=2;
        var healthy=Summon(p,"CS2_200"); var b=Summon(p,"CS2_120"); b.Damage=1;
        var enemy=Summon(p.Opponent,"CS2_182"); enemy.Damage=1;
        var source=PlayAt(g,p,id,2);
        var copies=p.BoardZone.Where(m=>m.Id!=a.Id && m.Id!=b.Id && m.Id!=healthy.Id && m.Id!=source.Id).ToArray();
        Core2026Proof.Check(copies.Length==2 && copies[0].Card.Id==a.Card.Id && copies[0].AttackDamage==8 && copies[0].BaseHealth==9 && copies[0].Damage==2 && copies[1].Card.Id==b.Card.Id && copies[1].Damage==1,"copies snapshot only damaged friendly minions, retaining stats/damage: "+string.Join(";",copies.Select(m=>$"{m.Card.Id} {m.AttackDamage}/{m.BaseHealth} d={m.Damage}")));
        Core2026Proof.Check(copies.All(m=>m.IsRush) && !a.IsRush && !b.IsRush,"only copies gain Rush");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m=>m.Id).SequenceEqual(new[]{location.Id,a.Id,source.Id,copies[0].Id,copies[1].Id,healthy.Id,b.Id}),"copies preserve creation order right of living source");
        Core2026Proof.Check(p.Opponent.BoardZone.Single().Id==enemy.Id,"enemy damaged minion excluded");
        foreach(var next in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(copies.All(m=>((Minion)next.IdEntityDic[m.Id]).IsRush),"copy Rush survives clone/snapshot");
        End(g); End(g); Core2026Proof.Check(copies.All(m=>m.IsRush),"Rush grant persists across turns");
        Kill(g,source); Core2026Proof.Check(copies.All(m=>m.IsRush),"copy Rush survives source leaving play");
        copies[0].Silence(); Core2026Proof.Check(!copies[0].IsRush,"granted Rush silences normally");
        var full=baseline.CloneExact(); var owner=full.Player1; LocationProofs.Location(owner);
        while(owner.BoardZone.FreeSpace>1) { var m=Summon(owner,"CS2_182"); m.Damage=1; }
        Play(full,owner,id); Core2026Proof.Check(owner.BoardZone.SlotCount==7 && owner.BoardZone.Count(m=>m.Card.Id=="CS2_182")==5,"no room after source entry creates no copies");
        var partial=baseline.CloneExact(); owner=partial.Player1; LocationProofs.Location(owner);
        var original=Summon(owner,"CS2_182"); original.Silence(); original.Damage=1;
        var later=Summon(owner,"CS2_120"); later.Damage=1;
        Summon(owner,"CS2_200"); Summon(owner,"CS2_200");
        Play(partial,owner,id);
        var only=owner.BoardZone.Single(m=>m.Card.Id==original.Card.Id && m.Id!=original.Id);
        Core2026Proof.Check(only.IsSilenced && only.IsRush && only.Damage==1 && owner.BoardZone.SlotCount==7
            && owner.BoardZone.Count(m=>m.Card.Id==later.Card.Id)==1,"partial capacity copies first damaged source, keeps Silence and grants Rush afterwards");
        var enraged=baseline.CloneExact(history:true); owner=enraged.Player1;
        var grommash=Summon(owner,"EX1_414"); Generic.DamageCharFunc(owner.Opponent.Hero,grommash,1,false); enraged.DeathProcessingAndAuraUpdate();
        Play(enraged,owner,id);
        var ragingCopy=owner.BoardZone.Single(m=>m.Card.Id==grommash.Card.Id && m.Id!=grommash.Id);
        Core2026Proof.Check(ragingCopy.AttackDamage==10 && ragingCopy.Damage==1 && ragingCopy.IsRush && ragingCopy.HasCharge,"damaged conditional aura copied once with Rush");
        ragingCopy.TakeHeal(owner.Hero,1); enraged.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ragingCopy.AttackDamage==4 && grommash.AttackDamage==10,"copied damaged aura heals independently without doubling");
        var none=baseline.CloneExact(); owner=none.Player1; Summon(owner,"CS2_182"); Play(none,owner,id);
        Core2026Proof.Check(owner.BoardZone.Count==2,"healthy-only board creates no copies");
    }
}
