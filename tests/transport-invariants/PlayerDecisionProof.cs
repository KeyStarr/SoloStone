using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

internal static class PlayerDecisionProof
{
    // Test-only continuation exercises legacy insertion, not retail shuffle semantics.
    private static readonly DecisionBattlecryTask HandRecipe = new DecisionBattlecryTask("transport/decision/hand/v1",
        new SelectEntityThenContinueTask(),
        EffectContext.Task(ctx => {
            if (ctx.LastChosenEntityId == 0) return TaskState.COMPLETE;
            ctx.RunLegacy(new MoveToDeck(EntityType.TARGET), ctx.LastChosenEntityId);
            return ctx.RunLegacy(new CustomTask((g, c, s, t, stack) =>
                t.Cost = Math.Max(0, t.Cost - 1)), ctx.LastChosenEntityId);
        }));

    private static void WithHandRecipe(Action proof)
    {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        var handTargetTag = (GameTag)1508;
        bool hadHandTargetTag = card.Tags.TryGetValue(handTargetTag, out int originalHandTarget);
        try
        {
            card.Tags[handTargetTag] = 1;
            card.Power = new Power { PowerTask = HandRecipe };
            proof();
        }
        finally
        {
            card.Power = original;
            if (hadHandTargetTag) card.Tags[handTargetTag] = originalHandTarget;
            else card.Tags.Remove(handTargetTag);
        }
    }

