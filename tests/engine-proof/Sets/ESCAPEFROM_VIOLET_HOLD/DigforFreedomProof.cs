using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class DigforFreedomProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_876", (g, p, id) => {
        // Tier D: exact pinned JAIL_876e/dbfId 127325 grants this Deathrattle.
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "no friendly minion means no legal target");
        var enemy = Summon(p.Opponent, "CS2_182"); var left = Summon(p, "CS2_182"); var target = Summon(p, "CS2_182");
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(targets.Length > 0 && targets.All(t => t.Target?.Id == target.Id || t.Target?.Id == left.Id), "friendly minion required, enemy and heroes excluded");
        var location = LocationProofs.Location(p); var right = Summon(p, "CS2_120");
        PlayEntity(g, p, spell, target);
        Core2026Proof.Check(target.HasDeathrattle && target.AppliedEnchantments.Single().Card.Id == "JAIL_876e"
            && target.AttackDamage == 4 && target.Health == 5 && p.BoardZone.SlotCount == 4, "exact enchantment, no immediate summons or stat change");
        var branch = g.CloneExact();
        var expected = Enumerable.Range(0, 2).Select(_ => OfflineCardPools.Pick(branch, VioletHoldPoolProof.Filter(id), p.HeroClass, target.Card.AssetId).Id).ToArray();
        Resume(g, next => {
            new ActivateDeathrattleTask(EntityType.SOURCE).Process(next, next.Player1, next.IdEntityDic[target.Id], null, new TaskStack());
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
        });
        var generated = SummonedResults(g, target, id, 2, 2);
        Core2026Proof.Check(generated.Select(c => c.Card.Id).SequenceEqual(expected) && right.ZonePosition == 5, "seeded summons right of live recipient, creation order, location occupies slot");
        foreach (var m in generated) { m.Silence(); Kill(g, m); }
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[target.Id]));
        SummonedResults(g, target, id, 2, 1);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == left && p.BoardZone.GetSlot(3) == location && p.BoardZone.GetSlot(4) == right,
            "two summons replace middle recipient rightward before location and right neighbor");
        Quiet(g, p);
        target = Summon(p, "CS2_182"); Play(g, p, id, target); target.Silence(); Kill(g, target);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1, "Silence removes granted Deathrattle");
        target = Summon(p, "CS2_182"); Play(g, p, id, target); Generic.ReturnToHandBlock(p, target);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1 && !target.HasDeathrattle, "bounce removes enchantment without triggering");
        EmptyHand(p);
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            Quiet(g, p); target = Summon(p, "CS2_182"); Play(g, p, id, target);
            long before = g.Random.DrawCount;
            Kill(g, target); SummonedResults(g, target, id, count == 0 ? 0 : 2, 1);
            Core2026Proof.Check(g.Random.DrawCount == before + (count == 0 ? 0 : 2), "empty/singleton pool and independent duplicate picks");
        });
        foreach (int slots in new[] { 5, 6, 7 })
        {
            Quiet(g, p); target = Summon(p, "CS2_182"); Play(g, p, id, target);
            while (p.BoardZone.SlotCount < slots) Summon(p, "CS2_120");
            if (slots == 7)
            {
                long before = g.Random.DrawCount;
                new ActivateDeathrattleTask(EntityType.SOURCE).Process(g, p, target, null, new TaskStack());
                g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount == before + 2, "full living board prevents summons but consumes both picks");
            }
            Kill(g, target);
            SummonedResults(g, target, id, Math.Min(2, 8 - slots), 1);
        }
        Quiet(g, p); target = Summon(p, "CS2_182");
        Play(g, p, id, target); Play(g, p, id, target);
        Kill(g, target);
        var stacked = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == target.Id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(stacked.Length == 4 && p.BoardZone.Slots.Skip(1).Select(m => m.Id)
            .SequenceEqual(new[] { stacked[2].Id, stacked[3].Id, stacked[0].Id, stacked[1].Id }),
            "each stacked Deathrattle inserts its pair rightward at recipient slot");
        foreach (var m in stacked) Member(g, target, m, VioletHoldPoolProof.Filter(id));
        Quiet(g, p); target = Summon(p, "CS2_182"); Play(g, p, id, target);
        new ControlTask(EntityType.TARGET, opposite: true).Process(g, p, spell, target, new TaskStack());
        int ownSlots = p.BoardZone.SlotCount;
        Kill(g, target); SummonedResults(g, target, id, 2, 1);
        Core2026Proof.Check(p.BoardZone.SlotCount == ownSlots, "granted Deathrattle follows recipient controller");
        Quiet(g, p);
        left = Summon(p, "CS2_182"); target = Summon(p, "CS2_182"); var doomed = Summon(p, "CS2_182"); right = Summon(p, "CS2_182");
        Play(g, p, id, target);
        Resume(g, next => {
            ((Minion)next.IdEntityDic[target.Id]).ToBeDestroyed = true;
            ((Minion)next.IdEntityDic[doomed.Id]).ToBeDestroyed = true;
            next.DeathProcessingAndAuraUpdate();
        });
        generated = SummonedResults(g, target, id, 2, 2);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(1) == left && p.BoardZone.GetSlot(4) == right,
            "simultaneous deaths preserve last recipient slot and rightward summon order");
        Quiet(g, p);
        left = Summon(p, "CS2_182"); target = Summon(p, "CS2_182"); right = Summon(p, "CS2_182"); Play(g, p, id, target);
        var enchantment = target.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_876e");
        enchantment.Power.DeathrattleTask.Process(g, p, enchantment, null, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == enchantment.Id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 2 && p.BoardZone.GetSlot(3) == generated[0] && p.BoardZone.GetSlot(4) == generated[1]
            && p.BoardZone.GetSlot(5) == right, "enchantment source inserts two summons right of living target");
        foreach (var m in generated) p.SetasideZone.Add(p.BoardZone.Remove(m));
        target.Silence(); Kill(g, target);
        enchantment.Power.DeathrattleTask.Process(g, p, enchantment, null, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == enchantment.Id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 2 && p.BoardZone.GetSlot(2) == generated[0] && p.BoardZone.GetSlot(3) == generated[1]
            && p.BoardZone.GetSlot(4) == right && p.BoardZone.GetSlot(0) == location, "enchantment source uses departed target's slot and fills rightward");
        End(g);
    });

    private static void Quiet(Game g, Controller p)
    {
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p);
    }
}
