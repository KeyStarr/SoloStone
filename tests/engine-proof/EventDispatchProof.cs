using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Triggers;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class EventDispatchProof
{
    private static void Check(bool condition, string message) => EventTraceProof.Check(condition, message);
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Raise(TriggerManager manager, string channel, IEntity sender, int amount = 0)
    {
        if (channel == nameof(TriggerManager.StatGainedTrigger)) { manager.Events.Raise(new StatGainedEvent((Minion)sender, sender, sender.Card, Zone.PLAY, amount, amount)); return; }
        if (channel == nameof(TriggerManager.HealIntentTrigger)) { manager.Events.Raise(new HealIntentEvent(sender)); return; }
        object[] args = channel == "GameStartTrigger" ? Array.Empty<object>() :
            channel == "CardPreparedTrigger" ? new object[] { sender, amount - 1, amount, amount } :
            channel == "OverloadTrigger" ? new object[] { sender, amount } : new object[] { sender };
        try { typeof(TriggerManager).GetMethod("On" + channel, Hidden).Invoke(manager, args); }
        catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }

    private static Game Create() => new(new GameConfig { RandomSeed = 1, Logging = false, History = true });

    private static void AllChannels()
    {
        var game = Create();
        var manager = game.TriggerManager;
        var events = typeof(TriggerManager).GetEvents();
        Check(events.Length == 32, "cover all manager channels including CARD_PREPARED, HEAL_INTENT and StatGained");
        foreach (EventInfo channel in events)
        {
            var calls = new List<string>();
            IEntity sender = channel.Name == "GameStartTrigger" ? null : channel.Name == nameof(TriggerManager.StatGainedTrigger)
                ? Entity.FromCard(game.Player1, Cards.FromId("CS2_231")) : game.Player1.Hero;
            TriggerManager.TriggerHandler first = entity => {
                Check(ReferenceEquals(entity, sender), "legacy sender identity/null");
                calls.Add("first");
            };
            TriggerManager.TriggerHandler second = _ => calls.Add("second");
            channel.AddEventHandler(manager, first);
            channel.AddEventHandler(manager, second);
            var traced = new List<(Delegate Handler, int Order)>();
            manager.Events.Trace = (value, handler, order) => {
                Check(value.Channel == channel.Name && ReferenceEquals(value.Sender, sender), "typed channel/sender");
                Check(value.GetType().Name == channel.Name.Replace("Trigger", "") + "Event", "typed channel payload");
                if (value is StatGainedEvent gain) Check(gain.Attack == 7 && gain.Health == 7 && gain.TargetId == sender.Id, "typed stat-gain payload");
                if (value is OverloadEvent overload) Check(overload.Amount == 7, "typed overload amount");
                if (value is CardPreparedEvent prepared) Check(prepared.ManaSpent == 6 && prepared.NominalDiscount == 7
                    && prepared.RealizedReduction == 7, "typed Prepare amounts");
                traced.Add((handler, order));
            };
            Raise(manager, channel.Name, sender, 7);
            Check(calls.SequenceEqual(new[] { "first", "second" }), "each channel dispatches exactly once in order");
            Check(traced.SequenceEqual(new[] { ((Delegate)first, 0), ((Delegate)second, 1) }), "trace identifies each callback/order");
            channel.RemoveEventHandler(manager, first);
            channel.RemoveEventHandler(manager, second);
            calls.Clear(); traced.Clear();
            Raise(manager, channel.Name, sender, 7);
            Check(calls.Count == 0 && traced.Count == 0, "empty channel does not invoke");
        }
    }

    private static void NestedSnapshot(bool trace)
    {
        var manager = new TriggerManager();
        var calls = new List<string>();
        var orders = new List<int>();
        if (trace) manager.Events.Trace = (_, _, order) => orders.Add(order);
        TriggerManager.TriggerHandler b = _ => calls.Add("b"), c = _ => calls.Add("c");
        bool nested = false;
        manager.DrawTrigger += _ => {
            calls.Add("a");
            if (nested) return;
            nested = true;
            manager.DrawTrigger -= b;
            manager.DrawTrigger += c;
            Raise(manager, "DrawTrigger", null);
        };
        manager.DrawTrigger += b;
        Raise(manager, "DrawTrigger", null);
        Check(calls.SequenceEqual(new[] { "a", "a", "c", "b" }), "nested invocation sees current list; outer keeps snapshot");
        if (trace) Check(orders.SequenceEqual(new[] { 0, 0, 1, 1 }), "nested trace preserves local invocation order");
        calls.Clear();
        Raise(manager, "DrawTrigger", null);
        Check(calls.SequenceEqual(new[] { "a", "c" }), "next dispatch sees add/remove");
    }

    private static void AfterAttackRoutes()
    {
        var game = Create();
        game.StartGame();
        var owner = Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        int global = 0, local = 0, raw = 0;
        var trigger = new Trigger(TriggerType.AFTER_ATTACK) {
            TriggerSource = TriggerSource.ALL, FastExecution = true,
            SingleTask = new CustomTask((_, _, _, _, _) => global++)
        }.Activate(owner);
        game.Player1.Hero.AfterAttackTrigger += _ => local++;
        TriggerManager.TriggerHandler handler = _ => raw++;
        game.TriggerManager.AddTrigger(TriggerType.AFTER_ATTACK, handler);
        var registrations = game.TriggerManager.Events.GetSubscriptions("AfterAttackTrigger");
        Check(registrations.Count == 2 && ReferenceEquals(registrations[0].Owner, trigger),
            "global AFTER_ATTACK activation and AddTrigger share ordinal registry");
        Raise(game.TriggerManager, "AfterAttackTrigger", game.Player1.Hero);
        Check(global == 1 && raw == 1 && local == 0, "global AFTER_ATTACK excludes character subscribers");
        game.Player1.Hero.OnAfterAttackTrigger();
        Check(global == 1 && raw == 1 && local == 1, "character AFTER_ATTACK excludes global subscribers");
        trigger.Remove();
        game.TriggerManager.RemoveTrigger(TriggerType.AFTER_ATTACK, handler);
        Check(game.TriggerManager.Events.GetSubscriptions("AfterAttackTrigger").Count == 0,
            "global AFTER_ATTACK removal clears both registration paths");
        Raise(game.TriggerManager, "AfterAttackTrigger", game.Player1.Hero);
        Check(global == 1 && raw == 1, "removed AFTER_ATTACK handlers stay silent");
    }

    // Oracle is the CLR's multicast invocation itself, independently of the bus adapter.
    private static string Mutations(int seed, bool bus, bool trace, string channel, bool local = false)
    {
        var game = Create();
        var manager = game.TriggerManager;
        IEntity sender = channel == nameof(TriggerManager.StatGainedTrigger)
            ? Entity.FromCard(game.Player1, Cards.FromId("CS2_231")) : game.Player1.Hero;
        object owner = local ? game.Player1.Hero : manager;
        var route = owner.GetType().GetEvent(channel);
        TriggerManager.TriggerHandler legacy = null;
        var handlers = new TriggerManager.TriggerHandler[5];
        var calls = new List<string>();
        var policy = new PolicyRandom((ulong)seed);
        int depth = 0, mutations = 0;
        void Add(TriggerManager.TriggerHandler handler) { if (bus) route.AddEventHandler(owner, handler); else legacy += handler; }
        void Remove(TriggerManager.TriggerHandler handler) { if (bus) route.RemoveEventHandler(owner, handler); else legacy -= handler; }
        void Invoke()
        {
            if (!bus) { legacy?.Invoke(null); return; }
            if (!local) Raise(manager, channel, sender);
            else if (channel == "AfterAttackTrigger") game.Player1.Hero.OnAfterAttackTrigger();
            else
            {
                game.Player1.Hero.Damage = 0;
                game.Player1.Hero.TakeDamage(game.Player2.Hero, 1);
            }
        }
        for (int i = 0; i < handlers.Length; i++)
        {
            int id = i;
            handlers[i] = _ => {
                calls.Add($"{depth}:{id}");
                if (++mutations > 100) return;
                int op = policy.Index(4), pick = policy.Index(handlers.Length);
                if (op == 0) Add(handlers[pick]);
                if (op == 1) Remove(handlers[pick]);
                if (op == 2) Remove(handlers[pick] + handlers[(pick + 1) % handlers.Length]);
                if (op == 3 && depth < 2) { depth++; Invoke(); depth--; }
            };
        }
        int traced = 0;
        if (trace)
        {
            var events = local ? ((Character)owner).Events : manager.Events;
            events.Trace = (_, _, _) => traced++;
        }
        foreach (var handler in handlers) Add(handler);
        Add(handlers[0] + handlers[1]); // Duplicate and compound removal must use Delegate.Remove semantics.
        for (int i = 0; i < 12; i++)
        {
            Invoke();
            Add(handlers[policy.Index(handlers.Length)]);
            Remove(handlers[policy.Index(handlers.Length)]);
        }
        if (trace) Check(traced == calls.Count, "one trace entry per callback under mutation");
        return string.Join(',', calls);
    }

    private static int Depth(Game game) => ((ICollection)typeof(TaskQueue).GetField("_eventStack", Hidden)
        .GetValue(game.TaskQueue)).Count;
    private static readonly PropertyInfo Metadata = typeof(Game).GetProperty("CurrentEventData", Hidden);
    private static int Amount(Game game) => (int)Metadata.PropertyType.GetProperty("EventNumber").GetValue(Metadata.GetValue(game));

    private static void Wrappers(bool trace)
    {
        var game = Create();
        var manager = game.TriggerManager;
        var calls = new List<string>();
        if (trace) manager.Events.Trace = (_, _, _) => { };
        manager.ZoneTrigger += sender => {
            Check(Depth(game) == 1, "zone callback inside existing wrapper");
            calls.Add("callback");
            game.TaskQueue.Enqueue(new CustomTask((_, _, _, _, _) => calls.Add("child")), game.Player1, sender, null);
        };
        game.TaskQueue.Enqueue(new CustomTask((_, _, _, _, _) => calls.Add("parent")), game.Player1, game.Player1.Hero, null);
        Raise(manager, "ZoneTrigger", game.Player1.Hero);
        Check(calls.SequenceEqual(new[] { "callback", "child" }) && Depth(game) == 0,
            "zone drains child tasks and restores parent without draining it");
        typeof(Game).GetMethod("ProcessTasks", Hidden).Invoke(game, null);
        Check(calls.SequenceEqual(new[] { "callback", "child", "parent" }), "parent queue survives zone wrapper");

        object initial = Metadata.GetValue(game);
        bool nested = false;
        manager.OverloadTrigger += sender => {
            Check(Amount(game) == (nested ? 3 : 7), "overload installs amount");
            object current = Metadata.GetValue(game);
            Check(ReferenceEquals(Metadata.PropertyType.GetProperty("EventSource").GetValue(current), sender), "overload source");
            if (nested) return;
            nested = true;
            Raise(manager, "OverloadTrigger", sender, 3);
            Check(ReferenceEquals(Metadata.GetValue(game), current), "nested overload restores outer metadata");
        };
        Raise(manager, "OverloadTrigger", game.Player1.Hero, 7);
        Check(ReferenceEquals(Metadata.GetValue(game), initial), "overload restores original metadata");

        var failure = new InvalidOperationException("subscriber failed");
        bool later = false;
        manager.HealTrigger += _ => throw failure;
        manager.HealTrigger += _ => later = true;
        try { Raise(manager, "HealTrigger", null); throw new Exception("missing subscriber exception"); }
        catch (InvalidOperationException error) { Check(ReferenceEquals(error, failure), "same subscriber exception"); }
        Check(!later, "subscriber exception stops invocation list");

        // Legacy wrappers deliberately lack finally cleanup. Do not silently fix exceptional paths.
        manager.OverloadTrigger += _ => throw failure;
        try { Raise(manager, "OverloadTrigger", game.Player1.Hero, 3); }
        catch (InvalidOperationException) { }
        Check(Amount(game) == 3, "legacy exceptional overload retains metadata");
        manager.ZoneTrigger += _ => throw failure;
        try { Raise(manager, "ZoneTrigger", game.Player1.Hero); }
        catch (InvalidOperationException) { }
        Check(Depth(game) == 1, "legacy exceptional zone retains child frame");
    }

    private static void TriggerQuirks()
    {
        var game = Create();
        game.StartGame();
        var calls = new List<string>();
        IPlayable Owner() => Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        Trigger Listen(IPlayable owner, TriggerType type, Action action, bool fast = false, bool once = false) =>
            new Trigger(type) { TriggerSource = TriggerSource.ALL, FastExecution = fast, RemoveAfterTriggered = once,
                SingleTask = new CustomTask((_, _, _, _, _) => action()) }.Activate(owner);
        void Drain() => typeof(Game).GetMethod("ProcessTasks", Hidden).Invoke(game, null);

        var normal = Listen(Owner(), TriggerType.DRAW, () => calls.Add("queued"));
        Listen(Owner(), TriggerType.DRAW, () => { calls.Add("fast"); normal.Remove(); }, fast: true, once: true);
        game.TriggerManager.DrawTrigger += _ => calls.Add("raw");
        Raise(game.TriggerManager, "DrawTrigger", game.Player1.Hero);
        Check(calls.SequenceEqual(new[] { "fast", "raw" }), "normal queues; fast executes inline in registration order");
        Drain();
        Check(calls.SequenceEqual(new[] { "fast", "raw", "queued" }), "removing owner preserves already queued task");

        calls.Clear();
        var once = Listen(Owner(), TriggerType.HEAL, () => calls.Add("once"), fast: true, once: true);
        var duplicate = game.TriggerManager.Events.GetSubscriptions("HealTrigger").Single().Handler;
        game.TriggerManager.HealTrigger += duplicate;
        Raise(game.TriggerManager, "HealTrigger", game.Player1.Hero);
        Check(calls.SequenceEqual(new[] { "once" }), "snapshot invokes duplicate callback but removed Trigger guard suppresses task");
        Check(game.TriggerManager.Events.GetSubscriptions("HealTrigger").Count == 1, "legacy remove removes last duplicate only");

        calls.Clear();
        var dormant = (Minion)Owner();
        Listen(dormant, TriggerType.ARMOR, () => calls.Add("awake"), fast: true);
        dormant[GameTag.DORMANT] = 1;
        Raise(game.TriggerManager, "ArmorTrigger", game.Player1.Hero);
        Check(calls.Count == 0, "dormant owner keeps callback but suppresses execution");
        dormant[GameTag.DORMANT] = 0;
        Raise(game.TriggerManager, "ArmorTrigger", game.Player1.Hero);
        Check(calls.SequenceEqual(new[] { "awake" }), "awake owner uses original registration");

        calls.Clear();
        var self = Owner();
        Listen(self, TriggerType.SUMMON, () => calls.Add("summon"), fast: true);
        Raise(game.TriggerManager, "SummonTrigger", self);
        Check(calls.Count == 0, "summon excludes its own listener");
        Raise(game.TriggerManager, "SummonTrigger", dormant);
        Check(calls.SequenceEqual(new[] { "summon" }), "summon includes other listener");

        calls.Clear();
        var secret = Entity.FromCard(game.Player1, Cards.FromId("EX1_287"), zone: game.Player1.HandZone);
        Listen(secret, TriggerType.FROZEN, () => calls.Add("secret"), fast: true);
        secret.IsExhausted = true;
        Raise(game.TriggerManager, "FreezeTrigger", game.Player2.Hero);
        Check(calls.Count == 0, "exhausted secret suppresses callback execution");
        secret.IsExhausted = false;
        Raise(game.TriggerManager, "FreezeTrigger", game.Player2.Hero);
        Check(calls.SequenceEqual(new[] { "secret" }), "unexhausted secret retains callback");
    }

    internal static void Run()
    {
        EventOrdinalProof.Run();
        AllChannels();
        AfterAttackRoutes();
        TriggerQuirks();
        foreach (bool trace in new[] { false, true })
        {
            NestedSnapshot(trace);
            Wrappers(trace);
            foreach (var channel in typeof(TriggerManager).GetEvents())
            for (int seed = 0; seed < (channel.Name == "DrawTrigger" ? 200 : 32); seed++)
                Check(Mutations(seed, false, false, channel.Name) == Mutations(seed, true, trace, channel.Name),
                    $"multicast mutation oracle {channel.Name} seed {seed}, trace={trace}");
            foreach (string channel in EventOrdinalProof.LocalChannels)
            for (int seed = 0; seed < 32; seed++)
                Check(Mutations(seed, false, false, channel, true) == Mutations(seed, true, trace, channel, true),
                    $"character multicast mutation oracle {channel} seed {seed}, trace={trace}");
        }
        var game = Create();
        game.TriggerManager.Events.Trace = (_, _, _) => throw new Exception("source diagnostic leaked to clone");
        game.TriggerManager.DrawTrigger += _ => throw new Exception("source callback leaked to clone");
        Game clone = game.CloneExact();
        Check(!ReferenceEquals(game.TriggerManager.Events, clone.TriggerManager.Events) && clone.TriggerManager.Events.Trace == null,
            "clone constructs independent bus without source diagnostics");
        Raise(clone.TriggerManager, "DrawTrigger", null);
        Console.WriteLine($"PASS {EventOrdinalProof.Channels.Length + EventOrdinalProof.LocalChannels.Length} typed channels, nested/mutation fuzz, zone/overload wrappers, exceptions, clone isolation");
    }
}
