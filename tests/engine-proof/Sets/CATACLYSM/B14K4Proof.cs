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

internal static class B14K4CataclysmProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_586", Blaze);
    private static void Blaze(Game g, Controller p, string id)
    {
        var location=LocationProofs.Location(p); var left=Summon(p,"CS2_182"); var body=PlayAt(g,p,id,2); var right=Summon(p,"CS2_182");
        Generic.DamageCharFunc(p.Opponent.Hero,body,1,false); g.DeathProcessingAndAuraUpdate();
        var child=p.BoardZone.Single(m=>m.Card.Id==id && m.Id!=body.Id);
        Core2026Proof.Check(child.AttackDamage==3 && child.Health==3 && child.HasDeathrattle && child.IsRace(Race.ELEMENTAL),"fresh 3/3 elemental, no copied damage");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m=>m.Id).SequenceEqual(new[]{location.Id,left.Id,body.Id,child.Id,right.Id}),"survivor summons beside living source");
        var clone=g.CloneExact(history:true); var cloneBody=(Minion)clone.IdEntityDic[body.Id];
        Generic.DamageCharFunc(clone.Player2.Hero,cloneBody,1,false); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.BoardZone.Count(m=>m.Card.Id==id)==3 && p.BoardZone.Count(m=>m.Card.Id==id)==2,"cloned trigger independent");
        Generic.DamageCharFunc(p.Hero,left,1,false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id==id)==2,"other friendly damage does not trigger");
        body.Silence(); Generic.DamageCharFunc(p.Opponent.Hero,body,1,false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id==id)==2,"Silence suppresses survival trigger");
        int face=p.Opponent.Hero.Damage; Kill(g,body); Core2026Proof.Check(p.Opponent.Hero.Damage==face,"Silence suppresses Deathrattle");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182");
        Generic.DamageCharFunc(p.Opponent.Hero,child,1,false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount==7,"survival summon respects mixed full board");
        foreach(bool reverse in new[]{false,true})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed); var owner=branch.Player1;
            var enemies=new[]{Summon(owner.Opponent,"CS2_182"),Summon(owner.Opponent,"CS2_200")};
            if(reverse) { owner.Opponent.BoardZone.Remove(enemies[0]); owner.Opponent.BoardZone.Add(enemies[0]); }
            var victim=Summon(owner,id); var targets=new TaskStack(); new IncludeTask(EntityType.ENEMIES).Process(branch,owner,victim,null,targets);
            branch.Random.SetSeed(g.Seed); var expected=branch.CloneExact(); var es=new TaskStack{Playables=targets.Playables.ToArray()};
            new RandomTask(1,EntityType.STACK).Process(expected,expected.Player1,expected.IdEntityDic[victim.Id],null,es);
            int chosen=es.Playables.Single().Id; Kill(branch,victim);
            Core2026Proof.Check(targets.Playables.Cast<ICharacter>().All(e=>e.Damage==(e.Id==chosen?2:0)),"seeded Deathrattle target follows reordered enemies and can hit hero");
            Core2026Proof.Check(owner.BoardZone.IsEmpty,"lethal does not create survivor copy");
        }
    }
}
