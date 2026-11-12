using HsSim;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using NativeHistory = PegasusGame.PowerHistory;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Zone = SabberStoneCore.Enums.Zone;
using Spell = SabberStoneCore.Model.Entities.Spell;

// The native client paces "big card, its effects, next card" only for PLAY blocks (PowerProcessor). Automatic casts
// (Fyrakk the Blazing) must reach it as one PLAY block per spell, flagged ForceShowBigCard, named for both seats before
// the block (opponent Secrets excepted), with the caster marked CAST_RANDOM_SPELLS meanwhile. Casts When Drawn keep
// their TOPDECK TRIGGER presentation (sub-spell recipes are keyed to it): no PLAY block, named as they leave the hand.
public static class AutoCastProof
{
    private const int CastRandomSpells = 437;
    private static void Check(bool value, string message) => SelfChecks.Check(value, "auto-cast " + message);

    internal static void Run()
    {
        int casts = 0;
        foreach (int owner in new[] { 1, 2 })
        for (int seed = 0; seed < 6; seed++)
            casts += Fyrakk(owner, seed);
        foreach (int owner in new[] { 1, 2 })
        foreach (string card in new[] { "JAIL_386t", "TIME_025t" })
            casts += Drawn(owner, card);
        IdentityRegressions();
        Coverage();
        foreach (int owner in new[] { 1, 2 })
        {
            NestedDeckCasts(owner); DeckSecret(owner);
            FutureIdentity(owner); RewoundIdentity(owner); UnscopedFlag(owner);
            FuturePlayIdentity(owner, "EX1_287"); FuturePlayIdentity(owner, "CS2_008");
            RemovedForgotten(owner); ConcealedFlag(owner, change: false); ConcealedFlag(owner, change: true);
        }
        Console.WriteLine($"PASS auto-cast transport: Fyrakk both seats x6 seeds; {casts} casts; Ohn'ahra->Slice nested deck casts, Mad Scientist deck Secrets, future/rewound/play identity, removal-cache and unscoped/concealed-flag negatives both seats; Casts When Drawn keep TRIGGER, one ForceShowBigCard PLAY block each, caster CAST_RANDOM_SPELLS scoped, no hand-play credit");
    }

    // Run every case even on the old plugin, so the report records independent red/green evidence.
    internal static void IdentityRegressions()
    {
        var failures = new List<string>();
        foreach (string scenario in new[] { "trigger-secret", "trigger-nonsecret", "play-burn", "retired", "start-of-game", "move-policy", "secret-policy", "attachment", "refull" })
        foreach (int owner in new[] { 1, 2 })
        {
            try { if (scenario is "start-of-game" or "move-policy" or "secret-policy" or "attachment" or "refull") OtherFutureIdentity(owner, scenario); else FutureRevealIdentity(owner, scenario); Console.WriteLine($"PASS identity {scenario} p{owner}"); }
            catch (Exception error) { failures.Add(error.Message); Console.WriteLine($"FAIL identity {scenario} p{owner}: {error.Message}"); }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
        foreach (int owner in new[] { 1, 2 }) ScopedTrade(owner);
        Console.WriteLine("PASS scoped Tradeable definition both seats");
    }

    // Positive control: the historical identity includes a composed printing's keywords, not only its base ID.
    private static void ScopedTrade(int owner)
    {
        var (game, session) = Start(owner, 15);
        var p = game.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        var card = Generic.DrawCard(p, GeneratedDefinitionRules.Creation(Cards.FromId("CORE_EX1_005"), Cards.FromId("CS2_182")));
        Check(card.Card[GameTag.TRADEABLE] == 1 && Cards.FromId(card.Card.Id)[GameTag.TRADEABLE] == 0,
            "fixture: composed definition is Tradeable, registry template is not");
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        session.PublishDecision(p); session.Process(TradeCardTask.Any(p, card));
        Check(card.Zone == p.DeckZone && Wire(session, cursor).Any(e => e.HasShowEntity && e.ShowEntity.Entity == card.Id
            && e.ShowEntity.Name == card.Card.Id), $"scoped Tradeable p{owner}: trade resolves and reveals its captured identity");
    }

    private static void FutureRevealIdentity(int owner, string scenario)
    {
        var (game, session) = Start(owner, 13);
        var p = game.CurrentPlayer;
        var source = (Spell)Entity.FromCard(p, Cards.FromId("EX1_287"), zone: p.DeckZone);
        if (scenario.StartsWith("trigger")) p.SecretZone.Add((Spell)p.DeckZone.Remove(source));
        if (scenario == "retired") p.SetasideZone.Add(p.DeckZone.Remove(source));
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        if (scenario == "retired")
        {
            var match = typeof(NativeSession).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .First(f => f.FieldType == typeof(NativeMatch)).GetValue(session);
            ((HashSet<int>)typeof(NativeMatch).GetField("retiredChoiceActors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(match)).Add(source.Id);
        }
        if (scenario == "play-burn") Generic.CastSpell(p, game, source, null, 0);
        else
        {
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", 0, 0));
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(source));
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
        }
        source.Zone.Remove(source); p.DeckZone.Add(source);
        string future = scenario == "trigger-secret" ? "EX1_289" : "CS2_029";
        var changed = Generic.ChangeEntityBlock(p, source, Cards.FromId(future), false);
        if (scenario == "play-burn")
        {
            // Real burn exit shape: frozen deck SHOW immediately followed by public GRAVEYARD move.
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(changed));
            p.GraveyardZone.Add(p.DeckZone.Remove(changed));
        }
        session.Flush(); game.PowerHistory.Last.Clear();
        var wire = Wire(session, cursor);
        var names = wire.Select((e, i) => (snapshot: e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.HasChangeEntity ? e.ChangeEntity : null, i))
            .Where(x => x.snapshot?.Entity == source.Id && x.snapshot.Name.Length > 0).ToArray();
        int start = wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id);
        Check(start >= 0, $"{scenario} p{owner}: fixture has source block");
        Check(!names.Any(x => x.snapshot.Name == future && (scenario != "play-burn" || x.i < start)),
            $"{scenario} p{owner}: future {future} leaked before its identity exists: " + string.Join(",", names.Select(x => x.snapshot.Name + "@" + x.i)));
        if (scenario.StartsWith("trigger"))
            Check(names.Any(x => x.snapshot.Name == "EX1_287" && x.i < start) || owner == 1,
                $"{scenario} p{owner}: activated Secret A named before its trigger");
        if (scenario == "play-burn")
        {
            Check(names.Any(x => x.snapshot.Name == future && x.i > start), $"{scenario} p{owner}: B still revealed at its later burn");
            if (owner == 2) Check(!names.Any(x => x.i < start), $"{scenario} p2: Secret A stays private before PLAY");
        }
    }

