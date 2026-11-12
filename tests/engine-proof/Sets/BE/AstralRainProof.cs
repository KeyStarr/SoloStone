using SabberStoneCore.Enums;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AstralRainProof
{
    internal static void Run() => StandardSetsProof.Case("BE_258", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 30;
        var friendly = Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 10 && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0, "five hits of two to sole enemy minion");
        Summon(p, "EX1_012"); Play(g, p, id); Core2026Proof.Check(enemy.Damage == 25, "Spell Damage applies to each of five hits");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var first = Summon(p.Opponent, "CS2_182"); first.BaseHealth = 2;
        var second = Summon(p.Opponent, "CS2_182"); second.BaseHealth = 8;
        var dormant = Summon(p.Opponent, "CS2_182"); dormant[GameTag.DORMANT] = 1;
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, id); Play(g, p, id);
        Core2026Proof.Check(first.Zone.Type == Zone.GRAVEYARD && second.Zone.Type == Zone.GRAVEYARD && dormant.Damage == 0, "each hit reselects living, non-Dormant enemies");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "seeded random selection survives cloning");
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && dormant.Damage == 0, "no eligible minions is harmless");
        var cinderfin = Summon(p.Opponent, "TLC_225"); Play(g, p, id);
        Core2026Proof.Check(cinderfin.Zone.Type == Zone.GRAVEYARD && p.Opponent.BoardZone.Count == 1
            && p.Opponent.GraveyardZone.Any(m => m.Card.Id == "TLC_249"), "deathrattle summon becomes eligible before later hits");
    });
}
