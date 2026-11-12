using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EyeBeamProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_801", (g, p, id) => {
        var left = Add(p, "CS2_182"); var beam = Add(p, id); var right = Add(p, "CS2_182"); g.AuraUpdate();
        Core2026Proof.Check(beam.HasLifeSteal && beam.Cost == 3 && beam[GameTag.COST] == 3, "middle card costs three; printed Lifesteal");
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        var options = p.Options().Where(t => t.Source?.Id == beam.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion) && options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == friend.Id), "minion targeting both sides; heroes excluded");
        var clone = g.Clone(); var cloneLeft = clone.IdEntityDic[left.Id]; clone.Player1.HandZone.Remove(cloneLeft); clone.Player1.SetasideZone.Add(cloneLeft); clone.AuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[beam.Id].Cost == 1 && beam.Cost == 3, "left edge discount and clone isolated");
        p.Hero.Damage = 10; PlayEntity(g, p, beam, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.Hero.Damage == 7, "non-Outcast pays three and heals dealt damage");
        beam = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(beam.Cost == 1 && beam[GameTag.COST] == 1, "right edge visible cost one");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true; PlayEntity(g, p, beam, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.Hero.Damage == 7, "shield prevents damage and healing");
        foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
        beam = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(beam.Cost == 1, "single card is Outcast");
        Add(p, "CS2_182"); g.AuraUpdate(); Core2026Proof.Check(beam.Cost == 1, "leftmost still discounted");
        Summon(p, "EX1_012"); PlayEntity(g, p, beam, friend);
        Core2026Proof.Check(friend.Damage == 4 && p.Hero.Damage == 3, "friendly target and Spell Damage scale Lifesteal");
    });
}
