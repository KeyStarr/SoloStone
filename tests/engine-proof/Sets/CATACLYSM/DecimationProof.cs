using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_DecimationProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_581", (g, p, id) => {
        // cost-sampling/decimation-snapshot.yaml: owner-approved D/C assumption, one resolution-time snapshot.
        Play(g, p, id); Core2026Proof.Check(p.Hero.Health == 30 && p.Opponent.Hero.Health == 30, "empty board excludes heroes");
        LocationProofs.Location(p);
        var dormant = Summon(p, "CORE_BT_156");
        var held = Add(p, id);
        var a = Summon(p, "CS2_182"); var b = Summon(p.Opponent, "CS2_182");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        var golem = Summon(p.Opponent, "EX1_556");
        PlayEntity(g, p, held);
        Core2026Proof.Check(a.Health == 0 && b.Health == 0 && shield.Health == 1 && !shield.HasDivineShield, "four active minions each receive five; shield absorbs batch");
        Core2026Proof.Check(dormant.Damage == 0 && p.BoardZone.GetLocations().Length == 1, "Dormant and location excluded from count and damage");
        var replacement = p.Opponent.BoardZone.Single(m => m.Card.Id == "skele21");
        Core2026Proof.Check(replacement.Health == 1 && golem.Zone == p.Opponent.GraveyardZone, "Deathrattle summon comes after batch, never takes shrinking damage");
        ParkBoard(p); ParkBoard(p.Opponent);
        a = Summon(p.Opponent, "CS2_182"); a.BaseHealth = 20;
        var power = Summon(p, "CS2_052");
        held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "hand counter tracks active minions");
        PlayEntity(g, p, held);
        Core2026Proof.Check(a.Damage == 4 && power.Damage == 4 && power.Zone == p.GraveyardZone, $"base one plus two minions plus one Spell Damage; visible base counter: a={a.Damage}, power={power.Damage}, tag={held[GameTag.TAG_SCRIPT_DATA_NUM_1]}");
    });
}
