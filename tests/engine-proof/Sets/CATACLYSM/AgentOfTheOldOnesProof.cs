using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AgentOfTheOldOnesProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_200", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "empty hand completes without Coin or decision");
        ClearBoard(g, p);
        var opponent = Add(p.Opponent, "CS2_182");
        foreach (string targetId in new[] { "CS2_182", "CS2_029", "CS2_106" })
        {
            var left = Add(p, "CS2_171"); var selected = Add(p, targetId); var right = Add(p, "CS2_171");
            selected.Cost = 1;
            var rng = g.Random.CaptureState();
            var source = Play(g, p, id);
            Core2026Proof.Check(p.Choice.ChoiceType == ChoiceType.TARGET && p.Choice.SourceId == source.Id &&
                p.Choice.Choices.SequenceEqual(new[] { left.Id, selected.Id, right.Id }), "offer exact current friendly hand, source excluded");
            Core2026Proof.Check(p.Options().Count == 3 && p.Options().All(t => t is ChooseTask), "bot gets legal hand choices");
            var clone = g.Clone(); var restored = Game.LoadSnapshot(g.SaveSnapshot());
            PlayerDecisionProof.Answer(clone, selected.Id);
            Core2026Proof.Check(p.Choice != null && selected.Card.Id == targetId, "answer in clone leaves original pending and unchanged");
            foreach (var candidate in new[] { g, restored }) PlayerDecisionProof.Answer(candidate, selected.Id);
            var coin = p.HandZone[1];
            Core2026Proof.Check(coin.Id == selected.Id && coin.Card.Id == "GAME_005" && coin.Card.AssetId == 1746 &&
                coin.Card.Implemented && coin.Card.Type == CardType.SPELL && coin.Cost == 0 && coin[GameTag.COST] == 0,
                "selected entity becomes exact zero-cost Coin, including cross-type transform and cost reset");
            Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { left.Id, selected.Id, right.Id }) &&
                left.Card.Id == "CS2_171" && right.Card.Id == "CS2_171" && opponent.Card.Id == "CS2_182",
                "hand order, unselected cards and opponent hand preserved");
            Core2026Proof.Check(p.Choice == null && p.IsComboActive && g.Random.CaptureState().Equals(rng), "answer completes once without RNG");
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored), "Coin continuation survives snapshot");
            foreach (var held in p.HandZone.ToArray()) { p.HandZone.Remove(held); p.SetasideZone.Add(held); }
            ClearBoard(g, p);
        }
        var first = Add(p, "CS2_182"); var second = Add(p, "CS2_182");
        Summon(p, "LOE_077"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, second.Id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { first.Id, second.Id }), "Brann rebuilds offers including transformed Coin");
        PlayerDecisionProof.Answer(g, first.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.All(c => c.Card.Id == "GAME_005"), "Brann transforms separately chosen cards");
        ClearBoard(g, p);
        var only = p.HandZone[0];
        var spare = p.HandZone[1]; p.HandZone.Remove(spare); p.SetasideZone.Add(spare);
        var pendingSource = Play(g, p, id); Kill(g, (SabberStoneCore.Model.Entities.Minion)pendingSource);
        PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "GAME_005", "committed choice finishes after source leaves play");
    });
}