    // Bounded history fixtures exercise the synthesis paths without requiring a card to combine these effects.
    private static void OtherFutureIdentity(int owner, string scenario)
    {
        var (game, session) = Start(owner, 14);
        var p = game.CurrentPlayer;
        var match = typeof(NativeSession).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .First(f => f.FieldType == typeof(NativeMatch)).GetValue(session);
        object Field(string name) => typeof(NativeMatch).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);
        void Set(string name, object value) => typeof(NativeMatch).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(match, value);
        string original = scenario == "start-of-game" ? "EDR_000" : scenario == "secret-policy" ? "EX1_287" : "CS2_024";
        string future = scenario == "start-of-game" ? "EDR_845" : scenario == "move-policy" ? "EX1_287" : "CS2_029";
        var source = Entity.FromCard(p, Cards.FromId(original), zone: (scenario == "move-policy" || scenario == "refull") ? p.SetasideZone : p.DeckZone);
        if (scenario == "move-policy" || scenario == "refull")
        {
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(source));
            p.DeckZone.Add(p.SetasideZone.Remove(source));
            game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryHideEntity { EntityID = source.Id, Zone = Zone.DECK });
        }
        if (scenario == "secret-policy") p.SecretZone.Add((Spell)p.DeckZone.Remove(source));
        var target = scenario == "attachment" ? (Spell)Entity.FromCard(p, Cards.FromId("CS2_008"), zone: p.DeckZone) : null;
        if (target != null)
        {
            source[GameTag.IS_NIGHTMARE_BONUS] = 1;
            ((Dictionary<int, HashSet<int>>)Field("privacyTargets"))[source.Id] = new HashSet<int> { target.Id };
        }
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        if (scenario == "move-policy" || scenario == "refull") Check(session.Client.Entities[source.Id].CardId == "", "move-policy fixture: source anonymous before move");
        // A captured hidden snapshot establishes the old identity without disclosing it on the wire.
        if (scenario != "move-policy" && scenario != "refull") game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(source));
        if (scenario == "start-of-game")
        {
            Set("startOfGameClosed", false); Set("startOfGameMainBegin", true);
            foreach (var tags in ((Dictionary<int, Dictionary<int, int>>)Field("clientTransportTags")).Values)
                tags.Remove((int)GameTag.MULLIGAN_STATE);
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0, effectIndex: -1));
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
        }
        else if (scenario == "move-policy" || scenario == "refull")
        {
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.ZONE, (int)Zone.PLAY));
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.ZONE, (int)Zone.DECK));
        }
        else if (scenario == "secret-policy")
        {
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.COST, 7));
            p.DeckZone.Add(source.Zone.Remove(source));
        }
        else Generic.CastSpell(p, game, target, p.Opponent.Hero, 0);
        int replacement = game.PowerHistory.Last.Count;
        var changed = Generic.ChangeEntityBlock(p, source, Cards.FromId(future), false);
        if (scenario == "refull")
        {
            // Recreate at the same ID: FULL, like CHANGE, begins a new identity and cannot cross an earlier move.
            game.PowerHistory.Last.RemoveRange(replacement, game.PowerHistory.Last.Count - replacement);
            var full = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(changed);
            full.Entity.Tags[GameTag.ZONE] = (int)Zone.PLAY;
            game.PowerHistory.Last.Add(full);
        }
        session.Flush(); game.PowerHistory.Last.Clear();
        var wire = Wire(session, cursor);
        var names = wire.Select((e, i) => (snapshot: e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.HasChangeEntity ? e.ChangeEntity : null, i))
            .Where(x => x.snapshot?.Entity == source.Id && x.snapshot.Name.Length > 0).ToArray();
        if (scenario == "secret-policy")
            Check(owner == 1 || !wire.Any(e => e.HasTagChange && e.TagChange.Entity == source.Id && e.TagChange.Tag == (int)GameTag.COST && e.TagChange.Value == 7),
                $"secret-policy p{owner}: no private Secret cost from future non-Secret definition");
        else if (scenario == "refull")
        {
            int move = wire.FindIndex(e => e.HasTagChange && e.TagChange.Entity == source.Id && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)Zone.PLAY);
            Check(move >= 0 && names.Any(x => x.i < move && x.snapshot.Name == original) && !names.Any(x => x.i < move && x.snapshot.Name == future),
                $"refull p{owner}: recreation identity cannot be hoisted across earlier move: " + string.Join(",", names.Select(x => x.snapshot.Name + "@" + x.i)));
        }
        else if (scenario == "attachment")
        {
            int play = wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == target.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY);
            Check(play >= 0 && names.Any(x => x.i < play && x.snapshot.Name == original) && !names.Any(x => x.i < play && x.snapshot.Name == future),
                $"attachment p{owner}: pre-PLAY attachment names A, never B: " + string.Join(",", names.Select(x => x.snapshot.Name + "@" + x.i)));
        }
        else
            Check(names.Any(x => x.snapshot.Name == original) && !names.Any(x => x.snapshot.Name == future),
                $"{scenario} p{owner}: reveal A, never future B: " + string.Join(",", names.Select(x => x.snapshot.Name + "@" + x.i)));
    }

    // Every admitted Standard/pool card, both seats: play it (targets on both boards, 10 mana) and pass two turns. Each
    // automatic play the engine records must reach the client as its own flagged PLAY block, in engine order, with the
    // engine's target, named first (the opponent's hidden Secrets excepted); the same action with history off must
    // reach the same state (the presentation is history only). Text-pattern candidates never seen auto-casting are
    // listed for the report.
    public static void Coverage(Action<string, int, IReadOnlyList<NativeHistory>, Game> admission = null, string onlyCard = null)
    {
        var pool = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds
            .Concat(SabberStoneCore.Model.OfflinePoolPolicy.Load().Members.Select(m => m.CardId)).Distinct()
            .Where(id => Cards.FromId(id) is { Implemented: true } card && card.Type != CardType.HERO_POWER && card.Type != CardType.ENCHANTMENT)
            .Where(id => onlyCard == null || onlyCard.Split(',').Contains(id, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        var casters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var unplayable = new List<string>();
        foreach (string id in pool)
        foreach (int owner in new[] { 1, 2 })
        {
            int plays;
            try { plays = CoverCard(id, owner, out bool played, admission); if (!played && owner == 1) unplayable.Add(id); }
            catch (Exception error) when (!error.Message.StartsWith("SELF-CHECK: auto-cast", StringComparison.Ordinal))
            { unplayable.Add(id + "(" + error.GetType().Name + ")"); continue; }
            if (plays > 0) casters[id] = casters.GetValueOrDefault(id) + plays;
        }
        var text = new[] { "cast", "spells costing" };
        var candidates = pool.Where(id => text.Any(t => (Cards.FromId(id).Text ?? "").Contains(t, StringComparison.OrdinalIgnoreCase))
            || Cards.FromId(id)[GameTag.CAST_RANDOM_SPELLS] != 0).ToArray();
        Directory.CreateDirectory(Runner.Output);
        File.WriteAllLines(Path.Combine(Runner.Output, "auto-cast-coverage.tsv"),
            casters.Select(p => $"covered\t{p.Key}\t{p.Value}\t{Cards.FromId(p.Key).Name}")
                .Concat(candidates.Where(id => !casters.ContainsKey(id)).Select(id => $"text-only\t{id}\t0\t{Cards.FromId(id).Name}\t{(Cards.FromId(id).Text ?? "").Replace('\n', ' ')}"))
                .Concat(unplayable.Select(id => $"unplayed\t{id}\t0\t{Cards.FromId(id.Split('(')[0]).Name}\t{(Cards.FromId(id.Split('(')[0]).Text ?? "").Replace('\n', ' ')}")));
        Check(onlyCard != null || casters.Count > 0, "coverage found automatic plays");
        Console.WriteLine($"PASS auto-cast coverage: {pool.Length} admitted cards x both seats; {casters.Count} cards auto-play ({casters.Values.Sum()} plays); "
            + $"{candidates.Count(id => !casters.ContainsKey(id))} text candidates without one; report={Runner.Output}/auto-cast-coverage.tsv");
    }

    // Ohn'ahra plays Slice and Dice off the deck; Slice replays this turn's card inside its own play. Slice is shown
    // (pre-PLAY reveal) while its historical zone is still DECK, and its scoped CAST_RANDOM_SPELLS must reach the client.
    private static void NestedDeckCasts(int owner)
    {
        var (game, session) = Start(owner, 5);
        var p = game.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        foreach (var old in p.HandZone.Concat(p.DeckZone).ToArray()) p.SetasideZone.Add(old.Zone.Remove(old));
        for (int i = 0; i < 5; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var slice = Entity.FromCard(p, Cards.FromId("JAIL_500"), zone: p.DeckZone);
        Check(p.DeckZone.TopCard == slice, "fixture: Slice and Dice on top of the deck");
        var ohnahra = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId("EDR_031"));
        Generic.SummonBlock(game, ohnahra, -1, null);
        var bolt = Generic.DrawCard(p, Cards.FromId("CS2_008"));
        session.Flush(); game.PowerHistory.Last.Clear();
        session.PublishDecision(p);
        session.Process(PlayCardTask.Any(p, bolt, p.Opponent.Hero));
        int cursor = session.Packets.Count, full = game.PowerHistory.Full.Count;
        session.PublishDecision(p);
        session.Process(EndTurnTask.Any(p));
        var wire = Wire(session, cursor);
        string name = $"Ohn'ahra -> Slice and Dice p{owner}";
        var nested = game.PowerHistory.Full.Skip(full).OfType<SabberStoneCore.Kettle.PowerHistoryEffectPlayStart>().Where(q => q.Source != slice.Id).ToArray();
        Check(nested.Length > 0, name + ": Slice replays this turn's card inside its own play");
        int slicePlay = wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == slice.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        int named = wire.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == slice.Id && e.ShowEntity.Name == "JAIL_500");
        int on = wire.FindIndex(e => e.HasTagChange && e.TagChange.Entity == slice.Id && e.TagChange.Tag == CastRandomSpells && e.TagChange.Value == 1);
        int off = wire.FindLastIndex(e => e.HasTagChange && e.TagChange.Entity == slice.Id && e.TagChange.Tag == CastRandomSpells && e.TagChange.Value == 0);
        int replay = wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == nested[0].Source && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        Check(named >= 0 && named < slicePlay, $"{name}: Slice shown before its PLAY ({named}/{slicePlay})");
        Check(slicePlay < on && on < replay && replay < off, $"{name}: Slice's CAST_RANDOM_SPELLS scopes the replay (play={slicePlay} on={on} replay={replay} off={off})");
        Check(session.Client.Entities[slice.Id].Get((GameTag)CastRandomSpells) == 0, name + ": flag cleared on the client");
    }

    // Mad Scientist puts a deck Secret into play with no snapshot of its own (the engine never names a deck card): the
    // human's own Secret is named before it binds its actor, the opponent's stays hidden.
    private static void DeckSecret(int owner)
    {
        var (game, session) = Start(owner, 6);
        var p = game.CurrentPlayer;
        foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
        for (int i = 0; i < 5; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var secret = Entity.FromCard(p, Cards.FromId("EX1_287"), zone: p.DeckZone);
        var scientist = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId("FP1_004"));
        Generic.SummonBlock(game, scientist, -1, null);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(session.Client.Entities[secret.Id].CardId == "", "fixture: deck Secret nameless on the client");
        scientist.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate();
        session.Flush(); game.PowerHistory.Last.Clear();
        string name = $"Mad Scientist p{owner}";
        Check(secret.Zone == p.SecretZone, name + ": Secret put into play from the deck");
        var shown = session.Client.Entities[secret.Id];
        if (owner == 1)
            Check(shown.CardId == "EX1_287" && shown.ActorCardId == "EX1_287", $"{name}: own Secret named and bound ({shown.CardId}/{shown.ActorCardId})");
        else
            Check(shown.CardId == "", $"{name}: opponent Secret stays hidden ({shown.CardId})");
        var violations = Invariants.ActorIdentity(game, session.Client).ToArray();
        Check(violations.Length == 0, name + ": " + string.Join("; ", violations.Select(v => v.ToString())));
    }

    // Negative: a deck Secret A enters SECRET, returns to the deck and is transformed into B before the flush. The
    // SECRET move must never show B (a later identity, hidden in the deck).
    private static void FutureIdentity(int owner)
    {
        var (game, session) = Start(owner, 8);
        var p = game.CurrentPlayer;
        foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
        for (int i = 0; i < 5; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var secret = Entity.FromCard(p, Cards.FromId("EX1_287"), zone: p.DeckZone);
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        p.SecretZone.Add((Spell)p.DeckZone.Remove(secret));
        p.DeckZone.Add(p.SecretZone.Remove((Spell)secret));
        var changed = Generic.ChangeEntityBlock(p, secret, Cards.FromId("EX1_289"), false);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(changed.Card.Id == "EX1_289" && changed.Zone == p.DeckZone, "fixture: transformed in the deck");
        var leaks = Wire(session, cursor).Where(e => (e.HasShowEntity || e.HasFullEntity || e.HasChangeEntity)
            && (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Entity == secret.Id
            && (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Name.Length > 0).ToArray();
        Check(leaks.Length == 0, $"future identity p{owner}: no name for a card now hidden in the deck, got "
            + string.Join(",", leaks.Select(e => (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Name)));
    }

    // Negative: a deck Secret transformed during a Rewind attempt is restored by the reset; Mad Scientist must then
    // name the restored card (the human's) or nothing (the opponent's), never the discarded transform.
    private static void RewoundIdentity(int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true, Shuffle = false,
            History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame(); var p = game.CurrentPlayer; p.BaseMana = 10;
        foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
        for (int i = 0; i < 5; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var secret = Entity.FromCard(p, Cards.FromId("EX1_287"), zone: p.DeckZone);
        var scientist = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId("FP1_004"));
        Generic.SummonBlock(game, scientist, -1, null);
        var source = Generic.DrawCard(p, Cards.FromId("TIME_001"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
        session.PublishDecision(p);
        session.Process(PlayCardTask.Any(p, source));
        Check(game.Rewind != null, "fixture: Rewind pending");
        game.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(p, secret, Cards.FromId("EX1_289"), false);
        session.Flush(); game.PowerHistory.Last.Clear();
        session.PublishDecision(p);
        session.Process(ChooseTask.Pick(p, game.Rewind.RewindEntityId));
        var restored = game.IdEntityDic[secret.Id];
        Check(restored.Card.Id == "EX1_287" && restored.Zone == restored.Controller.DeckZone, "fixture: reset restores the deck Secret");
        var body = (SabberStoneCore.Model.Entities.Minion)game.IdEntityDic[scientist.Id];
        game.PowerHistory.Last.Clear();
        body.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate();
        session.Flush(); game.PowerHistory.Last.Clear();
        string shown = session.Client.Entities[secret.Id].CardId;
        Check(restored.Zone == restored.Controller.SecretZone, "fixture: Mad Scientist put the restored Secret into play");
        Check(owner == 1 ? shown == "EX1_287" : shown == "", $"rewound identity p{owner}: shown '{shown}', never the discarded EX1_289");
    }

    // Negative: a card once shown, moved back to the deck, then the source of an unrelated block must not pass a
    // CAST_RANDOM_SPELLS flag: only an automatic play's own pre-PLAY reveal scopes it.
    private static void UnscopedFlag(int owner)
    {
        var (game, session) = Start(owner, 9);
        var p = game.CurrentPlayer;
        var minion = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId("CS2_182"));
        Generic.SummonBlock(game, minion, -1, null);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(session.Client.Entities[minion.Id].CardId == "CS2_182", "fixture: minion shown");
        int cursor = session.Packets.Count;
        session.Invoke("SendHistory", new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(minion.Id, GameTag.ZONE, (int)Zone.DECK),
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, minion.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(minion.Id, GameTag.CAST_RANDOM_SPELLS, 1),
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd() });
        Check(!Wire(session, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == minion.Id && e.TagChange.Tag == CastRandomSpells),
            $"unscoped flag p{owner}: a hidden deck card outside its automatic play passes no CAST_RANDOM_SPELLS");
    }

    // Negative: a deck card A is cast automatically (pre-PLAY reveal), returns to the deck and is transformed into B
    // before the flush. The reveal must show A as it was at the PLAY (or nothing), never B.
    private static void FuturePlayIdentity(int owner, string card)
    {
        var (game, session) = Start(owner, 10);
        var p = game.CurrentPlayer;
        foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
        for (int i = 0; i < 5; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var spell = (Spell)Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        Generic.CastSpell(p, game, spell, spell.Card.MustHaveTargetToPlay || card == "CS2_008" ? p.Opponent.Hero : null, 0);
        game.DeathProcessingAndAuraUpdate();
        spell.Zone?.Remove(spell);
        p.DeckZone.Add(spell);
        var changed = Generic.ChangeEntityBlock(p, spell, Cards.FromId("CS2_029"), false);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(changed.Card.Id == "CS2_029" && changed.Zone == p.DeckZone, "fixture: cast, returned and transformed in the deck");
        var names = Wire(session, cursor).Where(e => (e.HasShowEntity || e.HasFullEntity || e.HasChangeEntity)
            && (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Entity == spell.Id)
            .Select(e => (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Name).Where(n => n.Length > 0).ToArray();
        Check(!names.Contains("CS2_029"), $"future play identity {card} p{owner}: the later transform is never shown, got " + string.Join(",", names));
        // The opponent's Secret is never named, whatever it later becomes.
        if (owner == 2 && card == "EX1_287") Check(names.Length == 0, $"future play identity {card} p2: the opponent's Secret stays hidden, got " + string.Join(",", names));
    }

    // Negative: a card named and removed from the game in the same list must not remain in the identity cache.
    private static void RemovedForgotten(int owner)
    {
        var (game, session) = Start(owner, 11);
        var p = game.CurrentPlayer;
        var card = Entity.FromCard(p, Cards.FromId("CS2_029"));
        card[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
        session.Flush(); game.PowerHistory.Last.Clear();
        var match = (NativeMatch)typeof(NativeSession).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(session)
            ?? (NativeMatch)typeof(NativeSession).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .First(f => f.FieldType == typeof(NativeMatch)).GetValue(session);
        var cache = (System.Collections.IDictionary)typeof(NativeMatch).GetField("engineIdentityHistory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);
        Check(!cache.Contains(card.Id), $"removed forgotten p{owner}: a card named and removed in one list leaves no cached identity");
    }

    // Negative: an automatic PLAY reveals deck source A; a HIDE or CHANGE then conceals/replaces it without a move;
    // its CAST_RANDOM_SPELLS must no longer pass.
    private static void ConcealedFlag(int owner, bool change)
    {
        var (game, session) = Start(owner, 12);
        var p = game.CurrentPlayer;
        var source = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        var concealed = change
            ? (SabberStoneCore.Kettle.IPowerHistoryEntry)new SabberStoneCore.Kettle.PowerHistoryChangeEntity { CardId = "CS2_024",
                Entity = new SabberStoneCore.Kettle.PowerHistoryEntity { Id = source.Id, Name = "CS2_024", Tags = new Dictionary<GameTag, int> {
                    [GameTag.ZONE] = (int)Zone.DECK, [GameTag.CONTROLLER] = p.PlayerId, [GameTag.CARDTYPE] = (int)CardType.SPELL } } }
            : new SabberStoneCore.Kettle.PowerHistoryHideEntity { EntityID = source.Id, Zone = Zone.DECK };
        session.Invoke("SendHistory", new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            new SabberStoneCore.Kettle.PowerHistoryEffectPlayStart { BlockType = BlockType.PLAY, Source = source.Id, Index = -1, EffectCardId = "", ForceShowBigCard = true },
            concealed,
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.CAST_RANDOM_SPELLS, 1),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.CAST_RANDOM_SPELLS, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd() });
        Check(!Wire(session, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == source.Id && e.TagChange.Tag == CastRandomSpells),
            $"concealed flag ({(change ? "CHANGE" : "HIDE")}) p{owner}: no CAST_RANDOM_SPELLS after the revealed source is concealed or replaced");
    }

    // One cheap, plain admitted spell per school (Arcane..Fel): school conditions ("after you cast a Holy spell").
    private static readonly Lazy<string[]> SchoolSpells = new(() => Enumerable.Range(1, 7).Select(school =>
        SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Select(Cards.FromId)
            .Where(c => c is { Implemented: true } && c.Type == CardType.SPELL && c[(GameTag)1635] == school && c.Cost <= 3
                && !c.IsSecret && !c.IsQuest && !c.ChooseOne && c[GameTag.CASTSWHENDRAWN] == 0)
            .OrderBy(c => c.Cost).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id).Where(id => id != null).ToArray());

    // Diagnostic: one card, both seats, exceptions surfaced.
    internal static void CoverOne(string id)
    {
        foreach (int owner in new[] { 1, 2 })
            Console.WriteLine($"{id} p{owner}: automatic plays={CoverCard(id, owner, out bool played)} played={played}");
    }

    private static int CoverCard(string id, int owner, out bool played, Action<string, int, IReadOnlyList<NativeHistory>, Game> admission = null)
    {
        played = false;
        var definition = Cards.FromId(id);
        var heroClass = definition.Class is CardClass.DEATHKNIGHT or CardClass.DEMONHUNTER or CardClass.DRUID or CardClass.HUNTER or CardClass.MAGE
            or CardClass.PALADIN or CardClass.PRIEST or CardClass.ROGUE or CardClass.SHAMAN or CardClass.WARLOCK or CardClass.WARRIOR ? definition.Class : CardClass.MAGE;
        var game = new Game(new GameConfig { RandomSeed = 7, StartPlayer = owner, SkipMulligan = true, Shuffle = false,
            History = true, Logging = false, FormatType = FormatType.FT_STANDARD, Player1HeroClass = heroClass, Player2HeroClass = heroClass,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        NativeSession session = null;
        var admissionPackets = admission == null ? null : new List<NativeHistory>();
        var match = new NativeMatch((number, packet) => {
            if (admissionPackets != null && packet is NativeHistory history)
            {
                using var bytes = new MemoryStream();
                history.Serialize(bytes); bytes.Position = 0;
                var snapshot = new NativeHistory(); snapshot.Deserialize(bytes);
                admissionPackets.Add(snapshot);
            }
            session.Receive(number, packet);
        });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        try
        {
        var p = game.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        foreach (var side in new[] { p, p.Opponent })
        foreach (string target in new[] { "EX1_110", "CS2_172", "RLK_708", "CS2_182" })
        {
            var minion = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(side, Cards.FromId(target));
            Generic.SummonBlock(game, minion, -1, null);
            minion.Damage = target == "CS2_182" ? 0 : 1;
        }
        game.DeathProcessingAndAuraUpdate();
        // Spells of every school in deck and hand: "from your deck/hand", "after you cast", "if you've cast" conditions.
        string[] spells = SchoolSpells.Value;
        foreach (var side in new[] { p, p.Opponent })
        {
            foreach (var old in side.DeckZone.Concat(side.HandZone).ToArray()) side.SetasideZone.Add(old.Zone.Remove(old));
            // A Shred of Time (TIME_025t) for "cast a Shred of Time from your deck" (Fatebreaker, Ruinous Velocidrake).
            for (int i = 0; i < 24; i++) Entity.FromCard(side, Cards.FromId(i == 11 ? "TIME_025t" : i % 2 == 0 ? spells[i / 2 % spells.Length] : "CS2_182"), zone: side.DeckZone);
        }
        foreach (string spell in spells) Generic.DrawCard(p, Cards.FromId(spell));
        var card = Generic.DrawCard(p, definition);
        var payment = CostRules.Quote(p, card);
        if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Armor) p.Hero.Armor = Math.Max(p.Hero.Armor, payment.Amount);
        if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Health) p.Hero.Health = Math.Max(p.Hero.Health, payment.Amount + 1);
        if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Corpses) CorpseRules.Gain(p, Math.Max(0, payment.Amount - CorpseRules.Available(p)));
        session.Flush(); game.PowerHistory.Last.Clear();
        var initialHand = p.HandZone.Select(c => c.Id).ToHashSet();
        int cursor = session.Packets.Count, full = game.PowerHistory.Full.Count;
        void Settle()
        {
            for (int guard = 0; guard < 20 && game.State != State.COMPLETE; guard++)
            {
                var chooser = game.Player1.Choice != null ? game.Player1 : game.Player2.Choice != null ? game.Player2 : null;
                if (chooser == null) break;
                // Keep the timeline: a Rewind reset restores a checkpoint this fixture edited outside history.
                int pick = chooser.Choice.ChoiceAction == SabberStoneCore.Model.ChoiceAction.REWIND
                    ? chooser.Choice.Choices.First(c => c != game.Rewind.RewindEntityId) : chooser.Choice.Choices[0];
                session.PublishDecision(chooser);
                session.Process(ChooseTask.Pick(chooser, pick));
            }
        }
        bool Act(Func<PlayerTask, bool> pick)
        {
            if (game.State == State.COMPLETE || game.CurrentPlayer != p) return false;
            p.UsedMana = 0;
            var task = p.Options().FirstOrDefault(pick);
            if (task == null) return false;
            session.PublishDecision(p);
            session.Process(task); Settle();
            return true;
        }
        // Cast a spell first ("if you've cast a spell this turn").
        Act(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Card.Id == spells[0]);
        p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id).ToList();
        if (options.Count == 0) return 0;
        // History-only: the same action with presentation history on and off reaches the same state.
        var on = game.CloneExact(history: true); var off = game.CloneExact(history: false);
        foreach (var clone in new[] { on, off })
        {
            clone.CurrentPlayer.UsedMana = 0;
            clone.Process(clone.CurrentPlayer.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id).ToList()[0]);
        }
        Check(Canonical.State(on) == Canonical.State(off), $"{id} p{owner}: automatic-play presentation leaves state unchanged");
        played = Act(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
        // "After you cast": the rest of the hand; generated cards (a Bulb) are played too.
        for (int guard = 0; guard < 10 && Act(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Card.Type == CardType.SPELL); guard++) { }
        for (int guard = 0; guard < 4 && Act(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && !initialHand.Contains(t.Source.Id)); guard++) { }
        Act(t => t.PlayerTaskType == PlayerTaskType.ACTIVATE_LOCATION && t.Source?.Id == card.Id);
        Act(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK);
        Act(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == card.Id);
        // End-of-turn casters (Ohn'ahra, Creature of the Sacred Cave) fire while alive; deathrattles last.
        for (int turn = 0; turn < 2 && game.State != State.COMPLETE; turn++)
        { session.PublishDecision(game.CurrentPlayer); session.Process(EndTurnTask.Any(game.CurrentPlayer)); Settle(); }
        // Deathrattle casts.
        if (card is SabberStoneCore.Model.Entities.Minion body && body.Zone == p.BoardZone && game.State != State.COMPLETE)
        {
            game.PowerHistory.Last.Clear(); // already sent by the last processed action
            body.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate(); session.Flush(); game.PowerHistory.Last.Clear(); Settle();
        }
        var plays = game.PowerHistory.Full.Skip(full).OfType<SabberStoneCore.Kettle.PowerHistoryEffectPlayStart>().ToArray();
        if (plays.Length == 0) return 0;
        var wire = Wire(session, cursor);
        // Every engine PLAY block (hand plays and automatic plays alike) reaches the wire once, in order; the automatic
        // ones are matched by position (a weapon that casts its stored spell sources both kinds).
        var engineStarts = game.PowerHistory.Full.Skip(full).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Where(b => b.BlockType == BlockType.PLAY).ToArray();
        var wireStarts = wire.Select((e, i) => (e, i)).Where(x => x.e.HasPowerStart && (int)x.e.PowerStart.Type == (int)BlockType.PLAY).ToArray();
        string name = $"{id} p{owner}";
        Check(wireStarts.Select(x => x.e.PowerStart.Source).SequenceEqual(engineStarts.Select(b => b.Source)),
            name + ": every PLAY block reaches the client once, in engine order; wire=" + string.Join(",", wireStarts.Select(x => x.e.PowerStart.Source))
            + " engine=" + string.Join(",", engineStarts.Select(b => b.Source)));
        var starts = engineStarts.Select((b, k) => (b, k)).Where(x => x.b is SabberStoneCore.Kettle.PowerHistoryEffectPlayStart).Select(x => wireStarts[x.k]).ToArray();
        Check(engineStarts.Select((b, k) => (b, k)).Where(x => !(x.b is SabberStoneCore.Kettle.PowerHistoryEffectPlayStart)).All(x => !wireStarts[x.k].e.PowerStart.ForceShowBigCard),
            name + ": hand plays keep their presentation (no forced big card)");
        for (int k = 0; k < plays.Length; k++)
        {
            var (start, at) = starts[k];
            var engine = plays[k];
            Check(start.PowerStart.ForceShowBigCard == engine.ForceShowBigCard && start.PowerStart.SubOption == -1
                && start.PowerStart.Target == engine.Target, $"{name}: {engine.Source} flag/target {start.PowerStart.ForceShowBigCard}/{start.PowerStart.Target} vs {engine.ForceShowBigCard}/{engine.Target}");
            var source = game.IdEntityDic[engine.Source];
            // An installed opponent Secret stays hidden; one that fizzles (duplicate, full zone) is revealed by design.
            bool opponentSecret = source.Controller.PlayerId == 2 && source.Card.IsSecret && !source.Card.IsQuest;
            bool hidden = opponentSecret && source.Zone == source.Controller.SecretZone;
            int named = wire.FindIndex(e => (e.HasShowEntity || e.HasFullEntity) && (e.HasShowEntity ? e.ShowEntity : e.FullEntity).Entity == engine.Source
                && (e.HasShowEntity ? e.ShowEntity : e.FullEntity).Name.Length > 0);
            // A Secret cast on the other player's turn (Faceless Enigma's pick for the opponent) is live at once (#32, 7f5de0c), so a
            // later action of this scripted turn can trigger it; it is named when it triggers, after its PLAY.
            bool namedWhenTriggered = opponentSecret && source.Zone == source.Controller.GraveyardZone && named > at;
            Check(hidden ? named < 0 : namedWhenTriggered || named >= 0 && named < at, $"{name}: {source.Card.Id} named before its PLAY ({named}/{at}, hidden={hidden})");
        }
        var violations = Invariants.ActorIdentity(game, session.Client).ToArray();
        Check(violations.Length == 0, name + ": " + string.Join("; ", violations.Select(v => v.ToString())));
        return plays.Length;
        }
        finally { admission?.Invoke(id, owner, admissionPackets, game); }
    }

    private static (Game, NativeSession) Start(int owner, int seed)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = owner, SkipMulligan = true, Shuffle = false,
            History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush(); game.PowerHistory.Last.Clear();
        return (game, session);
    }

    private static List<PowerHistoryData> Wire(NativeSession session, int cursor) => session.Packets.Skip(cursor).Where(p => p.Id == 19)
        .SelectMany(p => { var h = new NativeHistory(); using var s = new MemoryStream(Convert.FromBase64String(p.Base64)); h.Deserialize(s); return h.List; })
        .ToList();

    private static int Fyrakk(int owner, int seed)
    {
        var (game, session) = Start(owner, seed);
        var p = game.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var fyrakk = Generic.DrawCard(p, Cards.FromId("FIR_959"));
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        int plays = game.HistoryFacts.Query.Plays().Count();
        session.Process(PlayCardTask.Any(p, fyrakk));
        var cast = game.IdEntityDic.Values.OfType<Spell>().Where(s => s[GameTag.CREATOR] == fyrakk.Id).ToArray();
        Check(cast.Length > 0, $"Fyrakk p{owner} s{seed} cast spells");
        Check(game.HistoryFacts.Query.Plays().Count() == plays + 1, "auto-casts add no card-play fact");
        var wire = Wire(session, cursor);
        string name = $"Fyrakk p{owner} s{seed}";
        foreach (var spell in cast) Block(wire, spell, fyrakk.Id, owner, name);
        int on = wire.FindIndex(e => e.HasTagChange && e.TagChange.Entity == fyrakk.Id && e.TagChange.Tag == CastRandomSpells && e.TagChange.Value == 1);
        int off = wire.FindLastIndex(e => e.HasTagChange && e.TagChange.Entity == fyrakk.Id && e.TagChange.Tag == CastRandomSpells && e.TagChange.Value == 0);
        int first = wire.FindIndex(e => e.HasPowerStart && cast.Any(s => s.Id == e.PowerStart.Source) && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        int last = wire.FindLastIndex(e => e.HasPowerStart && cast.Any(s => s.Id == e.PowerStart.Source) && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        Check(on >= 0 && on < first && off > last, name + ": caster carries CAST_RANDOM_SPELLS around its casts, on=" + on + " first=" + first + " last=" + last + " off=" + off);
        Check(session.Client.Entities[fyrakk.Id].Get((GameTag)CastRandomSpells) == 0, name + ": CAST_RANDOM_SPELLS cleared on the client");
        Check(fyrakk[(GameTag)CastRandomSpells] == 0, name + ": engine state untouched");
        // The NPC's own-deck memory reads PLAY blocks as card plays; an automatic play must neither count nor throw.
        foreach (var seat in new[] { game.Player1, game.Player2 }) Check(OwnDeckLedger.Observe(game, seat, null) != null, name + ": deck ledger reads auto-cast history");
        session.PublishDecision(game.CurrentPlayer);
        var violations = Invariants.Compare(game, session.Client).ToArray();
        Check(violations.Length == 0, name + " parity: " + string.Join("; ", violations.Select(v => v.ToString())));
        return cast.Length;
    }

    private static int Drawn(int owner, string card)
    {
        var (game, session) = Start(owner, 3);
        var p = game.CurrentPlayer;
        var top = Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        session.Flush(); game.PowerHistory.Last.Clear();
        int cursor = session.Packets.Count;
        Generic.Draw(p, top);
        game.DeathProcessingAndAuraUpdate();
        session.Flush(); game.PowerHistory.Last.Clear();
        var wire = Wire(session, cursor);
        string name = $"{card} p{owner}";
        Check(!wire.Any(e => e.HasPowerStart && e.PowerStart.Source == top.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY), name + ": no PLAY block");
        int trigger = wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == top.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
        int named = wire.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == top.Id && e.ShowEntity.Name == card);
        Check(trigger >= 0 && named >= 0 && (owner == 1 || named > trigger), name + ": named (the opponent's inside its TOPDECK trigger) (named=" + named + " trigger=" + trigger + ")");
        return 0;
    }

    // One PLAY block for the spell, flagged, SubOption -1, enclosing its POWER, the spell named before it (hidden Secrets excepted).
    private static void Block(List<PowerHistoryData> wire, Spell spell, int caster, int owner, string name)
    {
        var plays = wire.Select((e, i) => (e, i)).Where(x => x.e.HasPowerStart && x.e.PowerStart.Source == spell.Id
            && (int)x.e.PowerStart.Type == (int)BlockType.PLAY).ToArray();
        Check(plays.Length == 1, $"{name}: {spell.Card.Id} has one PLAY block, found {plays.Length}");
        var (start, at) = plays[0];
        bool hiddenSecret = spell.Card.IsSecret && !spell.IsQuest;
        Check(start.PowerStart.ForceShowBigCard == !hiddenSecret && start.PowerStart.SubOption == -1,
            $"{name}: {spell.Card.Id} PLAY ForceShowBigCard={start.PowerStart.ForceShowBigCard} SubOption={start.PowerStart.SubOption}");
        int depth = 0, end = -1;
        for (int i = at; i < wire.Count; i++)
        {
            if (wire[i].HasPowerStart) depth++;
            if (wire[i].HasPowerEnd && --depth == 0) { end = i; break; }
        }
        Check(end > at, name + ": PLAY block closes");
        var powers = wire.Select((e, i) => (e, i)).Where(x => x.e.HasPowerStart && x.e.PowerStart.Source == spell.Id
            && (int)x.e.PowerStart.Type == (int)BlockType.POWER).Select(x => x.i).ToArray();
        Check(powers.All(i => i > at && i < end), $"{name}: {spell.Card.Id} effects resolve inside its PLAY block");
        bool hidden = owner == 2 && hiddenSecret;
        int named = wire.FindIndex(e => (e.HasShowEntity || e.HasFullEntity) && (e.HasShowEntity ? e.ShowEntity : e.FullEntity).Entity == spell.Id
            && (e.HasShowEntity ? e.ShowEntity : e.FullEntity).Name.Length > 0);
        if (hidden)
            Check(named < 0 || named > at, $"{name}: opponent {spell.Card.Id} Secret is not named before its PLAY");
        else
            Check(named >= 0 && named < at, $"{name}: {spell.Card.Id} named before its PLAY block (named={named} play={at})");
    }
}
