using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WingedAberrationProof
{
    internal static void Run() => StandardSetsProof.Case("END_032", (g, p, id) => {
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.IsRush && !first.HasWindfury && !first.IsImmune && p.OverloadOwed == 0, "no Combo gives printed Rush only");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasWindfury && source.IsImmune && source.IsRush && p.OverloadOwed == 2 && p.OverloadThisGame == 2, "Combo grants Windfury, temporary Immune, Overload");
        Core2026Proof.Check(source.AppliedEnchantments.Any(e => e.Card.Id == "END_032e"), "exact Immune enchantment");
        var enemy = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target is Hero), "Rush cannot attack heroes before first attack");
        var attack = p.Options().SingleOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id);
        Core2026Proof.Check(attack != null && g.Process(attack), "first untouched Rush attack accepted");
        Core2026Proof.Check(source.Damage == 0 && enemy.Damage == 4 && source.NumAttacksThisTurn == 1, "Immune protects first Rush attack");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target is Hero), "Rush cannot attack heroes between attacks");
        attack = p.Options().SingleOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id);
        Core2026Proof.Check(attack != null && g.Process(attack), "second untouched Windfury attack accepted");
        Core2026Proof.Check(source.Damage == 0 && enemy.Zone == p.Opponent.GraveyardZone && source.NumAttacksThisTurn == 2, "Windfury allows second protected attack");
        Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id), "third attack forbidden with enemy minion and hero available");
        var clone = g.CloneExact(history: true); End(clone); Core2026Proof.Check(!((Minion)clone.IdEntityDic[source.Id]).IsImmune && source.IsImmune, "clone expiry independent");
        End(g); Core2026Proof.Check(!source.IsImmune && source.HasWindfury, "only Immune expires");
        End(g); Core2026Proof.Check(p.OverloadLocked == 2, "Combo Overload locks next turn");
        source.Silence(); Core2026Proof.Check(!source.HasWindfury && !source.IsImmune, "Silence removes granted keywords");
        Play(g, p, "GAME_005"); var temporary = (Minion)Play(g, p, id); temporary.Silence();
        Core2026Proof.Check(!temporary.IsImmune && !temporary.HasWindfury, "Silence removes unexpired Immune and Windfury");
    });
}
