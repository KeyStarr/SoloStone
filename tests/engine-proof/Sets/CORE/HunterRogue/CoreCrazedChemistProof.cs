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
internal static class CoreCrazedChemistProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BOT_576", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(friend.AttackDamage == 4 && enemy.AttackDamage == 4, "first play without Combo grants no buff");
        var source = Add(p, id); var options = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == friend) && options.All(t => t.Target is Minion m && m.Controller == p && m != source), "Combo targets friendly existing minions only, never heroes/enemies/self");
        PlayEntity(g, p, source, friend); Core2026Proof.Check(friend.AttackDamage == 8 && friend.Health == 5 && enemy.AttackDamage == 4
            && friend.AppliedEnchantments.Any(e => e.Card.Id == "BOT_576e"), "Combo grants permanent +4 Attack with pinned enchantment");
        Kill(g, (Minion)source); End(g); End(g); Core2026Proof.Check(friend.AttackDamage == 8, "buff survives source departure and turn end");
        Play(g, p, "CORE_SW_066", friend); Core2026Proof.Check(friend.AttackDamage == 4, "Silence removes buff");
        ParkBoard(p); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "Combo with no other friendly target still plays");
    }, legacyPoolMember: true);
}
