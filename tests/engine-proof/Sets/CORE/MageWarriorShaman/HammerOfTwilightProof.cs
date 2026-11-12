using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class HammerOfTwilightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_OG_031", (g, p, id) => {
        var location = LocationProofs.Location(p); var anchor = Summon(p,"CS2_182"); Play(g,p,id);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 4 && p.Hero.Weapon.Durability == 2,"printed 4/2 weapon");
        PoolPilotProof.Resume(g,next=>{
            Play(next,next.Player1,"CS2_082"); var owner=next.Player1; var token=owner.BoardZone.Single(m=>m.Card.Id=="OG_031a");
            Core2026Proof.Check(token.Card.AssetId==38273 && token.AttackDamage==4 && token.Health==2 && token.IsRace(Race.ELEMENTAL),"exact Twilight Elemental 4/2");
            Core2026Proof.Check(owner.BoardZone.GetSlot(2).Id==token.Id && owner.BoardZone.GetSlot(0).Id==location.Id && owner.BoardZone.GetSlot(1).Id==anchor.Id,"weapon Deathrattle appends after mixed board");
    });
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); Play(g,p,id); Play(g,p,"CS2_082");
        Core2026Proof.Check(p.BoardZone.SlotCount==7 && p.BoardZone.Count(m=>m.Card.Id=="OG_031a")==1,"full board blocks additional Elemental");
    }, legacyPoolMember: true);
}
