using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Events;

internal static class EventOrdinalProof
{
    // Every public channel must expose individual registrations, not an opaque multicast adapter.
    internal static readonly string[] Channels = typeof(TriggerManager).GetEvents().Select(e => e.Name).ToArray();

    internal static readonly string[] LocalChannels = { "PreDamageTrigger", "TakeDamageTrigger", "AfterAttackTrigger" };
    private static GameEventBus LocalBus(Character character) => character.Events;
    private static IEnumerable<(object owner, GameEventBus bus, string[] channels)> Routes(Game game)
    {
        yield return (game.TriggerManager, game.TriggerManager.Events, Channels);
        yield return (game.Player1.Hero, LocalBus(game.Player1.Hero), LocalChannels);
        yield return (game.Player2.Hero, LocalBus(game.Player2.Hero), LocalChannels);
    }

    private static GameEventSubscription[] Subscriptions(GameEventBus bus, string channel) => bus.GetSubscriptions(channel).ToArray();
    private static void Check(bool condition, string message) => EventTraceProof.Check(condition, message);

    internal static void Same(GameEventBus source, GameEventBus copy, IEnumerable<string> channels)
    {
        foreach (string channel in channels)
        {
            var before = Subscriptions(source, channel);
            var after = Subscriptions(copy, channel);
            Check(before.Select(s => s.Ordinal).SequenceEqual(after.Select(s => s.Ordinal)), channel + " restored registration ordinals");
            Check(before.Select(s => s.Handler.Method).SequenceEqual(after.Select(s => s.Handler.Method)), channel + " restored handler order");
            for (int i = 0; i < before.Length; i++)
                Check(before[i].Owner == null ? after[i].Owner == null : !ReferenceEquals(before[i].Owner, after[i].Owner),
                    channel + " restored owners isolated");
        }
    }

    private static void RoundTrips()
    {
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        var juggler = Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone);
        var trigger = juggler.ActivatedTrigger;
        var handler = (TriggerManager.TriggerHandler)Delegate.CreateDelegate(typeof(TriggerManager.TriggerHandler), trigger,
            trigger.GetType().GetMethod("Process", BindingFlags.Instance | BindingFlags.NonPublic));
        TriggerManager.TriggerHandler diagnostic = _ => { };
        foreach (var route in Routes(game))
        foreach (string name in route.channels)
        {
            var channel = route.owner.GetType().GetEvent(name);
            channel.AddEventHandler(route.owner, handler + handler);
            channel.RemoveEventHandler(route.owner, handler);
            channel.AddEventHandler(route.owner, diagnostic);
            channel.RemoveEventHandler(route.owner, diagnostic); // Consume ordinal, leave no unsupported snapshot callback.
        }
        game.DeathProcessingAndAuraUpdate();
        foreach (var route in Routes(game)) route.bus.Trace = (_, _, _) => throw new Exception("diagnostic restored");
        var clone = game.CloneExact(history: true);
        byte[] bytes = game.SaveSnapshot();
        var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "ordinal snapshot byte round-trip");
        var copies = new[] { clone, loaded, loaded.CloneExact(history: true) };
        foreach (var copy in copies)
        foreach (var pair in Routes(game).Zip(Routes(copy)))
        {
            Same(pair.First.bus, pair.Second.bus, pair.First.channels);
            Check(pair.Second.bus.Trace == null, "diagnostic not restored");
            foreach (string name in pair.Second.channels)
            {
                Check(Subscriptions(pair.Second.bus, name).All(s => ReferenceEquals(s.Owner, copy.IdEntityDic[juggler.Id].ActivatedTrigger)),
                    name + " registration owner aliases restored trigger");
                pair.Second.owner.GetType().GetEvent(name).AddEventHandler(pair.Second.owner, diagnostic);
            }
        }
        foreach (var route in Routes(game))
        foreach (string name in route.channels) route.owner.GetType().GetEvent(name).AddEventHandler(route.owner, diagnostic);
        foreach (var copy in copies)
        foreach (var pair in Routes(game).Zip(Routes(copy)))
        foreach (string name in pair.First.channels)
            Check(Subscriptions(pair.First.bus, name).Last().Ordinal == Subscriptions(pair.Second.bus, name).Last().Ordinal,
                name + " next ordinal survives gaps/restore");
    }

    private static void UnmappedHandlers()
    {
        // Code belongs to the engine even though its capture object does not. It cannot be
        // discarded as an external observer or rebound to an arbitrary restored owner.
        var method = new System.Reflection.Emit.DynamicMethod("UnmappedGameplayCallback", typeof(void),
            new[] { typeof(object), typeof(IEntity) }, typeof(Game).Module);
        method.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);
        var handler = (TriggerManager.TriggerHandler)method.CreateDelegate(typeof(TriggerManager.TriggerHandler), new object());
        var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, Logging = false });
        game.StartGame();
        foreach (var route in Routes(game))
        foreach (string name in route.channels)
        {
            var channel = route.owner.GetType().GetEvent(name);
            channel.AddEventHandler(route.owner, handler);
            bool cloneRejected = false, snapshotRejected = false;
            try { game.CloneExact(); }
            catch (InvalidOperationException error) when (error.Message.Contains("missing target mapping")) { cloneRejected = true; }
            try { game.SaveSnapshot(); }
            catch (NotSupportedException error) when (error.Message.Contains("Unsupported snapshot continuation")) { snapshotRejected = true; }
            Check(cloneRejected && snapshotRejected, name + " rejects unmapped gameplay handlers in clone and snapshot");
            channel.RemoveEventHandler(route.owner, handler);
        }
    }

    internal static void Run()
    {
        Check(Channels.Contains(nameof(TriggerManager.AfterAttackTrigger)),
            "global AFTER_ATTACK participates in ordinal, clone and snapshot proofs");
        foreach (bool local in new[] { false, true })
        foreach (string name in local ? LocalChannels : Channels)
        {
            var game = new Game(new GameConfig { RandomSeed = 1, Logging = false });
            object manager = local ? game.Player1.Hero : game.TriggerManager;
            var bus = local ? LocalBus(game.Player1.Hero) : game.TriggerManager.Events;
            var channel = manager.GetType().GetEvent(name);
            var owner = new List<int>();
            TriggerManager.TriggerHandler a = _ => owner.Add(1), b = _ => owner.Add(2);
            channel.AddEventHandler(manager, a + b + a + b);
            var original = Subscriptions(bus, name);
            EventTraceProof.Check(original.Length == 4, name + " has four explicit bus subscriptions");
            EventTraceProof.Check(original.Select(x => x.Ordinal).SequenceEqual(new long[] { 0, 1, 2, 3 }),
                name + " assigns registration ordinals");
            EventTraceProof.Check(ReferenceEquals(original[0].Owner, a.Target), "owner identity retained");
            channel.RemoveEventHandler(manager, a + b); // Last matching contiguous subsequence, as Delegate.Remove.
            channel.AddEventHandler(manager, a);
            var current = Subscriptions(bus, name);
            EventTraceProof.Check(current.Select(x => x.Ordinal).SequenceEqual(new long[] { 0, 1, 4 }),
                name + " preserves gaps and removes last compound match");
            EventTraceProof.Check(original.Length == 4 && original[3].Ordinal == 3, "subscription snapshot immutable");
        }
        RoundTrips();
        UnmappedHandlers();
        Console.WriteLine("PASS manager/character ordinals, owner aliases, clone/snapshot cursors and unmapped-handler rejection");
    }
}
