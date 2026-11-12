using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class BloodTapProof
{
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_712", (g, p, id) => {
        var minion = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var board = Summon(p, "CS2_182");
        CorpseRules.Gain(p, 1); Play(g, p, id);
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 6 && CorpseRules.Available(p) == 1
            && board.AttackDamage == 4 && (spell.AppliedEnchantments == null || spell.AppliedEnchantments.Count == 0), "insufficient corpses keeps +1/+1 base; hand minions only");
        CorpseRules.Gain(p, 1);
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(minion.AttackDamage == 7 && minion.Health == 8 && CorpseRules.Available(p) == 0
            && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2 && minion.AppliedEnchantments.All(e => e.Card.Id == "RLK_712e"), "exact spend two adds another +1/+1");
        PlayEntity(g, p, minion); minion.Silence();
        Core2026Proof.Check(minion.AttackDamage == 4 && minion.Health == 5, "hand buff persists on play and Silence removes it");
        EmptyHand(p); CorpseRules.Gain(p, 2); LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(CorpseRules.Available(p) == 0 && p.BoardZone.SlotCount == 7, "empty minion hand still spends on mixed full board");
    });
}
