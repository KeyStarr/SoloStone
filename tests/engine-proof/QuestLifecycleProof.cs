using SabberStoneCore.Config;
using HsSim;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Triggers;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class QuestLifecycleProof
{
    internal static Game Fixture(bool history = true)
    {
        var game = new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.PALADIN, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            p.BaseMana = 10;
        }
        return game;
    }

    private static readonly string[] Secrets = { "EX1_287", "EX1_289", "EX1_295", "EX1_294", "tt_010" };

    [GroupProof("automatic-quest-reveal")]
    internal static void AutomaticQuestReveal()
    {
        foreach (string id in new[] { "TLC_426", "UNG_942" })
        foreach (bool opponent in new[] { false, true })
        {
            var g = Fixture(); var p = opponent ? g.Player2 : g.Player1;
            var quest = (Spell)Entity.FromCard(p, Cards.FromId(id));
            int cursor = g.PowerHistory.Full.Count;
            new PlayTask(PlayType.SPELL).Process(g, p, p.Hero, null,
                new TaskStack { Playables = new[] { (IPlayable)quest } });
            Core2026Proof.Check(g.PowerHistory.Full.Skip(cursor).OfType<SabberStoneCore.Kettle.PowerHistoryShowEntity>()
                .Any(show => show.Entity.Id == quest.Id && show.Entity.Name == id
                    && show.Entity.Tags[GameTag.ZONE] == (int)Zone.SECRET
                    && show.Entity.Tags[GameTag.ZONE_POSITION] == quest.ZonePosition + 1),
                "automatically cast quest publishes installed identity: " + id + "/" + p.PlayerId);
            Core2026Proof.Check(quest.ActivatedTrigger != null && quest.IsExhausted
                && (id != "TLC_426" || quest.QuestState != null && quest.QuestProgress == 0 && quest.QuestTotalProgress == 6),
                "automatic quest uses normal installation state");
        }
        Console.WriteLine("PASS automatic quest reveal: cast-it effect; modern/legacy; both seats");
    }

    private static void RejectQuest(Game g, Controller p, string id) => RejectPlay(g, p, id);
    private static void RejectPlay(Game g, Controller p, string id)
    {
        p.UsedMana = 0;
        var card = Add(p, id);
        Core2026Proof.Check(!card.IsPlayableByPlayer, "entity validity rejects " + id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == card.Id), "options reject " + id);
        int mana = p.UsedMana, history = p.PlayHistory.Count;
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, card)) && p.UsedMana == mana && p.PlayHistory.Count == history
            && card.Zone == p.HandZone, "rejected before payment " + id);
    }

    // Borrow only the printed quest identity; no legacy quest recipe executes in these proofs.
    internal const string SyntheticQuestId = "UNG_942";
    internal const string SyntheticRewardId = "CS2_182";
    internal static void WithSyntheticQuest(Action proof)
    {
        var card = Cards.FromId(SyntheticQuestId); var original = card.Power;
        QuestDefinition.Register(new QuestDefinition(card.AssetId, 2, QuestRewardKind.CardToHand, SyntheticRewardId));
        try
        {
            card.Power = new Power { Trigger = new Trigger(TriggerType.ZONE) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Zone?.Type == Zone.HAND && p.Card.Id == SyntheticRewardId),
                SingleTask = EffectContext.Task(ctx => { ctx.AdvanceQuest(); return TaskState.COMPLETE; })
            } };
            proof();
        }
        finally { card.Power = original; QuestDefinition.Unregister(card.AssetId); }
    }

    internal static void Qualify(Game g)
    {
        Add(g.Player1, SyntheticRewardId);
        g.ProcessTasks();
    }

    internal static int RewardCount(Game g) => g.IdEntityDic.Values.Count(p => p.Card.Id == SyntheticRewardId
        && p[GameTag.DISPLAYED_CREATOR] != 0);

    private static void PinnedRule_RewardWaitsForHandSpace(bool fullHand) => WithSyntheticQuest(() =>
    {
        var g = Fixture(); var p = g.Player1;
        var quest = (Spell)Play(g, p, SyntheticQuestId);
        var trigger = quest.ActivatedTrigger;
        Core2026Proof.Check(g.TriggerManager.Events.GetSubscriptions(nameof(TriggerManager.ZoneTrigger))
            .Any(s => ReferenceEquals(s.Owner, trigger)), "synthetic modern listener subscribed");
        Add(p.Opponent, SyntheticRewardId); Add(p, "CS2_231"); g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 0 && RewardCount(g) == 0, "enemy and nonqualifying events ignored");
        Core2026Proof.Check(!QuestRules.Advance(quest, 0) && !QuestRules.Advance(quest, -1), "nonpositive progress ignored");
        Qualify(g);
        Core2026Proof.Check(quest.QuestProgress == 1 && RewardCount(g) == 0, "real event advances without premature reward");
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "nonrepeatable clone equal mid-progress");
        Qualify(clone);
        Core2026Proof.Check(RewardCount(clone) == 1 && RewardCount(g) == 0 && quest.QuestProgress == 1,
            "nonrepeatable clone completion independent");

        int nested = 0;
        g.TriggerManager.ZoneTrigger += entity => {
            if (entity[GameTag.DISPLAYED_CREATOR] != quest.Id) return;
            nested++;
            Core2026Proof.Check(quest.QuestState.Completing && quest.ActivatedTrigger == null, "reward event sees latch and detached trigger");
            new EffectContext(g, p.Id, quest.Id).AdvanceQuest();
        };
        if (fullHand) while (p.HandZone.Count < Controller.MaxHandSize - 1) Add(p, "CS2_231");
        // Retain the actual trigger recipe in the outer queue; the next real hand event completes first.
        g.TaskQueue.Enqueue(trigger.SingleTask, p, quest, null);
        Core2026Proof.Check(quest.QuestProgress == 1 && !g.TaskQueue.IsEmpty, "callback queued before completion");
        Qualify(g);
        Core2026Proof.Check(quest.QuestProgress == 2 && RewardCount(g) == (fullHand ? 0 : 1), "threshold completes once; full-hand reward is not created yet");
        Core2026Proof.Check(nested == (fullHand ? 0 : 1), "reward itself emits qualifying hand event");
        Core2026Proof.Check(quest.Zone == p.GraveyardZone && quest.ActivatedTrigger == null && p.SecretZone.ModernQuests.Count == 0,
            "completion removes quest and trigger");
        Core2026Proof.Check(g.TriggerManager.Events.GetSubscriptions(nameof(TriggerManager.ZoneTrigger))
            .All(s => !ReferenceEquals(s.Owner, trigger)), "completed listener detached from bus");
        new EffectContext(g, p.Id, quest.Id).AdvanceQuest(int.MaxValue);
        Core2026Proof.Check(RewardCount(g) == (fullHand ? 0 : 1), "post-removal callback grants nothing");
        if (fullHand)
        {
            End(g);
            Generic.DiscardBlock(p, p.HandZone[0]);
            Core2026Proof.Check(g.CurrentPlayer == p.Opponent && nested == 1, "reward delivered on opponent turn");
        }
        var reward = g.IdEntityDic.Values.Single(c => c[GameTag.DISPLAYED_CREATOR] == quest.Id);
        Core2026Proof.Check(reward.Zone == p.HandZone && RewardCount(g) == 1,
            "reward delivered once into hand; nothing burned");

        var removed = Fixture(); var owner = removed.Player1;
        var abandoned = (Spell)Play(removed, owner, SyntheticQuestId);
        removed.TaskQueue.Enqueue(abandoned.ActivatedTrigger.SingleTask, owner, abandoned, null);
        removed.TaskQueue.Enqueue(abandoned.ActivatedTrigger.SingleTask, owner, abandoned, null);
        Core2026Proof.Check(abandoned.QuestProgress == 0 && !removed.TaskQueue.IsEmpty, "callbacks queued before removal");
        owner.SecretZone.Remove(abandoned); owner.GraveyardZone.Add(abandoned);
        removed.ProcessTasks();
        Core2026Proof.Check(abandoned.QuestProgress == 0 && RewardCount(removed) == 0 && abandoned.ActivatedTrigger == null,
            "callbacks queued before explicit removal grant nothing");

        var overflow = Fixture(); var overflowQuest = (Spell)Play(overflow, overflow.Player1, SyntheticQuestId);
        Qualify(overflow);
        Core2026Proof.Check(QuestRules.Advance(overflowQuest, int.MaxValue) && overflowQuest.QuestProgress == 2 && RewardCount(overflow) == 1,
            "overflow-safe progress clamps at threshold");
    });

    private static void AutomaticPlacement()
    {
        foreach (int occupied in new[] { 3, 4 })
        foreach (string source in new[] { "deck", "random", "mysteries", "keysmith" })
        {
            var g = Fixture(); var p = g.Player1;
            Play(g, p, "TLC_426");
            foreach (string id in Secrets.Take(occupied)) Play(g, p, id);
            var candidate = Entity.FromCard(p, Cards.FromId(Secrets[4]), zone: p.DeckZone);
            var rng = g.Random.CaptureState();
            int entities = g.IdEntityDic.Count;
            int subscriptions = g.TriggerManager.Events.GetSubscriptions(nameof(TriggerManager.CastSpellTrigger)).Count;
            ISimpleTask task = source switch {
                "deck" => ComplexTask.PutSecretFromDeck,
                "random" => SpecificTask.CastRandomSecret(CardClass.HUNTER),
                "mysteries" => Cards.FromId("ICC_086").Power.PowerTask,
                _ => SpecificTask.ArcaneKeysmith
            };
            g.TaskQueue.Enqueue(task, p, p.Hero, null);
            g.ProcessTasks();
            if (occupied == 4)
            {
                Core2026Proof.Check(p.SecretZone.Count == 4 && candidate.Zone == p.DeckZone && candidate.ActivatedTrigger == null,
                    source + " full area leaves deck and trigger untouched");
                Core2026Proof.Check(g.Random.CaptureState().Equals(rng) && g.IdEntityDic.Count == entities && p.Choice == null,
                    source + " full area creates no RNG, entity or choice");
                Core2026Proof.Check(g.TriggerManager.Events.GetSubscriptions(nameof(TriggerManager.CastSpellTrigger)).Count == subscriptions,
                    source + " full area adds no listener");
            }
            else if (source == "keysmith") Core2026Proof.Check(p.Choice != null, "keysmith offers with free slot");
            else Core2026Proof.Check(p.SecretZone.Count == 4 && p.SecretZone.IsFull, source + " fills final shared slot");
        }
        foreach (int before in new[] { 2, 3 })
        {
            var g = Fixture(); var p = g.Player1;
            Play(g, p, "TLC_426");
            foreach (string id in Secrets.Take(before)) Play(g, p, id);
            Summon(p, "ICC_204");
            var rng = g.Random.CaptureState();
            Play(g, p, Secrets[before]);
            Core2026Proof.Check(p.SecretZone.Count == 4, "Putricide respects final shared slot");
            if (before == 3) Core2026Proof.Check(g.Random.CaptureState().Equals(rng), "blocked Putricide consumes no RNG");
        }
    }

    private static void AutomaticPlacementCards()
    {
        foreach (string id in new[] { "FP1_004", "GIL_634", "TRL_530", "DAL_141", "DAL_141ts" })
        {
            var g = Fixture(); var p = g.Player1;
            Play(g, p, "TLC_426");
            foreach (string secret in Secrets.Take(4)) Play(g, p, secret);
            var candidate = Entity.FromCard(p, Cards.FromId(Secrets[4]), zone: p.DeckZone);
            var rng = g.Random.CaptureState();
            if (id.StartsWith("DAL_"))
            {
                // Forced casts still need the helper's guard, beyond normal play requirements.
                var source = (Spell)Entity.FromCard(p, Cards.FromId(id));
                Generic.CastSpell(p, g, source, null, 0);
            }
            else
            {
                var minion = (Minion)Play(g, p, id);
                if (id != "TRL_530") Kill(g, minion);
            }
            Core2026Proof.Check(p.SecretZone.Count == 4 && candidate.Zone == p.DeckZone && candidate.ActivatedTrigger == null
                && g.Random.CaptureState().Equals(rng), id + " full area rejects automatic secret before deck/trigger/RNG changes");
        }
        foreach (string id in new[] { "TLC_426", "UNG_942" })
        {
            var g = Fixture(); var p = g.Player1;
            var active = Play(g, p, "TLC_426");
            p.CardsPlayedThisTurn.Clear(); p.CardsPlayedThisTurn.Add(Cards.FromId(id));
            int entities = g.IdEntityDic.Count; var rng = g.Random.CaptureState();
            g.TaskQueue.Enqueue(SpecificTask.ArchmageVargoth, p, p.Hero, null); g.ProcessTasks();
            Core2026Proof.Check(g.IdEntityDic.Count == entities && g.Random.CaptureState().Equals(rng)
                && p.SecretZone.ModernQuests.Single().Id == active.Id && p.SecretZone.Quest == null,
                "Vargoth rejects second modern/legacy quest before entity and target RNG");
        }
    }

    private static void RetainedSecret()
    {
        var g = Fixture(); var p = g.Player1;
        Play(g, p, "TLC_426");
        foreach (string id in Secrets.Take(3)) Play(g, p, id);
        p.UsedMana = 0;
        var secret = Add(p, Secrets[4]);
        var option = p.Options().First(t => t.Source?.Id == secret.Id);
        Play(g, p, Secrets[3]);
        int paid = p.UsedMana, plays = p.PlayHistory.Count, history = g.PowerHistory.Full.Count;
        var rng = g.Random.CaptureState();
        Core2026Proof.Check(!g.Process(option), "retained secret option rejected");
        Core2026Proof.Check(p.UsedMana == paid && p.PlayHistory.Count == plays && secret.Zone == p.HandZone
            && g.PowerHistory.Full.Count == history && g.Random.CaptureState().Equals(rng),
            "retained secret rejection precedes payment, history and RNG");
    }

    private static void ModernLegendaryCondition()
    {
        var g = Fixture(); var p = g.Player1;
        var triumph = Add(p, "CATA_308");
        Core2026Proof.Check(triumph.Cost == 5, "Triumph starts undiscounted");
        var quest = (Spell)Play(g, p, "TLC_426");
        Core2026Proof.Check(triumph.Cost == 1, "modern legendary quest alone discounts Triumph");
        Summon(p, "EX1_506");
        var clone = g.CloneExact(history: true);
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var branch in new[] { clone, restored })
        {
            Core2026Proof.Check(branch.IdEntityDic[triumph.Id].Cost == 1, "copied modern quest discounts Triumph");
            var copy = branch.Player1.SecretZone.ModernQuests.Single();
            Core2026Proof.Check(copy.QuestProgress == 1, "copied quest retains progress");
            branch.Player1.SecretZone.Remove(copy); branch.Player1.GraveyardZone.Add(copy); branch.AuraUpdate();
            Core2026Proof.Check(branch.IdEntityDic[triumph.Id].Cost == 5 && triumph.Cost == 1 && quest.QuestProgress == 1,
                "quest removal restores cost independently");
        }
        p.SecretZone.Remove(quest); p.GraveyardZone.Add(quest); g.AuraUpdate();
        Core2026Proof.Check(triumph.Cost == 5, "modern quest removal restores original cost");
    }

    internal static void Run()
    {
        Core2026Proof.Check(Cards.FromId("TLC_426")?.Implemented == true, "modern quest module registered");
        var g = Fixture(); var p = g.Player1;
        var retained = Add(p, "TLC_426");
        var staleOption = p.Options().First(t => t.Source?.Id == retained.Id);
        var quest = (Spell)Play(g, p, "TLC_426");
        int paid = p.UsedMana;
        Core2026Proof.Check(!g.Process(staleOption) && p.UsedMana == paid && retained.Zone == p.HandZone,
            "stale generated option rejected before payment");
        Core2026Proof.Check(quest.QuestProgress == 0 && quest.QuestTotalProgress == 6 && quest.Zone == p.SecretZone,
            "install modern quest at zero of six");
        Core2026Proof.Check(p.SecretZone.GetAll().Length == 0 && p.SecretZone.GetAll(q => q.IsQuest).Length == 0
            && !p.SecretZone.Any(q => q.IsQuest), "quest excluded from secret-only queries");
        Core2026Proof.Check(quest.QuestState != null && p.SecretZone.ModernQuests.Single() == quest && p.SecretZone.Quest == null,
            "modern membership separate from legacy singleton");
        RejectQuest(g, p, "TLC_426"); RejectQuest(g, p, "UNG_942");
        foreach (string id in Secrets.Take(4)) Play(g, p, id);
        Core2026Proof.Check(p.SecretZone.Count == 4 && p.SecretZone.IsFull && p.SecretZone.FreeSpace == 0, "quest plus four secrets fills area");
        RejectPlay(g, p, Secrets[4]);
        bool rejected = false;
        try { p.SecretZone.Add((Spell)Entity.FromCard(p, Cards.FromId(Secrets[4]))); }
        catch (SabberStoneCore.Exceptions.ZoneException) { rejected = true; }
        Core2026Proof.Check(rejected && p.SecretZone.Count == 4, "direct secret placement respects modern shared capacity");
        var fourSecrets = Fixture();
        foreach (string id in Secrets.Take(4)) Play(fourSecrets, fourSecrets.Player1, id);
        Play(fourSecrets, fourSecrets.Player1, "TLC_426");
        Core2026Proof.Check(fourSecrets.Player1.SecretZone.IsFull, "quest installs alongside four existing secrets");
        var full = Fixture();
        foreach (string id in Secrets) Play(full, full.Player1, id);
        RejectQuest(full, full.Player1, "TLC_426");
        var legacy = Fixture(); Play(legacy, legacy.Player1, "UNG_942");
        RejectQuest(legacy, legacy.Player1, "TLC_426");
        foreach (string id in Secrets) Play(legacy, legacy.Player1, id);
        Core2026Proof.Check(legacy.Player1.SecretZone.Count == 5 && legacy.Player1.SecretZone.Quest != null,
            "legacy quest retains independent capacity");
        AutomaticPlacement(); AutomaticPlacementCards(); RetainedSecret(); ModernLegendaryCondition();
        PinnedRule_RewardWaitsForHandSpace(false); PinnedRule_RewardWaitsForHandSpace(true);
        Console.WriteLine("PASS quest lifecycle");
    }
}
