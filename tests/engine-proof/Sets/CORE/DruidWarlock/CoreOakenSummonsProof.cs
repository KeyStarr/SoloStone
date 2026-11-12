using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreOakenSummonsProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_309", (g, p, id) => {
        PoolPilotProof.ClassDeck(p);
        var exact=Entity.FromCard(p,Cards.FromId("CS2_182")); p.DeckZone.Add(exact);
        var expensive=Entity.FromCard(p,Cards.FromId("EX1_116")); p.DeckZone.Add(expensive);
        var spell=Entity.FromCard(p,Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        var location=LocationProofs.Location(p); var left=Summon(p,"CS2_182");
        Play(g,p,id);
        Core2026Proof.Check(p.Hero.Armor==6 && p.Opponent.Hero.Armor==0 && p.DeckZone.Count==2 && exact.Zone==p.BoardZone && p.DeckZone.Contains(expensive) && p.DeckZone.Contains(spell),"six Armor and exact eligible four-cost deck entity; spells and cost five excluded");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m=>m.Id).SequenceEqual(new[]{location.Id,left.Id,exact.Id}),"spell summon appends after physical slots");
        Play(g,p,id); Core2026Proof.Check(p.Hero.Armor==12 && p.DeckZone.Count==2 && p.BoardZone.Count==2,"no eligible minion still grants Armor");
        var imp=Entity.FromCard(p,Cards.FromId("EX1_319")); p.DeckZone.Add(imp);
        PoolPilotProof.Resume(g,next=>{ Play(next,next.Player1,id); Core2026Proof.Check(next.IdEntityDic[imp.Id].Zone==next.Player1.BoardZone && next.Player1.Hero.Damage==0 && next.Player1.Hero.Armor==18,"deck summon skips battlecry and survives clone/load"); });
        var first=Entity.FromCard(p,Cards.FromId("CS2_182")); p.DeckZone.Add(first);
        var second=Entity.FromCard(p,Cards.FromId("EX1_008")); p.DeckZone.Add(second);
        long draws=g.Random.DrawCount;
        PoolPilotProof.Resume(g,next=>{
            Play(next,next.Player1,id);
            Core2026Proof.Check(next.Player1.BoardZone.Count(m=>m.Id==first.Id || m.Id==second.Id)==1
                && next.Player1.DeckZone.Count(c=>c.Id==first.Id || c.Id==second.Id)==1 && next.Random.DrawCount>draws,
                "one of two eligible deck entities selected using cloned game RNG");
        });
        exact=Entity.FromCard(p,Cards.FromId("CS2_182")); p.DeckZone.Add(exact);
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); Play(g,p,id);
        Core2026Proof.Check(p.Hero.Armor==30 && exact.Zone==p.DeckZone && p.BoardZone.SlotCount==7,"full physical board leaves deck minion untouched but grants Armor");
        ParkBoard(p); PoolPilotProof.ClassDeck(p); Play(g,p,id);
        Core2026Proof.Check(p.Hero.Armor==36 && p.BoardZone.Count==0 && p.Hero.Fatigue==0,"empty deck grants Armor without fatigue");
    }, legacyPoolMember: true);
}
