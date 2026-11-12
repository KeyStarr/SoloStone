using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FirstFlameProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_108", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false);
        var spellpower = Summon(p, "CS2_052");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3, "first damage uses Spell Damage");
        var second = p.HandZone.Single(c => c.Card.Id == "SW_108t");
        Core2026Proof.Check(second.Cost == 1 && second.Card.Implemented && second.Card.Text.Contains("$2"), "exact Second Flame identity/cost/text");
        var options = p.Options().Where(t => t.Source?.Id == second.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion) && options.Any(t => t.Target == friendly) && options.Any(t => t.Target == enemy), "Second Flame targets minions on either side");
        PlayEntity(g, p, second, friendly);
        Core2026Proof.Check(friendly.Damage == 3 && !p.HandZone.Any(c => c.Card.Id == "SW_108t"), "Second Flame uses Spell Damage and does not generate another");
        Kill(g, spellpower);
        var shield = Summon(p.Opponent, "EX1_008");
        Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && !shield.HasDivineShield && p.HandZone.Any(c => c.Card.Id == "SW_108t"), "prevented damage still grants Second Flame");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var first = p.HandZone.First(c => c.Card.Id == id); PlayEntity(g, p, first, friendly);
        Core2026Proof.Check(p.HandZone.Count == 10, "spell frees hand slot for Second Flame");
    });
}
