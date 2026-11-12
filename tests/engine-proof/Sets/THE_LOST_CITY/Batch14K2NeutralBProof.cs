using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Batch14K2NeutralBLostCityProof
{
    internal static void Run() { Cultist(); Ooze(); }

    private static void Cultist() => StandardSetsProof.Case("TLC_101", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3 && source.HasTaunt && source[GameTag.REBORN] == 1,
            "printed 2/3 Taunt Reborn");
        var other = Summon(p, "CS2_182"); other.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 2 && other.AttackDamage == 4, "other damaged minion grants no Attack");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 2, "damaged Cultist gains exactly three Attack");
        foreach (var copy in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var body = (Minion)copy.IdEntityDic[source.Id]; body.TakeHeal(copy.Player1.Hero, 1); copy.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(body.AttackDamage == 2 && body.Health == 3, "clone/load healing removes damaged Attack");
        }
        Core2026Proof.Check(source.AttackDamage == 5, "clone healing independent");
        End(g); Core2026Proof.Check(source.AttackDamage == 5, "damage bonus persists across turn boundary"); End(g);
        source.TakeHeal(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 2, "fully healing removes bonus");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 2 && !source.HasTaunt && source[GameTag.REBORN] == 0, "Silence removes bonus and keywords");
        Kill(g, source); Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == id), "silenced Cultist never reborn");
        var slots = DeathrattleSlotBoard(p, () => source = PlayAt(g, p, id, 2)); Kill(g, source);
        var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        CheckDeathrattleSlot(p, slots, new[] { reborn }, "Cultist Reborn uses physical death slot");
        Core2026Proof.Check(reborn.Health == 1 && reborn.AttackDamage == 5 && reborn.HasTaunt && reborn[GameTag.REBORN] == 0,
            "reborn is damaged and gains three Attack");
        reborn.TakeHeal(p.Hero, 2); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(reborn.AttackDamage == 2 && reborn.Health == 3, "reborn healing removes bonus");
        var enemy = Summon(p.Opponent, id); enemy.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.AttackDamage == 5 && reborn.AttackDamage == 2, "each side's damaged condition is independent");
        Generic.ReturnToHandBlock(p, reborn); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(reborn.Zone == p.HandZone && reborn.AttackDamage == 2, "leaving play removes aura");
        source = Summon(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Single(m => m.Card.Id == id).Health == 1,
            "full physical board frees exactly one slot for Cultist Reborn");
        g.DeathProcessingAndAuraUpdate();
    });

    private static void Ooze() => StandardSetsProof.Case("TLC_252", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); var victim = Summon(p, "CS2_200");
        Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_829te2"), p.Hero, victim, 3, 2);
        victim.TakeDamage(p.Opponent.Hero, 2); g.DeathProcessingAndAuraUpdate();
        LocationProofs.Location(p);
        var held = Add(p, id);
        Core2026Proof.Check(!held.Card.GetValidPlayTargets(p).Contains(enemy) && held.Card.GetValidPlayTargets(p).Contains(victim),
            "target is friendly minion only");
        Core2026Proof.Check(held.Card.GetValidPlayTargets(p).All(target => target is Minion), "Ooze excludes heroes and locations");
        PlayEntity(g, p, held, victim);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && ((Minion)held).AttackDamage == 3 && ((Minion)held).Health == 3,
            "3/3 destroys friendly minion");
        var bones = p.HandZone.Single();
        Core2026Proof.Check(bones.Card.Id == "TLC_829t" && bones.Card.AssetId == 117614 && bones.Cost == 2
            && bones[GameTag.TAG_SCRIPT_DATA_NUM_1] == 9 && bones[GameTag.TAG_SCRIPT_DATA_NUM_2] == 7
            && bones.Card.ATK == 0 && bones.Card.Health == 0 && bones.Card.Text == "Give a minion +{0}/+{1}.",
            "exact Bones carries current Attack and remaining Health");
        var duplicate = Generic.Copy(p, held, bones, Zone.HAND); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(duplicate[GameTag.TAG_SCRIPT_DATA_NUM_1] == 9 && duplicate[GameTag.TAG_SCRIPT_DATA_NUM_2] == 7,
            "direct hand copy retains independent Bones payload");
        foreach (var copy in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var target = copy.Player2.BoardZone.Single();
            PlayEntity(copy, copy.Player1, copy.IdEntityDic[bones.Id], target);
            Core2026Proof.Check(target.AttackDamage == 15 && target.Health == 14
                && target.AppliedEnchantments.Single().Card.Id == "TLC_829te", "clone/load Bones gives saved +9/+7 with exact enchantment");
            target.Silence(); copy.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(target.AttackDamage == 6 && target.Health == 7, "Silence removes Bones enchantment");
        }
        PlayEntity(g, p, bones, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 15 && enemy.Health == 14 && duplicate.Zone == p.HandZone,
            "original Bones works independently of its copy");
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        var naked = Add(p, id); PlayEntity(g, p, naked);
        Core2026Proof.Check(p.HandZone.IsEmpty, "targetless Ooze on empty board gives no Bones and cannot eat itself");
        ParkBoard(p); victim = Summon(p, "EX1_556"); PoolPilotProof.FillHand(p, 9);
        held = Add(p, id); PlayEntity(g, p, held, victim);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Last().Card.Id == "TLC_829t"
            && p.BoardZone.Any(m => m.Card.Id == "skele21"), "Deathrattle settles and freed hand slot receives Bones");
        victim = Summon(p, "CS2_182");
        held.Card.Power.PowerTask.Process(g, p, held, victim, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full hand still destroys, loses generated Bones");
    });
}
