using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class EffectContextProof
{
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("effect context: " + label);
    }

    private static RngState Rng(Game game) => game.Random.CaptureState();
    private static string State(Game game) => Canonical.State(game.CloneExact());
    private static void Same(Game expected, Game actual, string label, bool history = true, bool suffix = true)
    {
        Check(State(expected) == State(actual), label + " state");
        Check(Rng(expected).Equals(Rng(actual)), label + " RNG words/count");
        if (history) Check(Canonical.History(expected) == Canonical.History(actual), label + " full history bytes");
        if (suffix) Check(Canonical.Value(expected.PowerHistory.Last) == Canonical.Value(actual.PowerHistory.Last), label + " history suffix bytes");
        Check(expected.GetPendingDecisions().Select(d => d.ToJson()).SequenceEqual(
            actual.GetPendingDecisions().Select(d => d.ToJson())), label + " offers/decision IDs");
        Check(Canonical.Value(expected.IdEntityDic.Select(p => p).OrderBy(p => p.Key).Select(p => new { p.Key, p.Value.NativeTags })) ==
            Canonical.Value(actual.IdEntityDic.Select(p => p).OrderBy(p => p.Key).Select(p => new { p.Key, p.Value.NativeTags })), label + " native entity tags");
    }

    private static Game NewGame(long seed = 17, FormatType format = FormatType.FT_WILD)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false, FormatType = format, Player1HeroClass = CardClass.HUNTER,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); game.Player1.BaseMana = 10;
        return game;
    }

    private static void Step(Game game, PlayerTask task) => Check(game.Process(task), "accepted " + task.PlayerTaskType);
    private static Minion Summon(Game game, string card, bool enemy = false)
    {
        var entity = (Minion)Entity.FromCard(enemy ? game.Player2 : game.Player1, Cards.FromId(card));
        Generic.SummonBlock(game, entity, -1, null);
        game.DeathProcessingAndAuraUpdate();
        return entity;
    }

    private static (Game game, IPlayable source, IPlayable target) OperationFixture()
    {
        var game = NewGame();
        var source = Entity.FromCard(game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.HandZone);
        Summon(game, "CS2_052"); // spell damage
        Summon(game, "EX1_096", true); // damage/draw trigger
        var target = Summon(game, "EX1_008", true); // divine shield
        game.Player1.Hero.Damage = 5;
        return (game, source, target);
    }

    private static void Execute(Game game, IPlayable source, IPlayable target, ISimpleTask task)
    {
        game.TaskQueue.Enqueue(task, game.Player1, source, target);
        game.ProcessTasks();
        game.DeathProcessingAndAuraUpdate();
    }

    private static void Operations()
    {
        void Pair(string name, ISimpleTask legacy, Func<EffectContext, TaskState> typed,
            Action<Game> prepare = null)
        {
            var a = OperationFixture(); var b = OperationFixture();
            prepare?.Invoke(a.game); prepare?.Invoke(b.game);
            Execute(a.game, a.source, a.target, legacy);
            Execute(b.game, b.source, b.target, EffectContext.Task(typed));
            Same(a.game, b.game, name);
        }
        Pair("shield damage", new DamageTask(2, EntityType.TARGET, true), c => c.Damage(c.TargetId.Value, 2, true));
        Pair("immune damage", new DamageTask(2, EntityType.TARGET, true), c => c.Damage(c.TargetId.Value, 2, true),
            g => g.Player2.BoardZone.Last()[GameTag.IMMUNE] = 1);
        Pair("heal", new HealTask(3, EntityType.TARGET), c => c.Heal(c.TargetId.Value, 3),
            g => { var m = g.Player2.BoardZone.Last(); m[GameTag.HEALTH] = 5; m.Damage = 2; });
        Pair("draw", new DrawTask(2), c => c.Draw(2));
        Pair("buff", new AddEnchantmentTask("CS2_009e", EntityType.TARGET), c => c.GiveBuff(c.TargetId.Value, "CS2_009e"));
        Pair("transform", new TransformTask("CS2_tk1", EntityType.TARGET), c => c.Transform(c.TargetId.Value, "CS2_tk1"));
        Pair("hand", new AddCardTo("CS2_231", EntityType.HAND, 2), c => c.AddToHand("CS2_231", 2));
        Pair("hand full", new AddCardTo("CS2_231", EntityType.HAND, 2), c => c.AddToHand("CS2_231", 2),
            g => { while (g.Player1.HandZone.Count < 9) Entity.FromCard(g.Player1, Cards.FromId("CS2_231"), zone: g.Player1.HandZone); });
        Pair("destroy", new DestroyTask(EntityType.TARGET), c => c.Destroy(c.TargetId.Value));
        Pair("discover pool", new DiscoverTask(DiscoverType.BASIC_TOTEM), c => c.Discover(DiscoverType.BASIC_TOTEM));
        var offers = new[] { Cards.FromId("CS2_231"), Cards.FromId("CS2_182"), Cards.FromId("EX1_008") };
        Pair("discover offers", new CustomTask((g, c, s, t, stack) =>
            Generic.CreateChoiceCards(c, s, null, ChoiceType.GENERAL, ChoiceAction.HAND, offers, null)),
            c => { Check(c.Discover(offers), "offers installed"); return TaskState.COMPLETE; });
        Pair("legacy STOP", new DamageTask(0, EntityType.TARGET), c => c.RunLegacy(new DamageTask(0, EntityType.TARGET)));
        foreach (int position in new[] { -1, 0, 1, 20 })
        {
            var a = OperationFixture(); var b = OperationFixture();
            var stack = new TaskStack { Playables = new List<IPlayable>(), Number = position + 1 };
            new SummonTask(position < 0 ? SummonSide.DEFAULT : SummonSide.NUMBER, Cards.FromId("CS2_231"), addToStack: true)
                .Process(a.game, a.game.Player1, a.source, null, stack);
            int id = new EffectContext(b.game, b.game.Player1.Id, b.source.Id).Summon("CS2_231", position);
            Check(id == stack.Playables.Single().Id, "summon return ID");
            a.game.DeathProcessingAndAuraUpdate(); b.game.DeathProcessingAndAuraUpdate(); Same(a.game, b.game, "summon position " + position);
        }
        {
            var a = OperationFixture(); var b = OperationFixture();
            foreach (var game in new[] { a.game, b.game }) while (!game.Player1.BoardZone.IsFull) Summon(game, "CS2_231");
            int before = b.game.IdEntityDic.Count;
            new SummonTask("CS2_231").Process(a.game, a.game.Player1, a.source, null);
            Check(new EffectContext(b.game, b.game.Player1.Id, b.source.Id).Summon("CS2_231") == 0 && b.game.IdEntityDic.Count == before,
                "full board creates no entity");
            Same(a.game, b.game, "full board summon");
        }
        {
            var a = OperationFixture(); var b = OperationFixture();
            var targets = a.game.Player2.BoardZone.Select(m => m.Id).Concat(new[] { a.target.Id }).ToArray();
            new DamageTask(2, EntityType.STACK, true).Process(a.game, a.game.Player1, a.source, null,
                new TaskStack { Playables = targets.Select(id => a.game.IdEntityDic[id]).ToList() });
            new EffectContext(b.game, b.game.Player1.Id, b.source.Id).DamageBatch(targets, 2, true);
            a.game.DeathProcessingAndAuraUpdate(); b.game.DeathProcessingAndAuraUpdate(); Same(a.game, b.game, "batch order and duplicates");
            for (int count = 0; count <= 8; count++)
            {
                var pool = Enumerable.Range(0, 10).ToArray();
                var expected = pool.ChooseNElements(count, a.game.Random);
                Check(new EffectContext(b.game, b.game.Player1.Id, b.source.Id).SampleDistinct(pool, count).SequenceEqual(expected), "sample order");
                Check(Rng(a.game).Equals(Rng(b.game)), "sample rejection draw count");
            }
        }
        Console.WriteLine("PASS EffectContext operations: primitive history/state/RNG parity, shields, immunity, triggers, capacity, positions, batch order, sampling");
    }

    private static Game BeastFixture(long seed, FormatType format, int board)
    {
        var game = NewGame(seed, format);
        var rexxar = Entity.FromCard(game.Player1, Cards.FromId("ICC_828"), zone: game.Player1.HandZone);
        Step(game, PlayCardTask.Any(game.Player1, rexxar));
        for (int i = 0; i < board; i++) Summon(game, i == 0 ? "AT_100" : "CS2_231"); // Inspire before external answers
        game.Player1.UsedMana = 0;
        return game;
    }

    private static void Activate(Game game, bool legacy)
    {
        var power = game.Player1.Hero.HeroPower.Card.Power;
        var module = power.PowerTask;
        try
        {
            if (legacy) power.PowerTask = SpecificTask.BuildABeast;
            Step(game, HeroPowerTask.Any(game.Player1));
        }
        finally { power.PowerTask = module; }
    }

    private static void Pick(Game game, int index)
    {
        var decision = game.GetPendingDecisions().Single();
        var rng = Rng(game);
        Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { decision.Options[index % decision.Options.Count].OptionId }), out string error), "choice accepted: " + error);
        Check(Rng(game).Equals(rng), "answers consume no RNG");
    }

    private static void Resume(Game game, params int[] picks)
    {
        var clone = game.CloneExact(history: true);
        byte[] bytes = game.SaveSnapshot();
        var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "Discover boundary snapshot bytes round-trip");
        Same(game, loaded, "snapshot boundary"); Same(game, clone, "clone boundary", false, false);
        foreach (int pick in picks)
        {
            Pick(game, pick); Pick(clone, pick); Pick(loaded, pick);
            Same(game, clone, "clone resume", false); Same(game, loaded, "snapshot resume");
        }
    }

    private static void Beasts(bool full)
    {
        Check(EffectModules.ByDbfId[BuildABeast.DbfId] == "ICC_828p" &&
            BuildABeast.Components.All(p => EffectModules.ByDbfId[p.Key] == p.Value), "dbfId module/components registered");
        int scenarios = 0;
        {
            foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
            {
                foreach (int seed in full ? new[] { 1, 17, 91, 307 } : new[] { 17, 91 })
                foreach (int board in new[] { 0, 3, 7 })
                for (int first = 0; first < 3; first++)
                for (int second = 0; second < 3; second++)
                {
                    var old = BeastFixture(seed, format, board); var current = BeastFixture(seed, format, board);
                    var before = Rng(current);
                    Activate(old, true); Activate(current, false); Same(old, current, "first prompt");
                    Check(Rng(current).DrawCount >= before.DrawCount + 6, "both offers pre-sampled");
                    var choice = current.Player1.Choice;
                    Check(choice.Choices.Count == 3 && choice.NextChoice?.Choices.Count == 3 && choice.NextChoice.NextChoice == null,
                        "both groups exist at first prompt");
                    int[] ids = choice.Choices.Concat(choice.NextChoice.Choices).ToArray();
                    Check(ids.SequenceEqual(ids.OrderBy(id => id)) && ids.Distinct().Count() == 6, "first group allocated before second");
                    Check(ids.All(id => current.IdEntityDic[id].Zone.Type == Zone.SETASIDE &&
                        current.IdEntityDic[id][GameTag.CREATOR] == current.Player1.Hero.HeroPower.Id &&
                        current.IdEntityDic[id][GameTag.DISPLAYED_CREATOR] == current.Player1.Hero.HeroPower.Id), "offer zones/creators");
                    var firstCard = current.IdEntityDic[choice.Choices[first]].Card;
                    var secondCard = current.IdEntityDic[choice.NextChoice.Choices[second]].Card;
                    if (first == 0 && second == 0)
                    {
                        var checkpoint = Game.LoadSnapshot(current.SaveSnapshot());
                        Resume(checkpoint, first, second); // both answers from the first boundary
                    }
                    Pick(old, first); Pick(current, first); Same(old, current, "second prompt");
                    bool fullHand = board == 7;
                    if (fullHand)
                        foreach (var game in new[] { old, current })
                            while (!game.Player1.HandZone.IsFull) Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
                    if (first == 0 && second == 0)
                    {
                        var checkpoint = Game.LoadSnapshot(current.SaveSnapshot()); Resume(checkpoint, second);
                    }
                    Pick(old, second); Pick(current, second); Same(old, current, "composed beast");
                    var beast = current.IdEntityDic.Values.Single(e => e.Card.Name == "Zombeast");
                    Check(beast.Card.ATK == firstCard.ATK + secondCard.ATK && beast.Card.Health == firstCard.Health + secondCard.Health &&
                        beast.Card.Cost == firstCard.Cost + secondCard.Cost && ReferenceEquals(beast.Card.Power, firstCard.Power), "composition stats/power");
                    Check(beast[GameTag.DISPLAYED_CREATOR] == current.Player1.Hero.HeroPower.Id &&
                        beast.Zone.Type == (fullHand ? Zone.GRAVEYARD : Zone.HAND), "generated creator/capacity");
                    var loaded = Game.LoadSnapshot(current.SaveSnapshot()); Same(current, loaded, "dynamic card snapshot");
                    Check(!ReferenceEquals(loaded.IdEntityDic[beast.Id].Card, beast.Card) &&
                        Canonical.Value(loaded.IdEntityDic[beast.Id].Card.Tags) == Canonical.Value(beast.Card.Tags), "dynamic descriptor local/tags");
                    if (!fullHand && first == 0 && second == 0)
                    {
                        foreach (var game in new[] { old, current, loaded })
                        {
                            Step(game, EndTurnTask.Any(game.CurrentPlayer)); Step(game, EndTurnTask.Any(game.CurrentPlayer));
                        }
                        Activate(old, true); Activate(current, false); Activate(loaded, false);
                        Same(old, current, "repeat power"); Same(current, loaded, "restored repeat power");
                        foreach (var game in new[] { old, current, loaded }) { Pick(game, 2); Pick(game, 1); }
                        Same(old, current, "repeat answers"); Same(current, loaded, "restored repeat answers");
                    }
                    scenarios++;
                }
            }
            // Mixed formats in one process: each game offers from its own format's pools.
            foreach (var order in new[] { new[] { FormatType.FT_STANDARD, FormatType.FT_WILD }, new[] { FormatType.FT_WILD, FormatType.FT_STANDARD } })
            foreach (var format in order)
            {
                var old = BeastFixture(91, format, 0); var current = BeastFixture(91, format, 0);
                Activate(old, true); Activate(current, false); Same(old, current, "mixed-format offers");
                string prefix = LegacyGenerationV1.Format(format);
                var choice = current.Player1.Choice;
                Check(choice.Choices.All(id => LegacyGenerationV1.Get(prefix + "/Beasts/First").Contains(current.IdEntityDic[id].Card))
                    && choice.NextChoice.Choices.All(id => LegacyGenerationV1.Get(prefix + "/Beasts/Second").Contains(current.IdEntityDic[id].Card)),
                    $"offers from the {format} pools after {order[0]}");
                Pick(old, 0); Pick(old, 1); Pick(current, 0); Pick(current, 1); Same(old, current, "mixed-format answers");
            }
        }
        Console.WriteLine($"PASS Build-A-Beast: {scenarios} seeded legacy/module scenarios; all 9 pick pairs, 0/3/7 boards, full hand, repeat, both Discover clone/snapshot resumes, dynamic descriptors, mixed-format per-game pools");
    }

    internal static void Run(bool full) { Operations(); Beasts(full); }
}
