using System.IO;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class CorpseProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "Corpses: " + message);
    internal static Game NewGame(CardClass cls = CardClass.MAGE, bool enabled = true)
    {
        var config = new GameConfig { EnableCorpses = enabled, RandomSeed = 31, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, Logging = false, History = true, FormatType = FormatType.FT_WILD,
            Player1HeroClass = cls, Player2HeroClass = cls,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() };
        var game = new Game(config); config.EnableCorpses = !enabled;
        game.StartGame(); game.Player1.BaseMana = 10;
        Check(game.EnableCorpses == enabled, "configuration frozen at construction");
        return game;
    }
    private static string History(Game game) => string.Join("\n", game.PowerHistory.Full.Select(e => Canonical.Value(e)));
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Corpses: expected " + typeof(T).Name + ": " + message);
    }
    internal static void Run()
    {
        foreach (var cls in Cards.HeroClasses.Concat(new[] { CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT }))
        {
            var game = NewGame(cls); var p = game.Player1;
            Core2026Proof.Kill(game, Core2026Proof.Summon(p, "CS2_101t"));
            Check(CorpseRules.Available(p) == 1 && CorpseRules.Available(p.Opponent) == 0, "AllClassesAccumulate " + cls);
        }
        ControllerAtDeathOwnsCorpse(); TokensAndReborn(); MixedBoardCountsOnlyMinions(); NoCorpseToken();
        GainAndSpendCounters(); DeathCountExactlyOnce(); CloneIndependent(); SnapshotResume(); DisabledByDefaultNoStateHistoryOrRngChange();
        Console.WriteLine("PASS Corpses: all classes/control/tokens/Reborn/mixed board/no-Corpse/counters/atomic spending/death once/clone/snapshot/default disabled");
    }
    private static void ControllerAtDeathOwnsCorpse()
    {
        var game = NewGame(); var p = game.Player1;
        var minion = Core2026Proof.Summon(p, "CS2_182");
        new ControlTask(EntityType.TARGET).Process(game, p.Opponent, p.Opponent.Hero, minion);
        Check(minion.Controller == p.Opponent && CorpseRules.Available(p) == 0, "control change alone no corpse");
        Core2026Proof.Kill(game, minion);
        Check(CorpseRules.Available(p) == 0 && CorpseRules.Available(p.Opponent) == 1, "ControllerAtDeathOwnsCorpse");
    }
    private static void TokensAndReborn()
    {
        var game = NewGame(); var p = game.Player1;
        var original = Core2026Proof.Summon(p, "END_002");
        Core2026Proof.Kill(game, original);
        var reborn = p.BoardZone.Single();
        Check(reborn.Id != original.Id && reborn.Health == 1 && CorpseRules.Available(p) == 1, "Reborn is separate living minion");
        Core2026Proof.Kill(game, reborn);
        Check(CorpseRules.Available(p) == 2, "TokensAndReborn");
        var bounced = Core2026Proof.Summon(p, "CS2_101t");
        new ReturnHandTask(EntityType.TARGET).Process(game, p, p.Hero, bounced);
        var transformed = Core2026Proof.Summon(p, "CS2_182");
        Generic.ChangeEntityBlock(p, transformed, Cards.FromId("CS2_231"), true);
        game.DeathProcessingAndAuraUpdate();
        Check(CorpseRules.Available(p) == 2, "bounce/transform no corpse");
    }
    private static void MixedBoardCountsOnlyMinions()
    {
        var game = NewGame(); var p = game.Player1;
        var location = LocationProofs.Location(p);
        var first = Core2026Proof.Summon(p, "CS2_101t");
        var second = Core2026Proof.Summon(p.Opponent, "CS2_182");
        Core2026Proof.Play(game, p, "CS2_106");
        p.Hero.Weapon.Destroy(); location.Destroy(); first.Destroy(); second.Destroy();
        game.DeathProcessingAndAuraUpdate();
        Check(CorpseRules.Available(p) == 1 && CorpseRules.Available(p.Opponent) == 1
            && location.Zone == p.GraveyardZone && p.Hero.Weapon == null, "MixedBoardCountsOnlyMinions");
        p.Hero.Destroy(); game.DeathProcessingAndAuraUpdate();
        Check(CorpseRules.Available(p) == 1, "hero no corpse");
    }
    private static void NoCorpseToken()
    {
        var game = NewGame(); var p = game.Player1;
        // Token producers remain unimplemented; use their verified printed restriction on a test definition.
        foreach (bool mixed in new[] { false, true })
        {
            var definition = Cards.FromId("CS2_101t").Clone(); definition.Tags[(GameTag)2217] = 1;
            var minion = (Minion)Entity.FromCard(p, definition); Generic.SummonBlock(game, minion, -1, null);
            if (mixed) LocationProofs.Location(p).Destroy();
            Core2026Proof.Kill(game, minion);
        }
        Check(CorpseRules.Available(p) == 0, "NoCorpseToken both death paths");
    }
    private static void GainAndSpendCounters()
    {
        var game = NewGame(); var p = game.Player1;
        int before = game.PowerHistory.Full.Count;
        CorpseRules.Gain(p, 0); Check(CorpseRules.TrySpend(p, 0) && CorpseRules.SpendUpTo(p, 0) == 0, "zero operations succeed");
        Check(game.PowerHistory.Full.Count == before && !p.NativeTags.ContainsKey(GameTag.CORPSES), "zero operations emit/store nothing");
        CorpseRules.Gain(p, 5); Check(CorpseRules.TrySpend(p, 2), "spend succeeds");
        Check(p[GameTag.CORPSES] == 5 && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2 && CorpseRules.Available(p) == 3, "GainAndSpendCounters");
        string state = Canonical.State(game), history = History(game);
        Check(!CorpseRules.TrySpend(p, 4) && state == Canonical.State(game) && history == History(game), "InsufficientSpendAtomic");
        Check(CorpseRules.SpendUpTo(p, 2) == 2 && CorpseRules.SpendUpTo(p, 9) == 1 && CorpseRules.SpendUpTo(p, 1) == 0
            && p[GameTag.CORPSES] == 5 && p[GameTag.CORPSES_SPENT_THIS_GAME] == 5, "SpendUpTo");
        foreach (Action action in new Action[] { () => CorpseRules.Gain(p, -1), () => CorpseRules.TrySpend(p, -1), () => CorpseRules.SpendUpTo(p, -1) })
            Reject<ArgumentOutOfRangeException>(action, "negative rejected");
        var changes = game.PowerHistory.Full.OfType<PowerHistoryTagChange>().Where(t => t.Tag == GameTag.CORPSES || t.Tag == GameTag.CORPSES_SPENT_THIS_GAME).ToArray();
        Check(changes.Length == 4 && changes.All(t => t.EntityId == p.Id), "normal player TAG_CHANGE only");
        CorpseRules.Gain(p, int.MaxValue - 5);
        state = Canonical.State(game); history = History(game);
        Reject<OverflowException>(() => CorpseRules.Gain(p, 1), "overflow rejected");
        Check(state == Canonical.State(game) && history == History(game), "overflow atomic");
    }
    private static void DeathCountExactlyOnce()
    {
        foreach (bool mixed in new[] { false, true })
        {
            var game = NewGame(); var p = game.Player1;
            var first = Core2026Proof.Summon(p, "HERO_11bpt"); var second = Core2026Proof.Summon(p, "CS2_101t");
            if (mixed) LocationProofs.Location(p).Destroy();
            first.Destroy(); first.Destroy(); second.Destroy(); game.DeathProcessingAndAuraUpdate(); game.GraveYard();
            Core2026Proof.End(game);
            Check(CorpseRules.Available(p) == 2, "DeathCountExactlyOnce incl Ghoul killed before end, mixed=" + mixed);
        }
    }
    private static void CloneIndependent()
    {
        var game = NewGame(); CorpseRules.Gain(game.Player1, 5); CorpseRules.TrySpend(game.Player1, 1);
        var clone = game.CloneExact(history: true);
        Check(clone.EnableCorpses && CorpseRules.Available(clone.Player1) == 4, "clone flag/counters");
        CorpseRules.Gain(clone.Player1, 2); CorpseRules.TrySpend(clone.Player1, 3);
        Check(CorpseRules.Available(game.Player1) == 4 && CorpseRules.Available(clone.Player1) == 3, "CloneIndependent");
    }
    private static void SnapshotResume()
    {
        var game = NewGame(); Core2026Proof.Kill(game, Core2026Proof.Summon(game.Player1, "CS2_101t"));
        CorpseRules.Gain(game.Player1, 4); CorpseRules.TrySpend(game.Player1, 1);
        var bytes = game.SaveSnapshot(); var resumed = Game.LoadSnapshot(bytes);
        Check(resumed.EnableCorpses && bytes.SequenceEqual(resumed.SaveSnapshot()), "snapshot byte-stable flag/counters");
        Reject<SerializationException>(() => Game.LoadSnapshot(bytes, new GameConfig { FormatType = FormatType.FT_WILD, History = true, Logging = false }), "snapshot flag mismatch");
        Check(Game.LoadSnapshot(bytes, new GameConfig { FormatType = FormatType.FT_WILD, History = true, Logging = false, EnableCorpses = true }).EnableCorpses, "matching config accepted");
        var histories = new List<string>();
        foreach (var copy in new[] { game, resumed })
        {
            int start = copy.PowerHistory.Full.Count;
            Check(CorpseRules.TrySpend(copy.Player1, 2), "spend after snapshot");
            Core2026Proof.Kill(copy, Core2026Proof.Summon(copy.Player1, "HERO_11bpt"));
            Core2026Proof.End(copy); Core2026Proof.End(copy);
            histories.Add(string.Join("\n", copy.PowerHistory.Full.Skip(start).Select(e => Canonical.Value(e))));
        }
        Check(Canonical.State(game) == Canonical.State(resumed) && histories.Distinct().Count() == 1
            && Canonical.Value(game.Random.CaptureState()) == Canonical.Value(resumed.Random.CaptureState()), "SnapshotResume state/history/RNG");
        var bad = (byte[])bytes.Clone();
        using (var stream = new MemoryStream(bad))
        using (var reader = new BinaryReader(stream))
        { reader.ReadString(); Check(reader.ReadInt32() == 3, "enabled snapshot version"); reader.ReadString(); reader.ReadString(); bad[stream.Position] = 2; }
        Reject<SerializationException>(() => Game.LoadSnapshot(bad), "invalid corpse flag rejected");
    }
    private static void DisabledByDefaultNoStateHistoryOrRngChange()
    {
        Check(!new GameConfig().EnableCorpses, "disabled by default");
        var game = NewGame(enabled: false); var control = Game.LoadSnapshot(game.SaveSnapshot());
        CorpseRules.Gain(game.Player1, 3); Check(!CorpseRules.TrySpend(game.Player1, 1) && CorpseRules.SpendUpTo(game.Player1, 1) == 0, "disabled API no state");
        Check(Canonical.State(game) == Canonical.State(control) && History(game) == History(control), "disabled API no history/state");
        foreach (var copy in new[] { game, control })
        {
            var minion = Core2026Proof.Summon(copy.Player1, "CS2_101t");
            if (copy == game) CorpseRules.RecordDeath(minion);
            Core2026Proof.Kill(copy, minion);
            Check(!copy.Player1.NativeTags.ContainsKey(GameTag.CORPSES) && !copy.Player1.NativeTags.ContainsKey(GameTag.CORPSES_SPENT_THIS_GAME), "disabled deaths no tags");
        }
        Check(Canonical.State(game) == Canonical.State(control) && History(game) == History(control)
            && Canonical.Value(game.Random.CaptureState()) == Canonical.Value(control.Random.CaptureState()), "DisabledByDefaultNoStateHistoryOrRngChange");
        var legacy = game.SaveSnapshot();
        using (var stream = new MemoryStream(legacy)) using (var reader = new BinaryReader(stream))
        { reader.ReadString(); Check(reader.ReadInt32() == 2, "disabled snapshot preserves legacy layout"); }
        Check(!Game.LoadSnapshot(legacy).EnableCorpses && !game.CloneExact().EnableCorpses, "legacy snapshot absent flag decodes false");
    }
}
