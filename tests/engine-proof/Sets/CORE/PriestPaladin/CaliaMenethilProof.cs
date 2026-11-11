using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_CaliaMenethilProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("CORE_CATA_002", (g, p, id) => {
        var low = Summon(p, "CS2_120"); Kill(g, low);
        var high = (Minion)Add(p, "CS2_200"); high.Cost = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, high)), "discounted high printed-Cost minion play");
        high.AttackDamage = 20; Kill(g, high);
        Play(g, p, id);
        var result = p.BoardZone.Single(m => m.Card.Id == "CS2_200");
        Core2026Proof.Check(result.Id != high.Id && result.AttackDamage == result.Card.ATK
            && result.Health == result.Card.Health && result.Cost == result.Card.Cost,
            "death Cost ranking after hand reset and fresh resurrection");
        ParkBoard(p);
        var jailbird = (Minion)Add(p, "JAIL_453");
        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_453e"), p.Hero, jailbird, 20);
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, jailbird)) && jailbird.Cost == 0,
            "Jailbird intrinsic Prepare reductions survive forward play");
        Kill(g, jailbird);
        Core2026Proof.Check(g.HistoryFacts.Query.Deaths().Last().CostAtDeath == 0,
            "Jailbird exception retained at death, below normal three-Cost death");
        ParkBoard(p);
        // Two equal maximum A deaths versus one B death retain three candidate entries.
        var another = Summon(p, "CS2_200"); Kill(g, another);
        var tied = Summon(p, "CS2_213"); Kill(g, tied);
        var candidates = g.HistoryFacts.Query.Deaths().Where(f => f.ControllerId == p.Id && f.CostAtDeath == 6).ToArray();
        var random = g.Random.Clone(); string expected = candidates[random.Next(candidates.Length)].Definition.Materialize().Id;
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Any(m => m.Card.Id == expected), "uniform seeded per-death maximum ties without deduplication");
    });
}
