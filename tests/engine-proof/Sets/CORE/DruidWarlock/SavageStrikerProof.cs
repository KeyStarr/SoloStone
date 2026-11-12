using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class SavageStrikerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TRL_240", (g, p, id) => {
        var enemy=Summon(p.Opponent,"CS2_182"); var friend=Summon(p,"CS2_182");
        var source=Add(p,id); p.UsedMana=0;
        Core2026Proof.Check(p.Options().Any(t=>t.Source?.Id==source.Id) && p.Options().Where(t=>t.Source?.Id==source.Id).All(t=>t.Target==null),"zero hero Attack allows untargeted body");
        PlayEntity(g,p,source); Core2026Proof.Check(enemy.Damage==0 && friend.Damage==0,"zero Attack deals no damage");
        p.Hero[GameTag.ATK]=3; Summon(p,"CS2_142"); source=Add(p,id); p.UsedMana=0;
        var options=p.Options().Where(t=>t.Source?.Id==source.Id).ToArray();
        Core2026Proof.Check(options.Length>0 && options.All(t=>t.Target?.Id==enemy.Id),"with Attack, only enemy minion is legal; friendly minions and heroes excluded");
        PlayEntity(g,p,source,enemy);
        Core2026Proof.Check(enemy.Damage==3 && friend.Damage==0 && p.Hero.Damage==0 && p.Opponent.Hero.Damage==0,"damage equals hero Attack, unaffected by Spell Damage");
        ParkBoard(p.Opponent); Play(g,p,id); Core2026Proof.Check(p.BoardZone.Count==5,"positive Attack with no enemy minion still allows body");
    }, legacyPoolMember: true);
}
