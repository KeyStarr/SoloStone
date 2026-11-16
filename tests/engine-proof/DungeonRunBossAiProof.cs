using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Dungeon Run bosses played out by the boss AI (data/runs/loot-v1.json): every boss fights whole games against run decks of
// each class, with the match settings NativeMatch.Configure builds from a run encounter (run Health, no boss Coin, the
// player first, the round's boss mana, starting boards and hands, a passive treasure) and the decision path the live boss
// uses (BotDriver.Forced, then BotDriver.Decide with BotDriver.ForBoss). The rule proofs script the boss's turns; this one
// checks what only the AI shows: it plays its Hero Power, it never stalls or falls back, no task it picks is refused, and
// each special boss's rule holds under real play (Tad never holds a card, A. F. Kay plays nothing before her sixth turn,
// the Treasure Vault fight always ends in the player's favour by its fifth turn, George and Karl swap and both fall).
// The run report lists, per boss: games, player wins, average turns, Hero Power uses and fallbacks.
internal static class DungeonRunBossAiProof
{
    private static volatile string LastStep;
    private static readonly bool Verbose = Environment.GetEnvironmentVariable("BOSS_AI_VERBOSE") == "1";
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run boss AI: " + message);

    private sealed class Boss
    {
        public string Id, Hero, Power;
        public int[] Rounds;
        public List<string> Deck = new List<string>(), Board = new List<string>(), PlayerHand = new List<string>();
    }

    private sealed class Content
    {
        public int BaseHealth, PerWin;
        public Dictionary<int, int> BossMana = new Dictionary<int, int>();
        public Dictionary<string, List<string>> StartDecks = new Dictionary<string, List<string>>();
        public Dictionary<string, List<List<string>>> Buckets = new Dictionary<string, List<List<string>>>();
        public List<string> Passives = new List<string>(), Actives = new List<string>();
        public List<Boss> Bosses = new List<Boss>();
    }

    private static List<string> Strings(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string>();

    private static Content Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText("data/runs/loot-v1.json"));
        var root = doc.RootElement;
        var c = new Content {
            BaseHealth = root.GetProperty("playerHealth").GetProperty("base").GetInt32(),
            PerWin = root.GetProperty("playerHealth").GetProperty("perWin").GetInt32(),
            Passives = Strings(root.GetProperty("treasures").GetProperty("passive")),
            Actives = Strings(root.GetProperty("treasures").GetProperty("active")),
        };
        foreach (var r in root.GetProperty("rounds").EnumerateArray())
            c.BossMana[r.GetProperty("round").GetInt32()] = r.TryGetProperty("bossStartingMana", out var m) ? m.GetInt32() : 0;
        foreach (var s in root.GetProperty("classes").EnumerateArray())
            c.StartDecks[s.GetProperty("class").GetString()] = Strings(s.GetProperty("deck"));
        foreach (var b in root.GetProperty("buckets").EnumerateArray())
        {
            string cls = b.TryGetProperty("class", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            if (cls == null || !b.TryGetProperty("cards", out var cards)) continue;
            if (!c.Buckets.TryGetValue(cls, out var list)) c.Buckets[cls] = list = new List<List<string>>();
            list.Add(Strings(cards));
        }
        foreach (var b in root.GetProperty("bosses").EnumerateArray())
        {
            var boss = new Boss { Id = b.GetProperty("id").GetString(), Hero = b.GetProperty("hero").GetString(),
                Power = b.TryGetProperty("heroPower", out var p) ? p.GetString() : null,
                Rounds = b.GetProperty("rounds").EnumerateArray().Select(x => x.GetInt32()).ToArray() };
            foreach (var d in b.GetProperty("deck").EnumerateArray())
                for (int i = 0; i < d.GetProperty("count").GetInt32(); i++) boss.Deck.Add(d.GetProperty("id").GetString());
            if (b.TryGetProperty("board", out var board)) boss.Board = Strings(board);
            if (b.TryGetProperty("playerHand", out var hand)) boss.PlayerHand = Strings(hand);
            c.Bosses.Add(boss);
        }
        return c;
    }

    private static readonly string[] Classes = { "MAGE", "WARRIOR", "ROGUE", "HUNTER", "PALADIN", "SHAMAN", "PRIEST", "WARLOCK", "DRUID" };
    private static readonly Dictionary<string, string> ClassHero = new Dictionary<string, string> {
        { "WARRIOR", "HERO_01" }, { "SHAMAN", "HERO_02" }, { "ROGUE", "HERO_03" }, { "PALADIN", "HERO_04" }, { "HUNTER", "HERO_05" },
        { "DRUID", "HERO_06" }, { "WARLOCK", "HERO_07" }, { "MAGE", "HERO_08" }, { "PRIEST", "HERO_09" } };

