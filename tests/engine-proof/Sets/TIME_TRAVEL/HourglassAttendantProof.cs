using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HourglassAttendantProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_100", (g, p, id) => {
        var m = (Minion)Play(g, p, id); Core2026Proof.Check(m.HasDivineShield, "printed Divine Shield");
        var hand = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_008"); var enemy = (Minion)Add(p.Opponent, "CS2_182"); var board = Summon(p, "CS2_182");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[hand.Id]).AttackDamage == 5 && hand.AttackDamage == 4, "clone end trigger isolated");
        End(g); Core2026Proof.Check(hand.AttackDamage == 5 && hand.Health == 6 && hand.AppliedEnchantments.Single().Card.Id == "TIME_100e", "own end buffs held minions");
        Core2026Proof.Check(enemy.AttackDamage == 4 && board.AttackDamage == 4 && spell.Cost == spell.Card.Cost, "excludes opponent hand, board and spells");
        End(g); Core2026Proof.Check(hand.AttackDamage == 5, "opponent end does not trigger");
        Play(g, p, "BE_228", m); End(g); Core2026Proof.Check(hand.AttackDamage == 5, "Silence removes end trigger; buffs persist"); End(g);
        var gone = Summon(p, id); Kill(g, gone); End(g); Core2026Proof.Check(hand.AttackDamage == 5, "dead source does not trigger");
    });
}
