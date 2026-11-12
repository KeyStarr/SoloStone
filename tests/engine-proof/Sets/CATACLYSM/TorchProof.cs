using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TorchProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_585", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 1;
        var healthy = Summon(p, "CS2_182"); var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length == 1 && options[0].Target?.Id == target.Id && spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8, "only damaged minions targetable; initial displayed damage eight");
        PlayEntity(g, p, spell, target);
        var returned = p.HandZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && returned[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && returned.Cost == 1, "eight damage kills four Health and returns four excess with visible number");
        var clone = g.CloneExact(history: true); var cloneTarget = Summon(clone.Player1.Opponent, "CS2_120"); cloneTarget.Damage = 1;
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[returned.Id], cloneTarget);
        Core2026Proof.Check(clone.Player1.HandZone.Single(m => m.Card.Id == id)[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && returned[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "returned damage evolves independently in clone");
        target = Summon(p, "CS2_182"); target.Damage = 1; PlayEntity(g, p, returned, target);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && !p.HandZone.Any(m => m.Card.Id == id), "exact lethal produces no excess or return");
        target = Summon(p.Opponent, "EX1_563"); target.Damage = 1; Play(g, p, id, target);
        Core2026Proof.Check(target.Health == 3 && !p.HandZone.Any(m => m.Card.Id == id), "nonlethal damage produces no return");
        target = Summon(p.Opponent, "CS2_182"); target.Damage = 1; target.HasDivineShield = true; Play(g, p, id, target);
        Core2026Proof.Check(target.Health == 4 && !target.HasDivineShield && !p.HandZone.Any(m => m.Card.Id == id), "absorbed damage produces no excess");
        healthy.Damage = 1; healthy.IsImmune = true; Play(g, p, id, healthy);
        Core2026Proof.Check(healthy.Health == 4 && !p.HandZone.Any(m => m.Card.Id == id), "Immune target takes no damage and creates no return");
        Summon(p, "CS2_052"); target = Summon(p.Opponent, "CS2_120"); target.Damage = 1; Play(g, p, id, target);
        returned = p.HandZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(returned[GameTag.TAG_SCRIPT_DATA_NUM_1] == 7, "spell damage contributes to excess");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        target = Summon(p.Opponent, "CS2_120"); target.Damage = 1; Play(g, p, id, target);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone[9].Card.Id == id, "return uses vacated hand slot");
    });
}
