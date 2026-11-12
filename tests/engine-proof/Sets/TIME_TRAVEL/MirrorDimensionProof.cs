using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MirrorDimensionProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_006", (g, p, id) => {
        var dragon = Summon(p, "TIME_056"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TIME_006t1") == 1, "board Dragon does not satisfy holding");
        ClearBoard(g, p); Add(p, "TIME_056"); var left = Summon(p, "CS2_182"); Play(g, p, id);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "TIME_006t1").ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 0 && m.Health == 4 && m.HasTaunt && m.Card.Implemented), "held Dragon gives two exact pinned tokens");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(tokens.OrderBy(m => m.Id).Select(m => m.Id))), "spell appends tokens in creation order");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TIME_006t1") == 1, "one free slot limits second summon");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7, "full board suppresses both summons");
    });
}
