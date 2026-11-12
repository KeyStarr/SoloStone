using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ClockworkBoarsProof
{
    internal static void Run() => StandardSetsProof.Case("BE_730", (g, p, id) => {
        var left = Summon(p, "CS2_182");
        Play(g, p, id);
        var boars = p.BoardZone.Where(m => m.Card.Id == "BE_730t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(boars.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(boars.Select(m => m.Id))),
            "spell appends two Boars in creation order");
        Core2026Proof.Check(boars.All(m => m.Card.AssetId == 130864 && m.Card.Implemented && m.Cost == 1 && m.AttackDamage == 1 && m.Health == 1
            && m.HasCharge && m.IsRace(Race.BEAST) && m.IsRace(Race.MECHANICAL) && m.Card.Races.Count == 2),
            "pinned 1/1 Charge tokens retain both types");
        foreach (var boar in boars)
            Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == boar.Id && t.Target?.Id == p.Opponent.Hero.Id),
                "printed Charge allows immediate hero attack");
        Core2026Proof.Check(g.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == boars[0].Id && t.Target?.Id == p.Opponent.Hero.Id)), "Boar attacks without readiness override");
        Play(g, p, "DS1_070", boars[1]);
        Play(g, p, "GVG_055", boars[1]);
        Core2026Proof.Check(boars[1].AttackDamage == 5 && boars[1].Health == 5 && boars[1].HasTaunt, "Beast and Mech targeting both accept generated Boar");
        boars[1].Silence();
        Core2026Proof.Check(boars[1].IsRace(Race.BEAST) && boars[1].IsRace(Race.MECHANICAL) && boars[1].AttackDamage == 1 && boars[1].Health == 1,
            "Silence removes buffs but retains both printed types");
        ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "BE_730t") == 1, "one space summons one Boar");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "BE_730t") == 1, "full board summons none");
    });
}
