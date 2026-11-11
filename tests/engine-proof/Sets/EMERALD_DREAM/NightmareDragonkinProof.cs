using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class NightmareDragonkinProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_890", (g, p, id) => {
        var left = Add(p, "CS2_029"); var right = Add(p, "CS2_182");
        var source = (Minion)Play(g, p, id); Kill(g, source);
        Cost(left, 4); Cost(right, 2);
        CopyCost(right, 2); PublishedCost(right, 2);
        var clone = g.Clone(); var cloneSource = Summon(clone.Player1, id); Kill(clone, cloneSource);
        Cost(clone.IdEntityDic[right.Id], 0); Cost(right, 2); Cost(clone.IdEntityDic[left.Id], 4);
        source = Summon(p, id); Kill(g, source); Cost(right, 0);
        source = Summon(p, id); Kill(g, source); Cost(right, 0);
        var silenceRight = Add(p, "CS2_182"); Cost(silenceRight, 4);
        source = Summon(p, id); source.Silence(); Kill(g, source); Cost(silenceRight, 4);
        var unsilenced = Summon(p, id); Kill(g, unsilenced); Cost(silenceRight, 2);
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 0, "empty hand safe");
        var spell = Add(p, "CS2_029"); source = Summon(p, id); Kill(g, source); Cost(spell, 2);
        End(g); End(g); Cost(spell, 2); PlayEntity(g, p, spell, p.Opponent.Hero);
    });
}
