using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BattleAtTheEndTimeProof
{
    internal static void Fill(Game g, Controller p, string id = "CS2_231")
    {
        while (!p.HandZone.IsFull) Add(p, id);
        g.ProcessTasks();
    }

    internal static void Empty(Game g, Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) p.GraveyardZone.Add(p.HandZone.Remove(card));
        g.ProcessTasks();
    }

    private static Spell Install(Game g) => (Spell)Play(g, g.Player1, "END_017");

    internal static void Run() => StandardSetsProof.Case("END_017", (g, p, id) => {
        EchoExpiry();
        DecisionBoundaries();
        var quest = Install(g);
        Add(p, "CS2_231"); Empty(g, p);
        Core2026Proof.Check(quest.QuestProgress == 0, "empty before full does nothing");
        for (int i = 0; i < 9; i++) Add(p, "CS2_032");
        g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 0, "nine is below full threshold");
        Add(p.Opponent, "CS2_231"); g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 0, "opponent hand change ignored");
        Add(p, "CS2_032"); g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 1 && quest.QuestState.Stage == 1, "empty first then full only reaches stage one");
        var burnt = Add(p, "CS2_182"); g.ProcessTasks();
        Core2026Proof.Check(burnt.Zone == p.GraveyardZone && quest.QuestProgress == 1, "overflow burn cannot re-count fullness");
        while (p.HandZone.Count > 1) p.GraveyardZone.Add(p.HandZone.Remove(p.HandZone[0]));
        g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 1, "one remaining card is not empty");
        PlayEntity(g, p, p.HandZone[0]);
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && quest.QuestProgress == 2
            && p.HandZone.Single().Card.Id == "END_017t", $"last spell leaving hand completes and grants reward once: zone={quest.Zone.Type} progress={quest.QuestProgress} stage={quest.QuestState.Stage} hand={string.Join(",",p.HandZone.Select(c=>c.Card.Id))}");
        Core2026Proof.Check(!QuestRules.Advance(quest) && p.HandZone.Count == 1, "stale progress cannot duplicate reward");
        Unpinned_AlreadyFullAtActivation();
        OpponentTurnAndResume();
        PinnedRule_CommittedTransientAndRewardWait();
        LastSpellDraws();
        LastCardTrade();
    });

    private static void EchoExpiry()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var source = Add(p, "GIL_654");
        while (!p.HandZone.IsFull) Add(p, "CS2_032");
        var quest = (Spell)Entity.FromCard(p, Cards.FromId("END_017"));
        Core2026Proof.Check(QuestRules.Install(quest), "Echo expiry quest installed");
        PlayEntity(g, p, source);
        foreach (var card in p.HandZone.ToArray().Where(c => c.Card.Id != "GIL_654")) PlayEntity(g, p, card);
        Core2026Proof.Check(quest.QuestProgress == 1 && p.HandZone.Count == 1, "only Echo card remains");
        End(g);
        Core2026Proof.Check(quest.QuestProgress == 2 && p.HandZone.Single().Card.Id == "END_017t",
            "last Echo expiry settles before opponent decision without manual drain");
        g.SaveSnapshot();
    }

    private static void DecisionBoundaries()
    {
        foreach (string path in new[] { "turn-start draw", "full-hand trade", "Echo copy" })
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            Spell quest;
            if (path == "turn-start draw")
            {
                quest = Install(g);
                while (p.HandZone.Count < 9) Add(p, "CS2_032");
                End(g); End(g);
            }
            else
            {
                // Activate with a full hand: only the later trade/Echo refill may count.
                var source = Add(p, path == "full-hand trade" ? "TLC_255" : "GIL_654");
                while (!p.HandZone.IsFull) Add(p, "CS2_032");
                quest = (Spell)Entity.FromCard(p, Cards.FromId("END_017"));
                Core2026Proof.Check(QuestRules.Install(quest), path + " quest installed");
                if (path == "full-hand trade")
                    Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.TradeCardTask.Any(p, source)), "full-hand trade accepted");
                else PlayEntity(g, p, source);
            }
            Core2026Proof.Check(g.Step == SabberStoneCore.Enums.Step.MAIN_ACTION && p.HandZone.Count == 10
                && quest.QuestProgress == 1, path + " settles at next decision without manual drain");
            byte[] bytes = g.SaveSnapshot();
            var exact = g.CloneExact(history: true);
            var simulation = g.CloneForSimulation(42);
            var restored = Game.LoadSnapshot(bytes);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(exact)
                && bytes.SequenceEqual(restored.SaveSnapshot()), path + " clone and snapshot equal");
            foreach (var copy in new[] { exact, simulation, restored })
            {
                var q = copy.Player1.SecretZone.ModernQuests.Single();
                Core2026Proof.Check(q.QuestProgress == 1 && q.QuestState.Stage == 1
                    && !ReferenceEquals(q.QuestState, quest.QuestState), path + " progress cloned independently");
                if (path == "Echo copy") { End(copy); End(copy); } // Let the Echo copy expire naturally.
                // Natural plays empty the hand; no test-side ProcessTasks hides a stranded fact.
                foreach (var card in copy.Player1.HandZone.ToArray()) PlayEntity(copy, copy.Player1, card);
                Core2026Proof.Check(q.Zone == copy.Player1.GraveyardZone && q.QuestProgress == 2
                    && copy.Player1.HandZone.Count(c => c.Card.Id == "END_017t") == 1
                    && !QuestRules.Advance(q), path + " resumed quest grants exactly once");
                copy.SaveSnapshot();
            }
            Core2026Proof.Check(quest.QuestProgress == 1 && p.HandZone.Count == 10,
                path + " resumed completion cannot change original");
        }
    }

    private static void Unpinned_AlreadyFullAtActivation()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        Fill(g, p);
        var quest = (Spell)Entity.FromCard(p, Cards.FromId("END_017"));
        Core2026Proof.Check(QuestRules.Install(quest), "install while already full");
        Add(p, "CS2_182"); g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 0, "Unpinned_AlreadyFullAtActivation: no retrospective or burn progress");
        p.GraveyardZone.Add(p.HandZone.Remove(p.HandZone[0]));
        Fill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 1, "Unpinned_AlreadyFullAtActivation: later fullness counts");
    }

    private static void OpponentTurnAndResume()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1; var quest = Install(g);
        End(g); Fill(g, p);
        Core2026Proof.Check(quest.QuestProgress == 1, "generation on opponent turn counts");
        var clone = g.CloneExact(history: true);
        byte[] bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && bytes.SequenceEqual(restored.SaveSnapshot()), "mid-stage clone and snapshot equal");
        var copied = clone.Player1.SecretZone.ModernQuests.Single();
        Core2026Proof.Check(!ReferenceEquals(quest.QuestState, copied.QuestState) && copied.QuestState.Stage == 1, "mid-stage state independent");
        Empty(clone, clone.Player1);
        Core2026Proof.Check(quest.QuestProgress == 1 && p.HandZone.Count == 10, "clone cannot mutate source");
        Empty(restored, restored.Player1); Empty(g, p);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
            && Canonical.History(g) == Canonical.History(restored), "resume identical through reward");
        foreach (var game in new[] { g, clone, restored })
        {
            game.ProcessTasks();
            Core2026Proof.Check(game.Player1.HandZone.Single().Card.Id == "END_017t"
                && game.Player1.SecretZone.ModernQuests.Count == 0, "resume grants exactly one reward");
        }
    }

    private static void PinnedRule_CommittedTransientAndRewardWait()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1; var quest = Install(g);
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        // Both transitions happen before the queued facts resolve.
        foreach (var card in p.HandZone.ToArray()) p.GraveyardZone.Add(p.HandZone.Remove(card));
        Fill(g, p);
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && quest.QuestProgress == 2 && p.HandZone.Count == 10
            && !g.IdEntityDic.Values.Any(c => c.Card.Id == "END_017t"), "committed full/empty facts survive refill; reward waits");
        Generic.DiscardBlock(p, p.HandZone[0]);
        Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "END_017t") == 1
            && !p.GraveyardZone.Any(c => c.Card.Id == "END_017t"), "discard delivers reward without burn");
    }

    private static void LastCardTrade()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1; var quest = Install(g);
        var source = Add(p, "TLC_255"); Fill(g, p);
        foreach (var card in p.HandZone.ToArray().Where(c => c != source)) p.GraveyardZone.Add(p.HandZone.Remove(card));
        g.ProcessTasks();
        Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.TradeCardTask.Any(p, source)), "last-card trade accepted");
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && p.HandZone.Count == 2
            && p.HandZone.Count(c => c.Card.Id == "END_017t") == 1, "last-card trade settles empty-hand fact before replacement draw");
    }

    private static void LastSpellDraws()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1; var quest = Install(g);
        Add(p, "CS2_023"); Fill(g, p);
        foreach (var card in p.HandZone.ToArray().Where(c => c.Card.Id != "CS2_023")) p.GraveyardZone.Add(p.HandZone.Remove(card));
        g.ProcessTasks(); PlayEntity(g, p, p.HandZone.Single());
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && p.HandZone.Count == 3
            && p.HandZone.Count(c => c.Card.Id == "END_017t") == 1, "last spell empties hand before Arcane Intellect draws");
    }
}
