using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Zone = SabberStoneCore.Enums.Zone;
using EngineCard = SabberStoneCore.Model.Card;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Game = SabberStoneCore.Model.Game;
using Minion = SabberStoneCore.Model.Entities.Minion;
using IPlayable = SabberStoneCore.Model.Entities.IPlayable;
using Controller = SabberStoneCore.Model.Entities.Controller;

// Retail 186852 (hsreplay-test-data hslog-tests/186852_hearthstone_access_eoe.power.log L2326-2495, Gigafin):
// appendages are FULL_ENTITYs inside TRIGGER(body, SubOption -1, EffectIndex 0, Target 0) nested in the body's
// PLAY block. ZoneMgr.CheckAndIgnoreServerChangeList drops card zone changes made directly in a predicted PLAY list.
internal static class ColossalAppendageProof
{
    private const string Record = "tests/fixtures/playtest/match-706260b3-magmaw.jsonl";
    private const string RecordedMagmawFingerprint = "622f074660b48dd6739bee5a3cb87108bc9890edceccef8c6ae59bbca5b3e66a";
    private const string RecordedMagmawState = "b3f9ff66aaa0242f635a9a3bbbda2a897636677c3693445dfdc2e222774610f7";
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Colossal appendages " + message);

    internal static void Run()
    {
        DeckPlaytestRecord();
        int bodies = 0;
        foreach (var card in Cards.All.Where(c => c.ColossalAppendageIds != null).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            PlayedFromHand(card, opponent: false);
            PlayedFromHand(card, opponent: true);
            EffectSummoned(card);
            bodies++;
        }
        Check(bodies >= 24, "every declared body is covered: " + bodies);
        MagmawTailDeathRefill();
        Console.WriteLine($"PASS ColossalAppendageProof: match-706260b3 Magmaw, {bodies} bodies x hand play both seats + effect summon, Magmaw refill under a predicted spell; retail TRIGGER shape, predicted PLAY keeps appendages");
    }