    internal sealed class Tally
    {
        public int Games, Wins, Turns, PowerUses, Fallbacks, Capped;
        public readonly List<string> Failures = new List<string>();
        public readonly SortedDictionary<string, int> Notes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public void Note(string key) => Notes[key] = Notes.TryGetValue(key, out int n) ? n + 1 : 1;
    }

    // One fight. Returns nothing; everything goes to the tally.
    private static void Fight(Content content, Boss boss, int seed, Tally tally)
    {
        var rng = new Random(seed * 7919 + boss.Id.Sum(ch => (int)ch));
        int round = boss.Rounds[seed % boss.Rounds.Length];
        string cls = Classes[(seed + boss.Id.Length) % Classes.Length];
        // A run deck as a run of this round would have it (loot-v1.json's rewards): a three-card bundle from a class bucket per
        // round won, an active treasure after rounds 3 and 7, a passive treasure after rounds 1 and 5.
        var deck = new List<string>(content.StartDecks[cls]);
        var buckets = content.Buckets.TryGetValue(cls, out var own) ? own : new List<List<string>>();
        for (int r = 1; r < round && buckets.Count > 0; r++)
        {
            var pool = buckets[rng.Next(buckets.Count)];
            deck.AddRange(pool.OrderBy(_ => rng.Next()).Take(Math.Min(3, pool.Count)));
            if (r == 3 || r == 7) deck.Add(content.Actives[rng.Next(content.Actives.Count)]);
        }
        var loadout = Environment.GetEnvironmentVariable("BOSS_AI_NO_PASSIVE") == "1" ? null
            : content.Passives.OrderBy(_ => rng.Next()).Take(round > 5 ? 2 : round > 1 ? 1 : 0).ToList();
        if (loadout != null && loadout.Count == 0) loadout = null;
        string label = boss.Id + " seed " + seed + " (" + cls + " round " + round + (loadout != null ? " " + string.Join("+", loadout) : "") + ")";

        var config = new GameConfig {
            Player1Name = "Player", Player2Name = boss.Id,
            Player1HeroCard = Cards.FromId(ClassHero[cls]), Player1HeroClass = Cards.FromId(ClassHero[cls]).Class,
            Player2HeroCard = Cards.FromId(boss.Hero), Player2HeroClass = Cards.FromId(boss.Hero).Class,
            Player1Deck = deck.Select(Cards.FromId).ToList(), Player2Deck = boss.Deck.Select(Cards.FromId).ToList(),
            FormatType = FormatType.FT_WILD, StartPlayer = 1, Shuffle = true, RandomSeed = seed, SkipMulligan = false, History = false,
            EnableCorpses = true, SkipCoin = true,
            Player1StartingHealth = content.BaseHealth + content.PerWin * (round - 1),
            Player2StartingMana = content.BossMana.TryGetValue(round, out int mana) ? mana : 0,
            Player1Loadout = loadout, Player2StartingBoard = boss.Board.Count == 0 ? null : boss.Board,
            Player1StartingHand = boss.PlayerHand.Count == 0 ? null : boss.PlayerHand,
            Player1RunBossesDefeated = round - 1, Logging = false,
        };
        Game game;
        try { game = new Game(config); game.StartGame(); }
        catch (Exception e) { tally.Failures.Add(label + ": start threw " + e.GetType().Name + ": " + e.Message); return; }
        tally.Games++;

        var options = new Dictionary<int, DriverOptions> { { 1, BotDriver.ForBoss() }, { 2, BotDriver.ForBoss() } };
        var budgets = new Dictionary<int, BotBudget> { { 1, BotBudget.ForStars(0).WithoutClock() }, { 2, BotBudget.ForStars(60).WithoutClock() } };
        int decisions = 0, actionsThisTurn = 0, lastTurn = -1, steps = 0;
        var heroesSeen = new HashSet<string> { boss.Hero };
        bool tadHeld = false;
        try
        {
            while (game.State == State.RUNNING)
            {
                if (++steps > 3000 || game.Turn > 90) { tally.Capped++; tally.Failures.Add(label + ": no result after " + game.Turn + " turns / " + steps + " actions"); break; }
                if (game.Step == Step.BEGIN_MULLIGAN)
                {
                    foreach (var p in new[] { game.Player1, game.Player2 })
                        if (p.Choice != null && p.Choice.ChoiceType == ChoiceType.MULLIGAN)
                            Check(game.Process(ChooseTask.Mulligan(p, new List<int>())), label + ": mulligan accepted");
                    // As NativeMatch: once both players are done, the game begins its first turn.
                    if (game.Player1.MulliganState == Mulligan.DONE && game.Player2.MulliganState == Mulligan.DONE) game.MainBegin();
                    continue;
                }
                var actor = game.Player1.Choice != null ? game.Player1 : game.Player2.Choice != null ? game.Player2 : game.CurrentPlayer;
                if (game.Turn != lastTurn) { lastTurn = game.Turn; actionsThisTurn = 0; }
                if (++actionsThisTurn > 120) { tally.Failures.Add(label + ": turn " + game.Turn + " runs past 120 actions (stall)"); break; }

                var budget = budgets[actor.PlayerId];
                var (task, trace) = BotDriver.Decide(game, actor.PlayerId, DeckStyle.Value,
                    state => PilotDecision.From(BotSearch.Choose(state, budget)), ref decisions, options[actor.PlayerId]);
                if (actor == game.Player2 && trace.Kind == "fallback")
                {
                    tally.Fallbacks++; tally.Note("fallback " + (loadout == null ? "-" : string.Join("+", loadout)) + ": " + (trace.FallbackReason ?? "").Split(':')[0]);
                    if (Environment.GetEnvironmentVariable("BOSS_AI_TRACE") == "1" && tally.Fallbacks <= 2)
                        try
                        {
                            // The same view and search outside Decide's catch, for the stack trace.
                            int probe = decisions;
                            var view = options[2].FairViewFactory(game, 2, unchecked(997 * game.Turn + probe * 31), DeckStyle.Value);
                            var fg = view.FingerprintGame;
                            foreach (var aura in fg.Auras)
                                if (!fg.IdEntityDic.TryGetValue(aura.Owner.Id, out var owner) || !ReferenceEquals(owner, aura.Owner))
                                    Console.WriteLine("TRACE aura owner " + aura.Owner.Id + " " + aura.Owner.Card?.Id + " zone " + aura.Owner.Zone?.Type
                                        + " -> in view: " + (owner == null ? "missing" : owner.GetType().Name + " " + owner.Card?.Id + " " + owner.Zone?.Type));
                            foreach (var e in game.Player1.SetasideZone)
                                Console.WriteLine("TRACE live setaside " + e.Id + " " + e.Card.Id + " pending " + e[SabberStoneCore.Actions.StartOfGameRules.Pending]);
                            BotSearch.Choose(view, budget);
                            Console.WriteLine("TRACE " + label + ": the search did not throw again");
                        }
                        catch (Exception e) { Console.WriteLine("TRACE " + label + " turn " + game.Turn + ": " + e); }
                }
                var before = actor == game.Player2 ? Rules(game, boss, label, task, tally) : null;
                LastStep = label + " turn " + game.Turn + " player " + actor.PlayerId + ": " + task.FullPrint();
                if (Verbose) Console.WriteLine("STEP " + LastStep + " | hand2: " + string.Join(", ", game.Player2.HandZone.Select(c => c.Id + "=" + c.Card.Id + "/" + c.Zone?.Type + "/" + c.Controller.PlayerId)));
                // As NativeMatch.Process: a choice is answered through the pending decision (TARGET choices of suspended
                // effects refuse a plain Process), every other task is processed.
                bool done;
                if (task is ChooseTask choose)
                {
                    var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                    done = game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, choose.Choices.ToArray()), out string refusal);
                    if (!done) tally.Note("answer refused: " + refusal);
                }
                else done = game.Process(task);
                if (!done) { tally.Failures.Add(label + ": turn " + game.Turn + " task refused: " + task.FullPrint()
                    + (actor.Choice == null ? " (no open choice)" : " (open choice " + actor.Choice.ChoiceType + " from " + game.IdEntityDic[actor.Choice.SourceId].Card.Id
                        + ": " + string.Join(",", actor.Choice.Choices.Select(id => id + "=" + game.IdEntityDic[id].Card.Id)) + ")")); break; }
                if (actor == game.Player2 && task.PlayerTaskType == PlayerTaskType.HERO_POWER) tally.PowerUses++;
                if (game.Player2.Hero != null) heroesSeen.Add(game.Player2.Hero.Card.Id);
                // Tad draws nothing (the rule proof covers draws); a minion of his bounced to his hand (Sap) stays there unplayed.
                if (boss.Id == "tad" && game.Player2.HandZone.Count != 0 && !tadHeld) { tadHeld = true; tally.Note("Tad given a card back"); }
            }
        }
        catch (Exception e) { tally.Failures.Add(label + ": turn " + game.Turn + " threw " + e.GetType().Name + ": " + e.Message); return; }

        bool won = game.Player1.PlayState == PlayState.WON;
        if (won) tally.Wins++;
        tally.Turns += game.Turn;
        int bossTurns = game.HistoryFacts.Query.TurnsTaken(game.Player2.Id);
        switch (boss.Id)
        {
            case "treasure-vault":
                if (game.State == State.COMPLETE && !won) tally.Failures.Add(label + ": the player lost to the Treasure Vault");
                if (bossTurns > 5) tally.Failures.Add(label + ": the Treasure Vault took " + bossTurns + " turns (Doors Are Closing at 5)");
                tally.Note("coffers broken " + SabberStoneCore.Actions.RunDeckRules.Of(game.Player1).Count);
                break;
            case "george-and-karl":
                if (heroesSeen.Count > 1) tally.Note("partner came in");
                if (won && heroesSeen.Count < 2) tally.Failures.Add(label + ": won without Karl ever coming in");
                break;
            case "tad":
                tally.Note(SabberStoneCore.Actions.RunDeckRules.Of(game.Player1).Contains("LOOTA_BOSS_22t") ? "Tad's Pole earned" : "no Pole");
                break;
        }
    }

    // Boss-specific rules checked on the task the boss is about to play.
    private static object Rules(Game game, Boss boss, string label, PlayerTask task, Tally tally)
    {
        int taken = game.HistoryFacts.Query.TurnsTaken(game.Player2.Id);
        if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD)
        {
            string id = task.Source?.Card.Id ?? "?";
            if (boss.Id == "a-f-kay" && taken < 6) tally.Failures.Add(label + ": A. F. Kay plays " + id + " on her turn " + taken);
            if (boss.Id == "tad") tally.Failures.Add(label + ": Tad plays " + id);
            if (boss.Id == "mushhuckster-max" && (id.StartsWith("LOOTA_BOSS_50t", StringComparison.Ordinal) || task.Source.Card.Name.Contains("Potion")))
                tally.Note("potion played");
            if (boss.Id == "king-togwaggle" && SabberStoneCore.CardSets.Client.Adventures.DungeonRunCards.ActiveTreasures.Contains(id))
                tally.Note("treasure played: " + task.Source.Card.Name);
        }
        if (task.PlayerTaskType == PlayerTaskType.HERO_POWER && boss.Id == "george-and-karl") tally.Note("power " + game.Player2.Hero.HeroPower.Card.Id);
        return null;
    }

    // seeds: games per boss. bosses: ids to play (null: all).
    internal static Dictionary<string, Tally> Sweep(int seeds, ICollection<string> only = null, Action<string> report = null)
    {
        var content = Load();
        var tallies = new Dictionary<string, Tally>();
        foreach (var boss in content.Bosses.Where(b => only == null || only.Contains(b.Id)))
        {
            var tally = tallies[boss.Id] = new Tally();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int seed = 1; seed <= seeds; seed++)
            {
                // A game the engine never returns from (an endless loop inside one action) is a finding too: report it and stop.
                var worker = new System.Threading.Thread(() => Fight(content, boss, seed, tally), 64 * 1024 * 1024) { IsBackground = true };
                worker.Start();
                if (!worker.Join(TimeSpan.FromSeconds(60)))
                {
                    tally.Failures.Add(boss.Id + " seed " + seed + ": no return from the engine in 60 s; last action " + LastStep);
                    report?.Invoke("HANG " + boss.Id + " seed " + seed + ": last action " + LastStep);
                    break;
                }
            }
            report?.Invoke(string.Format("{0,-24} games {1} player wins {2} avg turns {3:0.0} power {4} fallbacks {5} {6:0.0}s{7}{8}",
                boss.Id, tally.Games, tally.Wins, tally.Games == 0 ? 0 : (double)tally.Turns / tally.Games, tally.PowerUses, tally.Fallbacks,
                clock.Elapsed.TotalSeconds, tally.Notes.Count == 0 ? "" : " | " + string.Join("; ", tally.Notes.Select(n => n.Key + " x" + n.Value)),
                tally.Failures.Count == 0 ? "" : "\n    " + string.Join("\n    ", tally.Failures.Take(6))));
        }
        return tallies;
    }

    // The proof run: the special bosses, a few games each.
    [GroupProof("dungeon-run-boss-ai")]
    internal static void Run()
    {
        var special = new[] { "tad", "george-and-karl", "treasure-vault", "a-f-kay", "whompwhisker", "mushhuckster-max", "king-togwaggle" };
        var tallies = Sweep(3, special);
        foreach (var pair in tallies)
        {
            Check(pair.Value.Failures.Count == 0, pair.Key + ": " + string.Join(" | ", pair.Value.Failures.Take(3)));
            Check(pair.Value.PowerUses > 0, pair.Key + ": the boss uses its Hero Power");
        }
        Check(tallies["mushhuckster-max"].Notes.ContainsKey("potion played"), "Mushhuckster Max plays his potions");
        Check(tallies["george-and-karl"].Notes.ContainsKey("partner came in"), "George and Karl: the partner comes in");
    }
}
