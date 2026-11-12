using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SundancerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_152", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player1.HandZone.Count(e => e.Card.Id == "BE_152t") == 1 && p.HandZone.Count == 0, "clone end trigger isolated");
        End(g); var blessing = p.HandZone.Single(e => e.Card.Id == "BE_152t");
        Core2026Proof.Check(blessing.Card.Implemented && blessing.Cost == 2 && blessing.Card.Text == "Give a minion +2/+3.", "exact two-Cost Blessing generated at own end");
        End(g); Core2026Proof.Check(p.HandZone.Count(e => e.Card.Id == "BE_152t") == 1, "opponent end does not trigger");
        var target = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, "BE_152t", enemy, target, false);
        PlayEntity(g, p, blessing, target); Core2026Proof.Check(target.AttackDamage == 6 && target.Health == 8 && target.AppliedEnchantments.Single().Card.Id == "BE_152e", "Blessing gives +2/+3");
        target.Silence(); Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5, "Silence removes Blessing stats");
        source.Silence(); End(g); Core2026Proof.Check(p.HandZone.Count(e => e.Card.Id == "BE_152t") == 1, "Silence removes end trigger"); End(g);
        var gone = Summon(p, id); Kill(g, gone); End(g); Core2026Proof.Check(p.HandZone.Count(e => e.Card.Id == "BE_152t") == 1, "dead source does not trigger"); End(g);
        Summon(p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        End(g); Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(e => e.Card.Id == "BE_152t") == 1, "full hand cannot receive another Blessing");
    });
}
