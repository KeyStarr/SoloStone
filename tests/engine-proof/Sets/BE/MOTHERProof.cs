using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using System;

internal static class MOTHERProof
{
    internal static void Run() => StandardSetsProof.Case("BE_036", (g, p, id) => {
        foreach (bool brann in new[] { false, true })
        {
            var replay = g.CloneExact(history: true); var owner = replay.Player1;
            if (brann) Summon(owner, "LOE_077");
            Play(replay, owner, id);
            var first = Add(owner, "EX1_561"); var second = Add(owner, "EX1_561");
            Play(replay, owner, "GIL_820");
            var cards = new[] { first, second };
            Core2026Proof.Check(owner.Choice == null && !replay.Resolution.HasSuspendedEffect && owner.HandZone.Count == 2,
                "MOTHER Shudderwock completes each automatic decision, including Brann");
            Core2026Proof.Check(cards.All(c => c.AppliedEnchantments.Count(e => e.Card.Id == "BE_036e2") == (brann ? 2 : 1)) &&
                cards.Sum(c => c.AppliedEnchantments.Where(e => e.Card.Id == "BE_036e2").Sum(e => e.ScriptTag1)) == (brann ? 18 : 9) &&
                (brann ? cards.All(c => c.Cost <= 1) : cards.Select(c => c.Cost).OrderBy(cost => cost).SequenceEqual(new[] { 4, 5 })),
                "MOTHER replay discounts once per activation; no lost or duplicate reductions");
            Core2026Proof.Check(Canonical.State(replay) == Canonical.State(Game.LoadSnapshot(replay.SaveSnapshot())),
                "MOTHER replay leaves a settled snapshot boundary");
        }
        var batch9 = g.CloneExact(history: true);
        RunBatch9(batch9, batch9.Player1, id);
        Core2026Proof.Check(HsSim.Canonical.State(batch9) == HsSim.Canonical.State(batch9.CloneExact(history: true)) &&
            HsSim.Canonical.State(batch9) == HsSim.Canonical.State(SabberStoneCore.Model.Game.LoadSnapshot(batch9.SaveSnapshot())), "batch-9 proof clone/snapshot future");
        RunSelectors(g, p, id);
    });

