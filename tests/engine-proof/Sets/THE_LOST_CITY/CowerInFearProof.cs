using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CowerInFearProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_823", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false);
        var beast = Add(p, "CORE_LOE_039"); var other = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && beast.Cost == 1 && beast[GameTag.COST] == 1 && other.Cost == 4 && spell.Cost == 4, $"damage and visible secondary-Beast discount; other cards unaffected: damage={enemy.Damage}, beast={beast.Cost}/{beast[GameTag.COST]}, other={other.Cost}, spell={spell.Cost}");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[beast.Id]);
        Core2026Proof.Check(beast.Zone == p.HandZone && beast.Cost == 1, "clone consumption isolated");
        PlayEntity(g, p, other); Core2026Proof.Check(beast.Cost == 1, "non-Beast minion does not consume");
        var added = Add(p, "CS2_171"); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(added.Cost == 0, "later Beast gets clamped discount");
        PlayEntity(g, p, beast); Core2026Proof.Check(added.Cost == 1, "first Beast consumes discount for all others");
        Play(g, p, id, friend); Core2026Proof.Check(friend.Damage == 3 && added.Cost == 0, "friendly target works and second cast installs next discount");
        End(g); Core2026Proof.Check(added.Cost == 1 && added[GameTag.COST] == 1, "unused cost aura expires at end of turn");
        End(g); ClearBoard(g, p); Summon(p, "EX1_012"); enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 4, "Spell Damage applies");
    });
}
