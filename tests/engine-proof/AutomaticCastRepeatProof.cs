using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AutomaticCastRepeatProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { Unpinned_LockedTargetKeepsIndependentDraw, ChooseOneKeepsSelectedBranch, SecretAndQuestWithHandProgress })
        {
            try { test(); Console.WriteLine("PASS automatic repeat " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.Name + ": " + e); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
    }

    private static void Unpinned_LockedTargetKeepsIndependentDraw()
    {
        // w08-repeat-target: losing the locked target suppresses only the targeted portion.
        foreach (bool otherMinion in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var electra = Play(g, p, "BOT_411");
            var survivor = otherMinion ? Summon(p.Opponent, "CS2_182") : null;
            // Legacy StateTaskList mutates catalogue task State; assert gameplay in each resumed game independently.
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = next.Player1; int hand = owner.Opponent.HandZone.Count;
                int deck = owner.Opponent.DeckZone.Count;
                var random = next.Random.CaptureState();
                var spell = Play(next, owner, "EX1_161", (ICharacter)next.IdEntityDic[electra.Id]);
                Core2026Proof.Check(owner.Opponent.HandZone.Count == hand + 4 && owner.Opponent.DeckZone.Count == deck - 4,
                    "Unpinned_Electra Naturalize draws four after its locked target dies; other minion=" + otherMinion);
                Core2026Proof.Check(next.IdEntityDic[electra.Id].Zone == owner.GraveyardZone
                    && (survivor == null || next.IdEntityDic[survivor.Id].Zone == owner.Opponent.BoardZone),
                    "locked repeat never selects a replacement minion");
                Core2026Proof.Check(next.Random.CaptureState().Equals(random), "locked repeat consumes no target/mode RNG");
                var casts = next.HistoryFacts.Query.Casts().Where(f => f.EntityId == spell.Id).ToArray();
                Core2026Proof.Check(casts.Length == 4 && casts.Count(f => f.CasterKind == HistoryCastKind.Repeat) == 2
                    && next.HistoryFacts.Query.Plays().Count(f => f.EntityId == spell.Id) == 1,
                    "locked repeat records one Player and one Repeat cast, one play");
            }
        }
    }

    private static void SecretAndQuestWithHandProgress()
    {
        foreach (string repeater in new[] { "EDR_464", "BOT_411" })
        foreach (string id in new[] { "EX1_289", "CORE_EX1_289", "UNG_028", "TLC_426" })
        foreach (bool progress in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            Play(g, p, repeater);
            var held = progress ? Add(p, "FIR_918") : null;
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = next.Player1;
                var spell = Play(next, owner, id);
                Core2026Proof.Check(spell.Zone == owner.SecretZone && !owner.GraveyardZone.Contains(spell)
                    && owner.SecretZone.Count(s => s.Id == spell.Id) == 1,
                    "repeat preserves one active Secret/Quest: " + id + "; progress=" + progress);
                Core2026Proof.Check(spell.ActivatedTrigger != null || spell is Spell { QuestState: not null },
                    "repeated Secret/Quest remains armed");
                if (held != null) Core2026Proof.Check(next.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                    "one hand play advances held spell once");
                Core2026Proof.Check(next.HistoryFacts.Query.Casts().Count(f => f.EntityId == spell.Id) == 4
                    && next.HistoryFacts.Query.Plays().Count(f => f.EntityId == spell.Id) == 1,
                    "one play, two complete cast attempts");
                Core2026Proof.Check(next.SpellCompletion == null, "repeat leaves no pending decision");
            }
        }
    }

    private static void ChooseOneKeepsSelectedBranch()
    {
        foreach (int mode in new[] { 2, 1 })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            Play(g, p, "BOT_411");
            var target = Summon(p.Opponent, "CS2_200");
            var spell = Add(p, "EX1_154");
            PoolPilotProof.Resume(g, next => {
                var owner = next.Player1; var victim = (Minion)next.IdEntityDic[target.Id];
                int hand = owner.HandZone.Count, deck = owner.DeckZone.Count;
                var random = next.Random.CaptureState();
                var task = owner.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                    && t.Source?.Id == spell.Id && t.Target?.Id == target.Id && t.ChooseOne == mode);
                Core2026Proof.Check(next.Process(task), "selected Wrath branch accepted");
                int draws = mode == 1 ? 0 : 2;
                Core2026Proof.Check(victim.Damage == (mode == 1 ? 6 : 2)
                    && owner.HandZone.Count == hand - 1 + draws && owner.DeckZone.Count == deck - draws,
                    "Electra preserves selected Wrath branch " + mode + ": damage=" + victim.Damage
                    + ", draws=" + (deck - owner.DeckZone.Count));
                Core2026Proof.Check(next.Random.CaptureState().Equals(random), "selected repeat branch consumes no mode RNG");
            });
        }
    }
}
