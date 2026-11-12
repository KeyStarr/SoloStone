extern alias TransportFixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Actions;
using SabberStoneCore.Model.Decisions;
using PegasusGame;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    // Replays a real game (tests/native-match/fixtures/invisible-greenwing.json, 47 recorded actions, every fingerprint
    // and RNG state checked) in which an opponent's Taunt minion was invisible on turn 14: the engine had the 4/5 body
    // (entity 86, an Illusion token, EDR_260t) but its board slot looked empty. The token is drawn and summoned in one
    // packet (deck -> hand -> setaside -> play). The client hides an actor that passes through "setaside" and never shows
    // it again on play. Fix: board transit keeps the last real zone instead of the setaside hop. BoardIdentityHistory
    // repeats this with staged boards for both seats and checks the invisible Taunt still blocks attacks.
    private static void GreenwingJournalReplay()
    {
        using var document = JsonDocument.Parse(File.ReadAllText("tests/native-match/fixtures/invisible-greenwing.json"));
        var fixture = document.RootElement;
        var human = fixture.GetProperty("humanDeck"); var npc = fixture.GetProperty("npcDeck");
        var hero1 = Cards.FromId(human.GetProperty("hero").GetString()); var hero2 = Cards.FromId(npc.GetProperty("hero").GetString());
        string hash = fixture.GetProperty("poolHash").GetString();
        var policy = (OfflinePoolPolicy)typeof(OfflinePoolPolicy).GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Invoke(null, new object[] { XDocument.Load("tests/goldens/pools/" + hash + ".xml") });
        var game = new SabberStoneCore.Model.Game(new GameConfig {
            Player1Name = "Player", Player2Name = "RocketRhoda",
            Player1HeroClass = hero1.Class, Player1HeroCard = hero1,
            Player2HeroClass = hero2.Class, Player2HeroCard = hero2,
            Player1Deck = human.GetProperty("cards").EnumerateArray().Select(c => Cards.FromId(c.GetString())).ToList(),
            Player2Deck = npc.GetProperty("cards").EnumerateArray().Select(c => Cards.FromId(c.GetString())).ToList(),
            RandomSeed = fixture.GetProperty("engineSeed").GetInt32(), FormatType = FormatType.FT_WILD,
            StartPlayer = 1, Shuffle = true, SkipMulligan = false, History = true, EnableCorpses = true,
            Logging = false });
        typeof(SabberStoneCore.Model.Game).GetProperty("OfflinePools").SetValue(game, policy.BuildManifest());
        game.StartGame();
        var capture = new Capture(); var traces = new List<string>();
        var rawZones = new List<int>(); bool transformed = false;
        var match = new NativeMatch(capture.Send) { BotTrace = traces.Add };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        FlushFieldHistory(match);
        foreach (var row in fixture.GetProperty("actions").EnumerateArray())
        {
            string type = row.GetProperty("type").GetString();
            if (type == "main-begin") game.MainBegin();
            else
            {
                var owner = row.GetProperty("seat").GetInt32() == 1 ? game.Player1 : game.Player2;
                var pending = game.GetPendingDecisions().SingleOrDefault(d => d.ControllerId == owner.Id);
                if (pending != null && (type == "mulligan" || row.GetProperty("action").GetString().StartsWith("CHOOSE:", StringComparison.Ordinal)))
                {
                    var choices = row.TryGetProperty(type == "mulligan" ? "keeps" : "choices", out var picked)
                        ? picked.EnumerateArray().Select(c => c.GetInt32()).ToArray()
                        : row.GetProperty("action").GetString().Split(':').Last().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                    Check(game.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, choices), out string error), "journal choice " + error);
                }
                else
                {
                    if (game.Step == Step.BEGIN_MULLIGAN) game.MainBegin();
                    string action = row.GetProperty("action").GetString();
                    var task = owner.Options().Single(t => BotAction.From(t).ToString() == action);
                    InvokeProcess(match, task);
                }
            }
            foreach (var entry in game.PowerHistory.Last)
            {
                if (entry is SabberStoneCore.Kettle.PowerHistoryTagChange tag && tag.EntityId == 86 && tag.Tag == GameTag.ZONE) rawZones.Add(tag.Value);
                if (entry is SabberStoneCore.Kettle.PowerHistoryChangeEntity change && change.Entity.Id == 86) transformed = true;
            }
            FlushFieldHistory(match);
            Check(MatchRecorder.Fingerprint(game) == row.GetProperty("fingerprint").GetString(), "journal fingerprint sequence " + row.GetProperty("sequence"));
            Check(MatchRecorder.RandomState(game) == row.GetProperty("rngState").GetString(), "journal RNG sequence " + row.GetProperty("sequence"));
        }
        var body = game.Player2.BoardZone.Single(m => m.Id == 86);
        Check(body.Card.Id == "EDR_260t" && body.AttackDamage == 4 && body.Health == 5 && body.HasTaunt && body.ZonePosition == 2,
            "original entity 86 is the 4/5 Taunt in slot 3 at turn 14");
        var packets = capture.HistorySnapshots().ToArray();
        var rows = packets.SelectMany(p => p.List).ToArray();
        var created = rows.Single(e => e.HasFullEntity && e.FullEntity.Entity == 86).FullEntity;
        Check(created.Name == "" && created.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == 2), "entity 86 created nameless in DECK on wire");
        Check(!transformed && rawZones.TakeLast(3).SequenceEqual(new[] { 3, 6, 1 }), "engine draw HAND/SETASIDE/PLAY, no transform");
        Check(traces.Any(t => t.StartsWith("BOARD_TRANSIT") && t.Contains("entity=86 ")), "exact entity transit logged");
        Check(TransportFixtures::TransportInvariants.BoardActorInvariant.Validate(packets).Count == 0, "original journal board visibility");
        DumpDrawHistory("greenwing-live-journal", capture, 0);
        Console.WriteLine("PASS original Greenwing journal: 47 actions, fingerprints/RNG, entity 86 at turn 14 position 3");
    }

    private static void BoardIdentityHistory()
    {
        GreenwingJournalReplay();
        foreach (bool opponent in new[] { false, true })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "showcase-demon-hunter-outcast", 7) {
                DebugScenario = DebugScenario.Parse((opponent ? "npc" : "human") + "DeckTop=EDR_260t,EDR_260t\nhumanMana=10\nnpcMana=10\nhumanBoard=CS2_182\nnpcBoard=CS2_182\nnpcHand=EDR_260"),
                BotTrace = _ => { }
            };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            if (opponent) { InvokeProcess(match, EndTurnTask.Any(match.Game.Player1)); FlushFieldHistory(match); }
            var owner = opponent ? match.Game.Player2 : match.Game.Player1;
            var bodies = owner.BoardZone.Where(m => m.Card.Id == "EDR_260t").ToArray();
            Check(bodies.Length == 2, "both chained Illusions summoned");
            DumpDrawHistory("greenwing-p" + owner.PlayerId, capture, 0);
            var actorFaults = TransportFixtures::TransportInvariants.BoardActorInvariant.Validate(capture.HistorySnapshots());
            Check(actorFaults.Count == 0, "Illusion actors p" + owner.PlayerId + ": " + string.Join("; ", actorFaults));
            // Disable the native repair and reinsert the old parking hop: complete identity alone cannot show it.
            var mutated = capture.HistorySnapshots().ToArray();
            int bodyId = bodies[0].Id;
            var packet = mutated.First(p => p.List.Any(e => e.HasTagChange && e.TagChange.Entity == bodyId
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.PLAY));
            int move = packet.List.FindIndex(e => e.HasTagChange && e.TagChange.Entity == bodyId
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.PLAY);
            packet.List.Insert(move, new PowerHistoryData { TagChange = new() { Entity = bodyId, Tag = (int)GameTag.ZONE,
                Value = (int)SabberStoneCore.Enums.Zone.SETASIDE } });
            Check(TransportFixtures::TransportInvariants.BoardActorInvariant.Validate(mutated, repairNullToPlayActors: false).Any(f => f.Contains("hidden board actor")),
                "old SETASIDE shape rejected despite complete identity");
            if (opponent)
            {
                InvokeProcess(match, EndTurnTask.Any(owner)); FlushFieldHistory(match);
                var attacker = match.Game.Player1.BoardZone.Single(m => m.Card.Id == "CS2_182");
                var attacks = match.Game.Player1.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id).ToArray();
                Check(attacks.Length == 2 && attacks.All(t => bodies.Any(b => b.Id == t.Target.Id)),
                    "Taunt remains authoritative: both Illusions targetable, enemy hero/non-Taunt excluded");
            }
            var faults = NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List));
            Check(faults.Count == 0, "Illusion admission p" + owner.PlayerId + ": " + string.Join("; ", faults));
        }
        Console.WriteLine("PASS Greenwing: chained turn-start summons, both viewers, native visibility mutation and Taunt legality");
    }
}
