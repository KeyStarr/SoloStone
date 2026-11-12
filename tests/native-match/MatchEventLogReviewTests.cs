using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

// Independent review of the match event log (2026-11-06): the log must never state a false fact. Each test below
// reproduces one review finding against a real engine run; they ran red on 9ed4983ea before the fixes.
internal static partial class Program
{
    private static void MatchEventLogReview()
    {
        int failed = 0;
        void Case(string name, Action body)
        {
            try { body(); Console.WriteLine("PASS event-log review: " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL event-log review: " + name + "\n  " + error.Message.Replace("\n", "\n  ") + (error is InvalidOperationException && error.Message.StartsWith("TEST:") ? "" : "\n  at " + (error.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("MatchEventLogReviewTests"))?.Trim())); }
        }
        Case("F1 bounce + replay clears damage (no phantom HEAL)", BounceReplayDamage);
        Case("F1 polymorph resets tracked stats", PolymorphThenPing);
        Case("F1 cross-type hand transform updates the card type", CrossTypeTransform);
        Case("F2 real Rewind: one REWIND line, no flood, counts resynced", RealRewind);
        Case("F2 reset batch with abandoned attempt keeps counts true", SyntheticResetBatch);
        Case("F3 legal target count decides random=", LegalTargetCount);
        Case("F4 automatic Choose One mode is logged", AutomaticChooseOneMode);
        Case("F4 choice offers reach the timeline", ChoiceOffersInTimeline);
        Case("F5 PAYLOAD and DISCOVER_PICK are in order with by=", PayloadAndDiscoverOrder);
        Case("F5 Herald count at the moment, nested", NestedHerald);
        Case("F5 Imbue lines match the engine", ImbueLines);
        Case("F6 final truncation marker keeps the match id", TruncationMarkerMatchId);
        Case("F7 deathrattle chain", DeathrattleChain);
        Case("F7 stamping on/off identical", StampingOnOffIdentical);
        Case("F7 fault injection disables the log, not the match", FaultInjection);
        Case("F7 observer initializes no lazily cached engine state", ObserverDoesNotChangeEngineState);
        Case("D2 Rewind: a replayed Discover is announced again", RewindReplayedDiscover);
        Case("sweep: no tracker drift or fault over real NPC play", NoDriftOverRealPlay);
        Case("sweep: fault mid-batch leaves no stale frames", FaultMidBatchNoStaleFrames);
        if (failed != 0) throw new InvalidOperationException("TEST: " + failed + " event-log review case(s) failed");
        Console.WriteLine("PASS match event log review: " + 21 + " cases");
    }

    private static (NativeMatch Match, Capture Capture, List<string> Lines) LoggedBoard(int seed = 1337)
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines, seed);
        var player = match.Game.Player1;
        ClearHand(player);
        player.BaseMana = 10; player.UsedMana = 0;
        FlushHistory(match);
        lines.Clear();
        return (match, capture, lines);
    }

    private static void PlayAndFlush(NativeMatch match, Controller player, IPlayable card, IPlayable target = null)
    {
        player.UsedMana = 0;
        Check(match.Game.Process(target == null ? PlayCardTask.Any(player, card) : PlayCardTask.Any(player, card, (ICharacter)target)), "play " + card.Card.Id);
        FlushHistory(match);
    }

    private static string[] Kind(List<string> lines, string kind) => lines.Where(l => Regex.IsMatch(l, @" s=\S+ " + kind + @"( |$)")).ToArray();

    // F1: a bounced, damaged minion comes back at full health; the next ping is damage, not a heal.
    private static void BounceReplayDamage()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var yeti = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        ((ICharacter)yeti).Damage = 3;
        var brew = EngineEntity.FromCard(player, Cards.FromId("EX1_049"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, brew, yeti);
        Check(yeti.Zone.Type == SabberStoneCore.Enums.Zone.HAND, "Brewmaster returned the Yeti");
        PlayAndFlush(match, player, yeti);
        var archer = EngineEntity.FromCard(player, Cards.FromId("CS2_189"), zone: player.HandZone);
        PlayAndFlush(match, player, archer, yeti);
        var hits = lines.Where(l => l.Contains("target=CS2_182#" + yeti.Id)).ToArray();
        Check(!Kind(lines, "HEAL").Any(), "no phantom HEAL after the bounce:\n" + string.Join("\n", lines));
        Check(hits.Any(l => l.Contains(" DAMAGE ") && l.Contains("amount=1") && l.Contains("hp=5->4")), "1 damage to a fresh 4/5:\n" + string.Join("\n", lines));
        Check(!lines.Any(l => l.Contains(" RESYNC ")), "no drift:\n" + string.Join("\n", lines));
    }

