using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // Owner live 2026-10-25: Oblivia's Skeleton Key chose and refreshed with nothing in front of her. Retail sends the
    // opponent's GENERAL EntityChoices to this client too, with hidden offers: ChoiceCardMgr fans her card backs, then
    // EntitiesChosen conceals them and the pick follows the normal reveal rules. Presentation only: same engine answers.
    private static void OpposingChoiceBacks()
    {
        // Discover (to hand), Adapt (fixed SETASIDE options), Dark Gift Discover, refreshable Skeleton Key.
        foreach (var (cardId, refreshes) in new[] { ("UNG_941", 0), ("TLC_245", 0), ("EDR_882", 0), ("JAIL_319", 2) })
        {
            var (capture, match) = OpposingChoiceMatch();
            var game = match.Game; var bot = game.Player2;
            var source = Generic.DrawCard(bot, Cards.FromId(cardId));
            FlushFieldHistory(match);
            int start = capture.Packets.Count;
            InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            Check(bot.Choice?.ChoiceType == ChoiceType.GENERAL, cardId + " opens an NPC GENERAL choice");
            var seenChoices = new HashSet<int>(); var seenEntities = new HashSet<int>();
            int rounds = 0, refreshed = 0;
            while (bot.Choice != null && bot.Choice.ChoiceType == ChoiceType.GENERAL)
            {
                Check(++rounds <= 8, cardId + " bounded NPC choices");
                var shown = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Last();
                if (Environment.GetEnvironmentVariable("HS_OPPOSING_DEBUG") == "1")
                    Console.WriteLine($"DEBUG {cardId} shown player={shown.PlayerId} type={shown.ChoiceType} source={shown.Source}/{source.Id} min={shown.CountMin} max={shown.CountMax} hide={shown.HideChosen} entities={string.Join(",", shown.Entities)} engine={string.Join(",", bot.Choice.Choices)} ");
                Check(shown.PlayerId == bot.PlayerId && shown.ChoiceType == (int)ChoiceType.GENERAL && shown.Source == bot.Choice.SourceId
                    && shown.CountMin == 1 && shown.CountMax == 1 && !shown.HideChosen, cardId + " opposing GENERAL EntityChoices published");
                Check(seenChoices.Add(shown.Id), cardId + " every NPC choice round gets a fresh native choice id");
                Check(capture.Packets.Count(p => p.Id == 17 && ((EntityChoices)p.Message).Id == shown.Id) == 1, cardId + " one packet per round");
                Check(shown.Entities.SequenceEqual(bot.Choice.Choices), cardId + " SETASIDE offers keep their own ids");
                Check(shown.Entities.All(seenEntities.Add), cardId + " a refresh deals a new set of backs");
                OpposingChoiceHidden(capture, shown.Entities, cardId);
                // Pick: refresh token while refreshes remain, otherwise the first real offer.
                int pick = bot.Choice.Choices.FirstOrDefault(id => refreshed < refreshes && game.IdEntityDic[id].Card.Id == "JAIL_319t");
                if (pick != 0) refreshed++;
                else pick = bot.Choice.Choices.First(id => game.IdEntityDic[id].Card.Id != "JAIL_319t");
                int answerStart = capture.Packets.Count;
                InvokeProcess(match, ChooseTask.Pick(bot, pick));
                FlushFieldHistory(match);
                int chosenAt = capture.Packets.FindIndex(answerStart, p => p.Id == 13);
                Check(chosenAt == answerStart, cardId + " EntitiesChosen comes first, before the answer's history");
                var chosen = (EntitiesChosen)capture.Packets[chosenAt].Message;
                Check(chosen.PlayerId == bot.PlayerId && chosen.ChooseEntities.Id == shown.Id && chosen.ChooseEntities.Entities.SequenceEqual(new[] { pick })
                    && chosen.ChoiceType == (int)ChoiceType.GENERAL, cardId + " EntitiesChosen names the NPC pick for its choice id");
                var answer = DeckDiscoverWireHistory(capture, answerStart);
                var state = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
                bool spent = game.IdEntityDic[pick].Zone?.Type == EngineZone.SETASIDE && bot.Choice?.Choices?.Contains(pick) != true;
                if (spent)
                {
                    int retire = answer.FindIndex(e => IsZone(e, pick, EngineZone.REMOVEDFROMGAME));
                    Check(retire >= 0 && answer.Count(e => IsZone(e, pick, EngineZone.REMOVEDFROMGAME)) == 1,
                        cardId + " spent SETASIDE pick (Adapt option / refresh token) retires once so its back cannot linger");
                    var after = capture.Packets.FindIndex(answerStart, p => p.Id == 17);
                    if (after >= 0)
                        Check(capture.Packets.Take(after).Skip(answerStart).Any(p => p.Id == 19 && ((PowerHistory)p.Message).List.Any(e => IsZone(e, pick, EngineZone.REMOVEDFROMGAME))),
                            cardId + " the spent pick retires before the next set of backs");
                }
                else Check(state[pick].Card == "" || game.IdEntityDic[pick].Zone?.Type == EngineZone.PLAY,
                    cardId + " the pick follows ordinary privacy (hidden in the NPC hand)");
                if (game.IdEntityDic[pick].Zone?.Type == EngineZone.HAND)
                    Check(state[pick] == ("", (int)EngineZone.HAND), cardId + " Discover pick flies to the NPC hand as a card back");
                foreach (int unchosen in shown.Entities.Where(id => id != pick))
                    Check(!answer.Any(e => (e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity) && SnapshotEntity(e) == unchosen && SnapshotName(e) != ""),
                        cardId + " unchosen offers are never named");
            }
            Check(refreshed == refreshes, cardId + " refreshed " + refreshed + " of " + refreshes);
            Check(capture.Packets.Skip(start).Count(p => p.Id == 13 && ((EntitiesChosen)p.Message).PlayerId == bot.PlayerId) == rounds,
                cardId + " every shown round is concealed exactly once");
            OpposingChoiceHidden(capture, seenEntities, cardId + " (final)");
            capture.SerializeAll();
            Console.WriteLine($"CHECK opposing choice backs: {cardId} rounds={rounds} refreshes={refreshed} offers={seenEntities.Count} hidden");
        }
        OpposingDeckChoiceProxies();
        OpposingChoicePacing();
        OpposingMulliganUnchanged();
        OpposingMulliganWaitsForReceipt(hooked: true);
        OpposingMulliganWaitsForReceipt(hooked: false);
        OpposingRewindChoiceActors();
    }

    // Rewind bypasses the deferred opposing-choice path. Its direct NPC answer paths still need to retire a chosen
    // Keep/KeepBoth actor that remains SETASIDE; a real reset owns its old graph, and human RewindUIManager owns both.
    private static void OpposingRewindChoiceActors()
    {
        foreach (bool runner in new[] { false, true })
        {
            var (capture, match) = OpposingRewindMatch(runner);
            var rewind = match.Game.Rewind;
            int selected = rewind.KeepEntityId, unchosen = rewind.RewindEntityId;
            int start = capture.Packets.Count;
            AnswerOpposingRewind(match, selected, runner);
            var packets = capture.Packets.Skip(start).ToList();
            int choices = packets.FindIndex(p => p.Id == 17);
            int chosen = packets.FindIndex(p => p.Id == 13);
            int retire = packets.FindIndex(p => p.Id == 19 && ((PowerHistory)p.Message).List.Any(e => IsZone(e, selected, EngineZone.REMOVEDFROMGAME)));
            Check(choices >= 0 && chosen > choices && retire > chosen,
                $"NPC Rewind Keep retires after EntityChoices and EntitiesChosen (runner={runner})");
            var answer = packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
            Check(answer.Count(e => IsZone(e, selected, EngineZone.REMOVEDFROMGAME)) == 1,
                $"NPC Rewind Keep actor retires exactly once (runner={runner})");
            Check(!answer.Any(e => IsZone(e, unchosen, EngineZone.REMOVEDFROMGAME)),
                $"NPC Rewind unchosen actor gets no synthetic removal (runner={runner})");
            Check(match.Game.IdEntityDic[selected].Zone?.Type == EngineZone.SETASIDE,
                $"NPC Rewind retirement is presentation-only (runner={runner})");
            capture.SerializeAll();
        }

        // A real Rewind sends its acknowledgment before GAME_RESET; the reset removes the old choice graph, so no
        // synthetic old-id history may follow it.
        {
            var (capture, match) = OpposingRewindMatch(runner: true);
            int selected = match.Game.Rewind.RewindEntityId, other = match.Game.Rewind.KeepEntityId;
            int start = capture.Packets.Count;
            AnswerOpposingRewind(match, selected, runner: true);
            var packets = capture.Packets.Skip(start).ToList();
            int chosen = packets.FindIndex(p => p.Id == 13);
            int reset = packets.FindIndex(p => p.Id == 19 && ((PowerHistory)p.Message).List.Any(e => e.HasResetGame));
            Check(chosen >= 0 && reset > chosen, "NPC real Rewind acknowledges before GAME_RESET");
            Check(!packets.Skip(reset + 1).Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List)
                .Any(e => SnapshotEntity(e) == selected || SnapshotEntity(e) == other || e.HasTagChange && (e.TagChange.Entity == selected || e.TagChange.Entity == other)),
                "NPC real Rewind emits no stale timeline entity after GAME_RESET");
            capture.SerializeAll();
        }

        // Morchie's KeepBoth replays without GAME_RESET, leaving the selected timeline actor for explicit retirement.
        {
            var (capture, match) = OpposingRewindMatch(runner: true, keepBoth: true);
            int selected = match.Game.Rewind.RewindEntityId;
            int start = capture.Packets.Count;
            AnswerOpposingRewind(match, selected, runner: true);
            var answer = DeckDiscoverWireHistory(capture, start);
            Check(!answer.Any(e => e.HasResetGame) && answer.Count(e => IsZone(e, selected, EngineZone.REMOVEDFROMGAME)) == 1,
                "NPC KeepBoth retires its chosen actor without GAME_RESET");
            capture.SerializeAll();
        }

        // Human timeline actors remain owned by RewindUIManager/reset handling.
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
            var human = match.Game.Player1;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(human, Cards.FromId("GAME_005"), zone: human.HandZone);
            source[GameTag.REWIND] = 1;
            FlushFieldHistory(match);
            var choice = ChoiceRetirementPlay(match, capture, source, 0);
            int keep = human.Choice.Choices.Single(id => match.Game.IdEntityDic[id].Card.Id == "TIME_000ta");
            int rewind = human.Choice.Choices.Single(id => match.Game.IdEntityDic[id].Card.Id == "TIME_000tb");
            int start = capture.Packets.Count;
            match.SendChoices(choice.Id, new List<int> { keep });
            var answer = DeckDiscoverWireHistory(capture, start);
            Check(!answer.Any(e => IsZone(e, keep, EngineZone.REMOVEDFROMGAME) || IsZone(e, rewind, EngineZone.REMOVEDFROMGAME)),
                "human Rewind Keep leaves timeline retirement to RewindUIManager");
            capture.SerializeAll();
        }
        Console.WriteLine("CHECK opposing choice backs: both NPC Rewind paths retire Keep; reset, KeepBoth and human ownership preserved");
    }

    private static (Capture Capture, NativeMatch Match) OpposingRewindMatch(bool runner, bool keepBoth = false)
    {
        var capture = new Capture();
        NativeMatch match;
        if (runner)
        {
            var deck = LocalDecks.Get(NpcCatalog.All[2].DeckId);
            match = new NativeMatch(capture.Send, deck.Id, deck.Id, 8427) { MatchId = "opposing-rewind", NativeNpcStartingTaunt = () => false };
        }
        else match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE), Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        match.Start();
        match.SendChoices(capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Last(c => c.PlayerId == 1).Id, new List<int>());
        InvokeProcess(match, EndTurnTask.Any(match.Game.Player1));
        FlushFieldHistory(match);
        var bot = match.Game.Player2;
        bot.BaseMana = 10; bot.UsedMana = 0;
        foreach (var old in bot.HandZone.ToArray()) bot.SetasideZone.Add(old.Zone.Remove(old));
        if (keepBoth) QuestRules.AddPlayerEffect(bot, bot.Hero, "END_036e");
        var source = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("GAME_005"), zone: bot.HandZone);
        source[GameTag.REWIND] = 1;
        FlushFieldHistory(match);
        InvokeProcess(match, PlayCardTask.Spell(bot, source));
        FlushFieldHistory(match);
        Check(match.Game.Rewind != null && match.Game.Player2.Choice?.ChoiceAction == ChoiceAction.REWIND,
            $"NPC Rewind fixture opens direct choice (runner={runner}, keepBoth={keepBoth})");
        return (capture, match);
    }

    private static void AnswerOpposingRewind(NativeMatch match, int pick, bool runner)
    {
        if (!runner)
        {
            typeof(NativeMatch).GetMethod("DriveUntilHumanInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            return;
        }
        match.ActNpc(new GoldenDecision(BotAction.From(ChooseTask.Pick(match.Game.Player2, pick)), 0, 0, 0, "Forced", null, null, "fallback"));
    }

    // Pinned GameState choice queue (client 37.0.0.254477; GameState.OnEntityChoices/OnEntitiesChosen/
    // CanProcess*/ProcessAllQueuedChoices/OnEntitiesChosenProcessed): one FIFO for both players. A processable
    // EntityChoices is dequeued but processed a frame later (DeferOneFrame) and stops the drain; a processable
    // EntitiesChosen (no choice of its player mapped, or the same id) is handled at once. Every replayed PowerHistory
    // drains the queue. While the Gameplay scene loads, history waits in a backlog and no player exists yet.
    // ChoiceCardMgr conceals a registered GENERAL choice before OnEntitiesChosenProcessed (one frame);
    // without a registered prompt there is nothing to conceal, so completion runs immediately.
    private sealed class ClientChoiceQueue
    {
        private readonly Queue<IProtoBuf> queued = new Queue<IProtoBuf>();
        private readonly List<Action> nextFrame = new List<Action>();
        private readonly Dictionary<int, int> mapped = new Dictionary<int, int>();
        private int backlog;
        private bool ready;
        private readonly bool requireEntities;
        private readonly HashSet<int> entities = new HashSet<int>();
        public ClientChoiceQueue(bool requireEntities = false) { this.requireEntities = requireEntities; }
        public int QueuedCount => queued.Count;
        public int DeferredCount => nextFrame.Count;
        public readonly List<string> Log = new List<string>();
        public int Mapped(int player) => mapped.TryGetValue(player, out int id) ? id : -1;
        public bool Queued(int id) => queued.Any(p => p is EntityChoices c && c.Id == id);

        public int Deliver(Capture capture, int cursor)
        {
            for (; cursor < capture.Packets.Count; cursor++)
            {
                var message = capture.Packets[cursor].Message;
                if (message is PowerHistory history)
                {
                    if (ready)
                    {
                        foreach (var entry in history.List.Where(e => e.HasFullEntity)) entities.Add(entry.FullEntity.Entity);
                        Drain();
                    }
                    else backlog++;
                }
                else if (message is EntityChoices choices) { if (CanProcess(choices)) Process(choices); else queued.Enqueue(choices); }
                else if (message is EntitiesChosen chosen) { if (CanProcess(chosen)) Process(chosen); else queued.Enqueue(chosen); }
            }
            return cursor;
        }

        public void LoadScene()
        {
            ready = true;
            for (; backlog > 0; backlog--) Drain();
        }

        public void EndFrame()
        {
            var run = nextFrame.ToList();
            nextFrame.Clear();
            foreach (var action in run) action();
        }

        private bool CanProcess(EntityChoices choices) => ready && !mapped.ContainsKey(choices.PlayerId)
            && (!requireEntities || choices.Entities.All(entities.Contains));
        private bool CanProcess(EntitiesChosen chosen) =>
            ready && (!requireEntities || chosen.ChooseEntities.Entities.All(entities.Contains))
                && (!mapped.TryGetValue(chosen.PlayerId, out int id) || id == chosen.ChooseEntities.Id);

        private void Process(EntityChoices choices)
        {
            mapped[choices.PlayerId] = choices.Id;
            Log.Add("choices:" + choices.PlayerId + ":" + choices.Id);
        }

        private void Process(EntitiesChosen chosen)
        {
            Log.Add("chosen:" + chosen.PlayerId + ":" + chosen.ChooseEntities.Id);
            if (chosen.ChoiceType == (int)ChoiceType.GENERAL && Mapped(chosen.PlayerId) == chosen.ChooseEntities.Id)
                nextFrame.Add(() => Processed(chosen));
            else Processed(chosen);
        }

        private void Processed(EntitiesChosen chosen)
        {
            mapped.Remove(chosen.PlayerId);
            Drain();
        }

        private void Drain()
        {
            while (queued.Count > 0)
            {
                if (queued.Peek() is EntityChoices choices)
                {
                    if (!CanProcess(choices)) return;
                    queued.Dequeue();
                    nextFrame.Add(() => { Process(choices); Drain(); });
                    return;
                }
                var chosen = (EntitiesChosen)queued.Peek();
                if (!CanProcess(chosen)) return;
                queued.Dequeue();
                Process(chosen);
            }
        }
    }

    // Live 2026-10-26 (batch-e verify): every Oblivia choice logged `OnEntityChoices() - id=N playerId=2 queued` and
    // never resolved. The NPC mulligan's EntitiesChosen reached GameState before its queued EntityChoices was processed,
    // so the mulligan stayed mapped for player 2 forever. With the receipt hook (ClientBridge) it waits; the model also
    // reproduces the live failure without it.
    private static void OpposingMulliganWaitsForReceipt(bool hooked)
    {
        string label = hooked ? "receipt-ordered" : "unordered (pre-fix)";
        var capture = new Capture();
        var client = new ClientChoiceQueue();
        var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE), Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        if (hooked) match.NativeOpposingChoiceReceived = id => client.Mapped(2) == id;
        int cursor = 0;
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                cursor = client.Deliver(capture, cursor);
                client.EndFrame();
                match.TickNpc();
            }
            cursor = client.Deliver(capture, cursor);
        }
        EntityChoices LastChoice(int player) => capture.Packets.Select(p => p.Message).OfType<EntityChoices>().Last(c => c.PlayerId == player);
        match.Start();
        // Start's packets reach a loading scene: history waits, both mulligans and the NPC answer queue.
        cursor = client.Deliver(capture, cursor);
        int npcMulligan = LastChoice(2).Id, humanMulligan = LastChoice(1).Id;
        Check(hooked == !capture.Packets.Any(p => p.Id == 13), label + ": NPC mulligan EntitiesChosen waits for receipt");
        client.LoadScene();
        Frames(3);
        Check(client.Mapped(1) == humanMulligan, label + ": human mulligan shown");
        if (hooked)
            Check(client.Mapped(2) == -1 && client.Log.IndexOf("choices:2:" + npcMulligan) >= 0
                && client.Log.IndexOf("choices:2:" + npcMulligan) < client.Log.IndexOf("chosen:2:" + npcMulligan)
                && match.DeferredOpposingChosen == 0,
                label + ": NPC mulligan EntitiesChosen follows its processed EntityChoices: " + string.Join(" ", client.Log));
        else
            Check(client.Mapped(2) == npcMulligan && client.Log.IndexOf("chosen:2:" + npcMulligan) < client.Log.IndexOf("choices:2:" + npcMulligan),
                label + ": model reproduces the live stuck mulligan: " + string.Join(" ", client.Log));
        match.SendChoices(humanMulligan, new List<int>());
        Frames(2);
        Check(client.Mapped(1) == -1, label + ": human mulligan concealed");

        // NPC Discover: the human sees its card backs, then the pick conceals them.
        InvokeProcess(match, EndTurnTask.Any(match.Game.Player1));
        FlushFieldHistory(match);
        var bot = match.Game.Player2;
        bot.BaseMana = 10; bot.UsedMana = 0;
        foreach (var old in bot.HandZone.ToArray()) bot.SetasideZone.Add(old.Zone.Remove(old));
        var source = Generic.DrawCard(bot, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        int npcDiscover = match.OpposingChoiceId;
        Check(npcDiscover > 0 && LastChoice(2).Id == npcDiscover, label + ": NPC Discover published");
        Frames(3);
        if (!hooked)
        {
            Check(client.Mapped(2) == npcMulligan && client.Queued(npcDiscover), label + ": NPC Discover stays queued behind the mulligan (live symptom)");
            InvokeProcess(match, ChooseTask.Pick(bot, bot.Choice.Choices[0]));
            FlushFieldHistory(match);
            InvokeProcess(match, EndTurnTask.Any(bot));
            FlushFieldHistory(match);
        }
        else
        {
            Check(client.Mapped(2) == npcDiscover, label + ": NPC Discover reaches ChoiceCardMgr as card backs");
            InvokeProcess(match, ChooseTask.Pick(bot, bot.Choice.Choices[0]));
            FlushFieldHistory(match);
            Frames(3);
            Check(client.Mapped(2) == -1 && client.Log.Contains("chosen:2:" + npcDiscover) && match.DeferredOpposingChosen == 0,
                label + ": NPC pick conceals its backs");
            InvokeProcess(match, EndTurnTask.Any(bot));
            FlushFieldHistory(match);
        }

        // The human's own Discover shares the queue.
        var human = match.Game.Player1;
        Check(match.Game.CurrentPlayer == human, label + ": human turn");
        human.BaseMana = 10; human.UsedMana = 0;
        var mine = Generic.DrawCard(human, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        InvokeProcess(match, human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == mine.Id));
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var humanDiscover = LastChoice(1);
        Frames(3);
        // An EntityChoices arriving for an unmapped player is processed at once, past the queue: the human's own
        // Discover works either way.
        Check(client.Mapped(1) == humanDiscover.Id, label + ": the human's own Discover is shown");
        match.SendChoices(humanDiscover.Id, new List<int> { humanDiscover.Entities[0] });
        Frames(3);
        Check(client.Mapped(1) == -1 && client.Log.Contains("chosen:1:" + humanDiscover.Id),
            label + ": the human's Discover answers and conceals: " + string.Join(" ", client.Log));
        if (!hooked)
        {
            Check(client.Mapped(2) == npcMulligan && client.Queued(npcDiscover), label + ": the NPC stays stuck on its mulligan");
            Console.WriteLine("CHECK opposing choice backs: client queue model reproduces the live stuck NPC mulligan without receipt ordering");
            return;
        }
        Check(client.Mapped(2) == -1, label + ": no NPC choice left mapped");
        capture.SerializeAll();
        Console.WriteLine("CHECK opposing choice backs: NPC mulligan, NPC Discover and human Discover all clear the client choice queue in order");
    }

    // Fairness: the human client must never learn an NPC offer's identity. Every snapshot of an offer is nameless and
    // carries only public hidden tags; the client graph holds it as a hidden SETASIDE entity.
    private static void OpposingChoiceHidden(Capture capture, IEnumerable<int> offers, string label)
    {
        var ids = new HashSet<int>(offers);
        var all = DeckDiscoverWireHistory(capture, 0);
        var publicTags = new HashSet<int> { (int)GameTag.ENTITY_ID, (int)GameTag.CONTROLLER, (int)GameTag.ZONE, (int)GameTag.ZONE_POSITION };
        foreach (var row in all.Where(e => ids.Contains(SnapshotEntity(e))))
        {
            Check(SnapshotName(row) == "", label + " offer " + SnapshotEntity(row) + " CARD_ID reached the human client");
            var tags = row.HasFullEntity ? row.FullEntity.Tags : row.HasShowEntity ? row.ShowEntity.Tags : row.ChangeEntity?.Tags;
            Check(tags == null || tags.All(t => publicTags.Contains(t.Name)), label + " offer " + SnapshotEntity(row) + " snapshot leaks definition tags: "
                + string.Join(",", (tags ?? new List<PegasusGame.Tag>()).Where(t => !publicTags.Contains(t.Name)).Select(t => t.Name)));
        }
        var state = DeckDiscoverWireState(all);
        foreach (int id in ids) Check(state.ContainsKey(id) && state[id].Card == "", label + " offer " + id + " exists hidden on the client");
        // The choice packets themselves carry ids only.
        Check(capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Where(c => c.PlayerId == 2)
            .All(c => c.Entities.All(id => state.TryGetValue(id, out var s) && s.Card == "" || !ids.Contains(id))), label + " choice packet ids are hidden entities");
    }

    // Deck-Discover offers stay real DECK entities: the opponent's backs are separate nameless SETASIDE stand-ins, so
    // ChoiceCardMgr never pulls a real deck Card (and no link names which deck card was offered).
    private static void OpposingDeckChoiceProxies()
    {
        var (capture, match) = OpposingChoiceMatch();
        var game = match.Game; var bot = game.Player2;
        foreach (var old in bot.DeckZone.ToArray()) bot.SetasideZone.Add(old.Zone.Remove(old));
        var deck = new[] { "EDR_852", "CS2_182", "CS2_200" }
            .Select(id => SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId(id), zone: bot.DeckZone)).ToArray();
        var source = Generic.DrawCard(bot, Cards.FromId("CORE_DS1_184"));
        FlushFieldHistory(match);
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        var offers = bot.Choice.Choices.ToArray();
        var shown = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Last();
        Check(shown.PlayerId == bot.PlayerId && shown.Entities.Count == offers.Length, "Tracking opposing choice published");
        var proxied = shown.Entities.Where((id, i) => id != offers[i]).ToList();
        Check(offers.All(id => game.IdEntityDic[id].Zone?.Type == EngineZone.SETASIDE) ? proxied.Count == 0
            : proxied.Count == offers.Count(id => game.IdEntityDic[id].Zone?.Type != EngineZone.SETASIDE),
            "exactly the non-SETASIDE offers get stand-ins");
        Check(shown.Entities.All(id => !deck.Any(c => c.Id == id)), "the opposing choice never shows a real DECK entity");
        OpposingChoiceHidden(capture, shown.Entities, "Tracking");
        var history = DeckDiscoverWireHistory(capture, 0);
        foreach (int proxy in proxied)
        {
            var create = history.Single(e => e.HasFullEntity && e.FullEntity.Entity == proxy);
            Check(create.FullEntity.Name == "" && SnapshotTag(create, GameTag.ZONE) == (int)EngineZone.SETASIDE
                && SnapshotTag(create, GameTag.CONTROLLER) == bot.PlayerId && SnapshotTag(create, GameTag.LINKED_ENTITY) == 0,
                "stand-in is a nameless unlinked NPC SETASIDE entity");
        }
        int pick = offers[0], answerStart = capture.Packets.Count;
        InvokeProcess(match, ChooseTask.Pick(bot, pick));
        FlushFieldHistory(match);
        var chosen = (EntitiesChosen)capture.Packets[answerStart].Message;
        Check(chosen.ChooseEntities.Entities.SequenceEqual(new[] { shown.Entities[0] }), "EntitiesChosen acknowledges the shown stand-in");
        var answer = DeckDiscoverWireHistory(capture, answerStart);
        if (proxied.Contains(shown.Entities[0]))
            Check(answer.Count(e => IsZone(e, shown.Entities[0], EngineZone.REMOVEDFROMGAME)) == 1, "chosen stand-in retires once");
        var state = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
        Check(deck.All(c => state.TryGetValue(c.Id, out var s) ? s.Card == "" : true), "deck originals stay nameless");
        capture.SerializeAll();
        Console.WriteLine($"CHECK opposing choice backs: Tracking stand-ins={proxied.Count} hidden, chosen retired");
    }

    // The NPC holds its pick until the backs are dealt (own 3 s cap from the first ready pick, outside the 4 s pacing
    // budget), then for a personality beat inside that budget; thinking while they are up counts.
    private static void OpposingChoicePacing()
    {
        var (capture, match) = OpposingChoiceMatch();
        double now = 1000; bool revealed = false; int asked = -1;
        match.NpcClock = () => now;
        match.NativeOpposingChoiceRevealed = id => { asked = id; return revealed; };
        var bot = match.Game.Player2;
        var source = Generic.DrawCard(bot, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        var play = bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
        Check(match.OpposingChoiceHold(BotAction.From(play)) == 0 && match.OpposingChoiceDwell(BotAction.From(play)) == 0, "no hold without a shown choice");
        InvokeProcess(match, play);
        FlushFieldHistory(match);
        var pick = BotAction.From(ChooseTask.Pick(bot, bot.Choice.Choices[0]));
        double beat = NativeMatch.OpposingChoiceBeatMilliseconds(null);
        Check(beat >= 1200 && beat <= 2500, "beat is human-plausible");
        // The deal-wait cap starts at the first ready pick, not at publish: a long think never eats it.
        now += 5000;
        Check(match.OpposingChoiceHold(pick) == 100 && asked == match.OpposingChoiceId, "holds for the native deal of this choice id");
        Check(match.OpposingChoiceDwell(pick) == 0, "no budgeted beat before the deal");
        now += 500; revealed = true; match.TickNpc();
        double revealedAt = now;
        Check(match.OpposingChoiceHold(pick) == 0 && match.OpposingChoiceDwell(pick) == beat, "dealt: hold ends, full beat from the reveal");
        now += 700;
        Check(Math.Abs(match.OpposingChoiceDwell(pick) - (beat - 700)) < 1e-6, "time looking at the backs counts");
        now = revealedAt + beat + 1;
        Check(match.OpposingChoiceDwell(pick) == 0, "beat ends");
        // A display that never deals stops holding 3 s after the first ready pick.
        var (_, match2) = OpposingChoiceMatch();
        double later = 0; match2.NpcClock = () => later; match2.NativeOpposingChoiceRevealed = id => false;
        var bot2 = match2.Game.Player2;
        var source2 = Generic.DrawCard(bot2, Cards.FromId("UNG_941"));
        FlushFieldHistory(match2);
        InvokeProcess(match2, bot2.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source2.Id));
        FlushFieldHistory(match2);
        var pick2 = BotAction.From(ChooseTask.Pick(bot2, bot2.Choice.Choices[0]));
        later = 10000;
        Check(match2.OpposingChoiceHold(pick2) == 100, "first ready pick starts the deal wait");
        later = 12999; Check(match2.OpposingChoiceHold(pick2) == 100, "still within the 3 s deal wait");
        later = 13000; Check(match2.OpposingChoiceHold(pick2) == 0 && match2.OpposingChoiceDwell(pick2) == 0, "undealt backs stop holding after 3 s");
        // A throwing native probe counts as dealt (Update must never throw).
        var (_, match3) = OpposingChoiceMatch();
        match3.NpcClock = () => 0; match3.NativeOpposingChoiceRevealed = id => throw new InvalidOperationException("probe");
        var bot3 = match3.Game.Player2;
        var source3 = Generic.DrawCard(bot3, Cards.FromId("UNG_941"));
        FlushFieldHistory(match3);
        InvokeProcess(match3, bot3.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source3.Id));
        FlushFieldHistory(match3);
        var pick3 = BotAction.From(ChooseTask.Pick(bot3, bot3.Choice.Choices[0]));
        match3.TickNpc();
        Check(match3.OpposingChoiceHold(pick3) == 0 && match3.OpposingChoiceDwell(pick3) == beat, "probe failure falls back to the beat");
        InvokeProcess(match3, ChooseTask.Pick(bot3, bot3.Choice.Choices[0]));
        FlushFieldHistory(match3);
        Check(match3.OpposingChoiceHold(pick3) == 0 && match3.OpposingChoiceDwell(pick3) == 0 && match3.OpposingChoiceId == -1, "answered choice has no hold");
        Console.WriteLine($"CHECK opposing choice backs: pacing beat={beat}, deal wait from first ready pick, bounded 3 s");
        OpposingChosenWaitsForReceipt();
        OpposingNamedOfferGetsStandIn();
        OpposingHoldThroughRunner(false);
        OpposingHoldThroughRunner(true);
    }

    // GameState processes an EntitiesChosen at once when no choice of that player is mapped. Sent before the client
    // processed its EntityChoices (deferred a frame by ProcessAllQueuedChoices), it would leave that choice mapped forever
    // and block every later choice. EntitiesChosen therefore waits, in order, for the client's receipt.
    private static void OpposingChosenWaitsForReceipt()
    {
        var (capture, match) = OpposingChoiceMatch();
        var received = new HashSet<int>();
        match.NativeOpposingChoiceReceived = id => received.Contains(id);
        var game = match.Game; var bot = game.Player2;
        var source = Generic.DrawCard(bot, Cards.FromId("JAIL_319"));
        FlushFieldHistory(match);
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        int first = match.OpposingChoiceId;
        int token = bot.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "JAIL_319t");
        int answerStart = capture.Packets.Count;
        InvokeProcess(match, ChooseTask.Pick(bot, token));
        FlushFieldHistory(match);
        Check(bot.Choice != null && match.OpposingChoiceId != first, "refresh re-offers while the first conceal waits");
        int second = match.OpposingChoiceId;
        Check(!capture.Packets.Skip(answerStart).Any(p => p.Id == 13) && match.DeferredOpposingChosen == 1,
            "no EntitiesChosen before the client processed that EntityChoices");
        Check(!DeckDiscoverWireHistory(capture, answerStart).Any(e => IsZone(e, token, EngineZone.REMOVEDFROMGAME)),
            "spent token is not retired while its choice may still be shown");
        int pick = bot.Choice.Choices.First(id => game.IdEntityDic[id].Card.Id != "JAIL_319t");
        InvokeProcess(match, ChooseTask.Pick(bot, pick));
        FlushFieldHistory(match);
        Check(match.DeferredOpposingChosen == 2, "the next conceal queues behind the first, in order");
        received.Add(second); match.TickNpc();
        Check(match.DeferredOpposingChosen == 2 && !capture.Packets.Skip(answerStart).Any(p => p.Id == 13), "a later receipt cannot jump the queue");
        received.Add(first);
        int flushed = capture.Packets.Count;
        match.TickNpc();
        var chosen = capture.Packets.Skip(flushed).Where(p => p.Id == 13).Select(p => (EntitiesChosen)p.Message).ToList();
        Check(match.DeferredOpposingChosen == 0 && chosen.Select(c => c.ChooseEntities.Id).SequenceEqual(new[] { first, second })
            && chosen[0].ChooseEntities.Entities.SequenceEqual(new[] { token }) && chosen[1].ChooseEntities.Entities.SequenceEqual(new[] { pick }),
            "receipts release both EntitiesChosen in order with their picks");
        int firstAt = capture.Packets.FindIndex(flushed, p => p.Id == 13);
        int retire = capture.Packets.FindIndex(flushed, p => p.Id == 19 && ((PowerHistory)p.Message).List.Any(e => IsZone(e, token, EngineZone.REMOVEDFROMGAME)));
        Check(retire > firstAt, "the spent token retires after its EntitiesChosen");
        capture.SerializeAll();
        Console.WriteLine("CHECK opposing choice backs: EntitiesChosen waits for client receipt, in order");
    }

    // Real NpcTurnRunner path: with the 4 s pacing budget already spent, the pick still waits for the deal (or its own
    // 3 s cap), and that wait spends no budget.
    private static void OpposingHoldThroughRunner(bool deals)
    {
        var profile = NpcCatalog.All[2]; var deck = LocalDecks.Get(profile.DeckId);
        var packets = new List<object>();
        double now = 0; bool revealed = false;
        var match = new NativeMatch((id, packet) => packets.Add(packet), deck.Id, deck.Id, 8427) {
            MatchId = "opposing-hold", NativeNpcStartingTaunt = () => false };
        match.NpcClock = () => now; match.NativeNpcIdle = () => true;
        match.NativeOpposingChoiceRevealed = id => revealed;
        match.Start();
        match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
        var options = packets.OfType<AllOptions>().Last();
        match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
        var game = match.Game; var bot = game.Player2;
        Check(game.CurrentPlayer == bot && match.NpcRunner != null, "NPC runner owns the turn");
        bot.BaseMana = 10; bot.UsedMana = 0;
        var source = Generic.DrawCard(bot, Cards.FromId("UNG_941"));
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        Check(match.OpposingChoiceId > 0, "runner faces a shown opposing choice");
        typeof(NpcTurnRunner).GetProperty(nameof(NpcTurnRunner.ArtificialDelayMilliseconds)).SetValue(match.NpcRunner, 4000.0);
        double holdStart = -1, answered = -1, dealAt = 2000;
        for (int frame = 0; frame < 2000 && answered < 0; frame++)
        {
            now += 70; // ~14 fps
            if (deals && now >= dealAt) revealed = true;
            match.TickNpc();
            var started = (double)typeof(NativeMatch).GetField("opposingHoldStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            if (holdStart < 0 && started >= 0) holdStart = started;
            if (bot.Choice == null || bot.Choice.ChoiceType != ChoiceType.GENERAL) answered = now;
        }
        Check(answered > 0 && holdStart >= 0, "runner answers the choice after holding");
        if (deals) Check(answered >= dealAt && answered - holdStart < NativeMatch.OpposingDealWaitMilliseconds,
            $"pick waits for the deal, then goes (budget spent): hold={holdStart} deal={dealAt} answered={answered}");
        else Check(answered - holdStart >= NativeMatch.OpposingDealWaitMilliseconds && answered - holdStart < NativeMatch.OpposingDealWaitMilliseconds + 1100,
            $"undealt pick waits its own 3 s cap: hold={holdStart} answered={answered}");
        Check(match.NpcRunner.ArtificialDelayMilliseconds == 4000, "deal wait spends no pacing budget: " + match.NpcRunner.ArtificialDelayMilliseconds);
        match.StopNpc();
        Console.WriteLine($"CHECK opposing choice backs: runner hold deals={deals} waited={answered - holdStart:F0}ms, budget untouched");
    }

    // Defensive: a SETASIDE offer the client already knows by name is listed through a nameless stand-in instead.
    private static void OpposingNamedOfferGetsStandIn()
    {
        var (capture, match) = OpposingChoiceMatch();
        var bot = match.Game.Player2;
        var source = Generic.DrawCard(bot, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        int named = bot.Choice.Choices[1];
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((Dictionary<int, string>)typeof(NativeMatch).GetField("clientTransportCards", flags).GetValue(match))[named] = "CS2_029";
        typeof(NativeMatch).GetField("opposingChoices", flags).SetValue(match, null);
        typeof(NativeMatch).GetMethod("PublishOpposingChoice", flags).Invoke(match,
            new object[] { ((SabberStoneCore.Kettle.PowerEntityChoices)null, (List<int>)null, (HashSet<int>)null) });
        var shown = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Last();
        Check(shown.Entities[0] == bot.Choice.Choices[0] && shown.Entities[2] == bot.Choice.Choices[2] && shown.Entities[1] != named,
            "a client-named offer is replaced by a stand-in; nameless offers keep their ids");
        var create = DeckDiscoverWireHistory(capture, 0).Single(e => e.HasFullEntity && e.FullEntity.Entity == shown.Entities[1]);
        Check(create.FullEntity.Name == "" && create.FullEntity.Tags.All(t => t.Name == (int)GameTag.ENTITY_ID || t.Name == (int)GameTag.CONTROLLER
            || t.Name == (int)GameTag.ZONE || t.Name == (int)GameTag.ZONE_POSITION), "stand-in is nameless");
        int start = capture.Packets.Count;
        InvokeProcess(match, ChooseTask.Pick(bot, named));
        FlushFieldHistory(match);
        Check(((EntitiesChosen)capture.Packets[start].Message).ChooseEntities.Entities.SequenceEqual(new[] { shown.Entities[1] }),
            "pick of the named offer is acknowledged through its stand-in");
        Console.WriteLine("CHECK opposing choice backs: client-named SETASIDE offer shown through a stand-in");
    }

    private static void OpposingMulliganUnchanged()
    {
        var (capture, match) = OpposingChoiceMatch();
        Check(capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message)
            .Count(c => c.PlayerId == 2) == 1, "NPC mulligan is still published once, by its own path");
        Check(capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message)
            .Where(c => c.PlayerId == 2).All(c => c.ChoiceType == (int)ChoiceType.MULLIGAN), "no GENERAL opposing choice without one pending");
    }

    // Retail's view of the other player's prompt: its GENERAL EntityChoices arrives, but every listed actor is nameless
    // in every snapshot this client ever received (card backs). Mulligans keep their own path.
    private static bool OpposingPromptsHidden(Capture capture, int chooser)
    {
        var named = capture.Packets.Select(p => p.Message).OfType<PowerHistory>().SelectMany(p => p.List)
            .SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }).Where(e => e != null && !string.IsNullOrEmpty(e.Name))
            .Select(e => e.Entity).ToHashSet();
        return capture.Packets.Select(p => p.Message).OfType<EntityChoices>().Where(c => c.ChoiceType != (int)ChoiceType.MULLIGAN)
            .All(c => c.PlayerId == chooser && c.ChoiceType == (int)ChoiceType.GENERAL && c.Entities.All(id => !named.Contains(id)));
    }

    private static (Capture, NativeMatch) OpposingChoiceMatch()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE), Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        match.Start();
        match.SendChoices(capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).Last(c => c.PlayerId == 1).Id, new List<int>());
        InvokeProcess(match, EndTurnTask.Any(match.Game.Player1));
        FlushFieldHistory(match);
        var bot = match.Game.Player2;
        Check(match.Game.CurrentPlayer == bot, "NPC turn");
        bot.BaseMana = 10; bot.UsedMana = 0;
        foreach (var old in bot.HandZone.ToArray()) bot.SetasideZone.Add(old.Zone.Remove(old));
        FlushFieldHistory(match);
        return (capture, match);
    }
}
