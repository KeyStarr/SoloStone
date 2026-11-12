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
using Tag = PegasusGame.Tag;
using Spell = SabberStoneCore.Model.Entities.Spell;

// Summoned/Casts When Drawn leave the drawer's hand through SETASIDE. The native client binds a card's actor when it
// processes the zone move, so every such card must carry its identity before it reaches PLAY/GRAVEYARD, from both
// seats, with room, a full board and a full hand.
internal static class DrawRevealProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "draw reveal " + message);

    internal static void Run()
    {
        ModelSelfCheck();
        RewindDrawEntries();
        BoardTransitRegressions();
        DiscoverBoardEntries();
        ParkedPrivacy();
        // Every implemented card with a when-drawn task: the general draw path, not a card list.
        var drawn = Cards.All.Where(c => c.Implemented && c.Power?.TopdeckTask != null).Select(c => c.Id)
            .Order(StringComparer.Ordinal).ToArray();
        foreach (string required in new[] { "EDR_260t", "TLC_513t2", "TIME_025t", "JAIL_386t", "JAIL_443t", "JAIL_879t", "JAIL_881t", "EDR_445pt3" })
            Check(drawn.Contains(required), "driver covers " + required);
        int cases = 0, revealed = 0;
        var failures = new List<string>();
        void Expect(bool value, string message) { if (!value) failures.Add(message); }
        foreach (string card in drawn)
        foreach (int drawer in new[] { 1, 2 })
        foreach (string mode in new[] { "room", "full-board", "full-hand" })
        {
            var (game, session, traces) = Start(drawer);
            var owner = game.CurrentPlayer;
            if (mode == "full-board")
                for (int i = 0; i < 7; i++) Generic.SummonBlock(game, (Minion)Entity.FromCard(owner, Cards.FromId("CS2_231")), -1, null);
            if (mode == "full-hand")
                while (owner.HandZone.Count < 10) Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var top = Entity.FromCard(owner, Cards.FromId(card), zone: owner.DeckZone);
            Flush(game, session);
            string name = $"{card}-{mode}-p{drawer}";
            try { Generic.Draw(owner, top); }
            catch (Exception error) { throw new InvalidOperationException("draw reveal fixture failed: " + name, error); }
            game.DeathProcessingAndAuraUpdate();
            Flush(game, session);
            var violations = Invariants.ActorIdentity(game, session.Client).ToArray();
            Expect(violations.Length == 0, name + ": " + string.Join("; ", violations.Select(v => v.ToString())));
            var shown = session.Client.Entities[top.Id];
            var zone = top.Zone?.Type ?? Zone.INVALID;
            bool burned = mode == "full-hand";
            if (zone is Zone.PLAY or Zone.GRAVEYARD || burned)
                Expect(shown.CardId == top.Card.Id && shown.ActorCardId == top.Card.Id, name + ": public card bound by identity, shown="
                    + shown.CardId + " actor=" + shown.ActorCardId + " zone=" + zone);
            // Casts/Summoned When Drawn resolve face up for both players (retail reveals the drawn card).
            if (!burned && top.Controller == game.Player2 && zone != Zone.HAND)
            {
                Expect(shown.CardId == top.Card.Id, name + ": opponent when-drawn card revealed, shown='" + shown.CardId + "' zone=" + zone);
                revealed++;
            }
            if (zone == Zone.HAND && top.Controller == game.Player2)
                Expect(shown.CardId == "", name + ": a when-drawn card still held stays private");
            Expect(!traces.Any(t => t.StartsWith("ANON_PUBLIC_MOVE", StringComparison.Ordinal)), name + ": " + string.Join("; ", traces));
            Expect(!Invariants.Compare(game, session.Client).Any(v => v.Tag is "CARD_ID" or "RESET_PRIVACY"), name + " identity parity");
            cases++;
        }
        Check(failures.Count == 0, failures.Count + " failures:\n" + string.Join("\n", failures));
        Check(revealed > 0, "opponent draws exercised");
        HiddenRetirements();
        Console.WriteLine($"PASS draw reveal transport: {drawn.Length} when-drawn cards x both seats x room/full board/full hand = {cases} cases; opponent reveals={revealed}; no nameless public actor");
    }

    private static void RewindDrawEntries()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var (game, session, traces) = Start(seat, "CS2_029");
            var owner = game.CurrentPlayer;
            owner.BaseMana = 10;
            var source = Generic.DrawCard(owner, Cards.FromId("TIME_003"));
            var body = Entity.FromCard(owner, Cards.FromId("EDR_260t"), zone: owner.DeckZone);
            Flush(game, session);
            session.PublishDecision(owner);
            session.Process(PlayCardTask.Any(owner, source));
            Check(game.Rewind != null && body.Zone == owner.BoardZone, "Vanguard drew and summoned Illusion before offer");
            game.PowerHistory.Last.Clear();
            session.PublishDecision(owner);
            session.Process(ChooseTask.Pick(owner, game.Rewind.RewindEntityId));
            Check(session.Client.HasRewound && game.IdEntityDic[body.Id].Zone.Type == Zone.PLAY,
                "Vanguard replay summons the restored Illusion");
            Check(BoardActorInvariant.Inspect(session.Client).Count() == 0, "board remains shown after reset/replay draw");
        }
        Console.WriteLine("PASS Rewind board transit: Portal Vanguard draws Illusion after GAME_RESET, both seats");
    }

    private static void BoardTransitRegressions()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string id in new[] { "CS2_182", "CORE_REV_990" })
        foreach (Zone origin in new[] { Zone.HAND, Zone.DECK, Zone.GRAVEYARD, Zone.SETASIDE, Zone.PLAY })
        foreach (bool splitPacket in new[] { false, true })
        foreach (int deckCount in splitPacket ? new[] { 0, 1 } : new[] { -1 })
        foreach (bool transform in id == "CS2_182" ? new[] { false, true } : new[] { false })
        {
            var (game, session, traces) = Start(seat);
            var owner = game.CurrentPlayer;
            if (deckCount >= 0)
                while (owner.DeckZone.Count > deckCount)
                    owner.SetasideZone.Add(owner.DeckZone.Remove(owner.DeckZone[0]));
            var zone = origin == Zone.HAND ? (SabberStoneCore.Model.Zones.IZone)owner.HandZone
                : origin == Zone.DECK ? owner.DeckZone : origin == Zone.GRAVEYARD ? owner.GraveyardZone
                : origin == Zone.PLAY ? owner.BoardZone : owner.SetasideZone;
            var body = Entity.FromCard(owner, Cards.FromId(id), zone: zone);
            Flush(game, session);
            if (origin != Zone.SETASIDE) owner.SetasideZone.Add(body.Zone.Remove(body));
            if (splitPacket)
            {
                Flush(game, session);
                Check(session.Client.Entities[body.Id].Get(GameTag.ZONE) == (int)Zone.SETASIDE,
                    "parking packet retains the actual SETASIDE zone");
                if (seat == 2 && origin is Zone.HAND or Zone.DECK)
                    Check(session.Client.Entities[body.Id].CardId == "", "parked opponent identity stays private");
                // An unrelated packet must not lose the parked actor's history.
                owner.BaseMana = 9;
                Flush(game, session);
            }
            var before = session.Client.Entities[body.Id];
            int deckEntries = before.DeckEntries, deckSummons = before.DeckSummons;
            var counts = new[] { 1, 2 }.Select(seatId => session.Client.Entities.Values.Count(e =>
                e.CardZone == (int)Zone.DECK && e.CardController == seatId)).ToArray();
            if (splitPacket) session.Client.EntryApplied = client => {
                foreach (int seatId in new[] { 1, 2 })
                    Check(client.Entities.Values.Count(e => e.CardZone == (int)Zone.DECK && e.CardController == seatId) == counts[seatId - 1],
                        "arrival never changes deck count/fatigue, including between entries");
            };
            if (transform) body = Generic.ChangeEntityBlock(owner, body, Cards.FromId("CS2_231"), false);
            owner.SetasideZone.Remove(body);
            if (body is Minion minion) Generic.SummonBlock(game, minion, -1, null);
            else owner.BoardZone.Add((BoardObject)body);
            try { Flush(game, session); }
            catch (Exception error) { throw new InvalidOperationException($"board transit {id} p{seat} from {origin} transform={transform} split={splitPacket}; " + string.Join("; ", traces), error); }
            var shown = session.Client.Entities[body.Id];
            Check(shown.CardId == body.Card.Id && shown.ActorCardId == body.Card.Id && !shown.HiddenByNullZone,
                $"board transit {id} p{seat} from {origin} transform={transform} split={splitPacket}");
            session.Client.EntryApplied = null;
            if (splitPacket)
                Check(shown.DeckEntries == deckEntries && shown.DeckSummons == deckSummons,
                    "null-to-PLAY never enters deck or selects native deck summon animation");
            else if (origin is Zone.HAND or Zone.DECK or Zone.GRAVEYARD)
                Check(traces.Any(t => t.StartsWith("BOARD_TRANSIT")), "same-packet transport repair retained");
            if (splitPacket && origin is Zone.HAND or Zone.DECK or Zone.GRAVEYARD)
            {
                var packets = session.Packets.Where(p => p.Id == 19).Select(p => {
                    var history = new NativeHistory();
                    using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
                    history.Deserialize(stream);
                    return history;
                }).ToArray();
                Check(shown.VisibilityRepairs > 0, "hidden different actor uses native visibility repair");
                Check(BoardActorInvariant.Validate(packets, repairNullToPlayActors: false).Any(f => f.Contains("hidden board actor")),
                    $"cross-packet visibility oracle catches disabled native hook p{seat} {id}");
            }
        }
        Console.WriteLine("PASS board transit: minion/location, both seats, hand/deck/graveyard/fresh setaside, same/cross packet");
    }

    private static void DiscoverBoardEntries()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string card in new[] { "CORE_AV_107", "DINO_424", "EDR_455" })
        {
            var (game, session, traces) = Start(seat);
            var owner = game.CurrentPlayer;
            owner.BaseMana = 10;
            var source = Generic.DrawCard(owner, Cards.FromId(card));
            if (card == "EDR_455")
                foreach (string dragonId in new[] { "EX1_043", "EX1_561" })
                {
                    var dragon = (Minion)Entity.FromCard(owner, Cards.FromId(dragonId));
                    Generic.SummonBlock(game, dragon, -1, null);
                    dragon.Destroy();
                    game.DeathProcessingAndAuraUpdate();
                }
            Flush(game, session);
            session.Process(PlayCardTask.Any(owner, source));
            game.PowerHistory.Last.Clear();
            var offers = owner.Choice.Choices.ToArray();
            Check(offers.All(id => game.IdEntityDic[id].Zone == owner.SetasideZone), card + " offers parked before answer");
            if (seat == 2) Check(offers.All(id => session.Client.Entities[id].CardId == ""), card + " opponent offers private");
            session.PublishDecision(owner);
            session.Process(ChooseTask.Pick(owner, offers[0]));
            Check(game.IdEntityDic[offers[0]].Zone == owner.BoardZone, card + " picked entity summoned");
            Check(BoardActorInvariant.Inspect(session.Client).Count() == 0, card + " visible after answer");
            Check(session.Client.Entities[offers[0]].DeckEntries == 0 && session.Client.Entities[offers[0]].DeckSummons == 0,
                card + " Discover summon never visits deck");
            if (seat == 2) Check(offers.Skip(1).All(id => session.Client.Entities[id].CardId == ""), card + " rejected offers remain private");
        }
        Console.WriteLine("PASS cross-packet Discover summons: Glaciate/Hero's Welcome/Succumb to Madness, both seats, rejected-offer privacy");
    }

    // Even malformed public moves cannot turn stored payloads or private choices into a disclosure.
    private static void ParkedPrivacy()
    {
        foreach (var marker in new[] { PayloadRules.Stored, Generic.PrivateDecisionOfferTag })
        foreach (string id in new[] { "CS2_182", "CORE_REV_990" })
        {
            var game = new Game(new GameConfig { StartPlayer = 1, SkipMulligan = true, History = true,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            var packets = new List<NativeHistory>();
            var match = new NativeMatch((number, packet) => { if (packet is NativeHistory history) packets.Add(history); });
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Send()
            {
                typeof(NativeMatch).GetMethod("SendHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(match, new object[] { game.PowerHistory.Last });
                game.PowerHistory.Last.Clear();
            }
            var body = Entity.FromCard(game.Player2, Cards.FromId(id), new EntityData { [marker] = 2 }, game.Player2.HandZone);
            Send();
            game.Player2.SetasideZone.Add(body.Zone.Remove(body));
            Send();
            game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(body.Id, GameTag.ZONE, (int)Zone.PLAY));
            Send();
            Check(!packets.SelectMany(p => p.List).Select(e => e.FullEntity ?? e.ShowEntity ?? e.ChangeEntity)
                .Any(e => e?.Entity == body.Id && e.Name.Length > 0), $"parked {marker} {id} never named by board recovery");
        }
        Console.WriteLine("PASS cross-packet board privacy: stored minion/location and private decision offers stay anonymous");
    }

    // Negative privacy: the normalizer never names a card the human does not see in retail. Secrets destroyed from the
    // opponent's SECRET zone (Eater of Secrets) retire nameless; rejected/destroyed deck offers are DiscoverRealProof's
    // "retired offers retain privacy" (Commander Geddon, RejectedOfferAction.Destroy).
    private static void HiddenRetirements()
    {
        var (game, session, traces) = Start(1);
        var human = game.Player1; var bot = game.Player2;
        human.BaseMana = 10; human.UsedMana = 0;
        var secret = (Spell)Entity.FromCard(bot, Cards.FromId("EX1_287"));
        Generic.CastSpell(bot, game, secret, null, 0);
        Flush(game, session);
        Check(secret.Zone == bot.SecretZone && session.Client.Entities[secret.Id].CardId == "", "fixture: hidden opponent Secret");
        var eater = Generic.DrawCard(human, Cards.FromId("OG_254"));
        Flush(game, session);
        session.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, eater));
        Check(secret.Zone == bot.GraveyardZone, "Eater of Secrets destroyed the Secret");
        Check(session.Client.Entities[secret.Id].CardId == "", "a destroyed opponent Secret is not named: " + session.Client.Entities[secret.Id].CardId);
        Check(!traces.Any(t => t.StartsWith("ANON_PUBLIC_MOVE", StringComparison.Ordinal)), "hidden retirement is not logged as a defect: " + string.Join("; ", traces));
    }

    private static (Game, NativeSession, List<string>) Start(int drawer, string deckCard = "CS2_182")
    {
        var game = new Game(new GameConfig { RandomSeed = 11, StartPlayer = drawer, SkipMulligan = true, Shuffle = false,
            History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId(deckCard), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId(deckCard), 30).ToList() });
        game.StartGame();
        NativeSession session = null;
        var traces = new List<string>();
        var match = new NativeMatch((id, packet) => session.Receive(id, packet)) { BotTrace = traces.Add };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        Flush(game, session);
        return (game, session, traces);
    }

    private static void Flush(Game game, NativeSession session)
    {
        session.Flush();
        game.PowerHistory.Last.Clear();
    }

    // The oracle itself: SHOW can update actor identity without repairing null-zone visibility.
    private static void ModelSelfCheck()
    {
        NativeHistory Packet(params PowerHistoryData[] entries) => new() { List = entries.ToList() };
        PowerHistoryData Move(int id, Zone zone) => new() { TagChange = new() { Entity = id, Tag = (int)GameTag.ZONE, Value = (int)zone } };
        PowerHistoryData Show(int id, string card, Zone zone) => new() { ShowEntity = new() { Entity = id, Name = card, Tags = new() {
            new Tag { Name = (int)GameTag.ZONE, Value = (int)zone }, new Tag { Name = (int)GameTag.CONTROLLER, Value = 2 } } } };
        PowerHistoryData Full(int id) => new() { FullEntity = new() { Entity = id, Name = "", Tags = new() {
            new Tag { Name = (int)GameTag.ENTITY_ID, Value = id },
            new Tag { Name = (int)GameTag.ZONE, Value = (int)Zone.HAND }, new Tag { Name = (int)GameTag.CONTROLLER, Value = 2 } } } };
        var late = new ClientModel { RepairNullToPlayActors = false };
        late.Apply(Packet(Full(90)));
        late.Apply(Packet(Move(90, Zone.SETASIDE), Move(90, Zone.PLAY), Show(90, "TLC_513t2", Zone.PLAY)));
        Check(late.Entities[90].CardId == "TLC_513t2" && late.Entities[90].ActorCardId == "TLC_513t2" && late.Entities[90].HiddenByNullZone, "model: SHOW repairs identity, not null-zone visibility");
        Check(late.LateIdentities.Count == 1 && late.LateIdentities[0].To == (int)Zone.PLAY, "model: late identity recorded");
        var early = new ClientModel { RepairNullToPlayActors = false };
        early.Apply(Packet(Full(90)));
        early.Apply(Packet(Move(90, Zone.SETASIDE), Show(90, "TLC_513t2", Zone.SETASIDE), Move(90, Zone.PLAY)));
        Check(early.Entities[90].ActorCardId == "TLC_513t2" && early.LateIdentities.Count == 0, "model: identity before the move binds the actor");
        Check(early.Entities[90].HiddenByNullZone, "named before PLAY still hidden after hand/null/new-actor transition");
        var direct = new ClientModel();
        direct.Apply(Packet(Full(90)));
        direct.Apply(Packet(Show(90, "TLC_513t2", Zone.HAND), Move(90, Zone.PLAY)));
        Check(!direct.Entities[90].HiddenByNullZone && direct.Entities[90].ActorCardId == "TLC_513t2",
            "direct hand-to-PLAY uses native summon transition");
        var hidden = new ClientModel();
        hidden.Apply(Packet(Full(90), Move(90, Zone.GRAVEYARD)));
        Check(hidden.Entities[90].ActorCardId == "" && hidden.LateIdentities.Count == 0, "model: never-named move stays a hidden actor");
    }
}