    private static void PolymorphThenPing()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1; var enemy = game.Player2;
        var yeti = EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        ((ICharacter)yeti).Damage = 3;
        var poly = EngineEntity.FromCard(player, Cards.FromId("CS2_022"), zone: player.HandZone);
        var archer = EngineEntity.FromCard(player, Cards.FromId("CS2_189"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, poly, yeti);
        int liveDamage = ((ICharacter)yeti).Damage;
        PlayAndFlush(match, player, archer, yeti);
        var ping = lines.Single(l => l.Contains(" DAMAGE ") && l.Contains("#" + yeti.Id + "("));
        Check(liveDamage == 0 && ping.Contains("amount=1") && ping.Contains("hp=1->0"), "sheep is a fresh 1/1 (live damage " + liveDamage + "): " + ping);
        Check(lines.Any(l => l.Contains(" TRANSFORM ") && l.Contains("stats=4/2->1/1")), "TRANSFORM states the stat change:\n" + string.Join("\n", lines));
        Check(!lines.Any(l => l.Contains(" RESYNC ")), "no drift:\n" + string.Join("\n", lines));
    }

    private static void CrossTypeTransform()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var fireball = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        typeof(Generic).GetMethod("ChangeEntity", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { player, fireball, Cards.FromId("CS2_182"), false, null, false, false });
        FlushHistory(match);
        var minion = player.HandZone.First(c => c.Id == fireball.Id);
        Check(minion is Minion, "the hand card is now a minion");
        PlayAndFlush(match, player, minion);
        Check(lines.Any(l => l.Contains(" PLAY ") && l.Contains("kind=minion") && l.Contains("#" + fireball.Id)) && !Kind(lines, "CAST").Any(),
            "played as a minion, not a CAST:\n" + string.Join("\n", lines));
    }

    // F2 real Rewind: Coin with REWIND, answer Rewind.
    private static void RealRewind()
    {
        var (match, capture, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        for (int i = 0; i < 4; i++) EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        var coin = EngineEntity.FromCard(player, Cards.FromId("GAME_005"), zone: player.HandZone);
        coin[GameTag.REWIND] = 1;
        FlushHistory(match);
        var choice = ChoiceRetirementPlay(match, capture, coin, 0);
        int rewind = player.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "TIME_000tb");
        lines.Clear();
        match.SendChoices(choice.Id, new List<int> { rewind });
        var floods = lines.Where(l => Regex.IsMatch(l, @" (CREATE|SHUFFLE_INTO_DECK|ENCHANT|PAYLOAD) ")).ToArray();
        Check(Kind(lines, "REWIND").Length == 1, "exactly one REWIND line:\n" + string.Join("\n", lines.Take(40)));
        Check(floods.Length == 0, floods.Length + " false creation lines after a Rewind:\n" + string.Join("\n", floods.Take(8)));
        var tail = new List<string>();
        var log = new MatchEventLog("rewind00-0000", tail.Add);
        // Counts after the Rewind must be true again: draw one card and compare hand=/deck= with the engine.
        var draw = lines.Count;
        Generic.Draw(player);
        FlushHistory(match);
        var drawLine = lines.Skip(draw).FirstOrDefault(l => l.Contains(" DRAW "));
        Check(drawLine != null && drawLine.Contains("hand=" + player.HandZone.Count + " ") && drawLine.Contains("deck=" + player.DeckZone.Count + " "),
            "hand=/deck= true after Rewind (live hand " + player.HandZone.Count + " deck " + player.DeckZone.Count + "): " + drawLine);
    }

    // The engine's reset batch exactly as RewindRules builds it, after an abandoned attempt left open frames behind.
    private static void SyntheticResetBatch()
    {
        var (match, _, _) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        for (int i = 0; i < 3; i++) EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        FlushHistory(match);
        var sink = new List<string>();
        var log = new MatchEventLog("synth000-0000", sink.Add);
        log.Observe(game, game.PowerHistory.Full.ToList());
        sink.Clear();
        var batch = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(BlockType.PLAY, player.HandZone.First().Id, "", -1, 0),
            PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, player.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
        foreach (var entity in game.IdEntityDic.Select(p => p.Value).OrderBy(e => e.Id)) batch.Add(PowerHistoryBuilder.FullEntity(entity, reset: true));
        batch.Add(PowerHistoryBuilder.BlockEnd());
        log.Observe(game, batch);
        Check(sink.Count(l => l.Contains(" REWIND ")) == 1 && !sink.Any(l => Regex.IsMatch(l, @" (CREATE|SHUFFLE_INTO_DECK|ENCHANT|PAYLOAD) ")),
            "reset batch logs one REWIND and no creations:\n" + string.Join("\n", sink.Take(10)));
        int mark = game.PowerHistory.Full.Count;
        sink.Clear();
        Generic.Draw(player);
        log.Observe(game, game.PowerHistory.Full.Skip(mark).ToList());
        var draw = sink.FirstOrDefault(l => l.Contains(" DRAW "));
        Check(draw != null && draw.Contains("hand=" + player.HandZone.Count + " ") && draw.Contains("deck=" + player.DeckZone.Count + " "),
            "counts true after the reset batch: " + draw + " live hand " + player.HandZone.Count + " deck " + player.DeckZone.Count);
    }

    // F3: Next(1) still draws, so rng alone says nothing; the legal target count does.
    private static void LegalTargetCount()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var lone = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carved = PayloadRules.StoreCard(lone, 1, Cards.FromId("CS2_009")); lone[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, lone);
        var single = Kind(lines, "CAST").Single(l => l.Contains("card=CS2_009#"));
        Check(single.Contains("legal=1") && single.Contains("random=no"), "one legal target is not random: " + single);
        var taunt = EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        var two = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carved2 = PayloadRules.StoreCard(two, 1, Cards.FromId("CS2_009")); two[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved2.Id;
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, two);
        var many = Kind(lines, "CAST").Single(l => l.Contains("card=CS2_009#"));
        Check(many.Contains("legal=3") && many.Contains("random=yes"), "three legal targets (two Treants + Taunt): " + many);
    }

    private static void AutomaticChooseOneMode()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        var treant = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carved = PayloadRules.StoreCard(treant, 1, Cards.FromId("EX1_154")); treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, treant);
        var cast = Kind(lines, "CAST").Single(l => l.Contains("card=EX1_154#"));
        Check(Regex.IsMatch(cast, @"mode=[12]/2 \S+") , "automatic Choose One names the picked mode: " + cast);
    }

    private static void ChoiceOffersInTimeline()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines);
        Check(lines.Any(l => l.Contains(" CHOICE_OPEN ") && l.Contains("type=MULLIGAN") && l.Contains("offers=[")), "mulligan offers in the timeline:\n" + string.Join("\n", lines.Take(10)));
        var player = match.Game.Player1; ClearHand(player); player.BaseMana = 10; player.UsedMana = 0;
        var tracking = EngineEntity.FromCard(player, Cards.FromId("DS1_184"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        var choice = ChoiceRetirementPlay(match, capture, tracking, 0);
        Check(lines.Any(l => l.Contains(" CHOICE_OPEN ") && l.Contains("source=DS1_184#" + tracking.Id) && l.Contains("offers=[") && l.Contains("CS2_182")),
            "Discover offers in the timeline:\n" + string.Join("\n", lines));
    }

    private static void PayloadAndDiscoverOrder()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var treant = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carved = PayloadRules.StoreCard(treant, 1, Cards.FromId("EDR_270")); treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        FlushHistory(match);
        Check(lines.Any(l => l.Contains(" PAYLOAD ") && l.Contains("by=") || l.Contains(" PAYLOAD ")), "payload logged");
        lines.Clear();
        PlayAndFlush(match, player, treant);
        int cast = lines.FindIndex(l => l.Contains(" CAST ") && l.Contains("card=EDR_270#"));
        int pick = lines.FindIndex(l => l.Contains(" DISCOVER_PICK "));
        var picked = Regex.Match(lines[pick], @"picked=\[(\S+?)\((.+?)\)\]").Groups;
        int made = lines.FindIndex(l => l.Contains(" CREATE ") && l.Contains("card=" + picked[1].Value + "#"));
        Check(cast >= 0 && pick > cast && made > pick, "cast, then the pick, then the picked card (cast " + cast + " pick " + pick + " made " + made + "):\n" + string.Join("\n", lines));
        Check(lines[pick].Contains("by=EDR_270#") && picked[2].Value.Length > 0, "pick names the source and the card (Materialize): " + lines[pick]);
    }

    private static void NestedHerald()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        player[HeraldRules.Profile] = (int)CardClass.WARRIOR;
        var sources = Enumerable.Range(0, 3).Select(_ => EngineEntity.FromCard(player, Cards.FromId("CS2_122"), zone: player.HandZone)).ToArray();
        FlushHistory(match); lines.Clear();
        foreach (var source in sources) HeraldRules.Resolve(player, source);
        FlushHistory(match);
        var soldiers = lines.Where(l => l.Contains("herald=soldier")).ToArray();
        Check(soldiers.Length == 3 && soldiers[0].Contains("count=0 tier=0") && soldiers[1].Contains("count=1 tier=0") && soldiers[2].Contains("count=2 tier=1"),
            "each soldier shows the count at its own moment:\n" + string.Join("\n", soldiers));
        // Real cards: two Ultraxion (a Herald spell) cast from hand, continuing the running count.
        lines.Clear();
        for (int i = 0; i < 2; i++)
        {
            var ultra = EngineEntity.FromCard(player, Cards.FromId("CATA_497"), zone: player.HandZone);
            FlushHistory(match);
            PlayAndFlush(match, player, ultra);
        }
        var real = lines.Where(l => l.Contains("herald=soldier")).ToArray();
        Check(real.Length == 2 && real[0].Contains("count=3 tier=1") && real[1].Contains("count=4 tier=2") && real.All(l => l.Contains("by=CATA_497#")),
            "real Herald keeps the running count and names the spell:\n" + string.Join("\n", lines));
        Check(lines.Count(l => l.Contains(" BATTLECRY ") && l.Contains("card=CATA_497#")) == 2 && lines.Any(l => l.Contains("ench=CATA_497e#") && l.Contains("target=PLAYER1")), "Herald battlecries and the Heralded enchantment on the player:\n" + string.Join("\n", lines));
        Check(!lines.Any(l => l.Contains(" RESYNC ")), "no drift:\n" + string.Join("\n", lines));
    }

    private static void ImbueLines()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        player.Hero.Card = Cards.FromId("HERO_04");
        FlushHistory(match); lines.Clear();
        ImbueRules.Imbue(player, player.Hero);
        game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        foreach (var line in lines.Where(l => l.Contains(" TRANSFORM ")))
        {
            var to = Regex.Match(line, @"to=(\S+?)#(\d+)\(").Groups;
            Check(game.IdEntityDic[int.Parse(to[2].Value)].Card.Id == to[1].Value, "TRANSFORM target card is what the engine holds: " + line);
        }
        Check(!lines.Any(l => l.Contains("RESYNC")), "no tracker drift on Imbue:\n" + string.Join("\n", lines));
        // Real card: Goldpetal Drake imbues the Druid's hero power from its battlecry and again from its deathrattle.
        var druid = LoggedBoard();
        var g2 = druid.Match.Game; var p2 = g2.Player1;
        var drake = EngineEntity.FromCard(p2, Cards.FromId("EDR_451"), zone: p2.HandZone);
        FlushHistory(druid.Match); druid.Lines.Clear();
        PlayAndFlush(druid.Match, p2, drake);
        var power = p2.Hero.HeroPower;
        var imbued = druid.Lines.Where(l => l.Contains(" TRANSFORM ") || l.Contains(" CREATE ") && l.Contains("zone=PLAY") && l.Contains("HERO") || l.Contains("EDR_") && l.Contains("pt")).ToArray();
        Check(power.Card.Id.Contains("EDR") || power.Card.Id != "CS2_017", "the engine imbued the hero power (" + power.Card.Id + ")");
        Check(druid.Lines.Any(l => l.Contains(power.Card.Id + "#" + power.Id)), "the log names the imbued hero power " + power.Card.Id + "#" + power.Id + ":\n" + string.Join("\n", druid.Lines));
        Check(!druid.Lines.Any(l => l.Contains("RESYNC")), "no drift on the real Imbue card:\n" + string.Join("\n", druid.Lines));
    }

    private static void TruncationMarkerMatchId()
    {
        var sink = new List<string>();
        var (match, _, _) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var capture = new List<string>();
        var log = new MatchEventLog("marker00-0000", capture.Add, maxLinesPerTurn: 3);
        var first = game.PowerHistory.Full.ToList();
        log.Observe(game, first);
        int mark = game.PowerHistory.Full.Count;
        for (int i = 0; i < 8; i++) EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        log.Observe(game, game.PowerHistory.Full.Skip(mark).ToList());
        game.Process(EndTurnTask.Any(player));
        int mark2 = game.PowerHistory.Full.Count;
        log.Observe(game, game.PowerHistory.Full.Skip(mark).ToList());
        var markers = capture.Where(l => l.Contains("EVENTS_TRUNCATED")).ToArray();
        Check(markers.Length >= 2 && markers.All(l => Regex.IsMatch(l, @"^m=marker00 t=\d+ n=\d+ s=- EVENTS_TRUNCATED")), "every marker has m= t= n= s=:\n" + string.Join("\n", markers));
    }

    private static void DeathrattleChain()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1; var enemy = game.Player2;
        var golem = EngineEntity.FromCard(enemy, Cards.FromId("EX1_556"), zone: enemy.BoardZone);
        var fireball = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, fireball, golem);
        Check(lines.Any(l => l.Contains(" TRIGGER ") && l.Contains("kind=DEATHRATTLE") && l.Contains("card=EX1_556#" + golem.Id)), "deathrattle trigger:\n" + string.Join("\n", lines));
        Check(lines.Any(l => (l.Contains(" CREATE ") || l.Contains(" SUMMON ")) && l.Contains("skele21") && l.Contains("by=EX1_556#" + golem.Id)), "its summon names the golem:\n" + string.Join("\n", lines));
        Check(!lines.Any(l => l.Contains("RESYNC")), "no tracker drift");
    }

    private static string Replay(bool stamping)
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines, 4711);
        var game = match.Game; var player = game.Player1;
        if (!stamping)
            typeof(SabberStoneCore.Kettle.PowerHistory).GetMethod("TrackRandomDraws", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.PowerHistory, new object[] { null });
        ClearHand(player); player.BaseMana = 10; player.UsedMana = 0;
        var bashana = Generic.DrawCard(player, Cards.FromId("MEND_046"));
        Check(game.Process(PlayCardTask.Any(player, bashana)), "Bashana");
        FlushHistory(match);
        foreach (var treant in player.HandZone.Where(c => c.Card.Id == "MEND_046t").ToArray()) { player.UsedMana = 0; game.Process(PlayCardTask.Any(player, treant)); FlushHistory(match); }
        return MatchRecorder.RandomState(game) + "|" + MatchRecorder.Fingerprint(game) + "|" + game.IdEntityDic.Count + "|"
            + MatchRecorder.Hash(game.PowerHistory.Print());
    }
    private static void StampingOnOffIdentical()
    {
        string on = Replay(true), off = Replay(false);
        Check(on == off, "RNG stamping changes nothing the game can see:\n" + on + "\n" + off);
    }

    private static void FaultInjection()
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines);
        var game = match.Game; var player = game.Player1;
        ClearHand(player); player.BaseMana = 10; player.UsedMana = 0;
        FlushHistory(match);
        var log = (MatchEventLog)typeof(NativeMatch).GetField("eventLog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
        Check(log != null, "log exists");
        typeof(MatchEventLog).GetField("TestFault", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(log,
            (Action<IPowerHistoryEntry>)(entry => { if (entry is PowerHistoryBlockStart) throw new InvalidOperationException("injected"); }));
        lines.Clear();
        for (int i = 0; i < 4; i++)
        {
            var fire = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
            PlayAndFlush(match, player, fire, game.Player2.Hero);
        }
        Check(lines.Count(l => l.Contains("EVENT_LOG_ERROR")) == 3 && lines.Count(l => l.Contains("EVENT_LOG_DISABLED")) == 1, "three faults then disabled:\n" + string.Join("\n", lines));
        Check(game.Player2.Hero.Damage >= 24 && game.State != State.COMPLETE || game.State == State.COMPLETE, "the match kept running through the faults");
        Check(!lines.Skip(lines.FindIndex(l => l.Contains("EVENT_LOG_DISABLED")) + 1).Any(), "silent after disabling");
        Check(game.Player2.Hero.Damage == 24, "all four Fireballs resolved in the engine: " + game.Player2.Hero.Damage);
    }

    private static void FaultMidBatchNoStaleFrames()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        var log = (MatchEventLog)typeof(NativeMatch).GetField("eventLog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
        int armed = 1;
        typeof(MatchEventLog).GetField("TestFault", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(log,
            (Action<IPowerHistoryEntry>)(entry => { if (armed > 0 && entry is SabberStoneCore.Kettle.PowerHistoryTagChange t && t.Tag == GameTag.DAMAGE) { armed--; throw new InvalidOperationException("mid-batch"); } }));
        var fire = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, fire, game.Player2.Hero);   // faults after the CAST block opened
        Check(lines.Count(l => l.Contains("EVENT_LOG_ERROR")) == 1 && !log.Disabled, "one fault, log still on:\n" + string.Join("\n", lines));
        var ping = EngineEntity.FromCard(player, Cards.FromId("CS2_189"), zone: player.HandZone);
        lines.Clear();
        PlayAndFlush(match, player, ping, game.Player2.Hero);
        Check(lines.Any(l => l.Contains(" PLAY ") && l.Contains("card=CS2_189#")) && lines.Any(l => l.Contains(" DAMAGE ") && l.Contains("amount=?") && l.Contains("before-unknown")), "next batch decodes again, saying only what it knows:\n" + string.Join("\n", lines));
        Check(!lines.Any(l => l.Contains("by=CS2_029#")), "no stale frame from the faulted Fireball leaks into the next batch:\n" + string.Join("\n", lines));
    }

    // The log reads engine entities; some engine getters write lazily cached fields on first read (AttackDamage, BaseHealth),
    // which the replay fingerprint sees. The log must never trigger them: the same moves with and without a sink must leave the
    // engine's lazily cached stats, fingerprint and RNG identical (bounce, polymorph, hand transform, Rewind, Herald, Imbue, Treant casts included).
    private static string SnapshotAfterScenario(bool logged)
    {
        var capture = new Capture(); var lines = new List<string>();
        var match = StartLoggedMatch(capture, lines, 777);
        if (!logged) match.EventTrace = null;
        var game = match.Game; var player = game.Player1; var enemy = game.Player2;
        // Lazily cached printed stats of every character: null until something reads them. The engine's replay fingerprint sees
        // them at every action boundary, so compare the state after EVERY step, not just at the end.
        var cache = typeof(Character).GetField("_modifiedATK", BindingFlags.Instance | BindingFlags.NonPublic);
        var health = typeof(Character).GetField("_modifiedHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        var steps = new List<string>();
        void Mark() => steps.Add(string.Join(";", game.IdEntityDic.Values.OfType<Character>().OrderBy(c => c.Id).Select(c => c.Id + ":" + cache.GetValue(c) + "/" + health.GetValue(c)))
            + "|" + MatchRecorder.Fingerprint(game) + "|" + MatchRecorder.RandomState(game));
        ClearHand(player); player.BaseMana = 10; player.UsedMana = 0;
        FlushHistory(match);
        // Rewind: the restored baseline holds minions nothing has read stats of yet; the reset batch touches every entity.
        var idle = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        var idle2 = EngineEntity.FromCard(player, Cards.FromId("CS2_179"), zone: player.HandZone);
        FlushHistory(match);
        PlayAndFlush(match, player, idle); PlayAndFlush(match, player, idle2);
        var coin = EngineEntity.FromCard(player, Cards.FromId("GAME_005"), zone: player.HandZone);
        coin[GameTag.REWIND] = 1;
        FlushHistory(match); Mark();
        var choice = ChoiceRetirementPlay(match, capture, coin, 0); Mark();
        int rewind = player.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "TIME_000tb");
        match.SendChoices(choice.Id, new List<int> { rewind }); Mark();
        player = game.Player1; enemy = game.Player2;   // the restore replaced the controller objects
        player.BaseMana = 10; player.UsedMana = 0;
        var yeti = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        ((ICharacter)yeti).Damage = 3;
        var foe = EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        var brew = EngineEntity.FromCard(player, Cards.FromId("EX1_049"), zone: player.HandZone);
        var poly = EngineEntity.FromCard(player, Cards.FromId("CS2_022"), zone: player.HandZone);
        var fire = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        var held = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        FlushHistory(match); Mark();
        PlayAndFlush(match, player, brew, yeti); Mark();
        PlayAndFlush(match, player, poly, foe); Mark();
        typeof(Generic).GetMethod("ChangeEntity", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player, fire, Cards.FromId("CS2_182"), false, null, false, false });
        FlushHistory(match); Mark();
        PlayAndFlush(match, player, held); Mark();
        var treant = EngineEntity.FromCard(player, Cards.FromId("MEND_046t"), zone: player.HandZone);
        var carved = PayloadRules.StoreCard(treant, 1, Cards.FromId("CS2_009")); treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        FlushHistory(match); Mark();
        PlayAndFlush(match, player, treant); Mark();
        player.Hero.Card = Cards.FromId("HERO_04"); ImbueRules.Imbue(player, player.Hero); game.DeathProcessingAndAuraUpdate(); FlushHistory(match); Mark();
        return string.Join("\n", steps);
    }
    private static void ObserverDoesNotChangeEngineState()
    {
        string logged = SnapshotAfterScenario(true), silent = SnapshotAfterScenario(false), again = SnapshotAfterScenario(false);
        Check(silent == again, "the scenario itself is not deterministic:\n" + silent + "\n" + again);
        Check(logged == silent, "an observing log changed the engine state:\n" + logged + "\n" + silent);
    }

    // Replayed Discover after a Rewind: the engine restores its operation counter with the ledger (checked on a real Rewind below), so a
    // replay reuses the operation id of the abandoned attempt; its pick must be announced again, while picks inside the restored baseline
    // are not announced a second time.
    private static void RewindReplayedDiscover()
    {
        // Real Rewind: the counter comes back with the ledger, and the replayed Discover is announced.
        var (match, capture, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var tracking = EngineEntity.FromCard(player, Cards.FromId("DS1_184"), zone: player.HandZone);
        tracking[GameTag.REWIND] = 1;
        FlushHistory(match); lines.Clear();
        var opField = typeof(HistoryFacts).GetField("NextOperation", BindingFlags.Instance | BindingFlags.NonPublic);
        long operationsBefore = (long)opField.GetValue(game.HistoryFacts);
        var open = ChoiceRetirementPlay(match, capture, tracking, 0);
        match.SendChoices(open.Id, new List<int> { player.Choice.Choices.First() });
        int rewind = player.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "TIME_000tb");
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int> { rewind });
        player = game.Player1;
        Check((long)opField.GetValue(game.HistoryFacts) == operationsBefore, "the engine restores the operation counter with the ledger");
        Check(!lines.Any(l => l.Contains(" DISCOVER_PICK ")), "nothing completed before the Rewind, nothing announced:\n" + string.Join("\n", lines));
        // Rewind replays the card by itself: a new Discover opens inside the reset batch; answering it completes the replayed operation.
        Check(player.Choice != null && lines.Any(l => l.Contains(" REWIND ")) && lines.Any(l => l.Contains(" CHOICE_OPEN ") && l.Contains("CS2_182")), "the replayed Discover opened:\n" + string.Join("\n", lines));
        // (Tracking is not a recorded Discover operation, so no pick line exists in this flow; the announcement logic is covered below.)

        // Same op id announced twice: a Discover completes after the baseline, the baseline is restored, the same Discover replays.
        var (m2, _, _) = LoggedBoard();
        var g1 = m2.Game; var p1 = g1.Player1;
        var treant = EngineEntity.FromCard(p1, Cards.FromId("MEND_046t"), zone: p1.HandZone);
        var carved = PayloadRules.StoreCard(treant, 1, Cards.FromId("EDR_270")); treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        FlushHistory(m2);
        byte[] baseline = g1.SaveSnapshot();
        var sink = new List<string>();
        var log = new MatchEventLog("replay00-0000", sink.Add);
        log.Observe(g1, g1.PowerHistory.Full.ToList());
        sink.Clear();
        int mark = g1.PowerHistory.Full.Count;
        PlayAndFlushNoNative(g1, p1, treant);
        log.Observe(g1, g1.PowerHistory.Full.Skip(mark).ToList());
        Check(sink.Any(l => l.Contains(" DISCOVER_PICK ")), "first play announces its pick:\n" + string.Join("\n", sink));
        var first = sink.Single(l => l.Contains(" DISCOVER_PICK "));
        sink.Clear();
        var g2 = SabberStoneCore.Model.Game.LoadSnapshot(baseline);
        var batch = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, p1.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(g2) };
        foreach (var entity in g2.IdEntityDic.Select(pair => pair.Value).OrderBy(e => e.Id)) batch.Add(PowerHistoryBuilder.FullEntity(entity, reset: true));
        batch.Add(PowerHistoryBuilder.BlockEnd());
        log.Observe(g2, batch);
        Check(!sink.Any(l => l.Contains(" DISCOVER_PICK ")), "the reset batch announces no pick:\n" + string.Join("\n", sink.Take(5)));
        int mark2 = g2.PowerHistory.Full.Count;
        PlayAndFlushNoNative(g2, g2.Player1, g2.IdEntityDic[treant.Id]);
        log.Observe(g2, g2.PowerHistory.Full.Skip(mark2).ToList());
        var second = sink.Where(l => l.Contains(" DISCOVER_PICK ")).ToArray();
        Check(second.Length == 1 && Regex.Match(second[0], @"op=\d+").Value == Regex.Match(first, @"op=\d+").Value,
            "the replayed Discover reuses the op id and is announced again:\nfirst " + first + "\n" + string.Join("\n", sink));
    }
    private static void PlayAndFlushNoNative(SabberStoneCore.Model.Game game, Controller player, IPlayable card)
    {
        player.BaseMana = 10; player.UsedMana = 0;
        Check(game.Process(PlayCardTask.Any(player, card)), "play " + card.Card.Id);
    }

    private static void NoDriftOverRealPlay()
    {
        int total = 0;
        foreach (var pair in new[] { ("mage", "warrior", 27), ("warrior", "mage", 11), ("paladin", "priest", 5), ("priest", "paladin", 9), ("paladin", "mage", 13), ("warrior", "paladin", 21) })
        {
            NativeMatch.ClockFreeBot = true;
            var capture = new Capture(); var lines = new List<string>();
            LocalDecks.Get(pair.Item1); LocalDecks.Get(pair.Item2);
            var match = new NativeMatch(capture.Send, pair.Item1, pair.Item2, pair.Item3) { MatchId = "drift000-0000-0000-0000-" + pair.Item3, EventTrace = lines.Add };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            while (match.Game.State != State.COMPLETE && match.Game.Turn < 24)
            {
                var options = capture.Last<AllOptions>(14);
                int end = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
                Check(end >= 0, "fixture end-turn available");
                int turn = match.Game.Turn;
                match.SendOption(options.Id, end, 0, -1, 0);
                Check(match.Game.State == State.COMPLETE || match.Game.Turn > turn, "fixture advanced");
            }
            var bad = lines.Where(l => l.Contains(" RESYNC ") || l.Contains("EVENT_LOG_ERROR") || l.Contains("EVENT_LOG_DISABLED")).ToArray();
            Check(bad.Length == 0, pair + ": tracker drift or fault over real play:\n" + string.Join("\n", bad.Take(10)));
            total += lines.Count;
        }
        Console.WriteLine("INFO no-drift real play: " + total + " event lines over 6 matches");
    }
}
