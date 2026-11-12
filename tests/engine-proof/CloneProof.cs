using System.Reflection;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CloneProof
{
    internal static void Benchmark()
    {
        var game = new Game(new GameConfig {
            RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList()
        });
        game.StartGame();
        for (int i = 0; i < 8; i++) game.Process(EndTurnTask.Any(game.CurrentPlayer));
        foreach (var player in new[] { game.Player1, game.Player2 })
        foreach (string card in new[] { "NEW1_019", "EX1_007", "EX1_162", "EX1_604", "CS2_122" })
            Entity.FromCard(player, Cards.FromId(card), zone: player.BoardZone);
        game.AuraUpdate();
        var handlers = EventFields.SelectMany(f => EngineHandlers(f, game.TriggerManager)).ToArray();
        for (int i = 0; i < 250; i++) game.CloneExact();
        var timings = new List<double>();
        var bindingTimings = new List<double>();
        for (int trial = 0; trial < 5; trial++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) GC.KeepAlive(game.CloneExact());
            timings.Add(watch.Elapsed.TotalSeconds);
            watch.Restart();
            for (int i = 0; i < 1000; i++)
            foreach (var handler in handlers)
                GC.KeepAlive(Delegate.CreateDelegate(typeof(TriggerManager.TriggerHandler), handler.Target, handler.Method));
            bindingTimings.Add(watch.Elapsed.TotalSeconds);
        }
        double median = timings.OrderBy(x => x).ElementAt(2);
        double binding = bindingTimings.OrderBy(x => x).ElementAt(2);
        Console.WriteLine($"clone benchmark: turn={game.Turn}, entities={game.IdEntityDic.Count}, handlers={handlers.Length}, " +
            $"1000 clones, median of 5 after 250 warmups: {1000 / median:F0} clones/s ({median * 1000:F2} ms); " +
            $"delegate rebinding alone={binding * 1000:F2} ms ({binding / median:P1})");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static readonly PropertyInfo RandomProperty = typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic);
    private static RngState Rng(Game game) => ((Util.DeepCloneableRandom)RandomProperty.GetValue(game)).CaptureState();

    private static List<PlayerTask> Legal(Game game) => game.CurrentPlayer.Options()
        .OrderBy(Key, StringComparer.Ordinal).ToList();

    private static string Key(PlayerTask task) => $"{task.PlayerTaskType}:{task.Controller.Id}:{task.Source?.Id}:{task.Target?.Id}:" +
        $"{(task is PlayCardTask play ? play.ZonePosition : 0)}:{task.ChooseOne}:" +
        (task is ChooseTask choice ? string.Join(",", choice.Choices) : "");

    private static void SameFuture(Game game, Game clone, int steps = 20)
    {
        var policy = new PolicyRandom(11);
        for (int i = 0; i < steps && game.State != State.COMPLETE; i++)
        {
            var legal = Legal(game);
            var clonedLegal = Legal(clone);
            Check(legal.Select(Key).SequenceEqual(clonedLegal.Select(Key)), "focused clone legal options");
            int index = policy.Index(legal.Count);
            Check(game.Process(legal[index]) && clone.Process(clonedLegal[index]), "focused continuation accepted");
            SameState(game, clone);
        }
    }

    private static void SameState(Game game, Game clone)
    {
        Check(Canonical.State(game) == Canonical.State(clone), "focused clone future state");
        Check(Rng(game).Equals(Rng(clone)), "focused clone future RNG");
        if (game.History)
            Check(game.PowerHistory.Last.Select(e => Canonical.Value(e)).SequenceEqual(
                clone.PowerHistory.Last.Select(e => Canonical.Value(e))), "focused clone future history");
    }

    private static void ExtraHeroAttacks()
    {
        foreach (bool history in new[] { true, false })
        {
            var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = history, Logging = false });
            game.StartGame();
            game.Player1.BaseMana = 10;
            var weapon = Entity.FromCard(game.Player1, Cards.FromId("TRL_325"), zone: game.Player1.HandZone);
            var victim = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.BoardZone);
            Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "Sul'thraze equipped");
            Check(game.Process(HeroAttackTask.Any(game.Player1, victim)), "Sul'thraze overkill attack");
            Check(game.Player1.Hero.ExtraAttacksThisTurn > 0 && game.Player1.Hero.CanAttack, "overkill grants extra attack");
            var clone = game.CloneExact(history: history);
            Check(clone.Player1.Hero.ExtraAttacksThisTurn == game.Player1.Hero.ExtraAttacksThisTurn, "extra attack backing field copied");
            Check(Legal(game).Select(Key).SequenceEqual(Legal(clone).Select(Key)), "extra attack legal options");
            Check(game.Process(HeroAttackTask.Any(game.Player1, game.Player2.Hero)) &&
                clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)), "granted extra attack accepted by both");
            SameState(game, clone);
            SameFuture(game, clone);
        }
    }

    private static void EnchantmentIdentity()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        game.Player1.BaseMana = 10;
        var minion = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
        var core = Entity.FromCard(game.Player1, Cards.FromId("UNG_067t1"), zone: game.Player1.HandZone);
        Check(game.Process(PlayCardTask.Any(game.Player1, core)), "Crystal Core played");
        var parent = game.Player1.AppliedEnchantments.Single(e => e.Card.Id == "UNG_067t1e");
        Check(minion.AppliedEnchantments.Any(e => ReferenceEquals(e.Creator, parent)), "Crystal Core child fixture");
        var clone = game.CloneExact(history: true);
        var clonedParent = (Enchantment)clone.IdEntityDic[parent.Id];
        var clonedMinion = (Minion)clone.IdEntityDic[minion.Id];
        Check(ReferenceEquals(clonedParent, clone.Player1.AppliedEnchantments.Single(e => e.Id == parent.Id)), "parent registry identity");
        Check(ReferenceEquals(clonedParent.OngoingEffect.Owner, clonedParent), "aura owner registry identity");
        foreach (var pair in clone.IdEntityDic)
        {
            if (pair.Value.AppliedEnchantments != null)
                foreach (var child in pair.Value.AppliedEnchantments)
                    Check(ReferenceEquals(child, clone.IdEntityDic[child.Id]), "attachment registry identity");
            if (pair.Value is Enchantment enchantment)
            {
                Check(ReferenceEquals(enchantment.Creator, clone.IdEntityDic[enchantment.Creator.Id]), "creator registry identity");
                Check(ReferenceEquals(enchantment.Target, enchantment.Target is Controller ?
                    clone.ControllerById(enchantment.Target.Id) : clone.IdEntityDic[enchantment.Target.Id]), "target registry identity");
            }
        }
        // Moving out of HAND_AND_BOARD exercises Aura.DeApply's creator reference comparison.
        game.Player1.BoardZone.Remove(minion);
        clone.Player1.BoardZone.Remove(clonedMinion);
        game.Player1.DeckZone.Add(minion);
        clone.Player1.DeckZone.Add(clonedMinion);
        game.AuraUpdate();
        clone.AuraUpdate();
        Check(!minion.AppliedEnchantments.Any(e => e.Creator == parent) &&
            !clonedMinion.AppliedEnchantments.Any(e => e.Creator == clonedParent), "aura child cleanup");
        Check(minion.AppliedEnchantments.Count == clonedMinion.AppliedEnchantments.Count, "no orphaned cloned children");
        Check(Canonical.State(game) == Canonical.State(clone), "Crystal Core cleanup state");
        SameFuture(game, clone);
    }

    private static readonly EventInfo[] EventFields = typeof(TriggerManager).GetEvents();

    private static Delegate[] EngineHandlers(EventInfo field, TriggerManager manager) =>
        manager.Events.GetSubscriptions(field.Name).Select(s => (Delegate)s.Handler)
        .Where(h => h.Method.Module.Assembly == typeof(Game).Assembly || h.Target?.GetType().Assembly == typeof(Game).Assembly)
        .ToArray();

    private static void SameSubscriptions(Game game, Game clone)
    {
        foreach (var field in EventFields)
        {
            var source = EngineHandlers(field, game.TriggerManager);
            var copied = EngineHandlers(field, clone.TriggerManager);
            Check(source.Length == copied.Length, $"engine handler count: {field.Name}");
            Check(game.TriggerManager.Events.GetSubscriptions(field.Name)
                .Where(s => source.Contains(s.Handler)).Select(s => s.Ordinal)
                .SequenceEqual(clone.TriggerManager.Events.GetSubscriptions(field.Name).Select(s => s.Ordinal)),
                $"engine registration ordinals: {field.Name}");
            for (int i = 0; i < source.Length; i++)
            {
                Check(source[i].Method == copied[i].Method, $"engine handler order: {field.Name}");
                Check(source[i].Target == null || !ReferenceEquals(source[i].Target, copied[i].Target),
                    $"engine handler source isolation: {field.Name}");
            }
        }
    }

    private static void SubscriptionOwnership()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone);
        // No current gameplay subscriber is static. Associate a synthetic static handler with
        // the engine module to exercise that ownership path without adding a live subscription.
        var method = new System.Reflection.Emit.DynamicMethod("StaticEngineHandler", typeof(void),
            new[] { typeof(IEntity) }, typeof(Game).Module);
        method.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);
        var staticHandler = (TriggerManager.TriggerHandler)method.CreateDelegate(typeof(TriggerManager.TriggerHandler));
        game.TriggerManager.EndTurnTrigger += staticHandler;
        int diagnostics = 0;
        game.TriggerManager.EndTurnTrigger += _ => diagnostics++;
        game.TriggerManager.EndTurnTrigger += ExternalObserver;
        var clone = game.CloneExact(history: true);
        SameSubscriptions(game, clone);
        clone.Process(EndTurnTask.Any(clone.CurrentPlayer));
        Check(diagnostics == 0, "external diagnostic closure excluded");
        game.TriggerManager.EndTurnTrigger -= ExternalObserver;
        game.Process(EndTurnTask.Any(game.CurrentPlayer));
        Check(diagnostics == 1, "source diagnostic remains subscribed");
        SameState(game, clone);

        var cloneHandlers = typeof(TriggerManager).GetMethod("CloneHandlers", BindingFlags.Static | BindingFlags.NonPublic);
        // An unmapped capture object must not be mistaken for an external diagnostic.
        var capturedMethod = new System.Reflection.Emit.DynamicMethod("CapturedEngineHandler", typeof(void),
            new[] { typeof(object), typeof(IEntity) }, typeof(Game).Module);
        capturedMethod.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);
        var handler = (TriggerManager.TriggerHandler)capturedMethod.CreateDelegate(typeof(TriggerManager.TriggerHandler), new object());
        bool rejected = false;
        try { cloneHandlers.Invoke(null, new object[] { handler, new Dictionary<object, object>(), clone }); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
        Check(rejected, "unmapped engine handler must fail, never disappear silently");
    }

    private static void ExternalObserver(IEntity sender) => throw new Exception("external static observer leaked into clone");

    private static void DetachedCostSubscription()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        var ghoul = Entity.FromCard(game.Player1, Cards.FromId("ICC_700"), zone: game.Player1.HandZone);
        game.Player1.Hero.Damage = 3;
        game.Player1.Hero.TakeHeal(game.Player1.Hero, 1);
        game.AuraUpdate();
        Check(ghoul.Cost == 0, "Happy Ghoul cost triggered");
        Check(game.Process(PlayCardTask.Any(game.Player1, ghoul)), "Happy Ghoul played");
        var endTurn = EventFields.Single(f => f.Name == "EndTurnTrigger");
        Check(ghoul.OngoingEffect == null && game.Auras.Count == 0 && EngineHandlers(endTurn, game.TriggerManager).Length == 1,
            "played cost aura retains end-turn subscription outside aura registry");
        var clone = game.CloneExact(history: true).CloneExact(history: true);
        Check(clone.Auras.Count == 0 && clone.IdEntityDic[ghoul.Id].OngoingEffect == null, "detached clone does not reactivate cost aura");
        SameSubscriptions(game, clone);
        game.Process(EndTurnTask.Any(game.CurrentPlayer));
        clone.Process(EndTurnTask.Any(clone.CurrentPlayer));
        SameState(game, clone);
        Check(EngineHandlers(endTurn, game.TriggerManager).Length == 0 && EngineHandlers(endTurn, clone.TriggerManager).Length == 0,
            "detached cost handlers unsubscribe from their own games");
        SameFuture(game, clone);
    }

    private static void DetachedHeroSubscription()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        game.Player1.BaseMana = 10;
        var oldHero = game.Player1.Hero;
        var gonk = (Minion)Entity.FromCard(game.Player1, Cards.FromId("TRL_241"), zone: game.Player1.BoardZone);
        var jaina = Entity.FromCard(game.Player1, Cards.FromId("ICC_833"), zone: game.Player1.HandZone);
        Check(game.Process(PlayCardTask.Any(game.Player1, jaina)), "hero replaced with Frost Lich Jaina");
        gonk.Silence();
        Check(gonk.ActivatedTrigger == null && oldHero.Events.GetSubscriptions("AfterAttackTrigger").Count == 1,
            "old hero retains removed Gonk callback");
        var clone = game.CloneExact(history: true);
        EventOrdinalProof.Same(oldHero.Events, ((Character)clone.IdEntityDic[oldHero.Id]).Events, EventOrdinalProof.LocalChannels);
        Check(clone.IdEntityDic[gonk.Id].ActivatedTrigger == null, "removed Gonk trigger stays detached");
        SameFuture(game, clone);
    }

    private static void ExpiredEnchantments()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        var card = Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_188o"), card, card);
        Check(game.OneTurnEffectEnchantments.Count == 1, "temporary enchantment fixture");
        game.Process(EndTurnTask.Any(game.CurrentPlayer));
        Check(game.OneTurnEffectEnchantments.Count == 0, "temporary enchantment expired");
        var clone = game.CloneExact(history: true);
        Check(clone.OneTurnEffectEnchantments.Count == 0, "clone must not reactivate expired enchantments");
        game.Process(EndTurnTask.Any(game.CurrentPlayer));
        clone.Process(EndTurnTask.Any(clone.CurrentPlayer));
        Check(game.PowerHistory.Last.Select(e => Canonical.Value(e)).SequenceEqual(
            clone.PowerHistory.Last.Select(e => Canonical.Value(e))), "expired enchantment does not emit cleanup twice");
    }

    private static void AncillaryRemoval()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        var card = Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(game, Cards.FromId("UNG_202e"), card, card);
        var clone = game.CloneExact(history: true);
        Check(game.Process(PlayCardTask.Any(game.Player1, card)) &&
            clone.Process(PlayCardTask.Any(clone.Player1, clone.IdEntityDic[card.Id])), "ancillary removal play accepted");
        Check(Canonical.State(game) == Canonical.State(clone), "clone preserves remove-when-played ancillary trigger");
    }

    private static void ChooseOneReferences()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, History = true, Logging = false });
        var card = Entity.FromCard(game.Player1, Cards.FromId("TRL_343"), zone: game.Player1.HandZone);
        var clone = game.CloneExact(history: true);
        var branches = clone.IdEntityDic[card.Id].ChooseOnePlayables;
        Check(branches.Length == 4 && branches.All(branch => ReferenceEquals(branch, clone.IdEntityDic[branch.Id])),
            "all four Choose One branches must belong to cloned registry");
    }

    private static void RegistrationOrder()
    {
        var game = new Game(new GameConfig { RandomSeed = 42, History = true, Logging = false });
        // The older juggler is on the right; reconstruction by board position reverses its callbacks.
        Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone);
        Entity.FromCard(game.Player2, Cards.FromId("EX1_007"), zone: game.Player2.BoardZone);
        Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone, zonePos: 0);
        game.AuraUpdate();
        var clone = game.CloneExact(history: true);
        int start = game.PowerHistory.Full.Count, cloneStart = clone.PowerHistory.Full.Count;
        Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        Entity.FromCard(clone.Player1, Cards.FromId("CS2_231"), zone: clone.Player1.BoardZone);
        Check(game.PowerHistory.Full.Skip(start).Select(e => Canonical.Value(e)).SequenceEqual(
            clone.PowerHistory.Full.Skip(cloneStart).Select(e => Canonical.Value(e))), "clone preserves interleaved listener registration order after board insertion");
        Check(Canonical.State(game) == Canonical.State(clone), "reordered listeners keep future state");
    }

    private static void CloneBookkeeping()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, History = true, Logging = false });
        int nextClone = game.NextCloneIndex;
        var clone = game.CloneExact();
        Check(game.NextCloneIndex == nextClone && clone.NextCloneIndex == nextClone,
            "exact cloning must not consume source clone index");
        var minion = (Minion)Entity.FromCard(game.Player1, Cards.FromId("EX1_335"), zone: game.Player1.BoardZone);
        clone = game.CloneExact();
        minion.IsExhausted = true;
        ((Minion)clone.IdEntityDic[minion.Id]).IsExhausted = true;
        Check(Canonical.State(game) == Canonical.State(clone), "history-disabled clone keeps source tag semantics");
        Check(clone.PowerHistory == null || clone.PowerHistory.Full.Count == 0, "history-disabled clone retains no history");
    }

    private static void EnrageRemoval()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, History = true, Logging = false });
        var minion = (Minion)Entity.FromCard(game.Player1, Cards.FromId("TLC_101"), zone: game.Player1.BoardZone);
        minion.Damage = 1;
        game.AuraUpdate();
        Check(minion.AppliedEnchantments?.Count == 1, "damaged enrage fixture has enchantment");
        var clone = game.CloneExact(history: true);
        Check(Canonical.State(game) == Canonical.State(clone), "enrage initial clone");
        minion.Damage = 0;
        ((Minion)clone.IdEntityDic[minion.Id]).Damage = 0;
        game.AuraUpdate();
        clone.AuraUpdate();
        Check(Canonical.State(game) == Canonical.State(clone), "enrage clone removes same enchantment when healed");
    }

    private static void FinjaRecruitment()
    {
        foreach (string id in new[] { "CFM_344", "CORE_CFM_344" })
        foreach (bool history in new[] { true, false })
        foreach (int spaces in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true,
                History = history, Logging = false });
            game.StartGame();
            var player = game.Player1;
            var finja = (Minion)Entity.FromCard(player, Cards.FromId(id), zone: player.BoardZone);
            while (player.BoardZone.Count < Game.MAX_MINIONS_ON_BOARD - spaces)
                Entity.FromCard(player, Cards.FromId("CS2_231"), zone: player.BoardZone);
            var recruits = new[] { "EX1_508", "EX1_507" }.Select(card =>
                Entity.FromCard(player, Cards.FromId(card), zone: player.DeckZone)).ToArray();
            var victim = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.BoardZone);
            finja.IsExhausted = false;
            Check(game.Process(MinionAttackTask.Any(player, finja, victim)), "Finja lethal attack accepted");
            Check(player.DeckZone.Count == 2 - spaces && player.BoardZone.Count == Game.MAX_MINIONS_ON_BOARD,
                "Finja moves only recruits that fit from deck to board");
            foreach (var recruit in recruits)
                Check(player.DeckZone.Count(p => p.Id == recruit.Id) + player.BoardZone.Count(p => p.Id == recruit.Id) == 1,
                    "recruited entity belongs to exactly one zone");
            var clone = game.CloneExact(history: history);
            Check(game.Hash() == clone.Hash(), "Finja aura recruits retain identical clone hash");
            Check(Canonical.State(game) == Canonical.State(clone), "Finja recruits retain identical canonical state");
            SameFuture(game, clone);
        }
    }

    internal static void Run()
    {
        FinjaRecruitment();
        ExtraHeroAttacks();
        EnchantmentIdentity();
        SubscriptionOwnership();
        DetachedCostSubscription();
        DetachedHeroSubscription();
        ExpiredEnchantments();
        AncillaryRemoval();
        ChooseOneReferences();
        RegistrationOrder();
        CloneBookkeeping();
        EnrageRemoval();
        // Enchantments, persistent triggers, positional auras, random effects and removal.
        string[] cards = { "EX1_604", "EX1_007", "NEW1_019", "NEW1_020", "EX1_162", "CS2_122",
            "CS2_189", "EX1_019", "EX1_277", "CS2_024" };
        foreach (bool sourceHistory in new[] { true, false })
        foreach (bool captureHistory in new[] { true, false })
        foreach (long seed in new long[] { 1, 19, 42, 123 })
        {
            var game = new Game(new GameConfig {
                RandomSeed = seed, StartPlayer = -1, Shuffle = true, SkipMulligan = true,
                History = sourceHistory, Logging = false, FormatType = FormatType.FT_WILD,
                Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(cards[i % cards.Length])).ToList(),
                Player2Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(cards[(i + 3) % cards.Length])).ToList()
            });
            game.StartGame();
            var policy = new PolicyRandom((ulong)seed);
            Game clone = null;
            int step = 0;
            while (game.State != State.COMPLETE && step < 2000)
            {
                if (step % 20 == 0)
                {
                    string before = Canonical.State(game);
                    RngState beforeRng = Rng(game);
                    int beforeIndex = game.NextCloneIndex, beforeHistory = game.PowerHistory?.Full.Count ?? 0;
                    clone = game.CloneExact(history: captureHistory);
                    SameSubscriptions(game, clone);
                    Check(Rng(game).Equals(beforeRng) && Rng(clone).Equals(beforeRng), "clone copies RNG without consuming source");
                    Check(game.NextCloneIndex == beforeIndex && (game.PowerHistory?.Full.Count ?? 0) == beforeHistory,
                        "clone leaves source counters/history untouched");
                    Check(Canonical.State(game) == before, $"clone mutated source: seed {seed}, step {step}");
                    Check(Canonical.State(clone) == before, $"clone.initial: seed {seed}, step {step}");
                }
                var legal = Legal(game);
                var cloneLegal = Legal(clone);
                Check(legal.Select(Key).SequenceEqual(cloneLegal.Select(Key)), $"clone.legal: seed {seed}, step {step}");
                int index = policy.Index(legal.Count);
                Check(game.Process(legal[index]) && clone.Process(cloneLegal[index]), "clone actions accepted");
                Check(Canonical.State(game) == Canonical.State(clone), $"clone.advance: seed {seed}, step {step}, action {Key(legal[index])}");
                Check(Rng(game).Equals(Rng(clone)), $"clone.rng: seed {seed}, step {step}");
                if (sourceHistory && captureHistory)
                    Check(game.PowerHistory.Last.Select(e => Canonical.Value(e)).SequenceEqual(
                        clone.PowerHistory.Last.Select(e => Canonical.Value(e))), $"clone.history: seed {seed}, step {step}");
                step++;
            }
            Check(game.State == State.COMPLETE, "clone proof game terminates");
        }
        Console.WriteLine("PASS clone state/RNG/history and 20-action futures; four seeds, four history modes, focused clone regressions");
    }
}
