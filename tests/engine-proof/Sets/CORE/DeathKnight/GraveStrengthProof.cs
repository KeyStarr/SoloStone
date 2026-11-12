using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class GraveStrengthProof
{
    internal static void Run() => DeathKnightC1Proof.Case("RLK_707", (g, p, id) => {
        var minion = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var hand = (Minion)Add(p, "CS2_182");
        CorpseRules.Gain(p, 4); Play(g, p, id);
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 5 && hand.AttackDamage == 4
            && enemy.AttackDamage == 4 && CorpseRules.Available(p) == 4, "insufficient corpses: +1 permanent attack, board friendlies only");
        CorpseRules.Gain(p, 1); Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(minion.AttackDamage == 8 && CorpseRules.Available(p) == 0
            && minion.AppliedEnchantments.Select(e => e.Card.Id).SequenceEqual(new[] { "RLK_707e", "RLK_707e2" }), "spend five gives +3 instead, exact export enchantments");
        End(g); End(g); Core2026Proof.Check(minion.AttackDamage == 8, "buff survives turns");
        minion.Silence(); Core2026Proof.Check(minion.AttackDamage == 4 && minion.Health == 5, "Silence removes both buffs");
        EmptyHand(p); StandardSetsProof.ClearBoard(g, p); CorpseRules.Gain(p, 5 - CorpseRules.Available(p)); Play(g, p, id);
        Core2026Proof.Check(CorpseRules.Available(p) == 0, "empty board still spends five");
        FullBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.GetAll().All(m => m.AttackDamage == 5) && p.BoardZone.SlotCount == 7, "mixed full board buffs minions, ignores location");
    });
}
