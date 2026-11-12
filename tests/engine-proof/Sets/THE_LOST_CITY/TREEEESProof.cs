using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TREEEESProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_230", Unpinned_TREEEES);
    private static void Unpinned_TREEEES(Game g, Controller p, string id)
    {
        var target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 2; target.AttackDamage = 0;
        var spell = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], (ICharacter)next.IdEntityDic[target.Id]));
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.All(m => m.Card.Id == "TLC_230t" && m.AttackDamage == 2 && m.Health == 2
            && m.NumAttacksThisTurn == 0 && m.IsExhausted), "pinned Treants; forced attack preserves fresh ordinary budget");
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count() == 1, "Unpinned_TreesSummonAttack: unused Treants survive target death, no retarget");
        ForcedCardProof.Clean(g);
        target = Summon(p, "CS2_200"); target.BaseHealth = 40;
        while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_182");
        Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 4 && p.BoardZone.Count == 5,
            "Unpinned_TreesSummonAttack: two initial slots, both summons before attacks, no replacement after deaths; friendly target");
        ForcedCardProof.Clean(g);
        spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "no minions blocks required target");
    }
}
