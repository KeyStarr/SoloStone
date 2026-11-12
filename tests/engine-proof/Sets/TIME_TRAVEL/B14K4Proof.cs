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

internal static class B14K4TimeTravelProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_850", Unpinned_BloodFighters);
    // fabled-spelldamage/warrior-blood-fighter-family-membership.yaml (D/E, approved),
    // forced-attack/forced-{budget,readiness}.yaml (C/D, approved): actual hand member, +5/+5, forced free attack.
    private static void Unpinned_BloodFighters(Game g, Controller p, string id)
    {
        foreach(string sourceId in new[]{id,"TIME_850t","TIME_850t1"})
        foreach(string heldId in new[]{id,"TIME_850t","TIME_850t1"})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed,CardClass.WARRIOR); var owner=branch.Player1;
            var slot=DeathrattleSlotBoard(owner,()=>PlayAt(branch,owner,sourceId,2)); var source=owner.BoardZone.Single(m=>m.Card.Id==sourceId);
            var held=(Minion)Add(owner,heldId); held.AttackDamage=9; held.BaseHealth=10;
            var unrelated=Add(owner,"CS2_182"); int hand=owner.HandZone.Count, damage=owner.Opponent.Hero.Damage;
            Kill(branch,source);
            Core2026Proof.Check(held.Zone==owner.BoardZone && held.AttackDamage==14 && held.BaseHealth==15 && owner.HandZone.Count==hand-1 && unrelated.Zone==owner.HandZone,"actual identity-qualified hand minion retains buffs and gains five stats");
            CheckDeathrattleSlot(owner,slot,new[]{held},"Blood Fighter takes dying slot");
            Core2026Proof.Check(sourceId!=id || owner.Opponent.Hero.Damage==damage+14,"LoGosh forces exactly one attack, including hero");
            Core2026Proof.Check(sourceId!="TIME_850t" || held.HasTaunt,"Broll grants Taunt");
            Core2026Proof.Check(sourceId!="TIME_850t1" || held.CantBeTargetedBySpells && held.CantBeTargetedByHeroPowers,"Valeera grants Elusive");
            held.Silence(); Core2026Proof.Check(held.AttackDamage==7 && held.BaseHealth==7,"family buff and granted keywords silence");
        }
        foreach(bool reverse in new[]{false,true})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed,CardClass.WARRIOR); var owner=branch.Player1;
            var source=Summon(owner,id); var ids=new[]{"TIME_850t","TIME_850t",id}; if(reverse) Array.Reverse(ids);
            var held=ids.Select(x=>Add(owner,x)).ToArray(); branch.Random.SetSeed(g.Seed);
            Kill(branch,source);
            Core2026Proof.Check(held[(int)g.Seed-1].Zone==owner.BoardZone && held.Count(x=>x.Zone==owner.HandZone)==2,"seeded reordered hand selection includes parent and does not deduplicate members");
        }
        foreach(bool reverse in new[]{false,true})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed,CardClass.WARRIOR); var owner=branch.Player1;
            var source=Summon(owner,id); var held=(Minion)Add(owner,"TIME_850t");
            var a=Summon(owner.Opponent,"CS2_182"); var b=Summon(owner.Opponent,"CS2_200"); a.BaseHealth=30; b.BaseHealth=30;
            if(reverse) { owner.Opponent.BoardZone.Remove(a); owner.Opponent.BoardZone.Add(a); }
            var targets=new TaskStack(); new IncludeTask(EntityType.ENEMIES).Process(branch,owner,source,null,targets);
            branch.Random.SetSeed(g.Seed); var probe=branch.CloneExact(); var pick=new TaskStack{Playables=targets.Playables.ToArray()};
            new RandomTask(1,EntityType.STACK).Process(probe,probe.Player1,probe.IdEntityDic[source.Id],null,pick);
            int selected=pick.Playables.Single().Id; Kill(branch,source);
            Core2026Proof.Check(targets.Playables.Cast<ICharacter>().All(e=>e.Damage==(e.Id==selected?12:0))
                && held.NumAttacksThisTurn==0,"seeded forced enemy target, reordered board; forced attack keeps normal budget");
        }
        var full=DeathKnightC1Proof.NewGame(g.Seed,CardClass.WARRIOR); var fp=full.Player1;
        var living=Summon(fp,id); var waiting=Add(fp,"TIME_850t"); LocationProofs.Location(fp);
        while(!fp.BoardZone.IsFull) Summon(fp,"CS2_182");
        living.Power.DeathrattleTask.Process(full,fp,living,null,new TaskStack());
        Core2026Proof.Check(waiting.Zone==fp.HandZone && fp.BoardZone.SlotCount==7,"living full-board Deathrattle leaves held member untouched");
        Kill(full,living);
        Core2026Proof.Check(waiting.Zone==fp.BoardZone && fp.BoardZone.SlotCount==7,"real death frees exactly one summon slot");
        var copied=Generic.Copy(p,p.Hero,Add(p,id),Zone.HAND);
        var fork=g.CloneExact(history:true); var dead=Summon(fork.Player1,id); Kill(fork,dead);
        Core2026Proof.Check(copied.Zone==p.HandZone && fork.Player1.BoardZone.Single().Card.Id==id,"copied parent qualifies, clone hand movement isolated");
        foreach(var e in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(e));
        var body=Summon(p,id); var card=Add(p,"TIME_850t"); body.Silence(); Kill(g,body);
        Core2026Proof.Check(card.Zone==p.HandZone,"silenced Deathrattle does not consume held member");
        body=Summon(p,id); p.HandZone.Remove(card); p.SetasideZone.Add(card); Kill(g,body);
        Core2026Proof.Check(p.BoardZone.IsEmpty,"no family member fizzles");
    }
}
