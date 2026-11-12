using System.Reflection;
using HsSim;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class CenariusProof
{
    private static void Check(bool condition, string message) => SelfChecks.Check(condition, "Cenarius " + message);
    internal static void Run()
    {
        ConcedeAtPrompts();
        for (int mask = 0; mask < 8; mask++)
        {
            var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true,
                Player1HeroClass = CardClass.DRUID, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
            game.StartGame();
            game.Player1.BaseMana = 10;
            Generic.SummonBlock(game, (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_231")), -1, null);
            game.DeathProcessingAndAuraUpdate();
            var source = Generic.DrawCard(game.Player1, Cards.FromId("EDR_209"));
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match);
            session.Flush();
            session.Process(PlayCardTask.Any(game.Player1, source));
            int lastPacketId = -1;
            for (int stage = 0; stage < 3; stage++)
            {
                var view = game.GetPendingDecisions().Single();
                if (stage == 0) session.PublishDecision(game.Player1);
                var wire = session.Packets.Last();
                Check(wire.Id == 17, "publishes choice, not normal options");
                var packet = new EntityChoices();
                using (var stream = new MemoryStream(Convert.FromBase64String(wire.Base64))) packet.Deserialize(stream);
                Check(packet.Id != lastPacketId && packet.PlayerId == 1 && packet.Source == source.Id
                    && packet.CountMin == 1 && packet.CountMax == 1 && packet.ChoiceType == (int)ChoiceType.GENERAL,
                    "fresh packet identity/owner/source/cardinality");
                Check(packet.Entities.SequenceEqual(view.Options.Select(o => o.OptionId)), "wire options equal authoritative offers");
                Check(packet.Entities.All(id => session.Client.Entities.TryGetValue(id, out var entity)
                    && entity.CardId == game.IdEntityDic[id].Card.Id), "show choices before reference");
                lastPacketId = packet.Id;
                int pick = packet.Entities[(mask >> stage) & 1];
                var bytes = game.SaveSnapshot();
                int packetsBefore = session.Packets.Count;
                foreach (var invalid in new[] { new List<int>(), new List<int> { pick, pick }, new List<int> { int.MaxValue } })
                {
                    bool rejected = false;
                    try { match.SendChoices(packet.Id, invalid); }
                    catch (ArgumentException) { rejected = true; }
                    Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()) && session.Packets.Count == packetsBefore,
                        "invalid cardinality/foreign pick leaves state and packets unchanged");
                }
                foreach (string fieldName in new[] { "humanDecisionId", "humanDecisionController" })
                {
                    var field = typeof(NativeMatch).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    object bound = field.GetValue(match);
                    field.SetValue(match, fieldName == "humanDecisionId" ? (object)(view.Id + 1000) : game.Player2.Id);
                    bool rejected = false;
                    try { match.SendChoices(packet.Id, new List<int> { pick }); }
                    catch (InvalidOperationException) { rejected = true; }
                    finally { field.SetValue(match, bound); }
                    Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()) && session.Packets.Count == packetsBefore,
                        "stale engine ID/foreign controller binding rejected without mutation");
                }
                match.SendChoices(packet.Id, new List<int> { pick });
                var published = session.Packets.Skip(packetsBefore).ToList();
                Check(published[0].Id == 13 && published.Any(p => p.Id == 19), "acknowledges and publishes resumed history");
                Check(published.Last().Id == (stage < 2 ? 17 : 14), "next prompt/options published automatically");
                Check(published.FindIndex(p => p.Id == 19) < published.Count - 1, "history precedes next prompt");
                string after = Canonical.Trace(game) + Canonical.Value(((Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).CaptureState());
                packetsBefore = session.Packets.Count;
                bool stale = false;
                try { match.SendChoices(packet.Id, new List<int> { pick }); }
                catch (InvalidOperationException) { stale = true; }
                Check(stale && after == Canonical.Trace(game) + Canonical.Value(((Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).CaptureState()) && packetsBefore == session.Packets.Count,
                    "replayed packet rejected without mutation or acknowledgment");
                Check(!Invariants.Compare(game, session.Client).Any(), "visible state matches after branch");
            }
            Check(!game.Resolution.HasSuspendedEffect, "three transported answers complete");
            int depth = 0;
            foreach (var wire in session.Packets.Where(p => p.Id == 19))
            {
                var packet = new PegasusGame.PowerHistory();
                using (var stream = new MemoryStream(Convert.FromBase64String(wire.Base64))) packet.Deserialize(stream);
                foreach (var entry in packet.List)
                {
                    if (entry.HasPowerStart) depth++;
                    if (entry.HasPowerEnd) Check(--depth >= 0, "block end has start");
                }
            }
            Check(depth == 0, "balanced transport blocks");
        }
        Console.WriteLine("PASS Cenarius transport: 8 branches/24 prompts, owner-scoped offers, show-before-reference, stats/zones/blocks; production SendChoices resumes all three decisions");
    }

    private static void ConcedeAtPrompts()
    {
        for (int stage = 0; stage < 3; stage++)
        foreach (bool botConcedes in new[] { false, true })
        {
            string directory = Path.Combine(Path.GetTempPath(), "hs-concede-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "profile.xml");
            string outcome = botConcedes ? "Won" : "Lost";
            try
            {
                string matchId;
                using (var profile = new LocalProfile(path))
                {
                    LadderBinding.Bind(profile);
                    var pending = profile.Begin("mage", "priest", 31);
                    matchId = pending.Id;
                    typeof(LocalLadder).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, pending);
                    LocalLadder.NativeStarted();
                    NativeSession session = null;
                    var match = new NativeMatch((id, packet) => session.Receive(id, packet));
                    var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true,
                        Player1HeroClass = CardClass.DRUID, History = true, Logging = false,
                        Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                        Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
                    game.StartGame(); game.Player1.BaseMana = 10;
                    var source = Generic.DrawCard(game.Player1, Cards.FromId("EDR_209"));
                    typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
                    session = new NativeSession(match);
                    session.Flush();
                    session.Process(PlayCardTask.Any(game.Player1, source));
                    session.PublishDecision(game.Player1);
                    for (int i = 0; i < stage; i++)
                    {
                        var view = game.GetPendingDecisions().Single();
                        match.SendChoices(match.HumanChoiceId, new List<int> { view.Options[i % 2].OptionId });
                    }
                    int choiceId = match.HumanChoiceId;
                    var answer = game.GetPendingDecisions().Single();
                    // An in-flight scope rejects cancellation. Input must survive an engine failure.
                    using (game.Resolution.Enter(SabberStoneCore.Model.Events.ResolutionKind.EffectResolution))
                    {
                        bool rejected = false;
                        try { match.Concede(); }
                        catch (Exception error) when (error is NotSupportedException || error is InvalidOperationException) { rejected = true; }
                        Check(rejected && match.HumanChoiceId == choiceId && game.Resolution.HasSuspendedEffect,
                            "failed concede preserves plugin prompt");
                    }
                    int cursor = session.Packets.Count;
                    if (botConcedes) session.Process(ConcedeTask.Any(game.Player2));
                    else { LocalLadder.MarkConcede(); match.Concede(); }
                    Check(game.State == State.COMPLETE && game.Player1.PlayState == (botConcedes ? PlayState.WON : PlayState.LOST) &&
                        game.Player2.PlayState == (botConcedes ? PlayState.LOST : PlayState.WON) && match.HumanChoiceId == -1,
                        "native prompt concede completes for either player");
                    Check(!game.Resolution.HasSuspendedEffect && game.GetPendingDecisions().Count == 0, "native cancelled decision removed");
                    Check(session.Packets.Skip(cursor).Any(p => p.Id == 19) &&
                        !session.Packets.Skip(cursor).Any(p => p.Id == 14 || p.Id == 17), "terminal history without more input");
                    Check(!Invariants.Compare(game, session.Client).Any(), "terminal native state matches engine");
                    Check(session.Client.Entities[game.Id].Get(GameTag.STATE) == (int)State.COMPLETE &&
                        session.Client.Entities[game.Player1.Id].Get(GameTag.PLAYSTATE) == (int)(botConcedes ? PlayState.WON : PlayState.LOST) &&
                        session.Client.Entities[game.Player2.Id].Get(GameTag.PLAYSTATE) == (int)(botConcedes ? PlayState.LOST : PlayState.WON),
                        "native history publishes terminal state and winner");
                    Check((long)typeof(NativeMatch).GetField("humanDecisionId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match) == 0 &&
                        (int)typeof(NativeMatch).GetField("humanDecisionController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match) == 0,
                        "accepted concede clears engine input binding");
                    bool stale = false;
                    try { match.SendChoices(choiceId, new List<int> { answer.Options[0].OptionId }); }
                    catch (InvalidOperationException) { stale = true; }
                    Check(stale, "late native answer rejected");
                    LocalLadder.NativeFinished();
                    LocalLadder.Observe(match);
                    LocalLadder.Observe(match);
                    Check(profile.Pending == null && profile.History.Count == 1 && profile.History[0].Result == outcome &&
                        profile.History[0].Gold == 0 && LocalLadder.Screen == "Result" && LocalLadder.RewardSaveError == null,
                        "real ladder observes terminal state and settles once");
                }
                using (var saved = new LocalProfile(path))
                    Check(saved.Pending == null && saved.History.Single().Id == matchId && saved.History[0].Result == outcome,
                        "concede settlement survives restart");
            }
            finally
            {
                LocalLadder.RewardPresentationComplete();
                Directory.Delete(directory, true);
            }
        }
        Console.WriteLine("PASS Cenarius native concede at all three prompts, either player: input preserved on failure, terminal transport, real ladder settlement/restart");
    }
}
