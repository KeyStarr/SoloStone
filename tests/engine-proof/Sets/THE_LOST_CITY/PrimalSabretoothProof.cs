using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrimalSabretoothProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_247", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasStealth && source.IsRace(Race.BEAST), "printed Stealth Beast");
        var victim = Summon(p.Opponent, "CS2_171"); var clone = g.CloneExact(history: true);
        Attack(clone, (Minion)clone.IdEntityDic[source.Id], (Minion)clone.IdEntityDic[victim.Id]);
        Core2026Proof.Check(clone.Player1.HandZone.Count == 1 && p.HandZone.Count == 0 && victim.Zone == p.Opponent.BoardZone, "clone kill and copy isolated");
        Attack(g, source, victim); var copy = p.HandZone.Single();
        Core2026Proof.Check(copy.Card.Id == "CS2_171" && copy.Id != victim.Id && ((Minion)copy).AttackDamage == 1 && ((Minion)copy).Health == 1, "killed minion copied to hand");
        End(g); End(g); Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.HandZone.Count == 2, "hero attack gives no copy");
        End(g); End(g); var tough = Summon(p.Opponent, "CS2_200"); Attack(g, source, tough); Core2026Proof.Check(p.HandZone.Count == 3 && tough.Zone == p.Opponent.BoardZone, "nonlethal attack gives no copy even when source dies");
        ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); victim = Summon(p.Opponent, "CS2_171");
        int held = p.HandZone.Count; Attack(g, source, victim); Core2026Proof.Check(p.HandZone.Count == held, "Silence removes kill-copy trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); victim = Summon(p.Opponent, "CS2_171");
        Attack(g, source, victim); Core2026Proof.Check(p.HandZone.Count == 10, "full hand receives no copy");
    });
}