    private static void RunBatch9(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "empty hand completes without selection");
        ClearBoard(g, p);
        var baseline = g.CloneExact(history: true);
        int[][] expected = {
            new[] {4,5,6,7,8,9,9,9,9}, new[] {5,4,5,6,7,8,9,9,9},
            new[] {6,5,4,5,6,7,8,9,9}, new[] {7,6,5,4,5,6,7,8,9},
            new[] {8,7,6,5,4,5,6,7,8}, new[] {9,8,7,6,5,4,5,6,7},
            new[] {9,9,8,7,6,5,4,5,6}, new[] {9,9,9,8,7,6,5,4,5},
            new[] {9,9,9,9,8,7,6,5,4}
        };
        for (int pick = 0; pick < 9; pick++)
        {
            var next = baseline.CloneExact(history: true); var owner = next.Player1;
            var cards = Enumerable.Range(0, 9).Select(_ => Add(owner, "EX1_561")).ToArray();
            var enemy = Add(owner.Opponent, "EX1_561");
            var source = Play(next, owner, id);
            Core2026Proof.Check(owner.Choice.Choices.SequenceEqual(cards.Select(c => c.Id)) &&
                owner.Options().Count == 9 && owner.Options().All(t => t is ChooseTask), "full hand offers each friendly entity, source excluded; bot can answer");
            var random = next.Random.CaptureState();
            var restored = Game.LoadSnapshot(next.SaveSnapshot()); var clone = next.CloneExact(history: true);
            foreach (var candidate in new[] {next, restored, clone}) PlayerDecisionProof.Answer(candidate, cards[pick].Id);
            Core2026Proof.Check(cards.Select(c => c.Cost).SequenceEqual(expected[pick]) &&
                cards.All(c => c[GameTag.COST] == c.Cost) && enemy.Cost == 9, "chosen five discount spreads by distance, stops at zero; visible COST; opponent unchanged");
            Core2026Proof.Check(cards.Select(c => c.Id).SequenceEqual(owner.HandZone.Select(c => c.Id)) &&
                cards.All(c => c.Zone == owner.HandZone && c.Controller == owner) && owner.Choice == null &&
                next.Random.CaptureState().Equals(random), "selection preserves identities/order/zone/controller and uses no RNG");
            Core2026Proof.Check(Canonical.State(next) == Canonical.State(restored) && Canonical.State(next) == Canonical.State(clone), "suspended clone/snapshot produces same discounts");
            ((Minion)source).Silence(); Kill(next, (Minion)source);
            End(next); End(next);
            Core2026Proof.Check(cards.Select(c => c.Cost).SequenceEqual(expected[pick]), "discount persists after source silence/death and turn end");
        }
        var cheap = Add(p, "GAME_005"); var middle = Add(p, "CS2_182"); var last = Add(p, "CS2_029");
        Play(g, p, id); PlayerDecisionProof.Answer(g, cheap.Id);
        Core2026Proof.Check(cheap.Cost == 0 && middle.Cost == 0 && last.Cost == 1, "over-reduction floors at zero without stopping spread");
        ClearBoard(g, p); Summon(p, "LOE_077");
        Play(g, p, id); PlayerDecisionProof.Answer(g, last.Id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3, "Brann opens second selection");
        PlayerDecisionProof.Answer(g, middle.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.All(c => c.Cost == 0), "Brann discounts stack and complete");
        PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        var singleton = Add(p, "EX1_561"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, singleton.Id);
        Core2026Proof.Check(singleton.Cost == 4 && p.Choice == null, "singleton selected and discounted once");
    }

    private static void CheckBrannHand(Game game, bool twice)
    {
        var cards = game.Player1.HandZone.ToArray();
        Core2026Proof.Check(cards.Select(c => c.Cost).SequenceEqual(twice ? new[] { 0, 0, 0 } : new[] { 3, 2, 3 }),
            "MOTHER Brann both reductions visible, floor zero");
        int[] amounts = { 4, 5, 4 };
        for (int i = 0; i < cards.Length; i++)
            Core2026Proof.Check(cards[i].AppliedEnchantments.Where(e => e.Card.Id == "BE_036e2").Select(e => e.ScriptTag1)
                .SequenceEqual(twice ? new[] { amounts[i], amounts[i] } : new[] { amounts[i] }), "MOTHER Brann retained exact enchant amounts");
    }

    private static void RunSelectors(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null, "MOTHER empty hand no prompt");
        ClearBoard(g, p);
        foreach (int count in new[] { 1, 3, 9 })
        foreach (int index in new[] { 0, count / 2, count - 1 }.Distinct())
        {
            var hand = Enumerable.Range(0, count).Select(_ => Add(p, "CS2_182")).ToArray();
            foreach (var held in hand) held.Cost = 8;
            Play(g, p, id);
            Core2026Proof.Check(p.Choice.Choices.SequenceEqual(hand.Select(c => c.Id)), "MOTHER exact ordered hand offer");
            var bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes); var clone = g.CloneExact(history: true);
            PlayerDecisionProof.Answer(clone, hand[index].Id);
            Core2026Proof.Check(bytes.SequenceEqual(g.SaveSnapshot()), "MOTHER clone answer isolated");
            PlayerDecisionProof.Answer(restored, hand[index].Id); PlayerDecisionProof.Answer(g, hand[index].Id);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored) && Canonical.State(g) == Canonical.State(clone), "MOTHER mid-decision clone/snapshot future");
            for (int i = 0; i < count; i++)
            {
                int reduction = Math.Max(0, 5 - Math.Abs(i - index));
                Core2026Proof.Check(hand[i].Cost == 8 - reduction, "MOTHER distance reduction at edges/short hand");
                var enchants = hand[i].AppliedEnchantments?.Where(e => e.Card.Id == "BE_036e2").ToArray() ?? Array.Empty<SabberStoneCore.Model.Entities.Enchantment>();
                Core2026Proof.Check(enchants.Length == (reduction > 0 ? 1 : 0) && enchants.All(e => e.ScriptTag1 == reduction), "MOTHER exact variable enchant");
            }
            foreach (var held in hand) { p.HandZone.Remove(held); p.SetasideZone.Add(held); }
            ClearBoard(g, p);
        }
        var left = Add(p, "CS2_186"); var chosen = Add(p, "CS2_186"); var right = Add(p, "CS2_186");
        Summon(p, "LOE_077"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, chosen.Id);
        Core2026Proof.Check(new[] { left.Cost, chosen.Cost, right.Cost }.SequenceEqual(new[] { 3, 2, 3 }) &&
            p.Choice.Choices.SequenceEqual(new[] { left.Id, chosen.Id, right.Id }), "MOTHER Brann first reduction and fresh offer");
        var middleBytes = g.SaveSnapshot();
        var middleClone = g.CloneExact(history: true); var middleRestored = Game.LoadSnapshot(middleBytes);
        foreach (var candidate in new[] { middleClone, middleRestored, g })
        {
            CheckBrannHand(candidate, twice: false);
            PlayerDecisionProof.Answer(candidate, chosen.Id);
            Core2026Proof.Check(candidate.Player1.Choice == null, "MOTHER Brann completes after second reduction");
            CheckBrannHand(candidate, twice: true);
            var afterClone = candidate.CloneExact(history: true); var afterRestored = Game.LoadSnapshot(candidate.SaveSnapshot());
            CheckBrannHand(afterClone, twice: true); CheckBrannHand(afterRestored, twice: true);
            Core2026Proof.Check(Canonical.State(candidate) == Canonical.State(afterClone) && Canonical.State(candidate) == Canonical.State(afterRestored),
                "MOTHER Brann clone/snapshot retains both reductions after completion");
            if (candidate != g) Core2026Proof.Check(middleBytes.SequenceEqual(g.SaveSnapshot()), "MOTHER Brann branch isolated between activations");
        }
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(middleClone) && Canonical.State(g) == Canonical.State(middleRestored), "MOTHER Brann saved futures identical");
    }
}
