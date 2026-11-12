using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static StandardSetsProof;
internal static class CyborgPatriarchProof
{
    internal static void Run() => Case("TIME_046", (g, p, id) => {
        var m = Core2026Proof.Summon(p, id);
        Core2026Proof.Check(m.AttackDamage == 3 && m.BaseHealth == 12 && m.HasTaunt && m.IsDormant, "3/12 Taunt initially Dormant");
        Core2026Proof.Check(m.AppliedEnchantments.Single().Card.AssetId == 120858 && m[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "Unpinned_DormantCounterProjection exact Prime Ape helper and countdown");
        var enemy = Core2026Proof.Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(!p.BoardZone.GetAll().Contains(m) && !enemy.ValidAttackTargets.Contains(m), "Unpinned_DormantExclusions sleeping Taunt excluded");
        DormantRules.Accelerate(m, 3);
        Core2026Proof.Check(!m.IsDormant && m.HasTaunt && m.AppliedEnchantments.Count == 0 && m.IsExhausted,
            "Unpinned_DormantClock Unpinned_DormantLifecycle awakens without stat loss or attack");
        Core2026Proof.Check(DormantRules.Make(m, 1, "TIME_046e", p.Hero), "Unpinned_DormantReplace external sleep after awakening");
    });
}
