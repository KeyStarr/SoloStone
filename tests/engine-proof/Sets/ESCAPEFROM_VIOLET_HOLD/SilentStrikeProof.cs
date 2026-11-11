using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SilentStrikeProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_001", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var ordinary = Summon(p, "EX1_008"); Play(g, p, id, ordinary);
        Core2026Proof.Check(ordinary.AttackDamage == 4 && enemy.Damage == 0 && ordinary.AppliedEnchantments.Any(e => e.Card.Id == "CAP_001e"), "ordinary target gets exact +3 Attack without damage");
        var hidden = Summon(p, "EX1_008"); hidden.HasStealth = true; Play(g, p, id, hidden);
        Core2026Proof.Check(hidden.AttackDamage == 4 && enemy.Damage == 4 && hidden.HasStealth, "stealthed target deals post-buff Attack without attacking");
        hidden.Silence(); Core2026Proof.Check(hidden.AttackDamage == 1, "silence removes Attack buff");
        ClearBoard(g, p.Opponent); hidden = Summon(p, "EX1_008"); hidden.HasStealth = true; Play(g, p, id, hidden);
        Core2026Proof.Check(hidden.AttackDamage == 4 && p.Opponent.Hero.Damage == 0, "no enemy minions means buff only");
        var one = Summon(p.Opponent, "CS2_200"); var two = Summon(p.Opponent, "CS2_200");
        hidden = Summon(p, "EX1_008"); hidden.HasStealth = true; Summon(p, "EX1_012"); Play(g, p, id, hidden);
        Core2026Proof.Check(hidden.AttackDamage == 4 && new[] { one, two }.All(m => m.Zone.Type == Zone.PLAY)
            && new[] { one.Damage, two.Damage }.OrderBy(d => d).SequenceEqual(new[] { 0, 5 }), "one random surviving enemy takes post-buff 4 Attack plus 1 Spell Damage");
        TargetRules(g, p, id, one, ordinary, false);
        ClearBoard(g, p); ClearBoard(g, p.Opponent); var card = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == card.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "spell needs a minion target");
    });
}
