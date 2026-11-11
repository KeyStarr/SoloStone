using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class PrimordialWyrmProof
{
    internal static void Run() => StandardSetsProof.Case("BE_255", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Health == 1, "does not trigger on its own play");
        Play(g, p, "CS2_182"); Core2026Proof.Check(source.Health == 2 && source.BaseHealth == 2, "friendly minion play adds one Health");
        Play(g, p, "CS2_008", p.Opponent.Hero); Summon(p, "CS2_182"); Core2026Proof.Check(source.Health == 2, "spell play and summon do not trigger");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_171");
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).Health == 3 && source.Health == 2, "cloned Health trigger isolated");
        End(g); Play(g, p.Opponent, "CS2_182"); Core2026Proof.Check(source.Health == 2, "opponent minion play does not trigger"); End(g);
        source.Damage = 1; Play(g, p, "CS2_171"); Core2026Proof.Check(source.Health == 2 && source.BaseHealth == 3 && source.Damage == 1, "permanent growth keeps existing damage across turns");
        source.Damage = 0; source.Silence(); Core2026Proof.Check(source.BaseHealth == 1, "Silence removes growth");
        Play(g, p, "CS2_171"); Core2026Proof.Check(source.BaseHealth == 1, "Silence removes trigger");
        Kill(g, source); Play(g, p, "CS2_171"); Core2026Proof.Check(source.BaseHealth == 1, "dead source no longer grows");
    });
}
