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

internal static class B14K4BEProof
{
    internal static void Run() => StandardSetsProof.Case("BE_561", Forge);
    // locations/loc-last-charge-clear.yaml (A, PINNED-RULE-ONLY); position/cooldown use existing location contract.
    private static void Forge(Game g, Controller p, string id)
    {
        foreach(int armor in new[]{0,2,3,7})
        {
            var branch=g.CloneExact(history:true); var owner=branch.Player1;
            var left=Summon(owner,"CS2_182"); var forge=LocationProofs.Play(owner,id,1); var right=Summon(owner,"CS2_182");
            owner.Hero.Armor=armor; LocationProofs.Use(forge);
            var token=owner.BoardZone.Single(m=>m.Card.Id=="BE_561t");
            Core2026Proof.Check(token.AttackDamage==(armor>=3?6:3) && token.Health==(armor>=3?6:3) && token.HasTaunt,"exact Golem stats, optional Armor threshold");
            Core2026Proof.Check(owner.Hero.Armor==armor-(armor>=3?3:0),"spends exactly three Armor when affordable");
            Core2026Proof.Check(owner.BoardZone.Slots.Select(m=>m.Id).SequenceEqual(new[]{left.Id,forge.Id,token.Id,right.Id}),"summon immediately right of physical location");
            Core2026Proof.Check(!LocationRules.CanActivate(owner,forge,null),"used location closed");
            var clone=branch.CloneExact(history:true);
            End(branch); End(branch); End(branch); End(branch);
            Core2026Proof.Check(LocationRules.CanActivate(owner,forge,null),"normal cooldown reopens location");
            while(!owner.BoardZone.IsFull) Summon(owner,"CS2_182");
            owner.Hero.Armor=3; LocationProofs.Use(forge);
            Core2026Proof.Check(forge.Zone==owner.GraveyardZone && owner.BoardZone.SlotCount==7 && owner.BoardZone.Count(m=>m.Card.Id=="BE_561t")==2 && owner.Hero.Armor==0,"final charge clears own slot first on full board");
            Core2026Proof.Check(clone.Player1.BoardZone.SlotCount==4,"clone preserves independent location state");
            token.Silence(); Core2026Proof.Check(token.AttackDamage==3 && token.Health==3 && !token.HasTaunt,"Golem buff and Taunt silence normally");
        }
        var blocked=LocationProofs.Play(p,id); while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); p.Hero.Armor=3;
        Core2026Proof.Check(!LocationRules.CanActivate(p,blocked,null) && p.Hero.Armor==3 && blocked.Durability==2,"nonfinal full board rejects without Armor payment");
    }
}
