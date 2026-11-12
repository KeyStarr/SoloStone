using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class SiamatProof
{
    private static readonly string[] Options = { "ULD_178a", "ULD_178a2", "ULD_178a3", "ULD_178a4" };
    private static readonly GameTag[] Keywords = { GameTag.WINDFURY, GameTag.DIVINE_SHIELD, GameTag.TAUNT, GameTag.RUSH };
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_178", (g, p, id) => {
        Summon(p.Opponent, "CS2_182");
        foreach (string option in Options) CostSliceProof.Definition(option);
        for (int first = 0; first < 4; first++)
        for (int second = 0; second < 4; second++)
        {
            if (first == second) continue;
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
            long draws = g.Random.DrawCount; var source = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.Choice.Choices.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(Options), "all four exact first-choice options");
            int chosen = p.Choice.Choices.Single(x => g.IdEntityDic[x].Card.Id == Options[first]);
            Resume(g, next => AnswerAndCheckTag(next, chosen, source.Id, Keywords[first]));
            Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(x => g.IdEntityDic[x].Card.Id)
                .SequenceEqual(Options.Where(x => x != Options[first])), "second choice excludes first and retains other three");
            chosen = p.Choice.Choices.Single(x => g.IdEntityDic[x].Card.Id == Options[second]);
            Resume(g, next => AnswerAndCheckTag(next, chosen, source.Id, Keywords[second]));
            for (int i = 0; i < 4; i++) Core2026Proof.Check(source[Keywords[i]] == (i == first || i == second ? 1 : 0), "exact two selected keywords");
            Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && p.BoardZone.SlotCount == 7
                && source.AttackDamage == 7 && source.Health == 7 && g.Random.DrawCount == draws, "choices grant keywords without RNG, hand cards or extra board slots");
            if (first == 3 || second == 3) Core2026Proof.Check(source.CanAttack && source.AttackableByRush, "granted Rush immediate readiness");
            source.Silence();
            Core2026Proof.Check(Keywords.All(k => source[k] == 0) && source.IsExhausted && !source.CanAttack, "Silence removes all granted keywords and same-turn readiness");
        }
    });

    private static void AnswerAndCheckTag(Game g, int chosen, int source, GameTag keyword)
    {
        int history = g.PowerHistory.Full.Count;
        CoreCostProof.Answer(g, chosen);
        Core2026Proof.Check(g.PowerHistory.Full.Skip(history).OfType<PowerHistoryTagChange>().Any(t =>
            t.EntityId == source && t.Tag == keyword && t.Value == 1), "each Siamat choice publishes its granted keyword");
    }
}
