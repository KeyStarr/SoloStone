using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DemonicConfinementProof
{
    // Unpinned_DormantClock Unpinned_DormantExclusions Unpinned_DormantLifecycle
    // Unpinned_DormantCounterProjection Unpinned_DormantReplace Unpinned_DormantFrozenThaw.
    internal static void Run() => StandardSetsProof.Case("JAIL_997", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("JAIL_997e1", 132716); EmeraldDreamCardStateProof.Token("JAIL_997e3", 132717);
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var demon = Summon(p, "CS2_065"); var enemyDemon = Summon(p.Opponent, "CS2_065");
        var sleeper = Summon(p, "TIME_046");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == friendly) && options.Any(t => t.Target == enemy)
            && options.Any(t => t.Target == demon) && options.Any(t => t.Target == enemyDemon)
            && options.All(t => t.Target is Minion && t.Target != sleeper), "both sides' active minions only; no heroes or Dormant targets");
        PlayEntity(g, p, spell, demon);
        Core2026Proof.Check(demon.AttackDamage == 4 && demon.BaseHealth == 6 && !demon.IsDormant
            && demon.AppliedEnchantments.Single().Card.Id == "JAIL_997e1", "friendly Demon +3/+3 instead of sleep");
        demon.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(demon.AttackDamage == 1 && demon.BaseHealth == 3 && demon.AppliedEnchantments.Count == 0, "buff removed by Silence");
        Play(g, p, id, enemyDemon);
        Core2026Proof.Check(enemyDemon.IsDormant && enemyDemon.AttackDamage == 1 && enemyDemon.BaseHealth == 3
            && enemyDemon.AppliedEnchantments.Single().Card.Id == "JAIL_997e3", "enemy Demon sleeps without buff");
        friendly.Damage = 2; friendly.IsFrozen = true; friendly.IsExhausted = false;
        Play(g, p, id, friendly); Play(g, p, id, enemy);
        Core2026Proof.Check(friendly.IsDormant && enemy.IsDormant && friendly.AttackDamage == 4 && friendly.BaseHealth == 5
            && friendly.Damage == 2 && DormantRules.TurnsRemaining(friendly) == 2, "non-Demons sleep two starts; damage retained");
        friendly.Silence(); friendly.TakeDamage(p.Opponent.Hero, 99);
        Core2026Proof.Check(!friendly.IsSilenced && friendly.Damage == 2, "Dormant excludes Silence and damage");
        PoolPilotProof.Resume(g, next => {
            var m = (Minion)next.IdEntityDic[friendly.Id];
            End(next);
            Core2026Proof.Check(DormantRules.TurnsRemaining(m) == 2 && !m.IsFrozen,
                "Unpinned_DormantFrozenThaw existing own-end thaw remains active while asleep");
            End(next); Core2026Proof.Check(m.IsDormant && DormantRules.TurnsRemaining(m) == 1, "first future own start");
            End(next); End(next);
            Core2026Proof.Check(!m.IsDormant && m.IsExhausted && m.Damage == 2 && m.AppliedEnchantments.Count == 0, "second future own start wakes unchanged minion");
            Core2026Proof.Check(!((Minion)next.IdEntityDic[enemy.Id]).IsDormant && !((Minion)next.IdEntityDic[enemyDemon.Id]).IsDormant,
                "enemy timers use their controller starts");
        });
        ParkBoard(p); ParkBoard(p.Opponent); PoolPilotProof.EmptyHand(p);
        spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "no minions: no legal play");
        var target = Summon(p, "CS2_182"); PlayEntity(g, p, spell, target);
        Generic.ReturnToHandBlock(p, target);
        Core2026Proof.Check(!target.IsDormant && target.AppliedEnchantments.Count == 0 && !DormantRules.HasState(target), "source spell gone, target bounce clears sleep");
        PlayEntity(g, p, target);
        Core2026Proof.Check(!target.IsDormant, "replay does not inherit external sleep");
    });
}
