using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15QuestClosureProof
{
    [GroupProof("b15-quest-closure")]
    internal static void Run()
    {
        Core2026Proof.Check(Cards.All.FirstOrDefault(c => c.Id == "TLC_817")?.Implemented == true, "quest closure split recipe missing");
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        Play(g, p, "TLC_817");
        Core2026Proof.Check(p.SecretZone.ModernQuests.Count == 2, "split quest installs both components");
        RewindState(g);
        p = g.Player1;
        var copy = Game.LoadSnapshot(g.SaveSnapshot());
        Core2026Proof.Check(copy.Player1.SecretZone.ModernQuests.Count == 2, "split quest snapshot membership");
        var holy = p.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t");
        QuestRules.Advance(holy, 4);
        Core2026Proof.Check(p.SecretZone.ModernQuests.Single().Card.Id == "TLC_817t2", "one completion leaves sibling active");
        Core2026Proof.Check(!QuestRules.CanInstall(p, Cards.FromId("TLC_426")), "unrelated quest blocked by sibling");
        foreach (var branch in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var sibling = branch.Player1.SecretZone.ModernQuests.Single();
            QuestRules.Advance(sibling, 4);
            Core2026Proof.Check(branch.Player1.SecretZone.ModernQuests.Count == 0
                && branch.Player1.HandZone.Single().Card.Id == "TLC_817t5", "resumed sibling completes and combines once");
        }
        Core2026Proof.Check(p.SecretZone.ModernQuests.Count == 1, "clone sibling completion independent");
        foreach (int secrets in new[] { 3, 4 })
        {
            var capacity = QuestLifecycleProof.Fixture();
            foreach (string id in new[] { "EX1_287", "EX1_289", "EX1_295", "EX1_294" }.Take(secrets)) Play(capacity, capacity.Player1, id);
            var wrapper = Add(capacity.Player1, "TLC_817");
            Core2026Proof.Check(wrapper.IsPlayableByPlayer == (secrets == 3), "split requires two free shared slots");
            if (secrets == 3) Play(capacity, capacity.Player1, "TLC_817");
        }
        CorpseProgress();
        var matching = QuestLifecycleProof.Fixture();
        Play(matching, matching.Player1, "TLC_229"); Play(matching, matching.Player1, "EX1_506");
        Enchantment.GetInstance(matching.Player1, matching.Player1.Hero, matching.Player1,
            Cards.FromId("TLC_229t14e"), Cards.FromId("UNG_999t3").AssetId);
        RewindState(matching);
        var played = (Minion)Play(matching, matching.Player1, "CS2_172");
        Core2026Proof.Check(matching.Player1.SecretZone.ModernQuests.Single().QuestProgress == 2 && played.AttackDamage == 6,
            "Rewind retains matching basis and actual Adapt memory");
        Console.WriteLine("PASS b15 quest closure facility");
    }
    internal static void RewindState(Game g)
    {
        string State() => string.Join(";", g.Player1.SecretZone.ModernQuests.Select(q => q.Id + ":" + q.QuestProgress + ":"
            + string.Join(",", Enumerable.Range(33060, 6).Select(i => q[(GameTag)i]))))
            + "|" + string.Join(",", g.Player1.AppliedEnchantments?.Where(e => e.Card.Id == "TLC_229t14e").Select(e => e.ScriptTag1)
                ?? Enumerable.Empty<int>()) + "|" + g.Player1[(GameTag)33066];
        string before = State();
        Play(g, g.Player1, "TIME_001");
        Core2026Proof.Check(g.Rewind != null, "quest state fixture opens Rewind");
        var choice = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(choice.Id, choice.ControllerId,
            new[] { g.Rewind.RewindEntityId }), out _), "Rewind accepted");
        while (g.Rewind != null)
        {
            choice = g.GetPendingDecisions().Single();
            Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(choice.Id, choice.ControllerId,
                new[] { g.Rewind.KeepEntityId }), out _), "keep replay accepted");
        }
        Core2026Proof.Check(State() == before, "Rewind preserves independent quest progress/masks and Adapt memories");
    }

    private static void CorpseProgress()
    {
        var card = Cards.FromId("UNG_028"); var original = card.Power;
        QuestDefinition.Register(new QuestDefinition(card.AssetId, 3, QuestRewardKind.CardToHand, "CS2_231"));
        QuestRules.RegisterCorpseSpend(card.AssetId);
        card.Power = new SabberStoneCore.Enchants.Power();
        try
        {
            var g = new Game(new SabberStoneCore.Config.GameConfig { EnableCorpses = true, RandomSeed = 19,
                StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true, Logging = false,
                FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            g.StartGame(); var p = g.Player1;
            foreach (var player in new[] { p, p.Opponent })
                foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
            p.BaseMana = 10;
            var quest = (Spell)Play(g, p, "UNG_028");
            Core2026Proof.Check(!CorpseRules.TrySpend(p, 1) && quest.QuestProgress == 0, "failed spend adds no progress");
            CorpseRules.Gain(p, 4); CorpseRules.Gain(p.Opponent, 4);
            CorpseRules.TrySpend(p.Opponent, 2); CorpseRules.TrySpend(p, 0);
            Core2026Proof.Check(quest.QuestProgress == 0, "gain, zero and opponent spend add no progress");
            Core2026Proof.Check(CorpseRules.TrySpend(p, 2) && quest.QuestProgress == 2, "actual spend advances by amount");
            RewindState(g); p = g.Player1; quest = (Spell)g.IdEntityDic[quest.Id];
            foreach (var branch in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                Core2026Proof.Check(CorpseRules.SpendUpTo(branch.Player1, 9) == 2
                    && branch.Player1.SecretZone.ModernQuests.Count == 0, "resumed partial spend clamps and completes once");
                Core2026Proof.Check(branch.Player1.HandZone.Count(c => c.Card.Id == "CS2_231") == 1, "one corpse reward");
            }
            Core2026Proof.Check(quest.QuestProgress == 2, "corpse clones independent");
            p.SecretZone.Remove(quest); p.GraveyardZone.Add(quest);
            CorpseRules.SpendUpTo(p, 9);
            Core2026Proof.Check(quest.QuestProgress == 2 && p.HandZone.Count == 0, "removed quest ignores later spend");
        }
        finally { card.Power = original; QuestDefinition.Unregister(card.AssetId); }
    }
}
