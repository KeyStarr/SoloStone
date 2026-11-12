using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RazidirProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_463", (g, p, id) => {
        var keep = Add(p.Opponent, "CS2_029"); var mine = Add(p, "CS2_029");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.DEMON) && source.IsRace(Race.BEAST) && mine.Zone == p.GraveyardZone && keep.Zone == p.Opponent.HandZone, "no Kindred discards own only card; both printed types");
        ClearBoard(g, p); Play(g, p, "CS2_171"); End(g); End(g);
        foreach (var owner in new[] { p, p.Opponent })
            foreach (var card in owner.HandZone.Where(card => card.Id != keep.Id).ToArray()) { owner.HandZone.Remove(card); owner.SetasideZone.Add(card); }
        mine = Add(p, "CS2_029"); var clone = g.Clone(); Play(clone, clone.Player1, id);
        Core2026Proof.Check(keep.Zone == p.Opponent.HandZone && clone.Player2.HandZone.Count == 0, "Kindred clone discard isolated");
        Play(g, p, id);
        Core2026Proof.Check(keep.Zone == p.Opponent.GraveyardZone && mine.Zone == p.HandZone, "previous-turn Beast discards opponent instead");
        Play(g, p, id); Core2026Proof.Check(mine.Zone == p.HandZone, "empty opponent hand never falls back to own hand");
        ClearBoard(g, p); End(g); End(g); End(g); End(g);
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        mine = Add(p, "CS2_029"); Play(g, p, "CS2_171");
        Play(g, p, id); Core2026Proof.Check(mine.Zone == p.GraveyardZone, "current-turn Beast does not activate Kindred");
        ClearBoard(g, p); Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "empty own hand is safe");
    });
}
