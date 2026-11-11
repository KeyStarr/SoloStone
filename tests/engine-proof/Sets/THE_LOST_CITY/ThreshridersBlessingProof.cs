using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class ThreshridersBlessingProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_477", (g, p, id) => {
        var target = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        Core2026Proof.Check(p.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == target.Id) &&
            p.Options().Where(t => t.Source?.Id == spell.Id).All(t => t.Target is Minion && t.Target.Controller == p), "friendly minion only; enemy and heroes excluded");
        PlayEntity(g, p, spell, target);
        Core2026Proof.Check(target.AttackDamage == 8 && target.Health == 9 && target.HasDeathrattle && target.AppliedEnchantments.Any(e => e.Card.Id == "TLC_477e"), "pinned +4/+4 and granted Deathrattle");
        var enchantment = target.AppliedEnchantments.Single(e => e.Card.Id == "TLC_477e");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[target.Id]));
        var output = p.BoardZone.Single(m => m.Id != right.Id);
        Batch9PoolProof.Generated(g, target, new[] { output }, 1, recipe: id);
        Core2026Proof.Check(output.Card.Cost == 4 && output.ZonePosition == 0 && p.BoardZone.GetSlot(1) == right, "granted Deathrattle owner, cost and former source slot");
        Batch9PoolProof.CleanBoard(g, p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            var m = Summon(p, "CS2_182"); Play(g, p, id, m); var rng = g.Random.CaptureState(); Kill(g, m);
            Core2026Proof.Check(p.BoardZone.SlotCount == 0 && rng.Equals(g.Random.CaptureState()), "empty granted pool");
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            var m = Summon(p, "CS2_182"); Play(g, p, id, m); Kill(g, m);
            Batch9PoolProof.Generated(g, m, p.BoardZone, 1, recipe: id);
        });
        Batch9PoolProof.CleanBoard(g, p);
        target = Summon(p, "CS2_182"); Play(g, p, id, target); target.Silence();
        Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5 && !target.HasDeathrattle, "Silence removes buff and Deathrattle");
        Kill(g, target); Core2026Proof.Check(p.BoardZone.SlotCount == 0, "silenced target summons nothing");
        var left = Summon(p, "CS2_182"); target = Summon(p, "CS2_182"); right = Summon(p, "CS2_182");
        Play(g, p, id, target); enchantment = target.AppliedEnchantments.Single(e => e.Card.Id == "TLC_477e");
        enchantment.Card.Power.DeathrattleTask.Process(g, p, target, null, new TaskStack()); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == target.Id);
        Core2026Proof.Check(output.ZonePosition == target.ZonePosition + 1 && p.BoardZone.GetSlot(output.ZonePosition + 1) == right,
            "granted Deathrattle summons right of live target");
        Batch9PoolProof.CleanBoard(g, p);
        target = Summon(p, "CS2_182"); Play(g, p, id, target); Play(g, p, id, target);
        Core2026Proof.Check(target.AttackDamage == 12 && target.Health == 13, "buff stacks");
        Kill(g, target); Core2026Proof.Check(p.BoardZone.SlotCount == 2 && p.BoardZone.All(m => m.Card.Cost == 4), "both granted Deathrattles run");
        Batch9PoolProof.CleanBoard(g, p); target = Summon(p, "CS2_182"); Play(g, p, id, target);
        End(g); End(g); Core2026Proof.Check(target.AttackDamage == 8 && target.Health == 9 && target.HasDeathrattle, "permanent enchantment survives turn end");
        enchantment = target.AppliedEnchantments.Single(e => e.Card.Id == "TLC_477e");
        LostCityPoolProof.FullBoard(p);
        long before = g.Random.DrawCount;
        enchantment.Card.Power.DeathrattleTask.Process(g, p, target, null, new TaskStack()); g.ProcessTasks();
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 1, "granted summon respects full mixed board");
    });
}
