using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void PublishHandChoice(NativeMatch match) => typeof(NativeMatch)
        .GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);

    // Playtest: Fast Forward's "pick a drawn card" choice drew its arrow but clicks on hand cards did nothing until the turn
    // timer ran out. The native GameState caches HasHandTarget when the choice is registered, before the drawn cards' actors
    // reach the hand zone, so InputManager dropped the clicks. The fix re-checks that predicate for the active local TARGET
    // choice. `journal` optionally replays a recorded game; that journal is not shipped in the repo.
    private static void HandTargetChoiceHistory(string journal = null)
    {
        PinnedTargetInputContract();
        // All hand-selector implementations (including captured draws, filtered and mixed hand/board).
        string[] sources = { "TIME_770", "JAIL_206", "CATA_200", "CATA_490", "CATA_566", "CATA_721",
            "CATA_897", "CATA_697", "CATA_979", "CATA_563", "CATA_209", "CATA_161", "BE_036",
            "BE_566", "JAIL_313", "EDR_518", "CATA_EVENT_001", "JAIL_887", "CATA_477" };
        foreach (string sourceId in sources)
        foreach (bool npc in new[] { false, true })
        foreach (int handSize in sourceId == "TIME_770" || sourceId == "JAIL_206" ? new[] { 1, 9, 10 } : new[] { 4 })
        {
            var (match, capture) = AnimationMatch(botTurn: npc);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
            for (int i = 1; i < handSize; i++)
                EngineEntity.FromCard(owner, Cards.FromId(i == 1 ? "CS2_029" : "CS2_182"), zone: owner.HandZone);
            // Malevolent Mutant needs a Fel spell; deck order deliberately includes a second Fast Forward.
            if (sourceId == "CATA_697") EngineEntity.FromCard(owner, Cards.FromId("CORE_BT_801"), zone: owner.HandZone);
            foreach (string id in new[] { "CS2_182", "TIME_770", "TIME_711" })
                EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(sourceId), zone: owner.HandZone);
            if (sourceId == "CATA_200") source[(GameTag)1508] = 3;
            FlushFieldHistory(match);
            int before = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            if (sourceId == "JAIL_887" || sourceId == "CATA_477")
            {
                source[GameTag.EXHAUSTED] = 0;
                FlushFieldHistory(match);
                InvokeProcess(match, owner.Options().First(t => t.Source?.Id == source.Id));
                FlushFieldHistory(match);
            }
            Check(owner.Choice?.ChoiceType == ChoiceType.TARGET, sourceId + " opens real hand TARGET");
            if (sourceId == "TIME_770" || sourceId == "JAIL_206")
            {
                int count = Math.Min(sourceId == "TIME_770" ? 2 : 3, 11 - handSize);
                Check(owner.Choice.Choices.Count == count && owner.Choice.Choices.All(id => game.IdEntityDic[id].Zone == owner.HandZone),
                    "captured draw offers exclude burned cards: " + sourceId + " hand=" + handSize);
                Check(source.Zone == null && source[(GameTag)1508] == 0, "suspended spell tag stays presentation-only");
            }
            int pick = owner.Choice.Choices.First(id => game.IdEntityDic[id].Zone == owner.HandZone);
            if (npc)
            {
                Check(!capture.Packets.Skip(before).Any(p => p.Id == 17), "NPC TARGET stays private: " + sourceId);
                InvokeProcess(match, ChooseTask.Pick(owner, pick)); FlushFieldHistory(match);
                Check(owner.Choice == null, "NPC selector resolves without client input: " + sourceId);
            }
            else
            {
                bool retiredSpell = sourceId == "TIME_770" && handSize == 1;
                if (retiredSpell)
                {
                    // Client can retire the source before a retained recipe ends; preserve the engine source.
                    var gone = new PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange {
                        Entity = source.Id, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME } };
                    typeof(NativeMatch).GetMethod("TrackVisibility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { gone });
                    typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match,
                        new object[] { new PowerHistory { List = new List<PowerHistoryData> { gone } } });
                }
                PublishHandChoice(match);
                if (retiredSpell) Check(capture.Last<EntityChoices>(17).Source == owner.Hero.Id, "retired spell uses public hero arrow/capability");
                AssertHandChoiceClick(match, capture, pick, delayed: sourceId == "TIME_770" || sourceId == "JAIL_206");
                Check(owner.Choice == null, "hand click resumes real continuation: " + sourceId);
                Check(ChoiceWireTag(capture, source.Id, 1508) == source[(GameTag)1508], "temporary capability restored: " + sourceId);
            }
            capture.SerializeAll();
        }
        if (journal != null) ReplayHandChoice(journal);
        Console.WriteLine("PASS hand TARGET: 19 sources, human/NPC, full hand/burn, delayed draw actors, native refresh and real continuation");
    }

    private static void AssertHandChoiceClick(NativeMatch match, Capture capture, int pick, bool delayed)
    {
        var packet = capture.Last<EntityChoices>(17);
        Check(packet.ChoiceType == (int)ChoiceType.TARGET && packet.CountMin == 1 && packet.CountMax == 1
            && packet.Entities.SequenceEqual(match.Game.Player1.Choice.Choices), "TARGET packet preserves exact engine membership/counts");
        Check(ChoiceWireTag(capture, packet.Source, 1508) > 0, "actual packet source has hand capability");
        var wire = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
        Check(!string.IsNullOrEmpty(wire[packet.Source].Card), "arrow source has public card identity");
        foreach (int id in packet.Entities.Where(id => match.Game.IdEntityDic[id].Zone == match.Game.Player1.HandZone))
            Check(wire[id].Card == match.Game.IdEntityDic[id].Card.Id && wire[id].Zone == 3
                && ChoiceWireTag(capture, id, (int)GameTag.ZONE_POSITION) > 0, "offers revealed in positioned hand on wire");
        var client = new HandChoiceClient(match, capture, delayed);
        Check(!delayed || !client.Click(pick, refresh: false), "old cache rejects click while draw history is pending");
        Check(!delayed || !client.Click(pick, refresh: true), "refresh waits for actual hand actor zone");
        client.CompleteHistory();
        client.CheckScope();
        Check(!delayed || !client.Click(pick, refresh: false), "old cache still rejects after actors arrive in hand");
        Check(client.Click(pick, refresh: true) && client.Sends == 1, "production refresh enables exactly one native choice send after hand arrival");
        Check(!client.Click(pick, refresh: true) && client.Sends == 1, "answered choice cannot send twice");
    }

    // Native methods are pinned by PinnedTargetInputContract; only Unity's animated zone boundary is doubled.
    private partial class HandChoiceClient
    {
        private enum CHOICE_TYPE { TARGET = 3 }
        private sealed class Choice { internal int ID; internal CHOICE_TYPE ChoiceType; internal int Source; }
        private sealed class NativeEntity { internal Func<bool> Ready; internal bool HasHandTarget() => Ready(); }
        private sealed class GameState
        {
            internal Choice Choice; internal NativeEntity Source; internal bool InChoice = true;
            internal Choice GetFriendlyEntityChoices() => Choice;
            internal bool IsInChoiceMode() => InChoice;
            internal NativeEntity GetEntity(int id) => Source;
        }
        private static bool OwnsLiveMatch = true;
        private static NativeMatch match;
        private readonly GameState front;
        private readonly EntityChoices packet;
        private readonly Capture capture;
        private bool animatedHand, cached;
        internal int Sends;
        internal HandChoiceClient(NativeMatch current, Capture capture, bool delayed)
        {
            match = current; this.capture = capture; packet = capture.Last<EntityChoices>(17);
            animatedHand = !delayed;
            front = new GameState { Choice = new Choice { ID = packet.Id, ChoiceType = CHOICE_TYPE.TARGET, Source = packet.Source },
                Source = new NativeEntity { Ready = () => animatedHand && ChoiceWireTag(capture, packet.Source, 1508) > 0 } };
            cached = front.Source.HasHandTarget();
        }
        internal void CompleteHistory() => animatedHand = true;
        internal void CheckScope()
        {
            bool Read() { bool result = false; RefreshHandChoiceTargeting(front, ref result); return result; }
            OwnsLiveMatch = false; Check(!Read(), "refresh refuses non-local match"); OwnsLiveMatch = true;
            front.Choice.ID++; Check(!Read(), "refresh refuses stale native choice"); front.Choice.ID--;
            front.Choice.ChoiceType = 0; Check(!Read(), "refresh leaves GENERAL alone"); front.Choice.ChoiceType = CHOICE_TYPE.TARGET;
            front.InChoice = false; Check(!Read(), "refresh refuses ordinary option mode"); front.InChoice = true;
            Check(Read(), "refresh rechecks source capability after animated hand arrival");
        }
        internal bool Click(int id, bool refresh)
        {
            bool capability = cached;
            if (refresh) RefreshHandChoiceTargeting(front, ref capability); // Extracted production postfix, not a copy.
            if (!front.InChoice || !capability || !animatedHand || !packet.Entities.Contains(id)) return false;
            Sends++; match.SendChoices(packet.Id, new List<int> { id }); front.InChoice = false; return true;
        }
    }

    private static void ReplayHandChoice(string journal)
    {
        var rows = File.ReadLines(journal).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => JsonDocument.Parse(s)).ToArray();
        var oldDecks = LocalDecks.ExternalDecks;
        try
        {
            var header = rows[0].RootElement;
            LocalDeck Deck(string key) { var d = header.GetProperty(key); return new LocalDeck("Replay", d.GetProperty("hero").GetString(),
                d.GetProperty("cards").EnumerateArray().Select(c => c.GetString()).ToArray()).WithSideboards(
                    d.GetProperty("sideboards").EnumerateObject().Select(p => new LocalSideboard { Source = p.Name,
                        Cards = p.Value.EnumerateArray().Select(c => c.GetString()).ToList() })); }
            var human = Deck("humanDeck"); var npc = Deck("npcDeck");
            LocalDecks.ExternalDecks = id => id == "hand-choice-replay" ? human : oldDecks?.Invoke(id);
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "hand-choice-replay", "warrior", header.GetProperty("engineSeed").GetInt32()) {
                StartingPlayer = header.GetProperty("firstPlayer").GetInt32(), SynchronousNpc = false };
            typeof(NativeMatch).GetField("opponentDeck", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, npc);
            match.Start();
            try
            {
                foreach (var document in rows.Skip(1))
                {
                    var row = document.RootElement;
                    if (!row.TryGetProperty("sequence", out var seq)) continue;
                    var g = match.Game; string type = row.GetProperty("type").GetString();
                    if (seq.GetInt32() == 1)
                    {
                        Check(MatchRecorder.Fingerprint(g) == row.GetProperty("fingerprint").GetString(), "external journal NPC mulligan matches");
                        continue;
                    }
                    if (type != "main-begin" && g.Step == Step.BEGIN_MULLIGAN
                        && g.Player1.MulliganState == Mulligan.DONE && g.Player2.MulliganState == Mulligan.DONE) g.MainBegin();
                    var owner = (!row.TryGetProperty("seat", out var seat) || seat.GetInt32() == 1) ? g.Player1 : g.Player2;
                    string action = row.TryGetProperty("action", out var recordedAction) ? recordedAction.GetString() : "";
                    if (type == "main-begin") g.MainBegin();
                    else if (action.StartsWith("CHOOSE:"))
                    {
                        var picks = type == "mulligan" ? row.GetProperty("keeps").EnumerateArray().Select(x => x.GetInt32()).ToList()
                            : action.Split(':').Last().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                        InvokeProcess(match, type == "mulligan" ? ChooseTask.Mulligan(owner, picks) : ChooseTask.Pick(owner, picks[0]));
                    }
                    else InvokeProcess(match, owner.Options().Single(t => BotAction.From(t).ToString() == action));
                    FlushFieldHistory(match);
                    Check(MatchRecorder.Fingerprint(g) == row.GetProperty("fingerprint").GetString(), "external replay fingerprint at " + seq);
                    Check(MatchRecorder.RandomState(g) == row.GetProperty("rngState").GetString(), "external replay RNG at " + seq);
                    if (g.Player1.Choice?.ChoiceType == ChoiceType.TARGET && g.IdEntityDic[g.Player1.Choice.SourceId].Card.Id == "TIME_770")
                    {
                        PublishHandChoice(match);
                        Check(g.Player1.HandZone.Count == 10 && g.Player1.Choice.Choices.Count == 2, "owner full hand contains two surviving draws; no burn");
                        AssertHandChoiceClick(match, capture, g.Player1.Choice.Choices[0], delayed: true);
                        Console.WriteLine("PASS external owner journal reconstructed through sequence " + seq + "; hand=10 offers=2; click resumes");
                        return;
                    }
                }
                throw new Exception("Journal contains no Fast Forward hand TARGET");
            }
            finally { match.StopNpc(); }
        }
        finally { LocalDecks.ExternalDecks = oldDecks; foreach (var row in rows) row.Dispose(); }
    }
}