    // Owner playtest 2026-10-25 (Steam Deck): Dark Gift Magmaw played onto an empty board showed no tails.
    private static void DeckPlaytestRecord()
    {
        var lines = File.ReadAllLines(Record).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var header = JsonDocument.Parse(lines[0]).RootElement;
        string Text(JsonElement row, string key) => row.GetProperty(key).GetString();
        List<EngineCard> Deck(string side) => header.GetProperty(side).GetProperty("cards").EnumerateArray()
            .Select(c => Cards.FromId(c.GetString())).ToList();
        var h1 = Cards.FromId(Text(header.GetProperty("humanDeck"), "hero"));
        var h2 = Cards.FromId(Text(header.GetProperty("npcDeck"), "hero"));
        var game = new Game(new GameConfig { Player1Name = "Player", Player2Name = Text(header, "npcName"),
            Player1HeroClass = h1.Class, Player1HeroCard = h1, Player1Deck = Deck("humanDeck"),
            Player2HeroClass = h2.Class, Player2HeroCard = h2, Player2Deck = Deck("npcDeck"),
            RandomSeed = header.GetProperty("engineSeed").GetInt32(), FormatType = FormatType.FT_WILD,
            StartPlayer = 1, Shuffle = true, SkipMulligan = false, History = true, EnableCorpses = true, Logging = false,
            ReplayPools = HsSim.PoolSnapshots.ForReplay(Text(header, "poolHash")) });
        game.StartGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush();
        bool played = false;
        foreach (var line in lines.Skip(1))
        {
            var row = JsonDocument.Parse(line).RootElement;
            string type = Text(row, "type");
            if (type is "emote" or "concession" or "result") continue;
            if (type == "main-begin")
            {
                if (game.Step == Step.BEGIN_MULLIGAN) { game.MainBegin(); session.Flush(); }
                continue;
            }
            var owner = row.GetProperty("seat").GetInt32() == 1 ? game.Player1 : game.Player2;
            string action = row.TryGetProperty("action", out var a) ? a.GetString() : null;
            PlayerTask task = type == "mulligan"
                ? ChooseTask.Mulligan(owner, row.GetProperty("keeps").EnumerateArray().Select(p => p.GetInt32()).ToList())
                : action.StartsWith("CHOOSE:", StringComparison.Ordinal)
                    ? ChooseTask.Pick(owner, row.TryGetProperty("choices", out var picks)
                        ? picks.EnumerateArray().Single().GetInt32() : int.Parse(action.Split(':').Last()))
                    : owner.Options().Single(t => BotAction.From(t).ToString() == action);
            if (type != "mulligan") session.PublishDecision(owner);
            if (row.GetProperty("sequence").GetInt32() != 63) { session.Process(task); continue; }
            var magmaw = (Minion)task.Source;
            Check(magmaw.Card.Id == "CATA_550" && magmaw.Id == 100 && owner.BoardZone.Count == 0, "record reaches the Magmaw play");
            int since = session.Packets.Count;
            session.Client.PredictLocalPlay(magmaw.Id, ((PlayCardTask)task).ZonePosition + 1);
            session.Process(task);
            // The record's fingerprint also hashes data the recording build did not have: 0c393973 extended the
            // achievement-history ledger and 55fe372f started writing NUM_TURNS_IN_PLAY (sleep visuals). Pin the recorded
            // game state without those two (the record's hash is that state plus its 2026-10-25 ledger), so gameplay
            // drift still fails while ledger or minion-age tag changes do not. The same goes for the entity-reference tag
            // (PayloadRules.ReferenceEntity, 2026-11-05) that grants such as Devoured Soul now carry for their client text.
            Check(Text(row, "fingerprint") == RecordedMagmawFingerprint && StateFingerprint(game) == RecordedMagmawState,
                "record replays to the recorded Magmaw state");
            var tails = Appendages(magmaw);
            Check(tails.Length == 6 && owner.BoardZone.IsFull, "engine summons six tails onto the full board");
            Entered(session, since, magmaw, tails, predicted: true, "match-706260b3 Magmaw");
            played = true;
            break;
        }
        Check(played, "record contains the Magmaw play");
    }

