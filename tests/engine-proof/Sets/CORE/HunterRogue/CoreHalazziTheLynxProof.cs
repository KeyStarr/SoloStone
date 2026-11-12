using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreHalazziTheLynxProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TRL_900", (g, p, id) => {
        var kept = Add(p, "CS2_182"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 2 && source[GameTag.RUSH] == 1, "current 4/2 Rush body");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Contains(kept) && p.HandZone.Count(c => c.Card.Id == "TRL_348t") == 9
            && p.HandZone.Where(c => c.Card.Id == "TRL_348t").All(c => c.Card.ATK == 1 && c.Card.Health == 1 && c.Card.Cost == 1 && c[GameTag.RUSH] == 1 && c.Card[(GameTag)1584] == 1), "fill only free hand slots with pinned 1/1 Rush Lynxes");
        var lynx = (Minion)p.HandZone.First(c => c.Card.Id == "TRL_348t"); var enemy = Summon(p.Opponent, "CS2_182");
        PlayEntity(g, p, lynx); Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source == lynx && t.Target == enemy)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source == lynx && t.Target == p.Opponent.Hero), "generated Lynx Rush attacks minions but not hero on summon turn");
        Attack(g, lynx, enemy);
        var second = Add(p, id); var before = p.HandZone.Where(c => c != second).Select(c => c.Id).ToHashSet();
        PlayEntity(g, p, second); Core2026Proof.Check(p.HandZone.Count == 10 && before.IsSubsetOf(p.HandZone.Select(c => c.Id)), "near-full hand preserves existing cards and fills remaining space");
    }, legacyPoolMember: true);
}
