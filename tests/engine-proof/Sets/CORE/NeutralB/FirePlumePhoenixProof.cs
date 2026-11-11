using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class FirePlumePhoenixProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_084", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = Add(p, id);
        var targets = p.Options().Where(t => t is PlayCardTask && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target?.Id == friend.Id) && targets.Any(t => t.Target?.Id == enemy.Id)
            && targets.Any(t => t.Target?.Id == p.Hero.Id) && targets.Any(t => t.Target?.Id == p.Opponent.Hero.Id)
            && targets.All(t => t.Target != null && t.Target.Id != source.Id), "Battlecry targets other friendly/enemy characters");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && friend.Damage == 0 && ((Minion)source).Health == 4
            && source.Card.IsRace(Race.ELEMENTAL) && source.Card.IsRace(Race.BEAST), "current 3 damage and dual-type 3/4");
        Play(g, p, id, friend); Core2026Proof.Check(friend.Damage == 3, "friendly minion takes three");
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "hero takes three");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "shield prevents Battlecry damage");
    }, legacyPoolMember: true);
}
