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

internal static class B14K4VioletHoldProof
{
    internal static void Run() => DeathKnightC1Proof.Case("JAIL_444", Sawbones);
    private static IPlayable PlaySawbones(Game g,Controller p,string id)
    {
        p.UsedMana=0; var card=Add(p,id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card)),"Sawbones play accepted"); return card;
    }
    private static void Sawbones(Game g, Controller p, string id)
    {
        var pristine=g.CloneExact(history:true); var location=LocationProofs.Location(p); var a=Summon(p,"CS2_182"); var b=Summon(p,"EX1_556"); var enemy=Summon(p.Opponent,"CS2_182");
        int deck=p.DeckZone.Count; var source=(Minion)PlaySawbones(g,p,id);
        Core2026Proof.Check(source.AttackDamage==5 && source.Health==3 && source[GameTag.PREPARE]==1,"printed Sawbones stats/Prepare");
        Core2026Proof.Check(a.Zone==p.GraveyardZone && b.Zone==p.GraveyardZone && enemy.Zone==p.Opponent.BoardZone && location.Zone==p.BoardZone,"destroys only other friendly minions");
        Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.Any(m=>m.Card.Id=="skele21") && source.Zone==p.BoardZone,"Deathrattle replacement survives original destruction batch");
        Core2026Proof.Check(p.DeckZone.Count==deck-2 && p.HandZone.Count==2 && p.UsedMana==3,"draws/refreshes once per destroyed minion");
        var empty=pristine.CloneExact(); var owner=empty.Player1; deck=owner.DeckZone.Count; PlaySawbones(empty,owner,id);
        Core2026Proof.Check(owner.DeckZone.Count==deck && owner.UsedMana==5,"no other minions no draw or refresh");
        var full=pristine.CloneExact(); owner=full.Player1; while(owner.HandZone.Count<9) Add(owner,"CS2_182");
        Summon(owner,"CS2_182"); Summon(owner,"CS2_182"); deck=owner.DeckZone.Count; PlaySawbones(full,owner,id);
        Core2026Proof.Check(owner.HandZone.Count==10 && owner.DeckZone.Count==deck-2 && owner.UsedMana==3,"full-hand burns still refresh Mana");
        var prepared=pristine.CloneExact(history:true); owner=prepared.Player1; var held=Add(owner,id);
        var option=owner.Options().Single(t=>t.PlayerTaskType==PlayerTaskType.PREPARE_CARD && t.Source?.Id==held.Id);
        Core2026Proof.Check(prepared.Process(option),"native Prepare action accepted"); End(prepared); End(prepared);
        Core2026Proof.Check(held[GameTag.PREPARED]==1 && held[GameTag.PREPARING]==0,"Prepare unlocks on next own turn");
        Summon(owner,"CS2_182"); owner.UsedMana=0; Core2026Proof.Check(prepared.Process(PlayCardTask.Any(owner,held)),"prepared Sawbones accepted");
        Core2026Proof.Check(owner.BoardZone.Single().Id==held.Id && owner.HandZone.Count==2,"prepared Battlecry uses same destroy/draw recipe");
    }
}
