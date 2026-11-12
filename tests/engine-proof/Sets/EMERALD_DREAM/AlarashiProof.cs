using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_AlarashiProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("EDR_493", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182");
        held.AttackDamage = 7; held.Health = 8; held.Cost = 2;
        Play(g, p, id);
        var result = (Minion)g.IdEntityDic[held.Id];
        Core2026Proof.Check(result.Zone == p.HandZone && result.Card.IsRace(Race.DEMON)
            && result.AttackDamage == 7 && result.Health == 8 && result.Cost == 2,
            "transform keeps current stats and persistent Cost");
        Core2026Proof.Check(result.AppliedEnchantments.Any(e => e.Card.Id == "EDR_493e2")
            && result.AppliedEnchantments.Any(e => e.Card.Id == "EDR_493e"), "real setter identities");
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        var auraOwner = Summon(p, "CS2_231");
        new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.HAND,
            new Effect(GameTag.COST, EffectOperator.SUB, 1)).Activate(auraOwner);
        held = (Minion)Add(p, "CS2_182"); held.Cost = 2; g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 1, "external hand Cost aura fixture");
        Play(g, p, id); result = (Minion)g.IdEntityDic[held.Id];
        Core2026Proof.Check(result.Cost == 1, "external aura applied exactly once after transform");
        var active = g.Auras.OfType<SabberStoneCore.Auras.Aura>().Single(a => a.Owner.Id == auraOwner.Id);
        active.Remove(); g.AuraUpdate();
        Core2026Proof.Check(result.Cost == 2, "removed aura is not baked into transformed Cost");
        var branch = Game.LoadSnapshot(g.SaveSnapshot());
        var copied = Generic.Copy(branch.Player2, branch.Player2.Hero, branch.IdEntityDic[result.Id], Zone.HAND);
        Core2026Proof.Check(copied.Cost == 2 && ((Minion)copied).AttackDamage == result.AttackDamage,
            "transformed persistent values copy and snapshot");
        ParkBoard(p);
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        var portal = Summon(p, "EX1_315");
        held = (Minion)Add(p, "CS2_182"); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 2 && CostRules.PersistentCost(held) == 4, "legacy Portal aura-free transform input");
        Play(g, p, id); result = (Minion)g.IdEntityDic[held.Id];
        Core2026Proof.Check(CostRules.PersistentCost(result) == 4, "Portal not baked into Demon setter");
        portal.Silence(); g.AuraUpdate();
        Core2026Proof.Check(result.Cost == 4, "Portal removal restores preserved Demon Cost");
    });
}
