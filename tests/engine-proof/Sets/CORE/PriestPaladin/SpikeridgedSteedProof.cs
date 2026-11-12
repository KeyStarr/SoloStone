using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class SpikeridgedSteedProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_952", (g, p, id) => {
        Minion bearer=null; var board=DeathrattleSlotBoard(p,()=>bearer=Summon(p,"CS2_182")); Play(g,p,id,bearer);
        Core2026Proof.Check(bearer.AttackDamage==6 && bearer.Health==11 && bearer.HasTaunt && bearer.HasDeathrattle,"+2/+6 Taunt and granted Deathrattle");
        PoolPilotProof.Resume(g,next=>{
            Kill(next,(Minion)next.IdEntityDic[bearer.Id]); var owner=next.Player1; var token=owner.BoardZone.Single(m=>m.Card.Id=="UNG_810");
            Core2026Proof.Check(token.Card.AssetId==41324 && token.AttackDamage==2 && token.Health==6 && token.HasTaunt && token.IsRace(Race.BEAST),"exact 2/6 Taunt Stegodon");
            Core2026Proof.Check(owner.BoardZone.GetSlot(2).Id==token.Id && owner.BoardZone.GetSlot(3).Id==board.Right.Id,"granted Deathrattle uses bearer physical dying slot");
    });
        ParkBoard(p); bearer=Summon(p,"CS2_182"); Play(g,p,id,bearer); bearer.Silence();
        Core2026Proof.Check(bearer.AttackDamage==4 && bearer.Health==5 && !bearer.HasTaunt && !bearer.HasDeathrattle,"Silence removes all granted effects");
        Kill(g,bearer); Core2026Proof.Check(p.BoardZone.SlotCount==0,"silenced bearer summons nothing");
        bearer=Summon(p.Opponent,"CS2_182"); Play(g,p,id,bearer); Kill(g,bearer);
        Core2026Proof.Check(p.Opponent.BoardZone.Single().Card.Id=="UNG_810" && p.BoardZone.SlotCount==0,"enemy bearer summons for its controller");
        bearer=Summon(p,"CS2_182"); Play(g,p,id,bearer); while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); Kill(g,bearer);
        Core2026Proof.Check(p.BoardZone.SlotCount==7 && p.BoardZone.Count(m=>m.Card.Id=="UNG_810")==1,"full board uses only freed slot");
        End(g);
    }, legacyPoolMember: true);
}
