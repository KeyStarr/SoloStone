using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HumbleJudgmentProof
{
    internal static void Run() => StandardSetsProof.Case("BE_154", (g, p, id) => {
        foreach (var e in p.DeckZone.ToArray()) { p.DeckZone.Remove(e); p.SetasideZone.Add(e); }
        var first = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_008"), zone: p.DeckZone);
        var last = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        var held = (Minion)Add(p, "CS2_182"); var enemy = (Minion)Add(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(first.Zone == p.HandZone && last.Zone == p.HandZone && spell.Zone == p.HandZone && p.DeckZone.Count == 0, "draws exactly three cards");
        Core2026Proof.Check(first.AttackDamage == 2 && first.Health == 2 && last.AttackDamage == 2 && last.Health == 2, "drawn minions set to 2/2 regardless of original stats");
        Core2026Proof.Check(held.AttackDamage == 4 && held.Health == 5 && enemy.Health == 5 && spell.Cost == 0, "existing hand, opponent and drawn spell unchanged");
        PlayEntity(g, p, first); Core2026Proof.Check(first.AttackDamage == 2 && first.Health == 2, "2/2 persists on play");
        first.Silence(); Core2026Proof.Check(first.AttackDamage == 4 && first.Health == 5, "Silence removes Humbled");
        int damage = p.Hero.Damage;
        Play(g, p, id); Core2026Proof.Check(p.Hero.Damage == damage + 6, "empty deck attempts all three draws for fatigue 1+2+3");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var burned = new[] { "CS2_182", "CS2_171", "CS2_182" }.Select(cid => Entity.FromCard(p, Cards.FromId(cid), zone: p.DeckZone)).ToArray();
        // Resolve the spell directly so the full hand remains full throughout all draws.
        Cards.FromId(id).Power.PowerTask.Process(g, p, p.Hero, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 0 && burned.All(e => e.Zone.Type == Zone.GRAVEYARD), "full hand burns draws without replacing existing cards");
    });
}
