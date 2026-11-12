using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UnleashtheCrocolisksProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_873", (g, p, id) => {
        var left = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        var tokens = p.Opponent.BoardZone.Where(m => m.Card.Id == "TIME_873t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.Hero.Armor == 10 && p.BoardZone.Count == 0 && tokens.Length == 2 && tokens.All(m => m.AttackDamage == 2 && m.Health == 3 && m.IsRace(Race.BEAST) && m.Card.Implemented), "ten armor and two exact opponent 2/3 Beasts");
        Core2026Proof.Check(p.Opponent.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, tokens[0].Id, tokens[1].Id }), "spell summons append in creation order");
        ClearBoard(g, p.Opponent); for (int i = 0; i < 6; i++) Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 20 && p.Opponent.BoardZone.Count == 7 && p.Opponent.BoardZone.Count(m => m.Card.Id == "TIME_873t") == 1, "one board slot summons one token");
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 30 && p.Opponent.BoardZone.Count == 7, "full enemy board does not block armor");
    });
}
