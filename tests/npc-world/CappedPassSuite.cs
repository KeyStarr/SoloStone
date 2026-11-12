using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CappedPassSuite
{
    private static string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "magmaw-capped-pass.json");
    private static Action<int, string, string> renewal; // sequence, recorded fingerprint, current canonical text; only while renewing
    // The recording's own catalog from tests/goldens/pools, read as PoolSnapshots.ForReplay does (C# 7.3 here): a later catalog edit
    // cannot change this game's engine state or RNG. The fair view still reads the live catalog (FairBotState.SecretPool, LocalCardCatalog).
    private static OfflinePoolManifest PinnedPools(string hash)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "pools", hash + ".xml");
        byte[] bytes = File.ReadAllBytes(path);
        using (var sha = SHA256.Create())
            Program.Require(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == File.ReadAllText(path + ".sha256").Trim(),
                "Magmaw pool snapshot checksum");
        var parse = typeof(OfflinePoolPolicy).GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var manifest = ((OfflinePoolPolicy)parse.Invoke(null, new object[] { XDocument.Load(new MemoryStream(bytes)) })).BuildManifest();
        Program.Require(manifest.Hash == hash, "Magmaw replay pool identity");
        return manifest;
    }
    private static LocalDeck Deck(JsonElement row) => new LocalDeck(row.GetProperty("id").GetString(),
        row.GetProperty("hero").GetString(), row.GetProperty("cards").EnumerateArray().Select(c => c.GetString()).ToArray());
    private static Game Replay(out NpcProfile profile, out DeckPlan plan, out GoldenPilot pilot, int through, bool allocation = true, bool deathValue = false)
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(fixture)))
        {
            var header = document.RootElement.GetProperty("header");
            var human = Deck(header.GetProperty("humanDeck")); var npc = Deck(header.GetProperty("npcDeck"));
            var pools = PinnedPools(header.GetProperty("poolHash").GetString());
            var config = new GameConfig {
                Player1Name = "Player", Player2Name = "Golem02", Player1HeroCard = Cards.FromId(human.Hero),
                Player2HeroCard = Cards.FromId(npc.Hero), Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.HUNTER,
                Player1Deck = human.Build(), Player2Deck = npc.Build(), StartPlayer = 1, Shuffle = true,
                SkipMulligan = false, History = true, Logging = false, EnableCorpses = true, FormatType = FormatType.FT_WILD,
                RandomSeed = header.GetProperty("engineSeed").GetInt32() };
            typeof(GameConfig).GetProperty("ReplayPools", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(config, pools);
            var game = new Game(config); game.StartGame();
            var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("regular");
            var personality = PersonalityCatalog.Parse(Program.Data("personalities-v1.json")).Find("chaotic");
            var deck = NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Find(header.GetProperty("npcDeck").GetProperty("id").GetString());
            plan = AutoDeckPlan.Derive(deck);
            profile = new NpcProfile("npc.g.0711", 1, "Golem02", deck.Id, null, 0,
                personality.ToPersonality(skill.ReplyBlendScale), NpcEncounterProfiles.StyledBudget(skill, personality), plan.Id);
            // Both arms pin an exact node-truncated path recorded on the base evaluator; this fixture guards capped continuation,
            // not valuation, so an evaluator family flag is off with it (death-value plays a different card at the cut).
            if (!deathValue) profile = profile.WithFixes(new AiFixes(allocation ? new[] { "death-value" } : new[] { "attack-allocation", "death-value" }));
            pilot = new GoldenPilot(profile, plan);
            foreach (var row in document.RootElement.GetProperty("actions").EnumerateArray())
            {
                int sequence = row.GetProperty("sequence").GetInt32(); if (sequence > through) break;
                string type = row.GetProperty("type").GetString();
                if (type == "decision") pilot.Options.FairViewFactory(game, 2, row.GetProperty("artificialSeed").GetInt32(), DeckStyle.Value);
                if (type == "main-begin") game.MainBegin();
                else {
                    var owner = row.GetProperty("seat").GetInt32() == 1 ? game.Player1 : game.Player2;
                    var pending = game.GetPendingDecisions().SingleOrDefault(d => d.ControllerId == owner.Id);
                    if (pending != null && (type == "mulligan" || row.GetProperty("action").GetString().StartsWith("CHOOSE:", StringComparison.Ordinal))) {
                        JsonElement selected;
                        var choices = row.TryGetProperty(type == "mulligan" ? "keeps" : "choices", out selected)
                            ? selected.EnumerateArray().Select(c => c.GetInt32()).ToArray()
                            : row.GetProperty("action").GetString().Split(':').Last().Split(',').Where(c => c.Length > 0).Select(int.Parse).ToArray();
                        string error;
                        Program.Require(game.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, choices), out error), "Magmaw choice #" + sequence + ": " + error);
                    } else {
                        string action = row.GetProperty("action").GetString();
                        Program.Require(game.Process(owner.Options().Single(t => BotAction.From(t).ToString() == action)), "Magmaw replay action " + sequence);
                    }
                }
                string recorded = row.GetProperty("fingerprint").GetString();
                if (renewal != null) renewal(sequence, recorded, MatchRecorder.FingerprintText(game));
                else Program.Require(MatchRecorder.Fingerprint(game) == recorded, "Magmaw fingerprint " + sequence);
                Program.Require(MatchRecorder.RandomState(game) == row.GetProperty("rngState").GetString(), "Magmaw RNG " + sequence);
            }
            return game;
        }
    }
    private static string Option(string[] args, string name)
    { int at = Array.IndexOf(args, name); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
    // `--magmaw-renew PATH [--dump DIR] [--baseline DIR [--accept "LABEL;LABEL" [--accept-behaviour "SHA: reason"] --write]]`: AiHarness journal-renew for this {header, actions} fixture.
    // Actions and every RNG state must still replay. --dump writes each row's canonical state; run it on the last passing commit.
    // --baseline compares this engine with such a dump and prints what drifted, by label. --write rewrites only `fingerprint` values;
    // it needs a baseline and --accept naming exactly the printed labels. BEHAVIOUR and STRUCTURAL labels also need
    // --accept-behaviour "SHA: reason", for a deliberate change this journal has no other way to absorb. Exit 1 reports drift without writing.
    internal static int Renew(string[] args)
    {
        fixture = Path.GetFullPath(args[1]);
        string dump = Option(args, "--dump"), baseline = Option(args, "--baseline");
        bool write = Array.IndexOf(args, "--write") >= 0;
        if (write && baseline == null) { Console.Error.WriteLine("--write needs --baseline DIR: see what drifted first"); return 1; }
        if (dump != null) Directory.CreateDirectory(dump);
        var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        var drift = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        int rows = 0, first = 0;
        renewal = (sequence, recorded, text) => {
            rows++;
            string name = sequence.ToString("000") + ".txt", old = null;
            if (dump != null) File.WriteAllText(Path.Combine(dump, name), text);
            if (baseline != null) {
                old = File.ReadAllText(Path.Combine(baseline, name));
                Program.Require(MatchRecorder.Hash(old) == recorded, "baseline is not the state this journal recorded at #" + sequence);
            }
            if (MatchRecorder.Hash(text) == recorded) return;
            if (first == 0) first = sequence;
            fingerprints[recorded] = MatchRecorder.Hash(text);
            if (old != null) foreach (var change in StateDiff(old, text)) {
                List<string> seen;
                if (!drift.TryGetValue(change.Key, out seen)) drift[change.Key] = seen = new List<string>();
                seen.Add("#" + sequence + " " + change.Value);
            }
        };
        NpcProfile profile; DeckPlan plan; GoldenPilot pilot;
        Replay(out profile, out plan, out pilot, int.MaxValue);
        Console.WriteLine(fixture + ": " + fingerprints.Count + " of " + rows + " fingerprints differ from the current engine"
            + (first == 0 ? "" : ", first at #" + first) + "; all RNG states match");
        foreach (var change in drift) Console.WriteLine("  " + change.Key + ": " + change.Value.Select(v => v.Split(' ')[0]).Distinct().Count() + " rows, "
            + change.Value.Count + " changes, first " + change.Value[0]);
        if (fingerprints.Count == 0) return 0;
        if (baseline == null) Console.WriteLine("  no --baseline: run --dump on the last passing commit to see what drifted");
        if (!write) return 1;
        string refusal = WriteRefusal(drift.Keys, Option(args, "--accept"), Option(args, "--accept-behaviour"));
        if (refusal != null) { Console.Error.WriteLine("refusing to write: " + refusal); return 1; }
        if (Gated(drift.Keys).Any()) Console.WriteLine("behaviour change accepted: " + Option(args, "--accept-behaviour"));
        string journal = File.ReadAllText(fixture);
        foreach (var pair in fingerprints) journal = journal.Replace("\"fingerprint\":\"" + pair.Key + "\"", "\"fingerprint\":\"" + pair.Value + "\"", StringComparison.Ordinal);
        File.WriteAllText(fixture, journal);
        Console.WriteLine("rewrote " + fingerprints.Count + " fingerprints");
        return 0;
    }
    private static IEnumerable<string> Gated(IEnumerable<string> labels) =>
        labels.Where(k => k.StartsWith("STRUCTURAL", StringComparison.Ordinal) || k.StartsWith("BEHAVIOUR", StringComparison.Ordinal));
    private static string WriteRefusal(IEnumerable<string> labels, string accept, string behaviour)
    {
        var names = labels.ToArray();
        if (!new HashSet<string>((accept ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)).SetEquals(names))
            return "--accept must name exactly: " + string.Join(";", names);
        var gated = Gated(names).ToArray();
        if (gated.Length > 0 && !Regex.IsMatch(behaviour ?? "", "^[0-9a-f]{7,40}: \\S.{9,}$"))
            return "these change behaviour: " + string.Join(";", gated) + ". Bisect them; a deliberate change needs --accept-behaviour \"SHA: reason\" and an audit in npc-ai-tuning.md";
        return null;
    }
    // Tags that decide what the game does, so drift in them is never a mere renewal. Not exhaustive: other labels still need reading.
    private static readonly HashSet<int> GameplayTags = new HashSet<int>(new[] {
        GameTag.HEALTH, GameTag.DAMAGE, GameTag.ATK, GameTag.ARMOR, GameTag.COST, GameTag.ZONE, GameTag.ZONE_POSITION, GameTag.CONTROLLER,
        GameTag.EXHAUSTED, GameTag.DURABILITY, GameTag.RESOURCES, GameTag.RESOURCES_USED, GameTag.TEMP_RESOURCES, GameTag.TAUNT,
        GameTag.DIVINE_SHIELD, GameTag.CHARGE, GameTag.STEALTH, GameTag.FROZEN, GameTag.SILENCED, GameTag.WINDFURY, GameTag.POISONOUS,
        GameTag.LIFESTEAL, GameTag.TURN, GameTag.STEP, GameTag.NEXT_STEP, GameTag.CURRENT_PLAYER, GameTag.PLAYSTATE, GameTag.OVERLOAD,
        GameTag.IMMUNE, GameTag.RUSH, GameTag.REBORN, GameTag.CARDTYPE, GameTag.ATTACHED, GameTag.CANT_ATTACK, GameTag.DORMANT,
        GameTag.SPELLPOWER, GameTag.NUM_TURNS_IN_PLAY }.Select(t => (int)t));
    private static Dictionary<string, string> TagMap(string tags) => tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Split('=')).ToDictionary(t => t[0], t => t[1]);
    private static IEnumerable<KeyValuePair<string, string>> TagDiff(string where, string name, string before, string after)
    {
        var old = TagMap(before); var now = TagMap(after);
        foreach (string tag in old.Keys.Union(now.Keys).OrderBy(int.Parse)) {
            string a, b; old.TryGetValue(tag, out a); now.TryGetValue(tag, out b);
            if (a == b) continue;
            int number = int.Parse(tag);
            string label = GameplayTags.Contains(number) ? "BEHAVIOUR tag " + tag + " " + (GameTag)number : "tag " + tag;
            yield return new KeyValuePair<string, string>(label + " on " + name, where + ": " + (a ?? "absent") + " -> " + (b ?? "absent"));
        }
    }
    // What differs between two MatchRecorder.FingerprintText states: label -> example. Other tags and history can drift without
    // behaviour changing, so the reader judges them from the example; a gameplay tag is BEHAVIOUR, a moved entity, card, zone or choice STRUCTURAL.
    internal static IEnumerable<KeyValuePair<string, string>> StateDiff(string before, string after)
    {
        const string history = "|history:";
        string[] Sections(string text) => text.Substring(0, text.IndexOf(history, StringComparison.Ordinal)).Split('|');
        Func<string, Dictionary<string, string[]>> entities = head => head.Split(';').Skip(1).Select(e => e.Split(new[] { ':' }, 3)).ToDictionary(e => e[0]);
        var change = new List<KeyValuePair<string, string>>();
        Action<string, string> structural = (label, example) => change.Add(new KeyValuePair<string, string>("STRUCTURAL " + label, example));
        string[] old = Sections(before), now = Sections(after);
        string[] oldHead = old[0].Split(';'), nowHead = now[0].Split(';');
        change.AddRange(TagDiff("game", "game", oldHead[0].Substring(5), nowHead[0].Substring(5)));
        var oldEntities = entities(old[0]); var nowEntities = entities(now[0]);
        foreach (string id in oldEntities.Keys.Union(nowEntities.Keys).OrderBy(int.Parse)) {
            string[] a, b; oldEntities.TryGetValue(id, out a); nowEntities.TryGetValue(id, out b);
            if (a == null || b == null) structural("entity " + (a == null ? "added" : "removed"), "entity " + id + " " + (a ?? b)[1]);
            else if (a[1] != b[1]) structural("entity card", "entity " + id + " " + a[1] + " -> " + b[1]);
            else change.AddRange(TagDiff("entity " + id, a[1], a[2], b[2]));
        }
        if (old.Length != now.Length) structural("choice", "sections " + old.Length + " -> " + now.Length);
        else for (int i = 1; i < old.Length; i++) {
            if (old[i] == now[i]) continue;
            if (!old[i].StartsWith("player:", StringComparison.Ordinal) || !now[i].StartsWith("player:", StringComparison.Ordinal)) { structural("choice", old[i] + " -> " + now[i]); continue; }
            string[] a = old[i].Split('/'), b = now[i].Split('/'), ownerA = a[0].Split(new[] { ':' }, 3), ownerB = b[0].Split(new[] { ':' }, 3);
            change.AddRange(TagDiff("player " + ownerA[1], "player", ownerA[2], ownerB[2]));
            if (!a.Skip(1).SequenceEqual(b.Skip(1))) structural("zone", "player " + ownerA[1]);
        }
        string[] oldHistory = before.Substring(before.IndexOf(history, StringComparison.Ordinal)).Split('\n'),
            nowHistory = after.Substring(after.IndexOf(history, StringComparison.Ordinal)).Split('\n');
        if (!oldHistory.SequenceEqual(nowHistory)) {
            Func<string, string, string> shown = (sign, line) => "\n      " + sign + " " + (line.Length > 160 ? line.Substring(0, 160) + "..." : line);
            change.Add(new KeyValuePair<string, string>("history text", string.Concat(oldHistory.Except(nowHistory).Take(6).Select(l => shown("-", l)))
                + string.Concat(nowHistory.Except(oldHistory).Take(6).Select(l => shown("+", l)))));
        }
        if (change.Count == 0 && before != after) structural("unclassified", "text differs");
        return change;
    }
    private static void StateDiffShapes()
    {
        const string state = "game:1=2;4:CS2_1:45=3,47=1;5:CS2_2:45=2|player:1:7=1/4//5//|choice:1:4|player:2:7=2////|history:x";
        Func<string, string> labels = text => string.Join(",", StateDiff(state, text).Select(c => c.Key));
        Program.Require(labels(state) == "", "identical states have no drift");
        Program.Require(labels(state.Replace("45=3,47=1", "45=3,47=1,32319=5")) == "tag 32319 on CS2_1", "new tag");
        Program.Require(labels(state.Replace("45=3", "45=2")) == "BEHAVIOUR tag 45 HEALTH on CS2_1", "stat tag is behaviour");
        Program.Require(labels(state.Replace("45=3,47=1", "45=3,47=1,49=2")) == "BEHAVIOUR tag 49 ZONE on CS2_1", "zone tag is behaviour");
        Program.Require(labels(state.Replace("game:1=2", "game:1=3")) == "tag 1 on game", "game tag");
        Program.Require(labels(state.Replace("player:1:7=1", "player:1:7=9")) == "tag 7 on player", "player tag");
        Program.Require(labels(state.Replace("|history:x", "|history:y")) == "history text", "history");
        string shown = StateDiff(state + "\nkeep\nold line", state + "\nkeep\nnew line").Single().Value;
        Program.Require(shown.Contains("- old line") && shown.Contains("+ new line") && !shown.Contains("keep"), "history shows only its changed lines");
        const string good = "f373be8c8: Devoured Soul names its donor";
        Program.Require(WriteRefusal(new[] { "tag 32319 on EDR_491e" }, "tag 32319 on EDR_491e", null) == null, "reference tag renews on its label");
        Program.Require(WriteRefusal(new[] { "history text" }, "history text", null) == null, "history renews on its label");
        Program.Require(WriteRefusal(new[] { "tag 1 on game" }, null, null) != null, "label must be named");
        foreach (string label in new[] { "BEHAVIOUR tag 45 HEALTH on X", "BEHAVIOUR tag 49 ZONE on X", "STRUCTURAL zone" }) {
            Program.Require(WriteRefusal(new[] { label }, label, null) != null, "label alone cannot accept " + label);
            Program.Require(WriteRefusal(new[] { label }, label, "because") != null, "reason needs a commit: " + label);
            Program.Require(WriteRefusal(new[] { label }, null, good) != null, "reason cannot stand in for the label: " + label);
            Program.Require(WriteRefusal(new[] { label }, label, good) == null, "commit and reason accept " + label);
        }
        Program.Require(labels(state.Replace("CS2_2", "CS2_3")) == "STRUCTURAL entity card", "card");
        Program.Require(labels(state.Replace(";5:CS2_2:45=2", "")) == "STRUCTURAL entity removed", "removed entity");
        Program.Require(labels(state.Replace("|player:1", ";6:CS2_3:45=1|player:1")) == "STRUCTURAL entity added", "added entity");
        Program.Require(labels(state.Replace("/4//5//", "/5//4//")) == "STRUCTURAL zone", "zone");
        Program.Require(labels(state.Replace("|choice:1:4", "|choice:1:5")) == "STRUCTURAL choice", "choice");
        Program.Require(labels(state.Replace("|choice:1:4", "")) == "STRUCTURAL choice", "choice gone");
        Console.WriteLine("PASS Magmaw renew classifies tags, history and structural drift");
    }
    private static void SeedCache(GoldenPilot pilot, FairBotState view, BotAction power, BotAction end)
    {
        pilot.Accept(view, new[] { power, end }, 0, LethalOutcome.Incomplete);
        // Also compiles against the pre-fix pilot for the red/green check.
        typeof(GoldenPilot).GetProperty("CachedInterrupted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(pilot, true);
    }
    private static void JournalRoot(bool allocation, bool deathValue = false)
    {
        NpcProfile profile; DeckPlan plan; GoldenPilot pilot;
        var game = Replay(out profile, out plan, out pilot, 110, allocation, deathValue);
        var view = pilot.Options.FairViewFactory(game, 2, 23515, DeckStyle.Value);
        var job = pilot.Job(view, new GoldenContext(22, 51, 23515, profile));
        var plannerField = typeof(GoldenDecisionJob).GetField("planner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var beamField = typeof(TurnPlannerJob).GetField("beam", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int steps = 0;
        while (!job.IsDone) {
            var planner = (TurnPlannerJob)plannerField.GetValue(job);
            if (planner != null && ((TurnBeam)beamField.GetValue(planner)).Nodes == 1572) break;
            job.StepNode(); Program.Require(++steps < 10000, "Magmaw reaches exact own-beam cutoff");
        }
        var first = Cut(job);
        Program.Require(deathValue || first.Action.ToString() == "PLAY_CARD:155:170:-1:-1:", "Magmaw truncated root matches recorded first action");
        var action = first.Action;
        for (int i = 1; i < 5; i++) {
            Program.Require(view.Actions.Contains(action) && game.Process(action.Resolve(game.Player2.Options())), "Magmaw commitment legal");
            view = pilot.Options.FairViewFactory(game, 2, 23515 + 31 * i, DeckStyle.Value);
            var next = pilot.Job(view, new GoldenContext(22, 51 + i, 23515 + 31 * i, profile));
            next.CapPlanNodes(() => 1);
            action = Cut(next).Action;
        }
        // The shipped evaluator commits differently, so it has no pinned target: it must still spend the turn, not end it.
        if (deathValue) {
            Program.Require(action.Type != PlayerTaskType.END_TURN, "Magmaw with death-value keeps acting after commitments: " + action);
            Console.WriteLine("PASS exact Magmaw own-beam cutoff with death-value keeps acting: " + action);
            return;
        }
        // Allocation ON keeps an additional 0/1 Taunt alive through its different commitments.
        // OFF must still reproduce the base's exact 5/7-Taunt trade after the recorded cutoff.
        int expectedTarget = allocation ? 182 : 10;
        Program.Require(action.Type == PlayerTaskType.MINION_ATTACK && action.Target == expectedTarget
            && view.Facts.Opponent.Board.Any(c => c.EntityId == action.Target && c.Taunt
                && !c.Stealth && !c.Untouchable && !c.Immune),
            "Magmaw root interrupted at 1572 must continue trading after commitments, allocation=" + allocation + ": " + action);
        Console.WriteLine("PASS exact Magmaw own-beam 1572 cutoff and four committed actions, allocation=" + allocation + ": " + action);
    }
    private static GoldenDecision Cut(GoldenDecisionJob job)
    { job.StopSearch(SearchStop.Clock); return Finish(job); }
    private static GoldenDecision Finish(GoldenDecisionJob job)
    { while (!job.IsDone) job.StepNode(); return job.Result; }
    private static void Magmaw()
    {
        NpcProfile profile; DeckPlan plan; GoldenPilot pilot;
        var game = Replay(out profile, out plan, out pilot, 113);
        var view = pilot.Options.FairViewFactory(game, 2, 23515, DeckStyle.Value);
        var context = new GoldenContext(22, 54, 23608, profile);
        pilot.Job(view, new GoldenContext(22, 51, 23515, profile));
        pilot.Job(view, context);
        var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        // Exact recorded commitments leave a cached pass at #115. The primary allowance is already gone.
        SeedCache(pilot, view, power, end);
        pilot.ChargePlan(int.MaxValue);
        string last = "", lastRng = "";
        using (var document = JsonDocument.Parse(File.ReadAllText(fixture))) {
            var row = document.RootElement.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("sequence").GetInt32() == 114);
            last = row.GetProperty("fingerprint").GetString(); lastRng = row.GetProperty("rngState").GetString();
        }
        Program.Require(game.Process(power.Resolve(game.Player2.Options()))
            && MatchRecorder.Fingerprint(game) == last && MatchRecorder.RandomState(game) == lastRng,
            "Magmaw authoritative #114 hero power fingerprint/RNG");
        // A real decision rebuilds its fair view with #115's seed; reply comparisons retain the common turn seed.
        var current = pilot.Options.FairViewFactory(game, 2, 23639, DeckStyle.Value);
        Program.Require(Math.Abs(current.Evaluate(new DeckPlanEvaluator(plan, profile.Budget.Brain)) - 108.88) < 1e-6,
            "journal post-commitment static value");
        var job = pilot.Job(current, new GoldenContext(22, 55, 23639, profile)); job.CapPlanNodes(() => 1);
        var decision = Finish(job);
        Program.Require(decision.Action.Type == PlayerTaskType.MINION_ATTACK && decision.Action.Target == 10,
            "Magmaw must trade through 5/9 Taunt at exhausted live cap: " + decision.Action);
        Program.Require(decision.SearchInfo.ValidationNodes > 370 && decision.SearchInfo.ValidationNodeCap == 7000,
            "Magmaw independent validation includes replies");
        var pass = TurnPlanner.Job(current, profile, plan, pilot.TurnSeed, 7000, pilot.PublicInputs.OpponentPlayed,
            pilot.PublicInputs, end); while (!pass.IsDone) pass.StepNode();
        Program.Require(decision.Value > pass.Result.Value + 1, "Magmaw improvement on identical reply footing");
        var dig = current.Facts.Own.Board.Single(c => c.EntityId == 171 && c.DeathValue.Any && !c.Exhausted);
        var trade = current.Actions.Single(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == dig.EntityId && a.Target == 10);
        SeedCache(pilot, current, trade, end);
        FairBotState changed = null;
        for (int seed = 1; seed <= 16; seed++) {
            var candidate = current.WithArtificialSeed(seed).Apply(trade);
            if (!pilot.CanReuse(candidate)) { changed = candidate; break; }
        }
        Program.Require(changed != null && changed.Actions.Contains(end), "public random Deathrattle result invalidates legal cached pass");
        var replan = pilot.Job(changed, new GoldenContext(22, 56, 23670, profile)).StopSearch(SearchStop.Clock);
        Program.Require(!pilot.PlanReused, "random outcome must replan an incompletely searched continuation");
        Console.WriteLine("PASS random Deathrattle outcome invalidates capped continuation despite legal END_TURN");
        Console.WriteLine("PASS Magmaw: 114 fingerprints/RNG; pass=" + pass.Result.Value + " continuation=" + decision.Value
            + " validationNodes=" + decision.SearchInfo.ValidationNodes + " action=" + decision.Action);
    }
    private static void Generic()
    {
        foreach (bool clock in new[] { false, true }) {
            var game = PassivitySuite.Fixture(2); RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
            var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); pilot.Job(view, RoundTwoSuite.Context(view, profile));
            var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            SeedCache(pilot, view, power, end);
            pilot.ChargePlan(int.MaxValue); var current = view.Apply(power);
            var job = pilot.Job(current, RoundTwoSuite.Context(current, profile, 2)); job.CapPlanNodes(() => 1);
            var decision = clock ? Cut(job) : Finish(job);
            Program.Require(decision.Action.Type == PlayerTaskType.MINION_ATTACK && decision.SearchInfo.ValidationNodes > 0,
                "capped cached END_TURN cannot bypass better continuation, clock=" + clock);
            var completed = current.Apply(decision.Action);
            var kept = pilot.Job(completed, RoundTwoSuite.Context(completed, profile, 3)).StopSearch(SearchStop.Clock);
            Program.Require(kept.Action.Type == PlayerTaskType.END_TURN && pilot.PlanReused
                && kept.SearchInfo.ValidationNodes == 0, "completed validation replaces incomplete cache provenance");
        }
        Console.WriteLine("PASS generic capped cached pass: ordinary and clock-exhausted primary budgets");
    }
    private static void ValidationLethal()
    {
        foreach (string mode in new[] { "clock", "reserve-only", "deadline", "decorated-deadline", "decorated-complete", "decorated-clock" }) {
            var game = PassivitySuite.Fixture(2); RoundTwoSuite.Body(game, true, 3, 3);
            game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); pilot.Job(view, RoundTwoSuite.Context(view, profile));
            var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            SeedCache(pilot, view, power, end); pilot.ChargePlan(int.MaxValue); view = view.Apply(power);
            var job = pilot.Job(view, RoundTwoSuite.Context(view, profile, 2));
            if (mode == "reserve-only") {
                while (!job.Validating && !job.IsDone) job.StepNode();
                Program.Require(job.Validating && job.LethalResult != "found", "lethal must be found only inside reserve");
            } else Program.Require(job.StopSearch(SearchStop.Clock) == null && job.Validating, "clock leaves reserve pending");
            GoldenDecision result;
            if (mode == "deadline" || mode.StartsWith("decorated-", StringComparison.Ordinal)) {
                var planner = (TurnPlannerJob)typeof(GoldenDecisionJob).GetField("validation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(job);
                // Stop between the reserve finding its witness and the outer job publishing it.
                while (!planner.IsDone) planner.StepNode();
                Program.Require(planner.Result.Value == 1000000 && !planner.HasPassComparison,
                    "verified lethal completes before reply comparison");
                if (mode.StartsWith("decorated-", StringComparison.Ordinal)) {
                    var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
                    var wrapped = new SkilledPilot(pilot, null, skill, 1).Begin(view, RoundTwoSuite.Context(view, profile, 2));
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    wrapped.GetType().GetField("current", flags).SetValue(wrapped, job);
                    wrapped.GetType().GetField("baseline", flags).SetValue(wrapped,
                        new GoldenDecision(end, 99, 1, 1, "Exhausted", null, null));
                    wrapped.GetType().GetField("variantKind", flags).SetValue(wrapped, MistakeKind.IgnoreAoeRisk);
                    if (mode == "decorated-deadline") result = ((INpcPassValidation)wrapped).StopValidation();
                    else {
                        if (mode == "decorated-clock") Program.Require(((INpcSearchContinuation)wrapped).StopSearch(SearchStop.Clock) == null,
                            "decorated clock cut retains pending validation");
                        while (!wrapped.IsDone) wrapped.StepNode();
                        result = wrapped.Result;
                    }
                    Program.Require(result.Nodes == job.Result.Nodes + 1, "decorated lethal counts each search once: " + mode);
                } else result = job.StopValidation();
            } else result = Finish(job);
            Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK
                && result.Action.Target == view.Facts.Opponent.Hero.EntityId && result.Value == 1000000,
                "validation cannot discard proven lethal: " + mode + " action=" + result.Action + " value=" + result.Value);
            Program.Require(!result.SearchInfo.ValidationCompared && !result.SearchInfo.ValidationFallback
                && result.SearchInfo.ValidationNodes > 0, "lethal needs no pass comparison or fallback: " + mode);
            Program.Require(result.SearchInfo.ValidationDeadline == mode.EndsWith("deadline", StringComparison.Ordinal),
                "lethal retains deadline flag: " + mode);
            Program.Require(view.Apply(result.Action).IsTerminal, "published lethal executes on real fair engine: " + mode);
            Console.WriteLine("PASS validation lethal " + mode + ": " + result.Action + " nodes=" + result.SearchInfo.ValidationNodes);
        }
    }
    private static void SolverClockLethal()
    {
        foreach (bool decorated in new[] { false, true }) {
            var game = PassivitySuite.Fixture(0); RoundTwoSuite.Body(game, true, 3, 3); game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile, PassivitySuite.Plan());
            var context = RoundTwoSuite.Context(view, profile); var job = pilot.Job(view, context);
            while (!job.LethalSearch.IsDone) job.LethalSearch.StepNode();
            Program.Require(job.LethalResult == "found" && !job.IsDone, "solver proves lethal before clock publication");
            IGoldenDecisionJob current = job;
            if (decorated) {
                var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
                current = new SkilledPilot(pilot, null, skill, 1).Begin(view, context);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                current.GetType().GetField("current", flags).SetValue(current, job);
                current.GetType().GetField("baseline", flags).SetValue(current,
                    new GoldenDecision(view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN), 99, 1, 1, "Exhausted", null, null));
                current.GetType().GetField("variantKind", flags).SetValue(current, MistakeKind.IgnoreAoeRisk);
            }
            var result = ((INpcSearchContinuation)current).StopSearch(SearchStop.Clock);
            Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.Value == 1000000
                && view.Apply(result.Action).IsTerminal && !result.SearchInfo.ValidationFallback,
                "clock publication must retain solver lethal score and line, decorated=" + decorated);
        }
        Console.WriteLine("PASS solver lethal survives direct and decorated clock publication");
    }
    private static void WorkerLethalAtValidationBoundary()
    {
        bool previous = GoldenPilot.ParallelLethalEnabled;
        GoldenPilot.ParallelLethalEnabled = true;
        GoldenDecisionJob job = null;
        try {
            var game = PassivitySuite.Fixture(0); RoundTwoSuite.Body(game, true, 3, 3); game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0];
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); job = pilot.Job(view, RoundTwoSuite.Context(view, profile));
            job.PollBackground();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var parallel = typeof(GoldenDecisionJob).GetField("parallel", flags).GetValue(job);
            var workers = (System.Threading.Tasks.Task[])parallel.GetType().GetField("running", flags).GetValue(parallel);
            System.Threading.Tasks.Task.WaitAll(workers);
            Program.Require(job.LethalResult != "found", "worker witness remains unpublished before owner collection");
            // Freeze the boundary after the guard reads progress but before Stop collects the completed workers.
            typeof(GoldenDecisionJob).GetMethod("Complete", flags).Invoke(job, new object[] {
                new[] { view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) }, 0d, SearchStop.Nodes, null });
            var result = job.IsDone ? job.Result : job.StopValidation();
            Program.Require(job.LethalResult == "found" && result.Value == 1000000
                && result.Action.Type == PlayerTaskType.MINION_ATTACK && view.Apply(result.Action).IsTerminal
                && !result.SearchInfo.ValidationFallback,
                "worker lethal collected at validation entry must replace pass");
            Console.WriteLine("PASS worker lethal collected at validation boundary replaces pass");
        }
        finally { job?.StopBackground(); GoldenPilot.ParallelLethalEnabled = previous; }
    }
    private static void EliteSecret()
    {
        var game = PassivitySuite.Fixture(2);
        RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
        SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EX1_287"), zone: game.Player1.SecretZone);
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("elite");
        var p = NpcCatalog.All[0];
        var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
            p.Personality, skill.Budget, p.PlanId, p.HeroSkinPower);
        var view = PassivitySuite.View(game); var plan = PassivitySuite.Plan();
        var normal = RoundTwoSuite.Search(view, plan, profile);
        Program.Require(normal.Line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK), "elite Secret normal continuation");
        var validation = TurnPlanner.Job(view, profile, plan, 71, 7000, comparePass: true);
        while (!validation.IsDone) validation.StepNode();
        Console.WriteLine("elite Secret comparison=" + validation.HasPassComparison + " nodes=" + validation.Result.Nodes
            + " candidates=" + string.Join(";", validation.Result.Candidates.Select(c => c.Key + "=" + c.Value))
            + " line=" + string.Join(";", validation.Result.Line));
        Program.Require(validation.HasPassComparison && validation.Result.Line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK),
            "elite Counterspell must compare pass and real continuation with full replies");
        Program.Require(validation.Result.Candidates.Any(c => c.Key.Type == PlayerTaskType.END_TURN)
            && validation.Result.Candidates.Any(c => c.Key.Type != PlayerTaskType.END_TURN), "both completed reply scores retained");
        var pilot = new GoldenPilot(profile, plan); pilot.Job(view, RoundTwoSuite.Context(view, profile));
        var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        SeedCache(pilot, view, power, end); pilot.ChargePlan(int.MaxValue);
        var current = view.Apply(power);
        var result = Cut(pilot.Job(current, RoundTwoSuite.Context(current, profile, 2)));
        Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.SearchInfo.ValidationCompared
            && !result.SearchInfo.ValidationFallback, "elite Secret cached pass cannot certify itself");
        Console.WriteLine("PASS elite Counterspell 3/3 vs 1/1 Taunt: direct and cached pass comparisons");
    }
    private static void WrappedDeadline()
    {
        var game = PassivitySuite.Fixture(2);
        RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
        var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
        var primary = new GoldenPilot(profile, PassivitySuite.Plan()); primary.Job(view, RoundTwoSuite.Context(view, profile));
        var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        SeedCache(primary, view, power, end); primary.ChargePlan(int.MaxValue); view = view.Apply(power);
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
        var pilot = new SkilledPilot(primary, null, skill, 1);
        var inner = pilot.Begin(view, RoundTwoSuite.Context(view, profile, 2));
        Program.Require(((INpcSearchContinuation)inner).StopSearch(SearchStop.Clock) == null,
            "decorated pass validation remains pending");
        var baseline = new GoldenDecision(view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK), 99, 1, 1, "Exhausted", null, null);
        // Model a variant's pending reserve after the original search already supplied a baseline.
        inner.GetType().GetField("baseline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(inner, baseline);
        var result = ((INpcPassValidation)inner).StopValidation();
        Program.Require(result.Action.Equals(baseline.Action) && result.SearchInfo.ValidationDeadline
            && result.SearchInfo.ValidationFallback, "deadline cannot accept an unpriced mistake variant");
        Console.WriteLine("PASS decorated validation deadline retains original baseline before mistake pricing");
    }
    // Review round 3: mistake pricing certifies a continuation only when it ends the turn (SkilledPilot.Complete). Below
    // ~1,960 plan nodes it has no reply finalists, and its best complete line is a released own-beam leaf: Whirlwinds keep
    // the beam two depths past Wisp/Wisp/Armor Up. That leaf must keep END_TURN, or every priced mistake is rejected.
    private static void PricingAtReplyLimitZero()
    {
        var game = PassivitySuite.Fixture(10, "CS2_231", "CS2_231", "EX1_400", "EX1_400");
        int wisp = game.Player1.HandZone[0].Id;
        var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0];
        Program.Require(profile.Fixes.On("attack-allocation"), "default profile runs attack-allocation");
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
        var owner = new SkilledPilot(new GoldenPilot(profile, PassivitySuite.Plan()), null, skill, 1);
        var job = owner.Begin(view, RoundTwoSuite.Context(view, profile));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var proposed = view.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == wisp);
        var raw = new GoldenDecision(proposed, -1000, 1, 1, "Exhausted", null, null);
        job.GetType().GetField("raw", flags).SetValue(job, raw);
        job.GetType().GetField("choice", flags).SetValue(job, new SkilledPilot.Choice { Index = 1 });
        job.GetType().GetField("pricingCap", flags).SetValue(job, 1500);
        job.GetType().GetMethod("StartPrice", flags).Invoke(job, new object[] { proposed });
        var pricing = (TurnPlannerJob)job.GetType().GetField("pricing", flags).GetValue(job);
        Program.Require(pricing != null && (int)typeof(TurnPlannerJob).GetField("replyLimit", flags).GetValue(pricing) == 0,
            "pricing at a late live cap has no reply finalists");
        var beam = (TurnBeam)typeof(TurnPlannerJob).GetField("beam", flags).GetValue(pricing);
        while (!beam.IsDone) job.StepNode();
        var best = TurnLine.Rank((System.Collections.Generic.IEnumerable<TurnLine>)typeof(TurnPlannerJob)
            .GetMethod("Candidates", flags).Invoke(pricing, null)).First();
        Program.Require(best.Completed && !best.Holds, "fixture: priced best complete leaf released its clone");
        while (!job.IsDone) job.StepNode();
        var reference = (PlannedTurn)job.GetType().GetField("reference", flags).GetValue(job);
        Program.Require(reference != null && reference.Line.Last().Type == PlayerTaskType.END_TURN,
            "released priced line keeps END_TURN: " + string.Join(";", reference?.Line ?? new BotAction[0]));
        Program.Require(job.Result != raw && job.Result.SearchInfo.Mistake == "softmax" && owner.SoftmaxDeviations == 1,
            "mistake pricing at replyLimit 0 completes and is accepted");
        Console.WriteLine("PASS mistake pricing at replyLimit 0 keeps END_TURN on a released best: " + string.Join(";", reference.Line));
    }
    internal static void Run() { StateDiffShapes(); PricingAtReplyLimitZero(); WorkerLethalAtValidationBoundary(); SolverClockLethal(); ValidationLethal(); WrappedDeadline(); EliteSecret(); Generic(); JournalRoot(false); JournalRoot(true); JournalRoot(true, true); Magmaw(); }
    internal static void RunMagmaw() { JournalRoot(false); JournalRoot(true); JournalRoot(true, true); Magmaw(); }
}