    // MatchRecorder.Fingerprint without its "|history:" ledger section, NUM_TURNS_IN_PLAY and the entity-reference tag: tags, zones and choices.
    private static string StateFingerprint(Game game)
    {
        static string Tags(IDictionary<GameTag, int> tags) => string.Join(",", tags.Where(t => t.Key != GameTag.NUM_TURNS_IN_PLAY && t.Key != SabberStoneCore.Actions.PayloadRules.ReferenceEntity).OrderBy(t => (int)t.Key)
            .Select(t => ((int)t.Key).ToString(System.Globalization.CultureInfo.InvariantCulture) + "=" + t.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        static string N(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var text = new System.Text.StringBuilder("game:").Append(Tags(game.NativeTags));
        foreach (var entity in game.IdEntityDic.Values.OrderBy(e => e.Id))
            text.Append(';').Append(N(entity.Id)).Append(':').Append(entity.Card?.Id).Append(':').Append(Tags(entity.NativeTags));
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            text.Append("|player:").Append(N(player.PlayerId)).Append(':').Append(Tags(player.NativeTags));
            foreach (var zone in new IEnumerable<IPlayable>[] { player.HandZone, player.DeckZone, player.BoardZone, player.SecretZone, player.GraveyardZone })
                text.Append('/').Append(string.Join(",", zone.Select(c => N(c.Id))));
            if (player.Choice != null)
                text.Append("|choice:").Append(N((int)player.Choice.ChoiceType)).Append(':').Append(string.Join(",", player.Choice.Choices.Select(N)));
        }
        return global::MatchRecorder.Hash(text.ToString());
    }

    private static void PlayedFromHand(EngineCard card, bool opponent)
    {
        string label = card.Id + (opponent ? " bot" : " human");
        IPlayable body = null;
        var (game, session) = Fixture(opponent ? 2 : 1, p =>
        {
            // One minion on the left makes left-side appendages shift the body inside the trigger.
            Generic.SummonBlock(p.Game, (Minion)Entity.FromCard(p, Cards.FromId("CS2_231")), -1, p.Hero);
            body = Generic.DrawCard(p, card);
        });
        var player = game.CurrentPlayer;
        session.PublishDecision(player);
        var play = player.Options().OfType<PlayCardTask>().FirstOrDefault(t => t.Source == body && t.ZonePosition == 1);
        Check(play != null, label + " playable from hand to the right of a minion");
        int since = session.Packets.Count;
        if (!opponent) session.Client.PredictLocalPlay(body.Id, play.ZonePosition + 1);
        session.Process(play);
        var minion = (Minion)body;
        var appendages = Appendages(minion);
        Check(appendages.Length > 0, label + " summons at least one appendage");
        Entered(session, since, minion, appendages, predicted: !opponent, label);
    }

    private static (Game Game, NativeSession Session) Fixture(int startPlayer, Action<Controller> setup)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = startPlayer, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        game.CurrentPlayer.BaseMana = 10;
        setup(game.CurrentPlayer);
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush();
        return (game, session);
    }

    // Lean: retail effect summons of a body are untraced; the same body TRIGGER nests under the summoning POWER.
    private static void EffectSummoned(EngineCard card)
    {
        var (game, session) = Fixture(1, _ => { });
        var player = game.CurrentPlayer;
        int since = session.Packets.Count;
        var body = (Minion)Entity.FromCard(player, card);
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, player.Hero.Id, "", -1, 0));
        Generic.SummonBlock(game, body, -1, player.Hero);
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
        game.DeathProcessingAndAuraUpdate();
        session.Flush();
        var appendages = Appendages(body);
        Check(appendages.Length > 0, card.Id + " effect summon creates appendages");
        Entered(session, since, body, appendages, predicted: false, card.Id + " effect summon", HistoryBlock.Type.POWER, player.Hero.Id);
    }

    // Lean: retail refills are untraced. A tail killed by the human's predicted spell refills in its own body TRIGGER.
    private static void MagmawTailDeathRefill()
    {
        IPlayable magmaw = null, moonfire = null;
        var (game, session) = Fixture(1, p =>
        {
            Generic.SummonBlock(p.Game, (Minion)Entity.FromCard(p, Cards.FromId("CS2_231")), -1, p.Hero);
            magmaw = Generic.DrawCard(p, Cards.FromId("CATA_550"));
            moonfire = Generic.DrawCard(p, Cards.FromId("CS2_008"));
        });
        var player = game.CurrentPlayer;
        session.PublishDecision(player);
        var play = player.Options().OfType<PlayCardTask>().First(t => t.Source == magmaw && t.ZonePosition == 1);
        session.Client.PredictLocalPlay(magmaw.Id, play.ZonePosition + 1);
        session.Process(play);
        var body = (Minion)magmaw;
        var before = Appendages(body).Select(m => m.Id).ToHashSet();
        Check(before.Count == 5 && player.BoardZone.IsFull, "Magmaw fills the board beside one minion");
        player.UsedMana = 0;
        session.PublishDecision(player);
        var victim = Appendages(body).First();
        var cast = player.Options().OfType<PlayCardTask>().Single(t => t.Source == moonfire && t.ActionTarget == victim);
        int since = session.Packets.Count;
        session.Client.PredictLocalSpellPlay(moonfire.Id);
        session.Process(cast);
        var refilled = Appendages(body).Where(m => !before.Contains(m.Id)).ToArray();
        Check(victim.Zone != player.BoardZone && refilled.Length == 1 && player.BoardZone.IsFull, "tail death refills one tail");
        Check(!session.Client.HasUnresolvedLocalChange, "Moonfire local PLAY prediction acknowledged");
        var chain = Creations(session.Packets.Skip(since)).Single(c => c.Entity == refilled[0].Id).Chain;
        Check(chain.Length > 0 && chain[^1].Type == HistoryBlock.Type.TRIGGER && chain[^1].Source == body.Id
            && chain[^1].SubOption == -1 && chain[^1].EffectIndex == 0, "refill is inside TRIGGER(body): " + Describe(chain.LastOrDefault()));
        Entered(session, since, body, refilled, predicted: true, "Magmaw refill", chain.Length > 1 ? chain[^2].Type : HistoryBlock.Type.PLAY,
            chain.Length > 1 ? chain[^2].Source : body.Id);
        Console.WriteLine("PASS Magmaw refill chain: " + string.Join(" > ", chain.Select(Describe)));
    }

    private static Minion[] Appendages(Minion body) => body.Controller.BoardZone
        .Where(m => m[GameTag.DISPLAYED_CREATOR] == body.Id && body.Card.ColossalAppendageIds.Contains(m.Card.Id)).ToArray();

    private static void Entered(NativeSession session, int since, Minion body, Minion[] appendages, bool predicted, string label,
        HistoryBlock.Type parentType = HistoryBlock.Type.PLAY, int? parentSource = null)
    {
        var starts = History(session.Packets.Skip(since)).Count(e => e.HasPowerStart);
        Check(starts == History(session.Packets.Skip(since)).Count(e => e.HasPowerEnd), label + " balanced blocks");
        if (predicted) Check(!session.Client.HasUnresolvedLocalChange, label + " local PLAY prediction acknowledged");
        var created = Creations(session.Packets.Skip(since)).Where(c => appendages.Any(m => m.Id == c.Entity)).ToArray();
        Check(created.Select(c => c.Entity).Distinct().Count() == appendages.Length && created.Length == appendages.Length,
            label + " one FULL_ENTITY per appendage");
        foreach (var (id, chain) in created)
        {
            Check(!session.Client.IgnoredCardZoneChanges.Contains(id), $"{label} appendage {id} survives predicted PLAY suppression");
            Check(session.Client.Entities[id].CardZone == (int)Zone.PLAY, $"{label} appendage {id} card is in PLAY");
            var inner = chain.LastOrDefault();
            var parent = chain.Length > 1 ? chain[^2] : null;
            Check(inner != null && inner.Type == HistoryBlock.Type.TRIGGER && inner.Source == body.Id && inner.SubOption == -1
                && inner.EffectIndex == 0 && inner.Target == 0 && inner.TriggerKeyword == 0,
                $"{label} appendage {id} is inside TRIGGER(body, -1, 0, 0): {Describe(inner)}");
            Check(parent != null && parent.Type == parentType && parent.Source == (parentSource ?? body.Id),
                $"{label} appendage {id} trigger is nested in {parentType}: {Describe(parent)}");
        }
        var violations = Invariants.Compare(body.Game, session.Client).ToArray();
        Check(violations.Length == 0, label + " ClientModel parity: " + string.Join("; ", violations.Take(5)));
    }

    private static string Describe(PowerHistoryStart start) => start == null ? "none"
        : $"{start.Type} source={start.Source} sub={start.SubOption} effect={start.EffectIndex} target={start.Target} keyword={start.TriggerKeyword}";

    private static IEnumerable<PowerHistoryData> History(IEnumerable<WirePacket> packets)
    {
        foreach (var packet in packets.Where(p => p.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(packet.Base64));
            var history = new PegasusGame.PowerHistory();
            history.Deserialize(stream);
            foreach (var entry in history.List) yield return entry;
        }
    }

    // Every FULL_ENTITY with its open block chain, innermost last.
    private static List<(int Entity, PowerHistoryStart[] Chain)> Creations(IEnumerable<WirePacket> packets)
    {
        var result = new List<(int, PowerHistoryStart[])>();
        var open = new List<PowerHistoryStart>();
        foreach (var entry in History(packets))
        {
            if (entry.HasPowerStart) open.Add(entry.PowerStart);
            else if (entry.HasPowerEnd && open.Count > 0) open.RemoveAt(open.Count - 1);
            else if (entry.HasFullEntity) result.Add((entry.FullEntity.Entity, open.ToArray()));
        }
        return result;
    }
}
