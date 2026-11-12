using System.Reflection;
using HsSim;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class QuestSnapshotProof
{
    private static void Summons(Game g, int count)
    {
        for (int i = 0; i < count; i++) Kill(g, Summon(g.Player1, "EX1_506"));
    }

    private static string State(Game g)
    {
        var quest = g.Player1.SecretZone.ModernQuests.Single();
        return Canonical.State(g) + $"|{quest.QuestProgress}|{quest.QuestState.CompletionGeneration}|{quest.QuestState.RewardedGeneration}|{DiveTheGolakkaDepthsProof.Stacks(g.Player1)}";
    }

    private static string Future(Game g)
    {
        Summons(g, 13);
        return State(g) + Canonical.History(g);
    }

    internal static void Child(string directory)
    {
        foreach (var path in Directory.GetFiles(directory, "*.snapshot"))
        {
            byte[] bytes = File.ReadAllBytes(path);
            var g = Game.LoadSnapshot(bytes);
            Core2026Proof.Check(bytes.SequenceEqual(g.SaveSnapshot()), "fresh-process snapshot round trip");
            Core2026Proof.Check((Path.GetFileName(path).StartsWith("pending-") ? PendingFuture(g) : Future(g)) == File.ReadAllText(path + ".future"), "fresh-process quest continuation identical");
        }
        Console.WriteLine("PASS quest fresh-process snapshot");
    }

    private static void ChildProcess(params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var child = System.Diagnostics.Process.Start(start);
        var output = child.StandardOutput.ReadToEnd(); var error = child.StandardError.ReadToEnd();
        Core2026Proof.Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh process: " + output + error);
        Console.Write(output);
    }

    private static string NonrepeatableFuture(Game g)
    {
        var quest = (Spell)g.IdEntityDic.Values.Single(c => c.Card.Id == QuestLifecycleProof.SyntheticQuestId);
        for (int i = 0; i < 3; i++) QuestLifecycleProof.Qualify(g);
        Core2026Proof.Check(QuestLifecycleProof.RewardCount(g) == 1 && quest.QuestProgress == 2
            && quest.Zone == g.Player1.GraveyardZone && quest.ActivatedTrigger == null
            && g.Player1.SecretZone.ModernQuests.Count == 0, "restored nonrepeatable grants once and stays removed");
        Core2026Proof.Check(g.TriggerManager.Events.GetSubscriptions(nameof(TriggerManager.ZoneTrigger)).Count == 0,
            "restored completed nonrepeatable trigger stays detached");
        return Canonical.State(g) + (g.History ? Canonical.History(g) : "") + Canonical.Value(g.Random.CaptureState());
    }

    // A fresh recipe host avoids changing the parent process's already-frozen snapshot registry.
    internal static void Nonrepeatable() => QuestLifecycleProof.WithSyntheticQuest(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-quest-once-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (bool history in new[] { false, true })
            foreach (int events in new[] { 1, 2 })
            {
                var g = QuestLifecycleProof.Fixture(history);
                var quest = (Spell)Play(g, g.Player1, QuestLifecycleProof.SyntheticQuestId);
                for (int i = 0; i < events; i++) QuestLifecycleProof.Qualify(g);
                Core2026Proof.Check(quest.QuestProgress == events && QuestLifecycleProof.RewardCount(g) == events - 1,
                    "nonrepeatable checkpoint before/after threshold");
                var bytes = g.SaveSnapshot();
                var clone = g.CloneExact(history: history); var restored = Game.LoadSnapshot(bytes);
                var copied = (Spell)clone.IdEntityDic[quest.Id];
                Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && bytes.SequenceEqual(restored.SaveSnapshot())
                    && !ReferenceEquals(quest.QuestState, copied.QuestState), "nonrepeatable clone/snapshot equal and independent");
                QuestLifecycleProof.Qualify(clone);
                Core2026Proof.Check(quest.QuestProgress == events && QuestLifecycleProof.RewardCount(g) == events - 1,
                    "clone cannot change original progress or grant");
                var path = Path.Combine(directory, $"{history}-{events}.snapshot");
                File.WriteAllBytes(path, bytes);
                File.WriteAllText(path + ".future", NonrepeatableFuture(restored));
                Core2026Proof.Check(NonrepeatableFuture(g) == File.ReadAllText(path + ".future"),
                    "same-process snapshot resumes identical future");
            }
            ChildProcess("--quest-nonrepeatable-child", directory);
        }
        finally { Directory.Delete(directory, true); }
    });

    internal static void NonrepeatableChild(string directory) => QuestLifecycleProof.WithSyntheticQuest(() =>
    {
        foreach (var path in Directory.GetFiles(directory, "*.snapshot"))
        {
            var bytes = File.ReadAllBytes(path); var g = Game.LoadSnapshot(bytes);
            Core2026Proof.Check(bytes.SequenceEqual(g.SaveSnapshot()), "fresh nonrepeatable snapshot round trip");
            Core2026Proof.Check(NonrepeatableFuture(g) == File.ReadAllText(path + ".future"), "fresh nonrepeatable future identical");
        }
        Console.WriteLine("PASS nonrepeatable fresh-process snapshots: before threshold, after removal, no duplicate grant, listener detached");
    });

    private static Game PendingFixture(bool history)
    {
        var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
        var first = (Spell)Play(g, p, "TLC_631");
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        Core2026Proof.Check(SabberStoneCore.Actions.QuestRules.Advance(first, 12), "modern completion at capacity");
        // Legacy UNG quests obey the current-client reward rule too.
        var second = (Spell)Entity.FromCard(p, Cards.FromId("UNG_942"));
        p.SecretZone.Add(second); second.QuestProgress = second.QuestTotalProgress - 1;
        g.TaskQueue.Enqueue(new SabberStoneCore.Tasks.SimpleTasks.QuestProgressTask("UNG_942t"), p, second, null);
        g.ProcessTasks();
        Core2026Proof.Check(first.Zone == p.GraveyardZone && second.Zone == p.GraveyardZone
            && !g.IdEntityDic.Values.Any(c => c.Card.Id == "TLC_631t" || c.Card.Id == "UNG_942t"),
            "both paths finish with no reward entity or burned card");
        End(g);
        return g;
    }

    private static string PendingFuture(Game g)
    {
        var p = g.Player1;
        Core2026Proof.Check(g.CurrentPlayer == p.Opponent, "pending delivery starts on opponent turn");
        int firstCreator = p.GraveyardZone.Single(c => c.Card.Id == "TLC_631").Id;
        int secondCreator = p.GraveyardZone.Single(c => c.Card.Id == "UNG_942").Id;
        p.SetasideZone.Add(p.HandZone.Remove(0));
        Core2026Proof.Check(p.HandZone.Last().Card.Id == "TLC_631t"
            && p.HandZone.Last()[SabberStoneCore.Enums.GameTag.DISPLAYED_CREATOR] == firstCreator
            && !g.IdEntityDic.Values.Any(c => c.Card.Id == "UNG_942t"), "first slot delivers first completion only");
        p.SetasideZone.Add(p.HandZone.RemoveForTrade(p.HandZone[0]));
        Core2026Proof.Check(p.HandZone.TakeLast(2).Select(c => c.Card.Id).SequenceEqual(new[] { "TLC_631t", "UNG_942t" })
            && p.HandZone.Last()[SabberStoneCore.Enums.GameTag.DISPLAYED_CREATOR] == secondCreator,
            "trade removal delivers second pending reward in FIFO order");
        p.SetasideZone.Add(p.HandZone.Remove(0));
        Core2026Proof.Check(p.HandZone.Count == 9
            && p.GraveyardZone.All(c => c.Card.Id != "TLC_631t" && c.Card.Id != "UNG_942t"),
            "index removal does not duplicate or burn rewards");
        return Canonical.State(g) + (g.History ? Canonical.History(g) : "");
    }

    private static void PinnedRule_PendingRewardRoundTrip(string directory)
    {
        foreach (bool history in new[] { false, true })
        {
            var g = PendingFixture(history);
            byte[] bytes = g.SaveSnapshot();
            var restored = Game.LoadSnapshot(bytes);
            var clone = g.CloneExact(history: history);
            var simulation = g.CloneForSimulation(42, history: history);
            Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot())
                && Canonical.State(g) == Canonical.State(clone) && g.Hash() == clone.Hash(), "pending snapshot/clone equal");
            string expected = PendingFuture(restored);
            // Exact clones intentionally omit prior history; compare future state and isolate source queue.
            PendingFuture(clone); PendingFuture(simulation);
            Core2026Proof.Check(!g.Player1.HandZone.Any(c => c.Card.Id == "TLC_631t"), "clone queue independent");
            Core2026Proof.Check(PendingFuture(g) == expected && Canonical.State(g) == Canonical.State(clone)
                && Canonical.State(g) == Canonical.State(simulation), "pending replay futures identical");
            string path = Path.Combine(directory, $"pending-{history}.snapshot");
            File.WriteAllBytes(path, bytes); File.WriteAllText(path + ".future", expected);
        }
    }

    internal static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-quest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (bool history in new[] { false, true })
            foreach (int summons in new[] { 4, 6, 10 })
            {
                var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
                var quest = (Spell)Play(g, p, "TLC_426");
                Summons(g, summons);
                int progress = quest.QuestProgress, stacks = DiveTheGolakkaDepthsProof.Stacks(p);
                int historyCount = history ? g.PowerHistory.Full.Count : 0;
                var clone = g.CloneExact(history: history);
                Core2026Proof.Check(State(g) == State(clone), "clone equal at progress/completion boundary");
                var copiedQuest = clone.Player1.SecretZone.ModernQuests.Single();
                Core2026Proof.Check(!ReferenceEquals(quest, copiedQuest) && !ReferenceEquals(quest.QuestState, copiedQuest.QuestState)
                    && copiedQuest.Zone == clone.Player1.SecretZone, "clone owns independent quest and state");
                byte[] bytes = g.SaveSnapshot();
                var restored = Game.LoadSnapshot(bytes);
                EventOrdinalProof.Same(g.TriggerManager.Events, clone.TriggerManager.Events, EventOrdinalProof.Channels);
                EventOrdinalProof.Same(g.TriggerManager.Events, restored.TriggerManager.Events, EventOrdinalProof.Channels);
                Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot()) && State(g) == State(restored), "snapshot equal in fresh game object");
                if (history)
                {
                    var path = Path.Combine(directory, summons + ".snapshot");
                    File.WriteAllBytes(path, bytes);
                    File.WriteAllText(path + ".future", Future(Game.LoadSnapshot(bytes)));
                }
                Summons(clone, 13);
                Core2026Proof.Check(quest.QuestProgress == progress && DiveTheGolakkaDepthsProof.Stacks(p) == stacks,
                    "clone progress and rewards cannot mutate source");
                Summons(restored, 13); Summons(g, 13);
                Core2026Proof.Check(State(g) == State(clone) && State(g) == State(restored), "resumed progress and rewards identical");
                Core2026Proof.Check(DiveTheGolakkaDepthsProof.Stacks(p) == (summons + 13) / 6
                    && quest.QuestProgress == (summons + 13) % 6, "resume grants each reward exactly once");
                if (history) Core2026Proof.Check(g.PowerHistory.Full.Skip(historyCount).Select(e => Canonical.Value(e)).SequenceEqual(clone.PowerHistory.Full.Select(e => Canonical.Value(e)))
                    && Canonical.History(g) == Canonical.History(restored), "resumed history identical");
            }
            PinnedRule_PendingRewardRoundTrip(directory);
            ChildProcess("--quest-child", directory);
            ChildProcess("--quest-nonrepeatable");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("PASS quest clone/snapshot: progress, completion, reward stacks, history on/off");
    }
}
