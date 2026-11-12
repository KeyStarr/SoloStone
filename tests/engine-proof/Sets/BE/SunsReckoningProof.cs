using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SunsReckoningProof
{
    internal static void Run() => StandardSetsProof.Case("BE_153", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 1 && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0, "one damage only to enemy minions");
        var final = p.DeckZone.Single(e => e.Card.Id == "BE_153t");
        Core2026Proof.Check(final.Card.Implemented && final.Cost == 2 && final.HasLifeSteal && final.Card.Text.Contains("$3"), "exact Final Reckoning token shuffled");
        Generic.Draw(p, final); p.Hero.Damage = 10;
        Summon(p.Opponent, "CS2_182"); Summon(p, "EX1_012");
        PlayEntity(g, p, final);
        Core2026Proof.Check(enemy.Health == 0 && p.Hero.Damage == 2 && friendly.Damage == 0, "Final Reckoning deals three plus Spell Damage to each enemy and Lifesteals");
        Core2026Proof.Check(p.DeckZone.All(e => e.Card.Id != "BE_153t"), "Final Reckoning does not shuffle another copy");
        ClearBoard(g, p.Opponent);
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count(e => e.Card.Id == "BE_153t") == 1, "empty enemy board still shuffles");
    });
}
