using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;
using NativeHistory = PegasusGame.PowerHistory;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class RewindTransportProof
{
    private static void Check(bool ok, string message) => SelfChecks.Check(ok, "Rewind " + message);
    private static NativeHistory History(WirePacket wire)
    {
        var packet = new NativeHistory();
        using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
        packet.Deserialize(stream);
        return packet;
    }
    internal static void Run()
    {
        foreach (int owner in new[] { 1, 2 }) PresentationStateReset(owner);
        foreach (var opponent in new[] { CardClass.ROGUE, CardClass.MAGE })
        foreach (bool rewind in new[] { true, false }) HeraldFallbackReset(opponent, rewind);
        MinionAge();
        CeaseToExistBoardOrder();
        EndpointZeroTags();
        GeneratedResetEndpointTags();
        foreach (string card in new[] { "CS2_231", "CORE_REV_990" })
        foreach (int position in new[] { 0, 1, 2 }) PlacedLocalChange(card, position);
        var localChangeFailures = new List<string>();
        PlayedSpellLocalChange(false, false, "EX1_277");
        foreach (bool morchie in new[] { true, false })
        foreach (bool rewind in new[] { true, false })
        {
            try { PlayedSpellLocalChange(morchie, rewind); }
            catch (InvalidOperationException error) { localChangeFailures.Add(error.Message); }
        }
        Check(localChangeFailures.Count == 0, string.Join("\n", localChangeFailures));
        foreach (int owner in new[] { 1, 2 })
        foreach (bool rewind in new[] { false, true }) BronzeHeroPower(owner, rewind);
        HiddenClocksworthReset(1); HiddenClocksworthReset(2);
        ResetModel();
        GrimlynxTextAndHandBuffSnapshot(1); GrimlynxTextAndHandBuffSnapshot(2);
        CardStateReset(1); CardStateReset(2);
        foreach (int owner in new[] { 1, 2 })
        foreach (bool remove in new[] { false, true }) SurvivingControllerAuras(owner, remove);
        SecretReset(false); SecretReset(true);
        ControllerCredit(1); ControllerCredit(2);
        foreach (var scenario in new[] { ("TIME_001", 0), ("TIME_001", 1), ("TIME_038", 2), ("TIME_038", 3) })
        foreach (int owner in new[] { 1, 2 })
            Scenario(scenario.Item1, scenario.Item2, owner);
        Console.WriteLine("PASS Rewind reset mutations: missing mana zero, hidden identity, non-priority zero rejected");
        Console.WriteLine("PASS Rewind transport: Keep/Rewind, Clocksworth RRK/RRR, either side, ack/reset/replay/options, stale input, privacy, rewards");
    }

    // Cross-class Herald cards in a Priest's hand across a Rewind reset: the reset snapshot restores each card's own class (entity
    // tag 4534) and AppendHeraldState re-sends the source-independent class on the CONTROLLER (the only tag the client text builder
    // reads), none stored in engine state. Opponent Rogue: controller 7 (Rogue); opponent Mage: controller 0, cards keep their own class.
    private static void HeraldFallbackReset(CardClass opponent, bool rewind)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.PRIEST, Player2HeroClass = opponent,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        var rite = Generic.DrawCard(player, Cards.FromId("CATA_785"));
        var fel = Generic.DrawCard(player, Cards.FromId("CATA_530"));
        var envoy = Generic.DrawCard(player, Cards.FromId("CATA_722"));
        var source = Generic.DrawCard(player, Cards.FromId("TIME_003"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(player);
        int controllerExpected = opponent == CardClass.ROGUE ? (int)CardClass.ROGUE : 0;
        void Verify(string stage)
        {
            Check(session.Client.Entities[player.Id].Get(HeraldRules.Profile) == controllerExpected,
                $"{stage} {opponent}: controller 4534 is {controllerExpected}, shown {session.Client.Entities[player.Id].Get(HeraldRules.Profile)}");
            foreach (var (card, own) in new[] { (rite, (int)CardClass.ROGUE), (fel, 14), (envoy, controllerExpected) })
                Check(session.Client.Entities[card.Id].Get(HeraldRules.Profile) == own,
                    $"{stage} {opponent}: {card.Card.Id} entity 4534 is {own}, shown {session.Client.Entities[card.Id].Get(HeraldRules.Profile)}");
            Check(player[HeraldRules.Profile] == 0, "fallback class is never stored on the engine controller");
        }
        Verify("before");
        session.Process(PlayCardTask.Any(player, source)); session.PublishDecision(player);
        Check(game.Rewind != null, "Portal Vanguard opens the Herald reset fixture");
        session.Process(ChooseTask.Pick(player, rewind ? game.Rewind.RewindEntityId : game.Rewind.KeepEntityId));
        session.Flush();
        Verify(rewind ? "after reset" : "after keep");
        Console.WriteLine($"PASS Rewind Herald fallback tags: opponent={opponent}; rewind={rewind}");
    }

    private static void PresentationStateReset(int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        // Match #8d40c122: rejected FIR_914 Discover offer remains SETASIDE across TIME_003's checkpoint.
        var rejected = Entity.FromCard(player, Cards.FromId("FIR_914"), zone: player.SetasideZone);
        rejected[Generic.PrivateDecisionOfferTag] = player.PlayerId;
        var held = Generic.DrawCard(player, Cards.FromId("FIR_911"));
        var source = Generic.DrawCard(player, Cards.FromId("TIME_003"));
        var active = Entity.FromCard(player, Cards.FromId("CS2_231"), zone: player.BoardZone);
        active[(GameTag)2505] = 1; active[(GameTag)2600] = 2;
        var inactive = Entity.FromCard(player, Cards.FromId("CS2_231"), zone: player.BoardZone);
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(player);
        session.Process(PlayCardTask.Any(player, source)); session.PublishDecision(player);
        Check(game.Rewind != null, "Portal Vanguard opens presentation reset fixture");
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        // Discarded attempt changed real loop state: reset must emit both on and off through native tag changes.
        active[(GameTag)2505] = 0; active[(GameTag)2600] = 0;
        inactive[(GameTag)2505] = 1; inactive[(GameTag)2600] = 3;
        held[(GameTag)2505] = 1; held[(GameTag)2600] = 3;
        // These newer tags have no engine card writer yet; author its wire events explicitly for the control.
        foreach (var entity in new[] { active, inactive, held })
            foreach (int tag in new[] { 2505, 2600 })
                game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(entity.Id, (GameTag)tag, entity[(GameTag)tag]));
        session.Flush();
        int cursor = session.Packets.Count;
        session.Client.ResetApplied = model => {
            foreach (int id in new[] { rejected.Id, held.Id })
                foreach (int tag in new[] { 2505, 2600 })
                    Check(model.Entities[id].Get((GameTag)tag) == 0,
                        $"definition-only fire absent on entity {id} tag {tag} for owner {owner}");
            Check(model.Entities[active.Id].Get((GameTag)2505) == 1 && model.Entities[active.Id].Get((GameTag)2600) == 2
                && model.Entities[inactive.Id].Get((GameTag)2505) == 0 && model.Entities[inactive.Id].Get((GameTag)2600) == 0,
                "real instance loop state restored, discarded loop cleared");
            Check(owner == 1 || model.Entities[rejected.Id].CardId == "" && model.Entities[held.Id].CardId == "",
                "opposing offer and hand stay anonymous");
            var bad = model.Copy(); bad.Entities[rejected.Id].Tags[2505] = 1;
            Check(Invariants.CompareReset(expected, bad).Any(v => v.Entity == rejected.Id && v.Tag == "RESET_ACTOR_STATE"),
                "oracle rejects fabricated Immolate state on retired offers");
        };
        session.Process(ChooseTask.Pick(player, game.Rewind.RewindEntityId));
        if (owner == 1)
            foreach (int tag in new[] { 2505, 2600 })
                Check(match.SnapshotVisibleTransportTags(game.IdEntityDic[held.Id]).GetValueOrDefault(tag) == 0,
                    "endpoint audit must not resurrect cleared definition-only fire");
        var history = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => History(w).List).ToArray();
        foreach (int id in new[] { rejected.Id, held.Id })
        {
            var snapshot = history.Single(e => e.HasFullEntity && e.FullEntity.Entity == id).FullEntity;
            Check(!snapshot.Tags.Any(t => t.Name is 2505 or 2600), "reset wire never starts definition-only fire");
            Check(!history.Any(e => e.HasTagChange && e.TagChange.Entity == id && e.TagChange.Tag is 2505 or 2600
                && e.TagChange.Value != 0), "no follow-up tag change starts definition-only fire");
        }
        Check(history.Any(e => e.HasTagChange && e.TagChange.Entity == inactive.Id && e.TagChange.Tag == 2505
            && e.TagChange.Value == 0), "native off transition explicitly clears discarded loop");
        // A reset may freeze effective values, but must not import additional definition-only visual families.
        var effective = new[] { GameTag.COST, GameTag.ARMOR, GameTag.SPELLPOWER, GameTag.EXHAUSTED,
            GameTag.LOCATION_ACTION_COOLDOWN, HeraldRules.Profile };
        foreach (var entity in expected.IdEntityDic.Values)
        {
            var ordinary = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity).Entity.Tags;
            var reset = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true).Entity.Tags;
            Check(reset.Keys.All(tag => ordinary.ContainsKey(tag) || entity.NativeTags.ContainsKey(tag) || effective.Contains(tag)),
                "reset snapshot adds no arbitrary definition state: " + entity.Card.Id);
        }
        Console.WriteLine($"PASS Rewind PresentationStateReset: owner={owner}; rejected Smoldering, held card, native on/off, privacy, mutation oracle");
    }

    private static void MinionAge()
    {
        foreach (int first in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = first, SkipMulligan = true,
                History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
            game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
            var minion = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
            var other = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player.Opponent, Cards.FromId("CS2_182"), zone: player.Opponent.BoardZone);
            int id = minion.Id, otherId = other.Id;
            var charge = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("CS2_182"));
            var rush = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("CS2_182"));
            charge[GameTag.CHARGE] = 1; rush[GameTag.RUSH] = 1;
            player.BoardZone.Add(charge); player.BoardZone.Add(rush);
            NativeSession session = null;
            var match = new NativeMatch((packetId, packet) => session.Receive(packetId, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); session.PublishDecision(player);
            Check(minion[GameTag.NUM_TURNS_IN_PLAY] == 0 && session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 0,
                "new minion starts asleep with zero age");
            Check(charge[GameTag.NUM_TURNS_IN_PLAY] == 0 && rush[GameTag.NUM_TURNS_IN_PLAY] == 0 &&
                !charge.IsExhausted && !rush.IsExhausted && rush.AttackableByRush,
                "new Charge and Rush minions keep their readiness exceptions");
            session.Process(EndTurnTask.Any(player));
            Check(minion[GameTag.NUM_TURNS_IN_PLAY] == 1 && other[GameTag.NUM_TURNS_IN_PLAY] == 1 &&
                session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 1 && session.Client.Entities[otherId].Get(GameTag.NUM_TURNS_IN_PLAY) == 1,
                "both boards age at each turn boundary and transmit age");
            session.Process(EndTurnTask.Any(player.Opponent));
            Check(minion[GameTag.NUM_TURNS_IN_PLAY] == 2 && player.Options().Any(o => o.Source?.Id == id),
                "aged friendly minion retains legal attack");
            foreach (var copy in new[] { game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
                Check(copy.IdEntityDic[id][GameTag.NUM_TURNS_IN_PLAY] == 2, "clone and save retain age");
            var control = new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET);
            control.Process(game, player, player.Hero, other); session.Flush();
            Check(other[GameTag.NUM_TURNS_IN_PLAY] == 2 && session.Client.Entities[otherId].Get(GameTag.NUM_TURNS_IN_PLAY) == 2,
                "control change preserves continuous board age");
            control.Process(game, player.Opponent, player.Opponent.Hero, other); session.Flush();
            minion = (SabberStoneCore.Model.Entities.Minion)Generic.ChangeEntityBlock(player, minion, Cards.FromId("CS2_120"), true);
            session.Flush();
            Check(minion[GameTag.NUM_TURNS_IN_PLAY] == 2 && session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 2,
                "same-entity transform preserves board age");
            Check(Generic.ReturnToHandBlock(player, minion), "return aged minion to hand"); session.Flush();
            Check(session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 0, "bounce clears native age");
            session.PublishDecision(player); session.Process(PlayCardTask.Any(player, minion));
            Check(game.IdEntityDic[id][GameTag.NUM_TURNS_IN_PLAY] == 0 && session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 0,
                "replayed minion starts a new board residence");
            var rewind = Generic.DrawCard(player, Cards.FromId("TIME_001")); session.Flush(); session.PublishDecision(player);
            session.Process(PlayCardTask.Any(player, rewind)); session.PublishDecision(player);
            var transaction = game.Rewind;
            Check(transaction != null, "age fixture offers rewind");
            session.Process(ChooseTask.Pick(player, transaction.RewindEntityId));
            Check(game.IdEntityDic[id][GameTag.NUM_TURNS_IN_PLAY] == 0 && session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 0 &&
                game.IdEntityDic[otherId][GameTag.NUM_TURNS_IN_PLAY] == 2 && session.Client.Entities[otherId].Get(GameTag.NUM_TURNS_IN_PLAY) == 2,
                "rewind restores old and newly replayed minion age");
            player = game.CurrentPlayer;
            var dormant = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("TIME_046"), zone: player.BoardZone);
            dormant[GameTag.NUM_TURNS_IN_PLAY] = 2; session.Flush();
            Check(DormantRules.Awaken(dormant), "age fixture dormant minion awakens"); session.Flush();
            Check(dormant[GameTag.NUM_TURNS_IN_PLAY] == 0 && session.Client.Entities[dormant.Id].Get(GameTag.NUM_TURNS_IN_PLAY) == 0,
                "awakening starts new sleep period");
            new SabberStoneCore.Tasks.SimpleTasks.ExtraTurnEffectTask().Process(game, player, player.Hero, null);
            session.Process(EndTurnTask.Any(player));
            Check(game.CurrentPlayer.Id == player.Id && game.IdEntityDic[id][GameTag.NUM_TURNS_IN_PLAY] == 1 &&
                session.Client.Entities[id].Get(GameTag.NUM_TURNS_IN_PLAY) == 1, "extra turn advances age and native state");
            var replaced = (SabberStoneCore.Model.Entities.Minion)game.IdEntityDic[id];
            var replacement = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("CS2_182"));
            player.BoardZone.Replace(replaced, replacement);
            Check(replacement[GameTag.NUM_TURNS_IN_PLAY] == 1, "replacement transform preserves board age");
        }
        Console.WriteLine("PASS Minion age: both boards, turns, client, clone/save, transform, control, bounce, awaken and Rewind");
    }

    private static void CeaseToExistBoardOrder()
    {
        foreach (int owner in new[] { 1, 2 })
        foreach (bool rewind in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = 1209393971, StartPlayer = owner,
                SkipMulligan = true, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
            game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
            foreach (string card in new[] { "CATA_474", "TIME_015", "JAIL_516", "CORE_ONY_022", "CS2_101t", "TSC_076t3", "TSC_076t2" })
                Entity.FromCard(player.Opponent, Cards.FromId(card), zone: player.Opponent.BoardZone);
            var source = Generic.DrawCard(player, Cards.FromId("TIME_433"));
            game.DeathProcessingAndAuraUpdate();
            // L11 replay cursor: first kill JAIL_516, retry kill the Recruit, independent of opening draws.
            game.Random.RestoreState(new RngState(6341384012711119017L, -481930079782541527L, 76));
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); session.PublishDecision(player);
            var baseline = player.Opponent.BoardZone.Select(m => m.Id).ToArray();
            Check(session.Client.BoardOrder(player.Opponent.PlayerId).SequenceEqual(baseline), "Cease fixture actor order");
            session.Process(PlayCardTask.Any(player, source));
            session.PublishDecision(player);
            int victim = baseline.Single(id => game.IdEntityDic[id].Zone.Type == EngineZone.GRAVEYARD);
            Check(victim == baseline[2], "Cease first attempt kills Scarlet Recruiter as in L11");
            Check(session.Client.BoardOrder(player.Opponent.PlayerId).SequenceEqual(baseline.Where(id => id != victim)), "Cease first death compacts actors");
            var transaction = game.Rewind;
            Check(transaction != null, "Cease opens timeline choice");
            if (rewind) session.Client.ResetApplied = model => Check(model.BoardOrder(player.Opponent.PlayerId).SequenceEqual(baseline),
                $"Cease restored actor order owner={owner}, victim={victim}: expected={string.Join(',', baseline)} shown={string.Join(',', model.BoardOrder(player.Opponent.PlayerId))}");
            session.Process(ChooseTask.Pick(player, rewind ? transaction.RewindEntityId : transaction.KeepEntityId));
            var expected = baseline.Where((id, index) => index != (rewind ? 4 : 2)).ToArray();
            Check(game.CurrentPlayer.Opponent.BoardZone.Select(m => m.Id).SequenceEqual(expected), "Cease retry kills Recruit as in L11; Keep retains first death");
            Check(session.Client.BoardOrder(player.Opponent.PlayerId).SequenceEqual(expected), "Cease final actor order after death compaction");
            Check(!Invariants.Compare(game, session.Client).Any(), "Cease entity tags agree too");
        }
        Console.WriteLine("PASS Cease to Exist board order: Keep/Rewind, either side; restored actors and retry death compaction");
    }
    private static void PlayedSpellLocalChange(bool morchie, bool rewind, string card = "TIME_001")
    {
        var game = new Game(new GameConfig { RandomSeed = 1903548393, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        if (morchie) Generic.SummonBlock(game,
            (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("END_036")), -1, player.Hero);
        var source = Generic.DrawCard(player, Cards.FromId(card));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(player);
        Check(session.Client.Entities[source.Id].Get(GameTag.ZONE_POSITION) > 0, "fixture starts in a real hand slot");
        var before = session.Client.Copy();
        session.Client.PredictLocalSpellPlay(source.Id);
        int cursor = session.Packets.Count;
        session.Process(PlayCardTask.Any(player, source));
        var play = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => History(w).List).ToList();
        if (card != "TIME_001")
        {
            Check(!session.Client.HasUnresolvedLocalChange, "ordinary untargeted spell also resolves local prediction");
            return;
        }
        session.PublishDecision(player);
        Check(game.Rewind != null && game.Rewind.KeepBoth == morchie, "fixture offers expected Rewind branch");
        bool unresolvedAtOffer = session.Client.HasUnresolvedLocalChange;
        int pick = rewind ? game.Rewind.RewindEntityId : game.Rewind.KeepEntityId;
        int keep = game.Rewind.KeepEntityId;
        Check(session.Client.Entities.ContainsKey(pick), "choice entity exists before native acknowledgment");
        int choiceId = match.HumanChoiceId;
        session.Client.ClickRewindChoice(choiceId, pick);
        Check(session.Client.InputEnabled == !rewind && session.Client.InputRestoreCount == 0,
            "native Rewind click disables input; Keep leaves input enabled");
        var awaitingAnswer = session.Client.Copy();
        int answerCursor = session.Packets.Count;
        match.SendChoices(match.HumanChoiceId, new List<int> { pick });
        Check(session.Client.InputEnabled && session.Client.InputRestoreCount == (rewind ? 1 : 0),
            $"NativeInputLifecycle Morchie={morchie} Rewind={rewind}: inputEnabled={session.Client.InputEnabled}; restoreCalls={session.Client.InputRestoreCount}; expected enabled, restoreCalls={(rewind ? 1 : 0)}");
        var answerPackets = session.Packets.Skip(answerCursor).ToArray();
        Check(answerPackets[0].Id == 13, "native input acknowledgment precedes continuation");
        var acknowledged = new EntitiesChosen();
        using (var stream = new MemoryStream(Convert.FromBase64String(answerPackets[0].Base64))) acknowledged.Deserialize(stream);
        Check(acknowledged.ChooseEntities.Id == choiceId && acknowledged.PlayerId == 1 &&
            acknowledged.ChooseEntities.Entities.SequenceEqual(new[] { morchie && rewind ? keep : pick }),
            "keep-both acknowledges retained timeline; ordinary answers echo selection");
        var ackOnly = awaitingAnswer.Copy();
        ackOnly.Apply(acknowledged);
        Check(ackOnly.InputEnabled == (morchie || !rewind) &&
            ackOnly.InputRestoreCount == (morchie && rewind ? 1 : 0),
            "keep-both restores at acknowledgment; plain Rewind waits for reset completion");
        var answerHistory = answerPackets.Where(w => w.Id == 19).SelectMany(w => History(w).List).ToList();
        Check(answerHistory.Count(e => e.HasResetGame) == (rewind && !morchie ? 1 : 0),
            "only plain Rewind resets the game");
        Check(game.CurrentPlayer.UsedMana == 3 && game.CurrentPlayer.Opponent.Hero.Damage == (morchie && rewind ? 12 : 6)
            && game.IdEntityDic[source.Id].Zone.Type == EngineZone.GRAVEYARD,
            "actual Rewind answer retains both knife outcomes only with Morchie; one payment and resolved source");
        if (rewind)
        {
            // Remove the actual restore-bearing wire response: no Keep ack for Morchie, no reset for plain Rewind.
            var mutant = awaitingAnswer.Copy();
            acknowledged.ChooseEntities.Entities = new List<int> { pick };
            mutant.Apply(acknowledged);
            if (morchie) mutant.Apply(new NativeHistory { List = answerHistory });
            Check(!mutant.InputEnabled && mutant.InputRestoreCount == 0,
                "native lifecycle rejects missing restore path");
        }
        Check(!session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => History(w).List)
            .Any(e => e.HasPowerStart && (e.PowerStart.Source == pick || e.PowerStart.Target == pick)
                || e.HasMetaData && e.MetaData.Info.Contains(pick)), "answered choice has no dangling history reference");
        Check(!unresolvedAtOffer && !session.Client.HasUnresolvedLocalChange,
            $"PlayedSpellLocalChange Morchie={morchie} Rewind={rewind}: unresolved HAND->PLAY prediction for TIME_001; expected position=0, shown={session.Client.Entities[source.Id].Get(GameTag.ZONE_POSITION)}");
        Check(game.Rewind == null && session.Packets.Last().Id == 14, "answer returns native options");
        if (morchie && rewind)
        {
            bool Clear(PowerHistoryData e) => e.HasTagChange && e.TagChange.Entity == source.Id
                && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value == 0;
            Check(play.Any(Clear), "native spell play clears hand position");
            foreach (string mutation in new[] { "missing position", "wrong position", "missing zone", "nested position" })
            {
                var model = before.Copy(); model.PredictLocalSpellPlay(source.Id);
                var entries = play.Where(e => !(Clear(e) && mutation != "missing zone") && !(mutation == "missing zone" && e.HasTagChange
                    && e.TagChange.Entity == source.Id && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)EngineZone.PLAY)).ToList();
                var position = new PowerHistoryData { TagChange = new() { Entity = source.Id,
                    Tag = (int)GameTag.ZONE_POSITION, Value = mutation == "wrong position" ? 5 : 0 } };
                int power = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                    && (int)e.PowerStart.Type == (int)BlockType.POWER);
                if (mutation == "wrong position") entries.Insert(power, position);
                if (mutation == "nested position") entries.Insert(power + 1, position);
                model.Apply(new NativeHistory { List = entries });
                Check(model.HasUnresolvedLocalChange, "ZoneMgr model rejects " + mutation);
            }
        }
        Console.WriteLine($"PASS PlayedSpellLocalChange: Morchie={morchie}; Rewind={rewind}; prediction acknowledged before offer and after answer");
        Console.WriteLine($"PASS NativeInputLifecycle: Morchie={morchie}; Rewind={rewind}; inputEnabled={session.Client.InputEnabled}; restoreCalls={session.Client.InputRestoreCount}");
    }
    private static void PlacedLocalChange(string card, int position)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        for (int i = 0; i < 2; i++) Generic.SummonBlock(game,
            (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("CS2_231")), -1, player.Hero);
        var source = Generic.DrawCard(player, Cards.FromId(card));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(player);
        session.Client.PredictLocalPlay(source.Id, position + 1);
        session.Process(player.Options().OfType<PlayCardTask>().Single(t => t.Source == source && t.ZonePosition == position));
        Check(!session.Client.HasUnresolvedLocalChange, $"ordinary {card} play at {position + 1} resolves local prediction");
        Console.WriteLine($"PASS PlacedLocalChange: {card}; position={position + 1}; history resolved");
    }
    private static void GrimlynxTextAndHandBuffSnapshot(int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        var grimlynx = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("BE_850t"), zone: player.BoardZone);
        grimlynx.AttackDamage = 4; grimlynx.Health = 10; grimlynx.Damage = 3;
        var source = Generic.DrawCard(player, Cards.FromId("CS2_231"));
        source[GameTag.REWIND] = 1;
        Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_087e"), player.Hero, source);
        Check(game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>().Any(), "fixture captures real hand-buff history");
        var detached = SabberStoneCore.Model.Entities.Enchantment.GetInstance(player, player.Hero, grimlynx, Cards.FromId("CS2_087e"));
        detached.Remove(); detached[GameTag.ATTACHED] = 0;
        Check(SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(detached).Entity.Tags[GameTag.ATTACHED] == 0,
            "reset snapshot preserves explicit detach despite retained runtime target reference");
        game.DeathProcessingAndAuraUpdate();
        var bytes = game.SaveSnapshot();
        Check(bytes.SequenceEqual(Game.LoadSnapshot(bytes).SaveSnapshot()), "new sub-spell history types round-trip in snapshot");
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.Process(PlayCardTask.Any(player, source));
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        session.Client.ResetApplied = model => {
            var shown = model.Entities[grimlynx.Id];
            Check(shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 7 && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 4,
                "Grimlynx reset text uses restored Health/Attack");
            shown.Tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] = 5;
            Check(Invariants.CompareReset(expected, model).Any(v => v.Entity == grimlynx.Id && v.Tag == "TAG_SCRIPT_DATA_NUM_1"),
                "reset oracle rejects printed default instead of restored effective text");
            shown.Tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] = 7;
        };
        session.Process(ChooseTask.Pick(player, game.Rewind.RewindEntityId));
        Check(!Invariants.Compare(game, session.Client).Any(), "Grimlynx replay preserves derived text and stats");
        Console.WriteLine($"PASS Rewind hand-buff snapshot and Grimlynx text reset: owner={owner}");
    }
    private static void HiddenClocksworthReset(int owner)
    {
        // rewind/client-ui-flow: the client filters CHANGE_ENTITY to TIME_038 inside GAME_RESET.
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var p = game.CurrentPlayer; p.BaseMana = 10;
        var clocksworth = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId("TIME_038"));
        Generic.SummonBlock(game, clocksworth, -1, p.Hero);
        var source = Generic.DrawCard(p, Cards.FromId("TIME_001"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush();
        session.Process(PlayCardTask.Any(p, source));
        Check(game.Rewind != null, "Clocksworth fixture has pending reset");
        game.PowerHistory.Last.Clear();
        new SabberStoneCore.Tasks.SimpleTasks.MoveToDeck(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(game, p, p.Hero, clocksworth);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(session.Client.Entities[clocksworth.Id].CardId == "", "candidate hides formerly public Clocksworth");
        int cursor = session.Packets.Count;
        session.Process(ChooseTask.Pick(p, game.Rewind.RewindEntityId));
        Check(session.Client.Entities[clocksworth.Id].CardId == "TIME_038" &&
            game.IdEntityDic[clocksworth.Id].Zone.Type == EngineZone.PLAY && !Invariants.Compare(game, session.Client).Any(),
            "reset restores base Clocksworth identity on either side");
        Check(session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => History(w).List)
            .Any(e => e.HasShowEntity && e.ShowEntity.Entity == clocksworth.Id && e.ShowEntity.Name == "TIME_038"),
            "reset emits Clocksworth reveal surviving client transform filter");
        Console.WriteLine($"PASS HiddenClocksworthReset: owner={owner}; hidden candidate restored before replay");
    }
    private static void BronzeHeroPower(int owner, bool rewind)
    {
        // Lean_ imbue-24: reuse B reset protocol for the HP source, both native perspectives.
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.ROGUE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var p = game.CurrentPlayer; p.BaseMana = 10;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        ImbueRules.Imbue(p, p.Hero, 2); int source = p.Hero.HeroPower.Id;
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(p);
        if (owner == 1)
        {
            var options = Field<SabberStoneCore.Kettle.PowerAllOptions>(match, "humanOptions");
            Check(options.PowerOptionList.Any(o => o.MainOption?.EntityId == source && o.MainOption.PowerKeyword == 0),
                "Bronze ordinary HP option has no special keyword");
        }
        int first = session.Packets.Count;
        session.Process(HeroPowerTask.Any(p)); // Source intentionally null: host resolves authoritative HP.
        Check(game.Rewind is { IsHeroPowerActivation: true } && match.RewardFacts.DistinctCardsPlayed == 0,
            "Bronze activation creates checkpoint without card reward");
        var initialHistory = session.Packets.Skip(first).Where(w => w.Id == 19).SelectMany(w => History(w).List).ToArray();
        // Shared payment emits one engine cost tag before PLAY; NativeMatch must not synthesize another.
        Check(initialHistory.Count(e => e.HasTagChange && e.TagChange.Entity == source &&
            e.TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND && e.TagChange.Value == 1) == 1,
            "HP activation has no duplicate hand cost synthesis");
        session.PublishDecision(p);
        var offer = new EntityChoices();
        using (var stream = new MemoryStream(Convert.FromBase64String(session.Packets.Last(w => w.Id == 17).Base64))) offer.Deserialize(stream);
        Check(offer.Source == source && offer.PlayerId == owner && offer.Entities.Count == 2, "Bronze offer HP source and owner");
        int discarded = p.HandZone.Single().Id;
        int attachment = p.HandZone.Single().AppliedEnchantments.Single().Id;
        var beforeReset = session.Client.Copy();
        var preAnswer = session.Client.Copy();
        var t = game.Rewind; var expected = Game.LoadSnapshot(t.Baseline); int resets = 0;
        Check(!expected.IdEntityDic.ContainsKey(discarded), "Bronze generated attempt absent from checkpoint");
        session.Client.ResetApplied = model => {
            resets++;
            Check(!Invariants.CompareReset(expected, model).Any(), "Bronze reset baseline parity");
            Check(model.Entities[source].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 2 &&
                model.Entities[source].Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 2 && model.Entities[source].Get(GameTag.EXHAUSTED) == 0,
                "Bronze reset restores both displayed magnitudes and exhaustion");
        };
        first = session.Packets.Count;
        int pick = rewind ? t.RewindEntityId : t.KeepEntityId;
        var bytes = game.SaveSnapshot();
        if (owner == 1)
        {
            bool stale = false;
            try { match.SendChoices(offer.Id - 1, new List<int> { pick }); } catch (InvalidOperationException) { stale = true; }
            Check(stale && bytes.SequenceEqual(game.SaveSnapshot()) && first == session.Packets.Count, "Bronze stale packet mutation-free");
            match.SendChoices(offer.Id, new List<int> { pick });
        }
        else session.Process(ChooseTask.Pick(p, pick));
        var packets = session.Packets.Skip(first).ToArray();
        var history = packets.Where(w => w.Id == 19).SelectMany(w => History(w).List).ToArray();
        Check(packets[0].Id == 13 && history.Count(e => e.HasResetGame) == (rewind ? 1 : 0)
            && resets == (rewind ? 1 : 0), "Bronze ack before reset/replay, Keep omits reset");
        if (rewind)
        {
            int resetStart = Array.FindIndex(history, e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.GAME_RESET);
            var prefix = history.Take(resetStart).ToList();
            Check(prefix.Count(e => e.HasTagChange && e.TagChange.Entity == discarded &&
                e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME) == 1,
                "Bronze attempt actor retires before reset, without exposing card identity");
            Check(prefix.Any(e => e.HasTagChange && e.TagChange.Entity == attachment &&
                e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME),
                "Bronze discarded enchantment retires with its card");
            beforeReset.Apply(new NativeHistory { List = prefix });
            Check(beforeReset.Entities[discarded].CardZone == (int)EngineZone.REMOVEDFROMGAME,
                "Bronze retirement moves client actor out of hand");
            Check(!prefix.Any(e => (e.HasShowEntity && e.ShowEntity.Entity == discarded) ||
                (e.HasChangeEntity && e.ChangeEntity.Entity == discarded)), "Bronze retirement reveals no hidden identity");
            // A cosmetic FULL is the highest reset ID, so the pinned cutoff retains omitted entities.
            var withPet = history.ToList();
            int reset = withPet.FindIndex(e => e.HasResetGame);
            var pet = new PowerHistoryData { FullEntity = new() { Entity = int.MaxValue, Name = "PET_10_1", Tags = new() {
                new() { Name = (int)GameTag.ENTITY_ID, Value = int.MaxValue },
                new() { Name = (int)GameTag.ZONE, Value = 10 }
            } } };
            withPet.Insert(reset + 1, pet);
            var clean = preAnswer.Copy(); clean.RemoveTombstonedEntities = true; clean.Apply(new NativeHistory { List = new() { pet } });
            var missingRetirement = clean.Copy();
            clean.Apply(new NativeHistory { List = withPet });
            missingRetirement.Apply(new NativeHistory { List = withPet.Where(e => !(e.HasTagChange &&
                e.TagChange.Entity == discarded && e.TagChange.Tag == (int)GameTag.ZONE &&
                e.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME)).ToList() });
            Check(!clean.Entities.ContainsKey(discarded) && !clean.Entities.ContainsKey(attachment) &&
                missingRetirement.Entities[discarded].CardZone == (int)EngineZone.HAND,
                "cosmetic cutoff model detects missing retirement");
            Check(clean.Entities[int.MaxValue].CardId == "PET_10_1", "cosmetic pet survives reset");
            Check(clean.Entities.Where(pair => pair.Value.CardZone == (int)EngineZone.HAND && pair.Value.CardController == owner)
                .Select(pair => pair.Key).OrderBy(id => id).SequenceEqual(game.CurrentPlayer.HandZone.Select(card => card.Id).OrderBy(id => id)),
                "client hand actors match engine hand after retry");
        }
        Check(history.Count(e => e.HasTagChange && e.TagChange.Entity == source &&
            e.TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND && e.TagChange.Value == 1) == (rewind ? 1 : 0),
            "HP replay has no duplicate hand cost synthesis");
        p = game.CurrentPlayer;
        Check(game.Rewind == null && p.UsedMana == 1 && p.Hero.HeroPower.IsExhausted && p.NumTimesHeroPowerUsedThisGame == 1
            && p.HandZone.Count == 1 && p.NumCardsPlayedThisTurn == 0 && match.RewardFacts.DistinctCardsPlayed == 0,
            "Bronze final payment/use/hand; zero card rewards before and after reset");
        var generated = p.HandZone.Single();
        if (rewind) Check(generated.Id > Math.Max(discarded, attachment), "rewind retry does not reuse discarded IDs");
        Check(p.Options().Any(option => option.Source?.Id == generated.Id), "Bronze retained result has a legal play option");
        if (owner == 1) Check(session.Client.Entities[generated.Id].Get(GameTag.COST) == generated.Cost
            && session.Client.Entities[generated.Id].CardId == generated.Card.Id, "Bronze generated discount visible to owner");
        else Check(session.Client.Entities[generated.Id].CardId == "", "Bronze opponent hand identity private");
        Check(!Invariants.Compare(game, session.Client).Any(), "Bronze both perspectives final parity");
        var expectedPowerTags = match.SnapshotVisibleTransportTags(p.Hero.HeroPower);
        bool nativeCollectible = session.Client.Entities[source].Tags.TryGetValue((int)GameTag.COLLECTIBLE, out int actualCollectible);
        bool expectedCollectible = expectedPowerTags.TryGetValue((int)GameTag.COLLECTIBLE, out int wantedCollectible);
        Check(NativeUIProjection.TestControlTransportTagMatches((int)GameTag.COLLECTIBLE, CardType.HERO_POWER,
            nativeCollectible, actualCollectible, expectedCollectible, wantedCollectible),
            "Bronze endpoint preserves strict COLLECTIBLE presence after replacement/reset");
        Console.WriteLine($"PASS Bronze Rewind transport: owner={owner}; rewind={rewind}; HP source; reset parity; reward=0");
    }

    private static void EndpointZeroTags()
    {
        foreach (var (type, tag, equivalent) in new[] {
            (CardType.MINION, GameTag.COLLECTIBLE, false),
            (CardType.SPELL, GameTag.COLLECTIBLE, false),
            (CardType.HERO_POWER, GameTag.COLLECTIBLE, false),
            (CardType.ENCHANTMENT, GameTag.COLLECTIBLE, false),
            (CardType.LOCATION, GameTag.COLLECTIBLE, false),
            (CardType.ENCHANTMENT, GameTag.DAMAGE, true),
            (CardType.ENCHANTMENT, GameTag.ATK, true),
            (CardType.ENCHANTMENT, GameTag.HEALTH, true),
            (CardType.ENCHANTMENT, GameTag.COST, true),
            (CardType.ENCHANTMENT, GameTag.RARITY, false),
            (CardType.ENCHANTMENT, GameTag.ATTACHED, false),
            (CardType.ENCHANTMENT, GameTag.CREATOR, false),
            (CardType.ENCHANTMENT, GameTag.IS_NIGHTMARE_BONUS, false),
            (CardType.MINION, GameTag.ATK, false),
            (CardType.MINION, GameTag.HEALTH, false),
            (CardType.SPELL, GameTag.ATK, false),
            (CardType.SPELL, GameTag.HEALTH, false),
            (CardType.SPELL, GameTag.COST, false),
            (CardType.MINION, GameTag.DAMAGE, false),
            (CardType.HERO, GameTag.DAMAGE, false),
            (CardType.LOCATION, GameTag.DAMAGE, false),
            (CardType.HERO_POWER, GameTag.DAMAGE, false),
            (CardType.MINION, GameTag.SPELLPOWER, false),
            (CardType.MINION, GameTag.CANT_BE_TARGETED_BY_SPELLS, false)
        })
        foreach (bool nativePresent in new[] { false, true })
        {
            Check(NativeUIProjection.TestControlTransportTagMatches((int)tag, type, nativePresent, 0, !nativePresent, 0) == equivalent,
                $"endpoint zero-presence scope: {type}/{tag}/{nativePresent}");
            Check(!NativeUIProjection.TestControlTransportTagMatches((int)tag, type, true, 1, false, 0)
                && !NativeUIProjection.TestControlTransportTagMatches((int)tag, type, false, 0, true, 1)
                && !NativeUIProjection.TestControlTransportTagMatches((int)tag, type, true, 1, true, 2),
                $"endpoint rejects real nonzero mismatch: {type}/{tag}");
            Check(!NativeUIProjection.TransportTagMatches(nativePresent, 0, !nativePresent, 0),
                "strict transport audit still rejects every zero presence mismatch");
        }
        Console.WriteLine("PASS endpoint zero tags: only enchantment HIDE stats; identity/attachment/nonzero/gameplay mismatches remain strict");
    }

    private static void GeneratedResetEndpointTags()
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        Generic.DrawCard(player, Cards.FromId("CATA_489")); // Real Shatter creation starts the halves in HAND.
        var generated = new[] {
            Entity.FromCard(player, Cards.FromId("CATA_528t"), zone: player.GraveyardZone),
            player.HandZone.Single(card => card.Card.Id == "CATA_489t"),
            player.HandZone.Single(card => card.Card.Id == "CATA_489t2"),
            Entity.FromCard(player.Opponent, Cards.FromId("CATA_580t"), zone: player.Opponent.BoardZone)
        };
        var attachment = SabberStoneCore.Model.Entities.Enchantment.GetInstance(player,
            player.Hero, generated[1], Cards.FromId("MEND_504e"));
        var source = Generic.DrawCard(player, Cards.FromId("TIME_003"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush(); session.PublishDecision(player);
        foreach (var entity in generated)
            Check(!session.Client.Entities[entity.Id].Tags.ContainsKey((int)GameTag.COLLECTIBLE),
                "ordinary generated FULL/SHOW omits COLLECTIBLE");
        Check(session.Client.Entities[attachment.Id].Tags[(int)GameTag.DAMAGE] == 0
            && !attachment.NativeTags.ContainsKey(GameTag.DAMAGE), "enchantment creation SHOW emits synthetic DAMAGE=0");
        session.Process(player.Options().OfType<PlayCardTask>().First(t => t.Source == source));
        session.PublishDecision(player);
        int cursor = session.Packets.Count;
        match.SendChoices(match.HumanChoiceId, new List<int> { game.Rewind.RewindEntityId });
        var history = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => History(w).List).ToList();
        int reset = history.FindIndex(e => e.HasResetGame);
        Check(reset >= 0, "generated fixture emits serialized reset");
        var resetFulls = history.Skip(reset + 1).TakeWhile(e => e.HasFullEntity).Select(e => e.FullEntity).ToArray();
        Check(resetFulls.Length > 0 && resetFulls.All(e => !e.Tags.Any(t =>
            t.Name == (int)GameTag.COLLECTIBLE && t.Value == 0)), "reset packet omits zero definition COLLECTIBLE");
        Check(!resetFulls.Single(e => e.Entity == source.Id).Tags.Any(t =>
            t.Name == (int)GameTag.COLLECTIBLE), "reset matches ordinary FULL: nonzero definition metadata stays omitted too");
        Check(resetFulls.Single(e => e.Entity == generated[0].Id).Tags.Any(t =>
            t.Name == (int)GameTag.DAMAGE && t.Value == 0), "reset packet preserves gameplay DAMAGE zero");
        foreach (var id in generated.Select(entity => entity.Id).Append(attachment.Id))
        {
            var entity = game.IdEntityDic[id];
            var expected = match.SnapshotVisibleTransportTags(entity);
            var actual = session.Client.Entities[id].Tags;
            var tag = id == attachment.Id ? GameTag.DAMAGE : GameTag.COLLECTIBLE;
            bool nativePresent = actual.TryGetValue((int)tag, out int nativeValue);
            bool expectedPresent = expected.TryGetValue((int)tag, out int expectedValue);
            Check(nativeValue == 0 && expectedValue == 0, $"TIME_003 reset default: {entity.Card.Id}/{tag}");
            if (id == attachment.Id)
                Check(nativePresent != expectedPresent, "TIME_003 reset reproduces zero presence drift");
            else Check(!nativePresent && !expectedPresent, "TIME_003 reset keeps zero COLLECTIBLE absent on both sides");
            Check(NativeUIProjection.TestControlTransportTagMatches((int)tag, entity.Card.Type,
                nativePresent, nativeValue, expectedPresent, expectedValue), "generated reset endpoint parity");
            Console.WriteLine($"PASS reset default {entity.Card.Id}/{tag}: nativePresent={nativePresent}; expectedPresent={expectedPresent}");
        }
        Console.WriteLine("PASS TIME_003 endpoint: reset retains generated-token COLLECTIBLE omission and enchantment synthetic DAMAGE zero");
    }

    private static void CardStateReset(int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        var player = game.CurrentPlayer; player.BaseMana = 10;
        foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
        var progress = Generic.DrawCard(player, Cards.FromId("JAIL_801"));
        var temporary = Generic.DrawCard(player, Cards.FromId("CS2_182"));
        TemporaryRules.Make(temporary);
        Generic.DrawCard(player, Cards.FromId("CATA_489"));
        var half = player.HandZone.First(card => card.Card.Id == "CATA_489t");
        var sleeper = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player, Cards.FromId("TIME_046"));
        Generic.SummonBlock(game, sleeper, -1, null);
        LeylineRules.Upgrade(player, player.Hero, cost: 1, extraTriggers: 1, effect: 1);
        Generic.DrawCard(player, Cards.FromId("MEND_500"));
        var source = Generic.DrawCard(player, Cards.FromId("TIME_001"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush();
        session.Process(PlayCardTask.Any(player, source));
        Check(progress[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "card-state candidate progress");
        game.PowerHistory.Last.Clear();
        Generic.DiscardBlock(player, temporary);
        Generic.DiscardBlock(player, half);
        DormantRules.Accelerate(sleeper, 1);
        game.DeathProcessingAndAuraUpdate(); session.Flush();
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        session.PublishDecision(player);
        session.Client.ResetApplied = model => {
            var errors = Invariants.CompareReset(expected, model).ToArray();
            Check(errors.Length == 0, "card-state reset oracle: " + string.Join("; ", errors.Select(e => e.ToString())));
        };
        if (owner == 1) match.SendChoices(match.HumanChoiceId, new List<int> { game.Rewind.RewindEntityId });
        else session.Process(ChooseTask.Pick(player, game.Rewind.RewindEntityId));
        var after = Invariants.Compare(game, session.Client).ToArray();
        Check(after.Length == 0, "card-state replay oracle: " + string.Join("; ", after.Select(e => e.ToString())));
        Check(game.IdEntityDic[temporary.Id].Zone == game.CurrentPlayer.HandZone && TemporaryRules.IsTemporary(game.IdEntityDic[temporary.Id])
            && game.IdEntityDic[half.Id].Zone == game.CurrentPlayer.HandZone
            && DormantRules.TurnsRemaining((SabberStoneCore.Model.Entities.Minion)game.IdEntityDic[sleeper.Id]) == 3
            && game.IdEntityDic[progress.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "all four card-state families restored on wire");
        Console.WriteLine($"PASS Rewind CardStateReset: owner={owner}; Dormant/Temporary/Shatter/progress, private attachments, reset/replay oracle");
    }
    private static void SurvivingControllerAuras(int owner, bool remove)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        var player = game.CurrentPlayer; player.BaseMana = 10;
        foreach (var ownerState in new[] { game.Player1, game.Player2 })
        foreach (var tag in new[] { (GameTag)31120, (GameTag)31121, (GameTag)31122 }) ownerState[tag] = 1;
        var fandrals = new List<SabberStoneCore.Model.Entities.Minion>();
        foreach (var p in new[] { game.Player1, game.Player2 })
        foreach (string id in new[] { "OG_044", "LOE_077", "CS2_142", "EX1_350" })
        {
            var minion = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(p, Cards.FromId(id));
            minion.BaseHealth = 30; // Keep every aura source alive through the random Daggers replay.
            Generic.SummonBlock(game, minion, -1, null);
            if (id == "OG_044") fandrals.Add(minion);
        }
        game.DeathProcessingAndAuraUpdate();
        var source = Generic.DrawCard(player, Cards.FromId("TIME_001"));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush();
        session.Process(PlayCardTask.Any(player, source));
        Check(game.Rewind != null, "aura-bearing Chrono Daggers opens offer");
        if (remove)
        {
            game.PowerHistory.Last.Clear();
            foreach (var fandral in fandrals) fandral.ToBeDestroyed = true;
            game.DeathProcessingAndAuraUpdate(); session.Flush();
        }
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        var before = session.Client.Copy();
        session.PublishDecision(player);
        session.Client.ResetApplied = model => {
            foreach (int id in new[] { 2, 3 })
            {
                Check(new[] { (GameTag)31120, (GameTag)31121, (GameTag)31122 }.All(tag => model.Entities[id].Get(tag) == 0),
                    "Leyline controller state stays private at reset");
                Check(model.Entities[id].Get(GameTag.CHOOSE_BOTH) == 1, "surviving Fandral CHOOSE_BOTH=1 at reset");
                Check(model.Entities[id].Get(GameTag.EXTRA_BATTLECRIES_BASE) == 1, "surviving Brann at reset");
                Check(model.Entities[id].Get(GameTag.SPELLPOWER_DOUBLE) == 1, "surviving Velen at reset");
                Check(model.Entities[id].Tags.GetValueOrDefault(291) == 1, "surviving spell damage at reset");
            }
            Check(!Invariants.CompareReset(expected, model).Any(), "effective controller aura reset oracle");
        };
        int first = session.Packets.Count;
        if (owner == 1) match.SendChoices(match.HumanChoiceId, new List<int> { game.Rewind.RewindEntityId });
        else session.Process(ChooseTask.Pick(player, game.Rewind.RewindEntityId));
        if (remove)
        {
            var entries = session.Packets.Skip(first).Where(p => p.Id == 19).SelectMany(p => History(p).List).ToList();
            int reset = entries.FindIndex(e => e.HasResetGame), end = entries.FindIndex(reset, e => e.HasPowerEnd);
            bool Correction(PowerHistoryData e) => e.HasTagChange && e.TagChange.Tag == (int)GameTag.CHOOSE_BOTH;
            Check(entries.Take(end + 1).Count(Correction) == 2, "both Fandral corrections exist");
            var mutant = before.Copy();
            mutant.Apply(new NativeHistory { List = entries.Take(end + 1).Where(e => !Correction(e)).ToList() });
            Check(Invariants.CompareReset(expected, mutant).Count(e => e.Tag == "CHOOSE_BOTH") == 2,
                "oracle detects dropped effective controller aura corrections");
        }
        Console.WriteLine($"PASS Rewind SurvivingControllerAuras: owner={owner}; discardedRemoval={remove}; Fandral/Brann/Velen/spell damage; effective reset oracle");
        PostResetAuraChanges(game, session, remove);
        Console.WriteLine($"PASS Rewind PostResetAuraChanges: owner={owner}; stacked={remove}; Velen removal/stack, spell damage death, Brann/Fandral removal; future oracle and dropped-alias mutation");
    }
    private static void PostResetAuraChanges(Game game, NativeSession session, bool stack)
    {
        void Compare(string step)
        {
            var errors = Invariants.Compare(game, session.Client).ToArray();
            Check(errors.Length == 0, step + ": " + string.Join("; ", errors.Select(e => e.ToString())));
        }
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            // Every reset-exposed controller aura must remain part of the ordinary future-state oracle.
            foreach (var tag in Invariants.ControllerAuraTags)
            {
                var mutant = session.Client.Copy();
                mutant.Entities[owner.Id].Tags[(int)tag] = owner[tag] + 1;
                Check(Invariants.Compare(game, mutant).Any(e => e.Entity == owner.Id && e.Tag == tag.ToString()),
                    "future oracle detects stale " + tag);
            }
            var velen = owner.BoardZone.Single(m => m.Card.Id == "EX1_350");
            if (stack)
            {
                game.PowerHistory.Last.Clear();
                Generic.SummonBlock(game, (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(owner, Cards.FromId("EX1_350")), -1, null);
                game.DeathProcessingAndAuraUpdate(); session.Flush();
                Check(owner[GameTag.SPELL_HEALING_DOUBLE] == 2 && session.Client.Entities[owner.Id].Get(GameTag.SPELL_HEALING_DOUBLE) == 2,
                    "post-reset second Velen publishes healing stack=2");
                Compare("Velen stack increase");
            }
            void Fireball(SabberStoneCore.Model.Entities.Minion target)
            {
                game.PowerHistory.Last.Clear();
                game.CurrentPlayer.UsedMana = 0;
                target.Damage = target.BaseHealth - 1;
                var spell = Generic.DrawCard(game.CurrentPlayer, Cards.FromId("CS2_029"));
                session.Flush(); Compare("Fireball draw");
                session.Process(PlayCardTask.Any(game.CurrentPlayer, spell, target));
                Check(target.Zone.Type == EngineZone.GRAVEYARD, "Fireball kills " + target.Card.Id);
            }
            var before = session.Client.Copy();
            int first = session.Packets.Count;
            Fireball(velen);
            int remaining = stack ? 1 : 0;
            Compare("Velen removal/stack decrease");
            Check(owner[GameTag.SPELL_HEALING_DOUBLE] == remaining && session.Client.Entities[owner.Id].Get(GameTag.SPELL_HEALING_DOUBLE) == remaining,
                "post-reset Velen death updates healing alias");
            var mutantHistory = session.Packets.Skip(first).Where(p => p.Id == 19).SelectMany(p => History(p).List).ToList();
            bool Alias(PowerHistoryData e) => e.HasTagChange && e.TagChange.Entity == owner.Id && e.TagChange.Tag == (int)GameTag.SPELL_HEALING_DOUBLE;
            Check(mutantHistory.Any(Alias), "post-reset Velen healing correction exists");
            before.Apply(new NativeHistory { List = mutantHistory.Where(e => !Alias(e)).ToList() });
            Check(Invariants.Compare(game, before).Any(e => e.Entity == owner.Id && e.Tag == "SPELL_HEALING_DOUBLE"),
                "ordinary future oracle detects dropped healing correction");
            if (stack)
            {
                Fireball(owner.BoardZone.Single(m => m.Card.Id == "EX1_350"));
                Compare("last stacked Velen removed");
            }
            foreach (var entry in new[] { ("CS2_142", (GameTag)291), ("LOE_077", GameTag.EXTRA_BATTLECRIES_BASE), ("OG_044", GameTag.CHOOSE_BOTH) })
            {
                Fireball(owner.BoardZone.Single(m => m.Card.Id == entry.Item1));
                Check(session.Client.Entities[owner.Id].Get(entry.Item2) == 0, "post-reset aura clears " + entry.Item1);
                Compare("aura removal " + entry.Item1);
            }
        }
    }
    private static void SecretReset(bool trigger)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 2, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); game.Player2.BaseMana = 10;
        var secret = Generic.DrawCard(game.Player2, Cards.FromId("EX1_287"));
        var quest = Generic.DrawCard(game.Player2, Cards.FromId("TLC_426"));
        var hand = Generic.DrawCard(game.Player2, Cards.FromId("CS2_182"));
        var source = Generic.DrawCard(game.Player1, Cards.FromId(trigger ? "TIME_001" : "TIME_038"));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush();
        session.Process(PlayCardTask.Any(game.Player2, secret));
        session.Process(PlayCardTask.Any(game.Player2, quest));
        session.Process(EndTurnTask.Any(game.Player2));
        game.Player1.BaseMana = 10;
        // Privacy links must resolve their terminal Secret from restored metadata, too.
        Field<Dictionary<int, HashSet<int>>>(match, "privacyTargets")[hand.Id] = new() { secret.Id };
        session.Process(PlayCardTask.Any(game.Player1, source));
        Check((secret.Zone == game.Player2.GraveyardZone) == trigger, "Counterspell activation fixture");
        if (trigger) Check(session.Client.Entities[secret.Id].CardId == "EX1_287", "activation explicitly reveals Secret");
        // A public play reveal during the discarded attempt must not survive restoration to hand.
        game.PowerHistory.Last.Clear();
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, hand.Id, "", -1, 0));
        Generic.RemoveFromZone(game.Player2, hand); game.Player2.GraveyardZone.Add(hand);
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
        session.Flush();
        Check(session.Client.Entities[hand.Id].CardId == "CS2_182", "discarded attempt reveals opposing hand card");
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        var before = session.Client.Copy();
        session.PublishDecision(game.Player1);
        int first = session.Packets.Count;
        session.Client.ResetApplied = model => { var errors = Invariants.CompareReset(expected, model).ToArray(); Check(errors.Length == 0, "Secret reset oracle: " + string.Join("; ", errors)); };
        match.SendChoices(match.HumanChoiceId, new List<int> { game.Rewind.RewindEntityId });
        var history = session.Packets.Skip(first).Where(p => p.Id == 19).SelectMany(p => History(p).List).ToList();
        int reset = history.FindIndex(e => e.HasResetGame), end = history.FindIndex(reset, e => e.HasPowerEnd);
        var block = history.Take(end + 1).ToList();
        foreach (int id in new[] { secret.Id, hand.Id })
        {
            var snapshots = block.Where(e => e.HasFullEntity || e.HasChangeEntity || e.HasShowEntity)
                .Select(e => e.HasFullEntity ? e.FullEntity : e.HasChangeEntity ? e.ChangeEntity : e.ShowEntity).Where(e => e.Entity == id).ToArray();
            Check(snapshots.Length > 0 && snapshots.All(e => e.Name == ""), "reset wire redacts Secret/hand identity");
            if (id == hand.Id || trigger)
                Check(block.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == id && e.ChangeEntity.Name == ""), "reset hides previously revealed identity with CHANGE_ENTITY");
        }
        Check(block.Single(e => e.HasFullEntity && e.FullEntity.Entity == quest.Id).FullEntity.Name == "TLC_426", "reset keeps opposing Quest public");
        var mutant = before.Copy();
        mutant.Apply(new NativeHistory { List = block.Where(e => !(e.HasChangeEntity && e.ChangeEntity.Entity == hand.Id)).ToList() });
        Check(Invariants.CompareReset(expected, mutant).Any(e => e.Entity == hand.Id && e.Tag == "RESET_PRIVACY"),
            "mutation dropping hidden identity correction must fail oracle");
        Console.WriteLine("PASS Rewind SecretReset: " + (trigger ? "triggered/revealed" : "untriggered") + "; Secret/hand redaction, public Quest, hidden-identity mutation detected");
    }
    private static void ControllerCredit(int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); var player = game.CurrentPlayer; player.BaseMana = 10;
        var sylvanas = (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(player.Opponent, Cards.FromId("EX1_016"));
        sylvanas[GameTag.ATK] = 7;
        Generic.SummonBlock(game, sylvanas, -1, null);
        var source = Generic.DrawCard(player, Cards.FromId("EX1_005"));
        source[GameTag.REWIND] = 1;
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match); session.Flush();
        session.Process(PlayCardTask.Any(player, source, sylvanas));
        Check(game.IdEntityDic[source.Id].Controller == player.Opponent && match.RewardFacts.DistinctCardsPlayed == (owner == 1 ? 1 : 0),
            "original player gets first-attempt credit despite theft");
        session.PublishDecision(player);
        var expected = Game.LoadSnapshot(game.Rewind.Baseline);
        session.Client.ResetApplied = model => Check(!Invariants.CompareReset(expected, model).Any(), "stolen-source reset oracle");
        if (owner == 1) match.SendChoices(match.HumanChoiceId, new List<int> { game.Rewind.RewindEntityId });
        else session.Process(ChooseTask.Pick(player, game.Rewind.RewindEntityId));
        Check(game.IdEntityDic[source.Id].Controller == game.CurrentPlayer.Opponent && match.RewardFacts.DistinctCardsPlayed == (owner == 1 ? 1 : 0),
            "replay credits original player even when source is stolen");
        Check(!Invariants.Compare(game, session.Client).Any(), "stolen-source replay oracle");
    }
    private static void ResetModel()
    {
        var model = new ClientModel(new[] { (int)GameTag.FROZEN });
        model.Entities[2] = new ClientEntity();
        model.Entities[2].Tags[(int)GameTag.RESOURCES_USED] = 8;
        model.Entities[79] = new ClientEntity { CardId = "CS2_231" };
        model.Entities[80] = new ClientEntity { CardId = "CS2_182" };
        model.Entities[80].Tags[(int)GameTag.TAUNT] = 1;
        model.Entities[80].Tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
        model.Entities[81] = new ClientEntity { CardId = "CS2_200" };
        model.Entities[81].Tags[(int)GameTag.COST] = 99;
        model.Entities[82] = new ClientEntity { CardId = "CS2_200" };
        model.Entities[82].Tags[2321] = 1;
        model.Entities[83] = new ClientEntity { CardId = "TIME_038t1" };
        model.Entities[83].Tags[2321] = 1;
        model.Entities[84] = new ClientEntity { CardId = "CS2_231" };
        model.Entities[84].Tags[(int)GameTag.ZONE] = (int)EngineZone.PLAY;
        model.Entities[84].Tags[(int)GameTag.CONTROLLER] = 1;
        model.Entities[85] = new ClientEntity { CardId = "CS2_231" };
        model.Entities[85].Tags[(int)GameTag.FROZEN] = 1;
        model.Entities[90] = new ClientEntity { CardId = "TIME_000tb" };
        model.Apply(new NativeHistory { List = new() {
            new() { PowerStart = new() { Type = (HistoryBlock.Type)11, Source = 80 } },
            new() { ResetGame = new() { SourceIsRewind = true, CreateGame = new() {
                GameEntity = new() { Id = 1, Tags = new() }, Players = new() {
                    new() { Entity = new() { Id = 2, Tags = new() { new() { Name = (int)GameTag.RESOURCES_USED, Value = 0 } } } }
                } } } },
            new() { FullEntity = new() { Entity = 80, Name = "CS2_182", Tags = new() } },
            new() { FullEntity = new() { Entity = 81, Name = "CS2_182", Tags = new() { new() { Name = (int)GameTag.COST, Value = 5 } } } },
            new() { FullEntity = new() { Entity = 82, Name = "CS2_182", Tags = new() } },
            new() { FullEntity = new() { Entity = 83, Name = "TIME_038", Tags = new() } },
            new() { FullEntity = new() { Entity = 84, Name = "CS2_231", Tags = new() {
                new() { Name = (int)GameTag.ZONE, Value = (int)EngineZone.HAND },
                new() { Name = (int)GameTag.ZONE_POSITION, Value = 2 },
                new() { Name = (int)GameTag.CONTROLLER, Value = 2 } } } },
            new() { FullEntity = new() { Entity = 85, Name = "CS2_231", Tags = new() } },
            new() { TagChange = new() { Entity = 80, Tag = (int)GameTag.TAG_SCRIPT_DATA_NUM_1, Value = 0 } },
            new() { ChangeEntity = new() { Entity = 80, Name = "CS2_231", Tags = new() } },
            new() { PowerEnd = new() }
        } });
        Check(model.Entities[2].Get(GameTag.RESOURCES_USED) == 8, "Rewind ignores CreateGame mana payload");
        Check(model.Entities.ContainsKey(79) && !model.Entities.ContainsKey(90), "client deletes only IDs above highest FULL");
        Check(model.Entities[80].Get(GameTag.TAUNT) == 0 && model.Entities[80].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0 &&
            model.Entities[80].CardId == "CS2_231", "client synthesizes omitted priority zero; explicit non-priority zero and CHANGE apply");
        Check(model.Entities[81].CardId == "CS2_200" && model.Entities[81].Get(GameTag.COST) == 99,
            "existing FULL ignores identity and non-priority tags without transform");
        Check(model.Entities[82].CardId == "CS2_182" && model.Entities[83].CardId == "TIME_038t1",
            "transform-counter diff synthesizes CHANGE except TIME_038 destination");
        Check(model.Entities[84].Get(GameTag.ZONE) == (int)EngineZone.HAND && model.Entities[84].Get(GameTag.ZONE_POSITION) == 2 &&
            model.Entities[84].Get(GameTag.CONTROLLER) == 2, "reset sets zone/position/controller directly");
        Check(model.Entities[85].Get(GameTag.FROZEN) == 0, "TagVisualConfiguration extends priority tags");
        Console.WriteLine("PASS Rewind ResetModel: ignored CreateGame/existing FULL, priority zero, transform, Clocksworth filter, zones, visual tags, cutoff");
    }
    private static void SettleOnce(NativeMatch match)
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-rewind-settle-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "profile.xml");
        string id;
        try
        {
            using (var profile = new LocalProfile(path))
            {
                LadderBinding.Bind(profile);
                var pending = profile.Begin("mage", "priest", 31);
                id = pending.Id;
                typeof(LocalLadder).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, pending);
                LocalLadder.NativeStarted();
                LocalLadder.MarkConcede(); match.Concede(); LocalLadder.NativeFinished();
                LocalLadder.Observe(match); LocalLadder.Observe(match);
                Check(profile.Pending == null && profile.History.Count == 1 && profile.History[0].Id == id,
                    "Rewind match settles durable profile once");
            }
            using var reloaded = new LocalProfile(path);
            Check(reloaded.Pending == null && reloaded.History.Single().Id == id, "Rewind settlement survives restart");
        }
        finally
        {
            LocalLadder.RewardPresentationComplete();
            Directory.Delete(directory, true);
        }
    }
    private static T Field<T>(NativeMatch match, string name) =>
        (T)typeof(NativeMatch).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
    private static void Scenario(string card, int rewinds, int owner)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = owner, SkipMulligan = true,
            History = true, Logging = false, EnableCorpses = true,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        var player = game.CurrentPlayer;
        player.BaseMana = 10;
        var source = Generic.DrawCard(player, Cards.FromId(card));
        game.Player1[HeraldRules.Profile] = (int)CardClass.WARRIOR;
        Generic.DrawCard(game.Player1, Cards.FromId("CATA_525"));
        // This retained actor checks identity reversal and non-priority explicit-zero clears.
        Generic.SummonBlock(game, (SabberStoneCore.Model.Entities.Minion)Entity.FromCard(game.Player1, Cards.FromId("CS3_007")), -1, null);
        var retained = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        retained.Card = retained.Card.Clone();
        retained.Card.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = 7;
        retained[GameTag.POWERED_UP] = 1;
        Generic.DrawCard(game.Player1, Cards.FromId("CORE_BT_801"));
        game.DeathProcessingAndAuraUpdate();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush();
        session.PublishDecision(player);
        Field<HashSet<string>>(match, "playedCardIds").Add("prior-play");
        Field<HashSet<int>>(match, "enemyBoardMinions").Add(game.Player1.Hero.Id);
        Field<HashSet<int>>(match, "defeatedEnemyMinions").Add(game.Player1.Hero.Id);
        Field<Dictionary<int, int>>(match, "rewardControllers")[-13] = 2;
        typeof(NativeMatch).GetField("minimumHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, 23);
        session.Process(PlayCardTask.Any(player, source));
        Check(session.Client.Entities[source.Id].Get(GameTag.REWIND) == 1, "own Rewind tag reaches live actor without definition hooks");
        Check(!NativeUIProjection.TryGetDefinitionTag(true, true, true, card, 3934, out _) &&
            !NativeUIProjection.TryGetDbfTag(true, card, 3945, out _), "definition allowlists need no Rewind additions");
        int resetCount = 0;
        for (int attempt = 0; game.Rewind != null; attempt++)
        {
            var transaction = game.Rewind;
            Check(transaction.Stage == RewindStage.Offered, "offer after settled play");
            int choiceStart = session.Packets.Count;
            session.PublishDecision(game.ControllerById(transaction.ControllerId));
            Check(!session.Packets.Skip(choiceStart).Any(p => p.Id == 14), "no options while pending");
            var offerWire = session.Packets.Last(p => p.Id == 17);
            var offer = new EntityChoices();
            using (var stream = new MemoryStream(Convert.FromBase64String(offerWire.Base64))) offer.Deserialize(stream);
            Check(offer.Source == source.Id && offer.PlayerId == owner && offer.ChoiceType == 2 && offer.Entities.Count == 2,
                "GENERAL offer source and owner");
            Check(offer.Entities.All(id => session.Client.Entities[id].CardId == game.IdEntityDic[id].Card.Id), "tokens exist before offer");
            if (owner == 2)
            {
                var keep = LocalBot.Choose(game.Player2.Options()) as ChooseTask;
                Check(keep?.Choices.Single() == transaction.KeepEntityId, "fallback bot Keep");
                var fair = FairBotState.Create(game, owner, 41, DeckStyle.Control);
                var decision = BotSearch.Choose(fair, BotBudget.ForStars(0));
                Check(decision.Nodes == 0 && ((ChooseTask)decision.Action.Resolve(game.Player2.Options())).Choices.Single() == transaction.KeepEntityId,
                    "fair bot returns Keep without search");
                var chooser = typeof(NativeMatch).GetMethod("ChooseBotTask", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(((ChooseTask)chooser.Invoke(match, new object[] { game.Player2.Options() })).Choices.Single() == transaction.KeepEntityId
                    && match.BotNodes == 0, "native bot Keep without search");
            }
            bool rewind = attempt < rewinds;
            int pick = rewind ? transaction.RewindEntityId : transaction.KeepEntityId;
            if (rewind && attempt == 0)
            {
                game.PowerHistory.Last.Clear();
                Generic.ChangeEntityBlock(game.Player1, game.IdEntityDic[retained.Id], Cards.FromId("CS2_200"), false);
                game.IdEntityDic[retained.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
                game.IdEntityDic[retained.Id][GameTag.TAG_SCRIPT_DATA_NUM_2] = 99;
                session.Flush();
                Field<HashSet<string>>(match, "playedCardIds").Add("discarded-play");
                Field<HashSet<int>>(match, "enemyBoardMinions").Add(game.Player1.Hero.HeroPower.Id);
                Field<HashSet<int>>(match, "defeatedEnemyMinions").Add(game.Player1.Hero.HeroPower.Id);
                Field<Dictionary<int, int>>(match, "rewardControllers")[-23] = 1;
                typeof(NativeMatch).GetField("minimumHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, 1);
            }
            var expected = Game.LoadSnapshot(transaction.Baseline);
            int[] candidateIds = game.IdEntityDic.Where(pair => !expected.IdEntityDic.ContainsKey(pair.Key)).Select(pair => pair.Key).ToArray();
            session.Client.ResetApplied = model =>
            {
                resetCount++;
                var errors = Invariants.CompareReset(expected, model).ToArray();
                Check(errors.Length == 0, "reset oracle: " + string.Join("; ", errors.Select(e => e.ToString())));
                Check(model.Entities.Keys.All(id => id <= expected.IdEntityDic.Max(pair => pair.Key)), "candidate actors deleted at cutoff");
                Check(model.Entities[retained.Id].CardId == expected.IdEntityDic[retained.Id].Card.Id &&
                    model.Entities[retained.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == expected.IdEntityDic[retained.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] &&
                    model.Entities[retained.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 0,
                    "identity and stale non-priority tag restored");
                foreach (var hidden in expected.Player1.DeckZone.Concat(expected.Player2.DeckZone).Concat(expected.Player2.HandZone))
                    Check(model.Entities[hidden.Id].CardId == "", "reset keeps hand/deck privacy");
            };
            int before = session.Packets.Count;
            int[] priorIds = Field<Dictionary<int, Dictionary<int, int>>>(match, "clientTransportTags")
                .Where(pair => pair.Value.GetValueOrDefault((int)GameTag.ZONE) != (int)EngineZone.REMOVEDFROMGAME)
                .Select(pair => pair.Key).ToArray();
            var beforeReset = session.Client.Copy();
            var saved = game.SaveSnapshot();
            if (owner == 1)
            {
                bool stale = false;
                try { match.SendChoices(offer.Id - 1, new List<int> { pick }); }
                catch (InvalidOperationException) { stale = true; }
                Check(stale && saved.SequenceEqual(game.SaveSnapshot()) && before == session.Packets.Count, "stale packet zero mutation");
                bool blocked = false;
                try { match.SendOption(match.HumanOptionsId, 0, 0, -1, 0); }
                catch (InvalidOperationException) { blocked = true; }
                Check(blocked && saved.SequenceEqual(game.SaveSnapshot()) && before == session.Packets.Count, "normal option blocked during Rewind choice");
                match.SendChoices(offer.Id, new List<int> { pick });
            }
            else session.Process(ChooseTask.Pick(game.Player2, pick));
            var packets = session.Packets.Skip(before).ToArray();
            Check(packets[0].Id == 13, "ack before history/deletion");
            var history = packets.Where(p => p.Id == 19).SelectMany(p => History(p).List).ToList();
            int reset = history.FindIndex(e => e.HasResetGame);
            Check((reset >= 0) == rewind, "reset only for Rewind");
            if (rewind)
            {
                foreach (int id in priorIds.Where(id => !expected.IdEntityDic.ContainsKey(id) && id != expected.Id && id != expected.Player1.Id && id != expected.Player2.Id))
                    Check(history.Take(reset - 1).Any(e => e.HasTagChange && e.TagChange.Entity == id &&
                        e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME),
                        "each repeated rewind retires absent client entity");
                foreach (int retired in candidateIds)
                {
                    Check(!Field<HashSet<int>>(match, "revealedEntities").Contains(retired) &&
                        !Field<HashSet<int>>(match, "privateDeckOffers").Contains(retired), "discarded IDs leave visibility caches");
                    foreach (string cache in new[] { "visibleTransportTags", "clientTransportTags", "clientTransportCards", "historicalControllers",
                        "historicalPositions", "historicalZones", "privacyTargets" })
                        Check(!((System.Collections.IDictionary)Field<object>(match, cache)).Contains(retired), "discarded IDs leave " + cache);
                }
                Check(reset > 0 && history[reset - 1].HasPowerStart && (int)history[reset - 1].PowerStart.Type == 11 &&
                    history[reset].ResetGame.SourceIsRewind && history[reset].ResetGame.CreateGame != null, "GAME_RESET field10 payload");
                int end = history.FindIndex(reset, e => e.HasPowerEnd);
                var block = history.Skip(reset + 1).Take(end - reset - 1).ToArray();
                Check(block.Count(e => e.HasFullEntity) == expected.IdEntityDic.Count, "all restored entities in reset");
                Check(block.TakeWhile(e => e.HasFullEntity).Count() == expected.IdEntityDic.Count, "FULLs precede corrections");
                Check(history.Skip(end + 1).Any(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.PLAY), "replay PLAY follows reset");
                if (attempt == 0)
                {
                    void DetectDroppedCorrection(string name, Func<PowerHistoryData, bool> drop, string tag)
                    {
                        Check(history.Take(end + 1).Any(drop), name + " correction exists");
                        var mutant = beforeReset.Copy();
                        mutant.Apply(new NativeHistory { List = history.Take(end + 1).Where(e => !drop(e)).ToList() });
                        Check(Invariants.CompareReset(expected, mutant).Any(e => e.Tag == tag), name + " mutation must fail reset oracle");
                    }
                    DetectDroppedCorrection("mana zero", e => e.HasTagChange && e.TagChange.Entity == player.Id &&
                        e.TagChange.Tag == (int)GameTag.RESOURCES_USED && e.TagChange.Value == 0, "RESOURCES_USED");
                    DetectDroppedCorrection("non-priority zero", e => e.HasTagChange && e.TagChange.Entity == retained.Id &&
                        e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2 && e.TagChange.Value == 0, "TAG_SCRIPT_DATA_NUM_2");
                }
                if (attempt == 0) Check(block.Any(e => e.HasTagChange && e.TagChange.Entity == retained.Id &&
                    e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2 && e.TagChange.Value == 0) &&
                    block.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == retained.Id), "explicit zero and identity correction inside reset");
            }
            if (owner == 1)
            {
                Check(packets.Last().Id == (game.Rewind == null ? 14 : 17), "options only when choice ends");
                saved = game.SaveSnapshot(); before = session.Packets.Count;
                bool duplicate = false;
                try { match.SendChoices(offer.Id, new List<int> { pick }); }
                catch (InvalidOperationException) { duplicate = true; }
                Check(duplicate && saved.SequenceEqual(game.SaveSnapshot()) && before == session.Packets.Count, "duplicate packet zero mutation");
            }
            Check(!Invariants.Compare(game, session.Client).Any(), "post-replay oracle");
            Check(session.Client.Entities[retained.Id].Get(GameTag.POWERED_UP) == game.IdEntityDic[retained.Id][GameTag.POWERED_UP],
                "reset does not adopt an engine-owned flag as a managed hand highlight");
        }
        Check(game.Player1.UsedMana + game.Player2.UsedMana == source.Card.Cost, "one net payment");
        Check(resetCount == rewinds, "oracle checked every reset");
        Check(match.RewardFacts.DistinctCardsPlayed == (owner == 1 ? 2 : 1), "prior plays retained, discarded plays removed, replay counted once");
        Check(Field<HashSet<int>>(match, "enemyBoardMinions").Contains(game.Player1.Hero.Id) && !Field<HashSet<int>>(match, "enemyBoardMinions").Contains(game.Player1.Hero.HeroPower.Id) &&
            Field<HashSet<int>>(match, "defeatedEnemyMinions").Contains(game.Player1.Hero.Id) && !Field<HashSet<int>>(match, "defeatedEnemyMinions").Contains(game.Player1.Hero.HeroPower.Id) &&
            Field<Dictionary<int, int>>(match, "rewardControllers").ContainsKey(-13) && !Field<Dictionary<int, int>>(match, "rewardControllers").ContainsKey(-23),
            "all reward observation sets restore to checkpoint");
        Check(match.RewardFacts.MinimumHealth == Math.Min(23, game.Player1.Hero.Health), "discarded minimum health restored");
        Check(Field<object>(match, "rewindRewards") == null, "transaction end drops reward checkpoint");
        if (owner == 1 && card == "TIME_038" && rewinds == 3) SettleOnce(match);
    }
}
