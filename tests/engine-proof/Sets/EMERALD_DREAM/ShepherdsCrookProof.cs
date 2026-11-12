using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShepherdsCrookProof
{
    // Unpinned_DormantRecipeOrder Unpinned_DormantClock Unpinned_DormantExclusions
    // Unpinned_DormantLifecycle Unpinned_DormantCounterProjection.
    private static void HeroAttack(Game g, Controller p)
    {
        var task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task), "hero attack accepted");
    }
    internal static void Run() => StandardSetsProof.Case("EDR_416", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("EDR_416t", 115083); EmeraldDreamCardStateProof.Token("EDR_416te", 115085);
        Play(g, p, id); var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        var wolf = Summon(p, "EX1_162");
        Attack(g, left, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.SlotCount == 3, "minion attack does not summon");
        int heroDamage = p.Opponent.Hero.Damage;
        HeroAttack(g, p);
        Core2026Proof.Check(p.BoardZone.SlotCount == 4, "completed hero attack summons exactly one Sheep");
        var sheep = (Minion)p.BoardZone.GetSlot(3);
        Core2026Proof.Check(p.BoardZone.Slots.Select(x => x.Id).SequenceEqual(new[] { location.Id, left.Id, wolf.Id, sheep.Id })
            && sheep.Card.Id == "EDR_416t" && sheep.Card.AssetId == 115083 && sheep.Card.IsRace(Race.BEAST)
            && sheep.AttackDamage == 3 && sheep.BaseHealth == 3 && sheep.Cost == 2 && sheep.IsDormant
            && DormantRules.TurnsRemaining(sheep) == 2 && sheep.AppliedEnchantments.Single().Card.Id == "EDR_416te",
            "Unpinned_DormantRecipeOrder right-end 3/3 Sheep enters asleep before adjacent aura");
        var timeline = g.PowerHistory.Full.ToArray();
        int combat = Array.FindLastIndex(timeline, e => e is PowerHistoryTagChange change && change.EntityId == p.Opponent.Hero.Id && change.Tag == GameTag.DAMAGE && change.Value == heroDamage + 3);
        int summon = Array.FindIndex(timeline, e => e is PowerHistoryFullEntity full && full.Entity.Id == sheep.Id);
        Core2026Proof.Check(combat >= 0 && summon > combat, "Unpinned_DormantRecipeOrder Sheep created after attack damage");
        sheep.Silence(); sheep.TakeDamage(p.Opponent.Hero, 99);
        Core2026Proof.Check(!sheep.IsSilenced && sheep.Damage == 0, "sleeping Sheep excludes Silence/damage");
        End(g); Play(g, p.Opponent, "CS2_106"); HeroAttack(g, p.Opponent);
        Core2026Proof.Check(p.BoardZone.SlotCount == 4 && DormantRules.TurnsRemaining(sheep) == 2, "opponent attack/start excluded");
        End(g);
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; var first = (Minion)next.IdEntityDic[sheep.Id];
            HeroAttack(next, owner);
            var second = (Minion)owner.BoardZone.GetSlot(4);
            Core2026Proof.Check(owner.Hero.Weapon == null && second.Card.Id == "EDR_416t" && second.IsDormant
                && DormantRules.TurnsRemaining(second) == 2 && DormantRules.TurnsRemaining(first) == 1,
                "final durability summons before break; independent timers");
            End(next); End(next);
            Core2026Proof.Check(!first.IsDormant && first.IsExhausted && first.AttackDamage == 4
                && first.AppliedEnchantments.All(e => e.Card.Id != "EDR_416te") && second.IsDormant && DormantRules.TurnsRemaining(second) == 1,
                $"second own start awakens first Sheep and restores eligible aura: dormant={first.IsDormant}, exhausted={first.IsExhausted}, attack={first.AttackDamage}, helpers={first.AppliedEnchantments.Count}, secondDormant={second.IsDormant}, secondTimer={DormantRules.TurnsRemaining(second)}");
        });
        ParkBoard(p); Play(g, p, "CS2_106"); HeroAttack(g, p);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "broken Crook no longer summons");
        Play(g, p, id); End(g); End(g);
        LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        HeroAttack(g, p);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && !p.BoardZone.Any(m => m.Card.Id == "EDR_416t"), "location plus six minions: no summon or overflow");
        ParkBoard(p); End(g); End(g);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182"); LocationProofs.Location(p);
        HeroAttack(g, p);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.GetSlot(6).Card.Id == "EDR_416t", "last free physical slot used at right end");
        ParkBoard(p); Play(g, p, id); Play(g, p, "CS2_106"); End(g); End(g); HeroAttack(g, p);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "replacing Crook removes trigger");
    });
}