    private static void Check(bool value, string message) => SelfChecks.Check(value, "player decision " + message);
    private static (Game game, NativeMatch match, NativeSession session) Fixture(bool bot = false)
    {
        var game = new Game(new GameConfig { RandomSeed = 17, StartPlayer = bot ? 2 : 1,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        game.StartGame(); game.CurrentPlayer.BaseMana = 10;
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        return (game, match, session);
    }

    private static EntityChoices Choice(NativeSession session)
    {
        var result = new EntityChoices();
        using var stream = new MemoryStream(Convert.FromBase64String(session.Packets.Last(p => p.Id == 17).Base64));
        result.Deserialize(stream); return result;
    }

    internal static void Run() => WithHandRecipe(RunCore);

    private static void RunCore()
    {
        var (game, match, session) = Fixture();
        var p = game.Player1;
        var source = Generic.DrawCard(p, Cards.FromId("EX1_319"));
        var offers = p.HandZone.Where(c => c != source).Select(c => c.Id).ToArray();
        session.Flush(); session.Process(PlayCardTask.Any(p, source)); session.PublishDecision(p);
        var packet = Choice(session);
        Check(packet.ChoiceType == 3 && packet.CountMin == 1 && packet.CountMax == 1 && packet.Source == source.Id &&
            packet.Entities.SequenceEqual(offers), "TARGET uses existing hand IDs, source, mandatory one pick");
        Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 1, "source permits native hand targeting");
        Check(packet.Entities.All(id => session.Client.Entities[id].Get(GameTag.ZONE) == (int)Zone.HAND), "offered actors remain in hand");
        Check(!Invariants.Compare(game, session.Client).Any(), "initial choice native state");
        var bytes = game.SaveSnapshot(); int packetCount = session.Packets.Count;
        foreach (var invalid in new[] { new List<int>(), new List<int> {source.Id}, new List<int> {offers[0], offers[0]} })
        {
            bool rejected = false;
            try { match.SendChoices(packet.Id, invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()) && packetCount == session.Packets.Count && match.HumanChoiceId == packet.Id,
                "invalid input preserves prompt, state, packets");
        }
        match.SendChoices(packet.Id, new List<int> { offers[0] });
        Check(p.Choice == null && game.IdEntityDic[offers[0]].Zone.Type == Zone.DECK, "production SendChoices resumes hand branch");
        Check(session.Packets.Skip(packetCount).Count(p => p.Id == 13) == 1, "one chosen acknowledgment");
        Check(!Invariants.Compare(game, session.Client).Any(), "post-selection native state");
        Check(Generic.IsDecisionInternalTag((GameTag)31008) && Generic.IsDecisionInternalTag((GameTag)31009) &&
            !Generic.IsDecisionInternalTag((GameTag)31010), "Outcast tag and distinct secret-pick reservation stay internal");
        Check(session.Client.Entities.Values.All(e => e.Tags.Keys.All(t => t < 31001 || t > 31009)), "private continuation tags never projected");
        DescriptorHand(); DescriptorBoard();
        DecisionSpellNoEarlyOptions(); ControllerChangeProjection(); SpellBots();
        FixedPrivacy();
        Bot();
        Console.WriteLine("PASS player decision transport: legacy/filtered Hand/Board TARGET packets, frozen offers/actors, no pending ordinary options, SendChoices/rejection/ack, internal-tag suppression, hero/location/prior-pick exclusions, deterministic bot");
    }

    private static void AssertPrompt(Game game, NativeSession session, EntityChoices packet)
    {
        var p = game.Player1; var decision = game.GetPendingDecisions().Single();
        Check(packet.ChoiceType == 3 && packet.CountMin == 1 && packet.CountMax == 1 &&
            packet.Entities.SequenceEqual(game.Resolution.SuspendedEffect.OfferEntityIds) &&
            packet.Entities.SequenceEqual(decision.Options.Select(o => o.EntityId)), "frozen engine offers equal native TARGET IDs");
        Check(packet.Entities.All(id => session.Client.Entities.ContainsKey(id)), "every offered entity has native actor before choice reference");
        Check(decision.Kind == DecisionKind.SuspendedEffect && p.Options().All(t => t is ChooseTask), "no ordinary options while selector pending");
        Check(session.Packets.All(w => w.Id != 14), "no ordinary option packet emitted while selector pending");
        Check(!Invariants.Compare(game, session.Client).Any(), "selector native projection matches engine");
        var actors = new HashSet<int>();
        foreach (var wire in session.Packets)
        {
            using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
            if (wire.Id == 17)
            {
                var choices = new EntityChoices(); choices.Deserialize(stream);
                Check(choices.Entities.All(actors.Contains), "packet 17 references only actors already created by history");
            }
            if (wire.Id != 19) continue;
            // SendHistory uses packet 19; inspect original packets, including tags later overwritten by zero.
            var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
            foreach (var entry in history.List)
            {
                if (entry.HasCreateGame)
                {
                    actors.Add(entry.CreateGame.GameEntity.Id);
                    foreach (var player in entry.CreateGame.Players) actors.Add(player.Entity.Id);
                }
                if (entry.HasFullEntity || entry.HasShowEntity || entry.HasChangeEntity)
                {
                    var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.ChangeEntity;
                    actors.Add(entity.Entity);
                    Check(entity.Tags.All(t => t.Name < 31001 || t.Name > 31009), "no internal tags in FULL_ENTITY/SHOW_ENTITY/CHANGE_ENTITY");
                }
                if (entry.HasTagChange) Check(entry.TagChange.Tag < 31001 || entry.TagChange.Tag > 31009, "no internal TAG_CHANGE");
            }
        }
    }

    private static void RejectPrompt(Game game, NativeMatch match, NativeSession session, EntityChoices packet)
    {
        var before = game.SaveSnapshot(); int packets = session.Packets.Count;
        foreach (var attempt in new[] {
            (packet.Id + 1000, new List<int> { packet.Entities[0] }),
            (packet.Id, new List<int>()), (packet.Id, new List<int> { game.Player1.Hero.HeroPower.Id }),
            (packet.Id, new List<int> { packet.Entities[0], packet.Entities[0] }) })
        {
            bool rejected = false;
            try { match.SendChoices(attempt.Item1, attempt.Item2); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { rejected = true; }
            Check(rejected && before.SequenceEqual(game.SaveSnapshot()) && packets == session.Packets.Count &&
                match.HumanChoiceId == packet.Id, "stale/invalid answer preserves all state/RNG and emits no packets");
        }
    }

    private static void DescriptorHand()
    {
        foreach (string cardId in new[] { "CATA_697", "BE_036" })
        foreach (int count in new[] { 0, 1, 3 })
        {
            var (game, match, session) = Fixture(); var p = game.Player1;
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            for (int i = 0; i < count; i++) Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var nonFel = Generic.DrawCard(p, Cards.FromId("CS2_029"));
            if (count == 0 && cardId == "BE_036") { p.HandZone.Remove(nonFel); p.SetasideZone.Add(nonFel); }
            var source = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush();
            int packets = session.Packets.Count;
            session.Process(PlayCardTask.Any(p, source)); session.PublishDecision(p);
            if (p.Choice == null)
            {
                Check(!session.Packets.Skip(packets).Any(w => w.Id == 17), "empty selector emits no prompt"); continue;
            }
            var packet = Choice(session); AssertPrompt(game, session, packet); RejectPrompt(game, match, session, packet);
            Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 1 &&
                packet.Entities.All(id => session.Client.Entities[id].Get(GameTag.ZONE) == (int)Zone.HAND), "existing native hand-target convention only");
            if (cardId == "CATA_697") Check(!packet.Entities.Contains(nonFel.Id), "native offers omit non-Fel spell");
            int before = session.Packets.Count;
            match.SendChoices(packet.Id, new List<int> { packet.Entities[0] });
            Check(session.Packets.Skip(before).Count(w => w.Id == 13) == 1, "descriptor one valid answer produces one acknowledgment");
            Check(source.Card[(GameTag)1508] == 1 && session.Client.Entities[source.Id].Get((GameTag)1508) == 1,
                "wave-1 minion export hand-target marker remains after choice");
            Check(!Invariants.Compare(game, session.Client).Any(), "descriptor continuation projects copy/cost enchants");
        }
        foreach (string cardId in new[] { "CATA_697", "BE_036" })
        {
            var (game, match, session) = Fixture(true); var p = game.Player2;
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            var brann = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("LOE_077"));
            Generic.SummonBlock(game, brann, -1, null); game.DeathProcessingAndAuraUpdate();
            var first = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var second = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var source = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush(); session.Process(PlayCardTask.Any(p, source));
            AssertSelectorActions(game, p, new[] { first.Id, second.Id }, 91);
            session.Invoke("DriveUntilHumanInput");
            Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && session.OpposingPromptsHidden(p.PlayerId), "native bot answers new descriptor through TryAnswer; human sees only card backs");
        }
    }

    private static void AssertSelectorActions(Game game, Controller player, int[] offers, int seed)
    {
        Check(game.GetSelectorAnswer().SelectedOptionIds.Single() == offers[0], "selector fallback uses first frozen offer");
        Check(game.Resolution.SuspendedEffect.OfferEntityIds.SequenceEqual(offers), "selector fixture has exact frozen offers");
        var before = game.SaveSnapshot();
        var view = FairBotState.Create(game, player.PlayerId, seed, DeckStyle.Value);
        var expected = offers.Select(id => BotAction.From(ChooseTask.Pick(player, id)).ToString()).Order(StringComparer.Ordinal).ToArray();
        // Search must consider every visible offer; first-valid is only the emergency fallback.
        foreach (var actions in new[] { view.Actions, view.OrderedActions })
            Check(actions.Select(a => a.ToString()).Order(StringComparer.Ordinal).SequenceEqual(expected),
                $"selector search exposes every frozen offer exactly once and no ordinary actions: expected={string.Join(";", expected)} actual={string.Join(";", actions.Select(a => a.ToString()))}");
        foreach (var action in view.Actions) view.Apply(action);
        Check(before.SequenceEqual(game.SaveSnapshot()), "selector search branches preserve authoritative state");
    }

    private static void DecisionSpellNoEarlyOptions()
    {
        foreach (string cardId in new[] { "JAIL_206", "TIME_770" })
        {
            var (game, match, session) = Fixture(); var p = game.Player1;
            var source = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush(); session.PublishDecision(p);
            var options = (PowerAllOptions)typeof(NativeMatch).GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            var play = options.PowerOptionList.Single(o => o.MainOption?.EntityId == source.Id);
            Check(source.Card[(GameTag)1508] == 0 && source[(GameTag)1508] == 0 &&
                session.Client.Entities[source.Id].Get((GameTag)1508) == 0 && play.MainOption.Targets.Count == 0,
                "draw-decision spell play option has no hand-target requirement");
            match.SendOption(options.Index, options.PowerOptionList.IndexOf(play), 0, -1, 0);
            Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 1, "post-draw choice gets transient native hand marker");
            match.SendChoices(Choice(session).Id, new List<int> { p.Choice.Choices[0] });
            Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 0, "native play option followed by captured hand click completes and clears marker");
        }
        foreach (string cardId in new[] { "JAIL_206", "TIME_770" })
        {
            var (game, match, session) = Fixture(); var p = game.Player1;
            var source = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush();
            session.Process(PlayCardTask.Any(p, source)); session.PublishDecision(p);
            Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 1, "pending spell has transient hand marker before concede");
            match.Concede();
            Check(p.Choice == null && session.Client.Entities[source.Id].Get((GameTag)1508) == 0,
                "concede clears transient hand marker with decision");
        }
        foreach (string cardId in new[] { "JAIL_206", "TIME_770", "CATA_554" })
        foreach (int count in new[] { 0, 1, 3 })
        {
            var (game, match, session) = Fixture(); var p = game.Player1;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            var old = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            for (int i = 0; i < count; i++) p.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_182")));
            var draws = p.DeckZone.Reverse().Take(cardId == "JAIL_206" ? 3 : 2).Select(c => c.Id).ToArray();
            Minion target = null;
            if (cardId == "CATA_554")
            {
                Generic.DrawCard(p, Cards.FromId("CATA_484"));
                target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
                Generic.SummonBlock(game, target, -1, null);
                for (int i = 0; i < count; i++) Generic.SummonBlock(game,
                    (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")), -1, null);
                game.DeathProcessingAndAuraUpdate();
            }
            var source = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush();
            session.Process(PlayCardTask.Any(p, source, target)); session.PublishDecision(p);
            if (p.Choice == null)
            {
                Check(count == 0 && !session.Packets.Any(w => w.Id == 17), "empty spell selector has no prompt"); continue;
            }
            var packet = Choice(session); AssertPrompt(game, session, packet); RejectPrompt(game, match, session, packet);
            Check(!packet.Entities.Contains(old.Id), "CapturedDrawOfferSet excludes pre-existing same-card entity");
            if (cardId != "CATA_554")
                Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 1 && packet.Entities.SequenceEqual(draws) && packet.Entities.All(id => session.Client.Entities[id].Get(GameTag.ZONE) == (int)Zone.HAND),
                    "CapturedDrawOfferSet equals surviving physical draws in draw order");
            else Check(!packet.Entities.Contains(target.Id), "spell board selector excludes effective first target");
            int before = session.Packets.Count;
            match.SendChoices(packet.Id, new List<int> {packet.Entities[0]});
            if (cardId != "CATA_554")
                Check(session.Client.Entities[source.Id].Get((GameTag)1508) == 0 && source[(GameTag)1508] == 0 &&
                    game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().All(t => (int)t.Tag != 1508),
                    "hand-target marker resets after accepted captured hand click and stays out of engine history");
            Check(session.Packets.Skip(before).Count(w => w.Id == 13) == 1, "spell answer acknowledged exactly once");
            Check(!Invariants.Compare(game, session.Client).Any(), "spell completion and native cost/health projection agree");
            int after = session.Packets.Count; string state = HsSim.Canonical.State(game) + HsSim.Canonical.History(game) + HsSim.Canonical.Value(game.Random.CaptureState()); bool stale = false;
            try { match.SendChoices(packet.Id, new List<int> {packet.Entities[0]}); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale && session.Packets.Count == after && state == HsSim.Canonical.State(game) + HsSim.Canonical.History(game) + HsSim.Canonical.Value(game.Random.CaptureState()), "StaleChoiceProducesNoPackets for consumed spell choice");
            Check(session.Client.Entities.Values.All(e => e.Tags.Keys.All(t => t < 31001 || t > 31009)), "NoInternalDecisionTags for spells");
        }
    }

    private static void ControllerChangeProjection()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool full in new[] { false, true })
        {
            var (game, match, session) = Fixture(bot); var p = game.CurrentPlayer;
            if (full) while (!p.Opponent.HandZone.IsFull) Generic.DrawCard(p.Opponent, Cards.FromId("CS2_182"));
            var source = Generic.DrawCard(p, Cards.FromId("JAIL_206")); session.Flush();
            session.Process(PlayCardTask.Any(p, source));
            int selected = p.Choice.Choices[0];
            if (!bot)
            {
                session.PublishDecision(p); var packet = Choice(session);
                match.SendChoices(packet.Id, new List<int> {selected});
            }
            else
            {
                var decision = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] {selected}), out _), "bot gives selected card");
                session.Flush();
            }
            var given = game.IdEntityDic[selected]; var actor = session.Client.Entities[selected];
            Check(given.Controller == p.Opponent && actor.Get(GameTag.CONTROLLER) == p.Opponent.PlayerId &&
                actor.Get(GameTag.ZONE) == (int)(full ? Zone.GRAVEYARD : Zone.HAND), "controller change projects actual hand transfer/burn for both perspectives");
            if (bot && !full) Check(actor.CardId == given.Card.Id, "bot gift is revealed to human receiving hand");
            Check(!Invariants.Compare(game, session.Client).Any(), "transferred entity native state agrees for both perspectives");
        }
        Console.WriteLine("PASS spell decision transport: DecisionSpellNoEarlyOptions, CapturedDrawOfferSet, StaleChoiceProducesNoPackets, NoInternalDecisionTags, controller transfer both perspectives/full hand");
    }

    private static void SpellBots()
    {
        foreach (string id in new[] { "JAIL_206", "TIME_770", "CATA_554" })
        {
            var (game, match, session) = Fixture(true); var p = game.Player2;
            Minion target = null;
            if (id == "CATA_554")
            {
                Generic.DrawCard(p, Cards.FromId("CATA_484"));
                target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
                Generic.SummonBlock(game, target, -1, null);
                Generic.SummonBlock(game, (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")), -1, null);
                game.DeathProcessingAndAuraUpdate();
            }
            var source = Generic.DrawCard(p, Cards.FromId(id)); session.Flush(); session.Process(PlayCardTask.Any(p, source, target));
            int selected = p.Choice.Choices[0];
            AssertSelectorActions(game, p, p.Choice.Choices.ToArray(), 88);
            session.Invoke("DriveUntilHumanInput");
            Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && session.OpposingPromptsHidden(p.PlayerId), "native bot completes real spell selector; its prompt reaches the human only as card backs");
            if (id == "JAIL_206") Check(game.IdEntityDic[selected].Controller == p.Opponent, "native bot gives first captured draw");
            if (id == "CATA_554") Check(((Minion)game.IdEntityDic[selected]).Health == 1, "native bot sets selected board minion Health");
        }
    }

    private static readonly DecisionBattlecryTask BoardRecipe = new DecisionBattlecryTask("transport/selector/board/v1",
        StateTaskList.SelectRepeated("transport/selector/board-repeat/v1",
            new SelectorSpec(SelectorSource.Board, SelectorSide.Enemy, includeHeroes: true, groupKey: "board",
                excludeEarlierPicks: true, excludePlayTarget: true), 2, EffectContext.Task(ctx => TaskState.COMPLETE)));
    private static readonly DecisionBattlecryTask MinionRecipe = new DecisionBattlecryTask("transport/selector/minions/v1",
        new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board, SelectorSide.Both, cardType: CardType.MINION)));

    private static void DescriptorBoard()
    {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        int handTag = card.Tags[(GameTag)1508];
        try
        {
            card.Tags.Remove((GameTag)1508);
            foreach (var recipe in new[] { BoardRecipe, MinionRecipe })
            {
                card.Power = new Power { PowerTask = recipe };
                var (game, match, session) = Fixture(); var p = game.Player1;
                var first = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")); Generic.SummonBlock(game, first, -1, null);
                var second = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")); Generic.SummonBlock(game, second, -1, null);
                var location = SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("JAIL_877"), zone: p.Opponent.BoardZone);
                game.DeathProcessingAndAuraUpdate();
                var source = Generic.DrawCard(p, card); session.Flush(); session.Process(PlayCardTask.Any(p, source)); session.PublishDecision(p);
                var packet = Choice(session); AssertPrompt(game, session, packet); RejectPrompt(game, match, session, packet);
                Check(!packet.Entities.Contains(location.Id) && packet.Entities.Contains(p.Opponent.Hero.Id) == (recipe == BoardRecipe) &&
                    session.Client.Entities[source.Id].Get((GameTag)1508) == 0, "board includes hero only when allowed; excludes locations; no hand tag added");
                Check(game.GetSelectorAnswer().SelectedOptionIds.Single() == packet.Entities.Min(), "board bot stable entity-ID tie break");
                first.IsImmune = true; session.Flush();
                typeof(Character).GetField("_modifiedHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(first, null);
                typeof(Character).GetField("_modifiedStealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(first, null);
                var cold = game.SaveSnapshot(); int coldPackets = session.Packets.Count;
                bool invalidated = false;
                try { match.SendChoices(packet.Id, new List<int> { first.Id }); }
                catch (InvalidOperationException) { invalidated = true; }
                Check(invalidated && cold.SequenceEqual(game.SaveSnapshot()) && coldPackets == session.Packets.Count &&
                    match.HumanChoiceId == packet.Id, "invalidated board answer preserves cold caches, prompt and packets");
                first.IsImmune = false; session.Flush();
                int packets = session.Packets.Count;
                match.SendChoices(packet.Id, new List<int> { first.Id });
                Check(session.Packets.Skip(packets).Count(w => w.Id == 13) == 1, "board answer acknowledgment once");
                if (recipe == BoardRecipe)
                {
                    var next = Choice(session); AssertPrompt(game, session, next);
                    var saved = game.SaveSnapshot(); int packetCount = session.Packets.Count;
                    bool stale = false;
                    try { match.SendChoices(packet.Id, new List<int> { first.Id }); }
                    catch (InvalidOperationException) { stale = true; }
                    Check(stale && saved.SequenceEqual(game.SaveSnapshot()) && session.Packets.Count == packetCount &&
                        match.HumanChoiceId == next.Id, "consumed answer cannot replay or disturb next prompt");
                    Check(!next.Entities.Contains(first.Id) && next.Entities.Contains(second.Id), "board repeated offer excludes earlier physical pick");
                    match.SendChoices(next.Id, new List<int> { second.Id });
                }
            }
        }
        finally { card.Power = original; card.Tags[(GameTag)1508] = handTag; }
    }

    private static readonly DecisionBattlecryTask FixedRecipe = new DecisionBattlecryTask("transport/decision/fixed/v1",
        new DiscoverThenContinueTask("UNG_999t6", "UNG_999t13", "UNG_999t14"),
        EffectContext.Task(ctx => ctx.GiveBuff(ctx.SourceId, ctx.LastChosenCardId + "e")));

    private static void FixedPrivacy()
    {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = FixedRecipe };
            foreach (bool bot in new[] { false, true })
            {
                var (game, match, session) = Fixture(bot); var p = game.CurrentPlayer;
                var source = Generic.DrawCard(p, card); session.Flush();
                session.Process(PlayCardTask.Any(p, source));
                var offers = p.Choice.Choices.ToArray();
                Check(offers.All(id => session.Client.Entities[id].CardId == (bot ? "" : game.IdEntityDic[id].Card.Id)), "fixed offers shown only to owner");
                if (!bot)
                {
                    session.PublishDecision(p); var packet = Choice(session);
                    Check(packet.ChoiceType == 2 && packet.CountMin == 1 && packet.CountMax == 1 && packet.Entities.SequenceEqual(offers), "fixed GENERAL packet");
                    match.SendChoices(packet.Id, new List<int>{ offers[0] });
                }
                else session.Process(ChooseTask.Pick(p, offers[0]));
                Check(((Minion)source).HasTaunt && session.Client.Entities[source.Id].Get(GameTag.TAUNT) == 1, "public branch consequence visible");
                if (bot) Check(offers.All(id => session.Client.Entities[id].CardId == ""), "private offers remain hidden after choice clears");
            }
        }
        finally { card.Power = original; }
    }

    private static void Bot()
    {
        var (game, match, session) = Fixture(true); var p = game.Player2;
        var source = Generic.DrawCard(p, Cards.FromId("EX1_319"));
        session.Flush(); session.Process(PlayCardTask.Any(p, source));
        var before = game.SaveSnapshot();
        var search = FairBotState.Create(game, p.PlayerId, 91, DeckStyle.Value);
        Check(search.CanExpand && search.Actions.Length == p.Choice.Choices.Count, "fair search enumerates hand selections");
        foreach (var action in search.Actions) Check(search.Apply(action).CanExpand, "fair search resumes selected branch");
        Check(before.SequenceEqual(game.SaveSnapshot()), "search choices leave authoritative state unchanged");
        session.Invoke("DriveUntilHumanInput");
        Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && game.CurrentPlayer == game.Player1, "fallback bot answers compulsory hand prompt");
        Check(p.DeckZone.Any(c => c.Cost == 3), "bot applies selected hand discount");
        Check(session.OpposingPromptsHidden(p.PlayerId), "bot hand selections reach the human only as card backs");
    }
}
