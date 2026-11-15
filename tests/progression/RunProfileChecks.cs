using System.Xml.Serialization;
using EngineCards = SabberStoneCore.Model.Cards;

// Run modes on the save (plugin/LocalProfileRuns.cs): a run starts, its boss fights are marked while under way, results move
// the run and the class tallies in one commit, an interrupted fight is fought again, and nothing of it reaches gold, dust,
// ladder or match history. Older saves stay byte-identical, and run data that cannot be right is refused at load.
internal static class RunProfileChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException || error is IOException || error is InvalidDataException)
        { Console.WriteLine("PASS " + message); return; }
        throw new Exception("Expected rejection: " + message);
    }

    static bool Playable(string id) { try { return EngineCards.FromId(id).Implemented; } catch (KeyNotFoundException) { return false; } }
    static bool Labelled(string id) { try { return EngineCards.FromId(id) != null; } catch (KeyNotFoundException) { return false; } }

    static string Describe(RunState s) => s == null ? "none" : string.Join("|", s.Status, s.Step, s.Round, s.NextBoss, s.LostTo,
        string.Join(",", s.Deck), string.Join(",", s.Passives), string.Join(",", s.BossesFought), string.Join(",", s.PendingRewards),
        s.Offer == null ? "-" : s.Offer.Kind + ":" + string.Join(";", s.Offer.Options.Select(o => o.Title + "=" + string.Join(",", o.Cards))), s.RunEndSeen);

    internal static void Run(string root)
    {
        if (NpcWorldJson.Parser == null) NpcWorldJson.Parser = text => MiniJSON.Json.Deserialize(text);
        RunDefinition definition = RunDefinition.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "runs", "complete-run.json")));
        var engine = new RunEngine(RunAdmission.Evaluate(definition, Playable, Labelled));
        string dir = Path.Combine(root, "runs"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");

        int gold; long dust; int matches; int stars;
        using (var profile = new LocalProfile(path))
        {
            profile.CommitChange(next => 0);
            Check(profile.RunOf(definition) == null && !File.ReadAllText(path).Contains("<Runs"), "a save that never played a run stores no run data");
            gold = profile.StoredGoldBalance; dust = profile.DustBalance; matches = profile.History.Count; stars = profile.Ladder.StarIndex;
            RunState started = profile.StartRun(engine, "MAGE", "HERO_08", 11);
            Check(started.Status == RunStatus.Active && Describe(profile.RunOf(definition)) == Describe(started), "a run starts and is stored");
            Reject(() => profile.StartRun(engine, "WARRIOR", "HERO_01", 12), "a second run of the mode is refused while one is active");
        }
        string fightId; string before;
        using (var profile = new LocalProfile(path))
        {
            before = Describe(profile.RunOf(definition));
            Check(before.StartsWith("Active|", StringComparison.Ordinal), "the run survives a relaunch");
            LocalRunFight fight = profile.BeginRunFight(engine);
            fightId = fight.Id;
            Check(fight.Encounter.Round == 1 && fight.Encounter.PlayerHealth == 15 && profile.RunFightUnderway(definition), "a boss fight is marked under way");
            Reject(() => profile.BeginLaunch("mage", "warrior", 5, _ => { }), "no ladder match starts during a run fight");
            Reject(() => profile.BeginRunFight(engine), "no second run fight");
            Reject(() => profile.RetireRun(engine), "a run cannot change during its fight");
        }
        using (var profile = new LocalProfile(path))
        {
            // The game closed mid-fight: recovery clears the marker and the same boss waits, with the same Health and deck.
            profile.RecoverInterrupted();
            Check(!profile.RunFightUnderway(definition) && Describe(profile.RunOf(definition)) == before, "an interrupted fight leaves the run as it was");
            Reject(() => profile.ResolveRunFight(engine, fightId, RunFightResult.Won), "the interrupted fight's result is not accepted");
            LocalRunFight again = profile.BeginRunFight(engine);
            Check(again.Encounter.Boss.Id == profile.RunOf(definition).NextBoss && again.Id != fightId, "the same boss is fought again");
            Reject(() => profile.ResolveRunFight(engine, "not-the-fight", RunFightResult.Won), "a result for another fight is refused");
            RunState won = profile.ResolveRunFight(engine, again.Id, RunFightResult.Won);
            Check(won.Offer != null && won.NextBoss != null && !profile.RunFightUnderway(definition), "a win moves the run to its rewards, the next boss already chosen");
            Check(profile.RunWins(definition).Single().Bosses == 1, "the win counts for the class");
        }
        using (var profile = new LocalProfile(path))
        {
            Check(profile.RunOf(definition).Offer != null, "a pending reward survives a relaunch");
            RunState state = profile.RunOf(definition);
            while (state.Status == RunStatus.Active)
            {
                if (state.Offer != null) state = profile.ChooseRunReward(engine, 1);
                else state = profile.ResolveRunFight(engine, profile.BeginRunFight(engine).Id, RunFightResult.Won);
            }
            Check(state.Status == RunStatus.Won && state.BossesDefeated == 8, "a whole run is won through the profile");
            var wins = profile.RunWins(definition).Single();
            Check(wins.Class == "MAGE" && wins.Bosses == 8 && wins.Runs == 1, "eight boss wins and one run win for Mage");
            profile.MarkRunEndSeen(engine);
            Check(profile.RunOf(definition).RunEndSeen, "the run-end screen is remembered");

            RunState lost = profile.StartRun(engine, "WARRIOR", "HERO_01", 13);
            lost = profile.ResolveRunFight(engine, profile.BeginRunFight(engine).Id, RunFightResult.Lost);
            Check(lost.Status == RunStatus.Lost && profile.RunWins(definition).All(w => w.Class != "WARRIOR"), "a loss ends the run and counts nothing");
            Check(profile.StoredGoldBalance == gold && profile.DustBalance == dust && profile.History.Count == matches && profile.Ladder.StarIndex == stars,
                "run fights move no gold, dust, ladder or match history");

            profile.SetRunClientFlag(definition, 161, new long[] { 3, 4 });
            profile.SetRunClientFlag(definition, 162, new long[] { 1 });
            profile.SetRunClientFlag(definition, 162, new long[0]);
        }
        using (var profile = new LocalProfile(path))
        {
            var flags = profile.RunClientFlags(definition);
            Check(flags.Count == 1 && flags[0].Subkey == 161 && flags[0].Values.SequenceEqual(new long[] { 3, 4 }), "client flags are kept, and an empty one removed");
            Check(profile.RunOf(definition).Status == RunStatus.Lost, "the last run stays readable after it ended");
        }

        // A run saved by another version of its definition cannot be continued; a new run can start.
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string file) { using (var stream = File.OpenRead(file)) return (LocalProfileData)serializer.Deserialize(stream); }
        void Write(string file, LocalProfileData data) { using (var stream = File.Create(file)) serializer.Serialize(stream, data); }
        string Variant(string name, Action<LocalProfileData> change)
        {
            string file = Path.Combine(dir, name + ".xml");
            var data = Read(path); change(data); Write(file, data); return file;
        }
        string old = Variant("older-definition", data => { data.Runs[0].Run.DefinitionVersion = 99; data.Runs[0].Run.Status = "Active"; data.Runs[0].Run.NextBoss = "shaman-a"; data.Runs[0].Run.LostTo = null; });
        using (var profile = new LocalProfile(old))
        {
            Check(profile.RunOf(definition) == null, "a run saved by another definition version is not continued");
            Check(profile.StartRun(engine, "MAGE", "HERO_08", 21).Status == RunStatus.Active, "a new run replaces it");
        }
        Reject(() => new LocalProfile(Variant("fight-on-lost-run", data => data.Runs[0].FightId = "x")).Dispose(), "a fight marker on an ended run is refused at load");
        Reject(() => new LocalProfile(Variant("two-slots", data => data.Runs.Add(data.Runs[0]))).Dispose(), "two slots for one mode are refused at load");
        Reject(() => new LocalProfile(Variant("wins-over-bosses", data => data.Runs[0].Wins[0].Runs = 9)).Dispose(), "more run wins than boss wins is refused at load");
    }
}
