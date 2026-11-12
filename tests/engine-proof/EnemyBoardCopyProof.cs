using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class EnemyBoardCopyProof
{
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool silenced in new[] { false, true })
        foreach (string auraId in new[] { "CS2_122", "EX1_162" })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var original = Summon(p.Opponent, "CS2_122"); // Raid Leader: printed aura must stay off when silenced.
            if (silenced) { original.Silence(); g.DeathProcessingAndAuraUpdate(); }
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), original.Controller.Hero, original); // +2/+2
            original.Damage = 1; original.NumAttacksThisTurn = 1;
            original.IsExhausted = true;
            var enemyAura = Summon(p.Opponent, auraId);
            var neighbor = Summon(p, "CS2_182");
            var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY, preserveBoardMinionState: true);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.Id != original.Id && copy.Controller == p && copy.Zone == p.BoardZone
                && copy.Damage == 1 && copy.AttackDamage == 4 && copy.BaseHealth == 4
                && copy.NumAttacksThisTurn == 0 && copy.IsExhausted && copy.IsSilenced == silenced,
                "Unpinned_EnemyBoardCopy: current state without enemy aura, identity or spent attacks");
            Core2026Proof.Check((!history || copy.AppliedEnchantments.Count == 1 && copy.AppliedEnchantments[0].Card.Id == "CS2_009e")
                && neighbor.AttackDamage == (silenced ? 4 : 5), "copied enchantment and Silence before printed aura activation");
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)next.IdEntityDic[copy.Id];
                var source = (Minion)next.IdEntityDic[original.Id];
                Core2026Proof.Check(copied.Controller == next.Player1 && copied.Damage == 1 && copied.IsSilenced == silenced
                    && copied.AttackDamage == 4 && copied.NumAttacksThisTurn == 0,
                    "Unpinned_EnemyBoardCopy: clone/snapshot keeps independent copied state");
                Kill(next, source);
                Core2026Proof.Check(copied.Zone == next.Player1.BoardZone && copied.AttackDamage == 4,
                    "original departure does not remove copy or its buff");
                copied.Silence(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(copied.AttackDamage == 2 && copied.BaseHealth == 2 && (copied.AppliedEnchantments?.Count ?? 0) == 0
                    && ((Minion)next.IdEntityDic[neighbor.Id]).AttackDamage == 4,
                    "Silence removes independent copied enchantment and aura");
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var original = Summon(p.Opponent, "CS2_182");
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_046e"), original, original);
            var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY, preserveBoardMinionState: true);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.AttackDamage == 7, "copied one-turn buff active");
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                End(next);
                Core2026Proof.Check(((Minion)next.IdEntityDic[copy.Id]).AttackDamage == 4
                    && ((Minion)next.IdEntityDic[original.Id]).AttackDamage == 4,
                    "Unpinned_EnemyBoardCopy: copied temporary buff expires independently after clone/snapshot");
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var original = Summon(p.Opponent, "EX1_393"); original.Damage = 1; g.DeathProcessingAndAuraUpdate();
            var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY, preserveBoardMinionState: true);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy.AttackDamage == 5 && copy.Damage == 1,
                "Unpinned_EnemyBoardCopy: damaged Amani Enrage applied once, not twice");
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)next.IdEntityDic[copy.Id];
                copied.TakeHeal(next.Player1.Hero, 1); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(copied.AttackDamage == 2 && ((Minion)next.IdEntityDic[original.Id]).AttackDamage == 5,
                    "copied Enrage clears on healing independently, including clone/snapshot");
            }
        }
        Console.WriteLine("PASS Unpinned_EnemyBoardCopy: enemy state, Silence, aura rebinding, clone/snapshot, departure");
    }
}
