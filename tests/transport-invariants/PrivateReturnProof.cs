using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Zone = SabberStoneCore.Enums.Zone;

internal static class PrivateReturnProof
{
    internal static void Run()
    {
        RetiredResetPrivacy();
        foreach (string fault in new[] { "none", "damage-tag", "damage-amount", "damage-target" }) Combat(fault);
        var drifted = new List<string>();
        foreach (string path in Directory.GetFiles("tests/transport-invariants/replays/private-return", "*.json").Order(StringComparer.Ordinal))
        {
            var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(path), Program.Json);
            SelfChecks.Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30,
                "private return replay pins both original stress decks");
            var runner = new Runner();
            // Name the replay: a bare "fingerprint changed" does not say which of the ten moved (all are listed before failing).
            try { runner.Run(replay, verify: true); }
            catch (InvalidOperationException error) { drifted.Add(Path.GetFileName(path) + ": " + error.Message); continue; }
            SelfChecks.Check(runner.ViolationCount == 0 && runner.Completed.Values.Sum() == 1,
                $"private return {Path.GetFileName(path)}: legal hashes, actions, engine fingerprint and findings");
        }
        SelfChecks.Check(drifted.Count == 0, "private return replays failed: " + string.Join("; ", drifted));
        Console.WriteLine("PASS private return: 10 exact replays; two-burn retired reset privacy mutations; reset reference privacy/alias mutations; TARGET/JOUST entity and numeric amount remapping; alias parity/actor/ghost corruptions; damage hook/amount/target corruptions rejected");
    }

    private static void RetiredResetPrivacy()
    {
        var filler = Enumerable.Repeat(Cards.FromId("CS2_182"), 29).ToList();
        var game = new Game(new GameConfig { RandomSeed = 23, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = filler, Player2Deck = filler.Append(Cards.FromId("JAIL_509")).ToList() });
        game.StartGame();
        NativeSession session = null;
        byte[] resetBytes = null;
        var match = new NativeMatch((id, packet) => {
            if (packet is PegasusGame.PowerHistory history && history.List.Any(row => row.HasResetGame))
            {
                using var stream = new MemoryStream();
                history.Serialize(stream); resetBytes = stream.ToArray();
            }
            session.Receive(id, packet);
        });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        void Flush() { session.Flush(); game.PowerHistory.Last.Clear(); }
        Flush();
        var owner = game.Player2;
        while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        var first = Entity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
        var second = Entity.FromCard(owner, Cards.FromId("CS2_024"), zone: owner.DeckZone);
        Flush(); Generic.Draw(owner, first); Generic.Draw(owner, second); Flush();
        owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
        var selected = new[] { first, second }.Single(card => card.Zone == owner.HandZone);
        int alias = match.WireReturnId(selected.Id);
        SelfChecks.Check(selected.Card.Id == "CS2_024" && alias != selected.Id
            && session.Client.Entities[alias].CardId == ""
            && session.Client.Entities[selected.Id].Get(GameTag.ZONE) == (int)Zone.SETASIDE,
            "seed 23 returns Frostbolt anonymously from two distinct burns");
        var privateBurns = new HashSet<int> { first.Id, second.Id };
        var reset = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, owner.Id, "", -1, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(game)
        };
        reset.AddRange(game.IdEntityDic.Values.Select(entity => SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true)));
        reset.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
        session.Invoke("SendHistory", reset);
        SelfChecks.Check(!Invariants.CompareReset(game, session.Client, privateBurns: privateBurns).Any(),
            "two-burn clean reset preserves anonymous original and alias");
        session.History.Finish();
        SelfChecks.Check(session.History.Findings.Count == 0, "two-burn clean history preserves privacy");
        foreach (string fault in new[] { "show", "full", "change", "show-hide" })
        {
            var packet = new PegasusGame.PowerHistory();
            using var stream = new MemoryStream(resetBytes);
            packet.Deserialize(stream);
            var snapshot = new PowerHistoryEntity { Entity = selected.Id, Name = "CS2_024" };
            if (fault == "full") packet.List.Single(row => row.HasFullEntity && row.FullEntity.Entity == selected.Id).FullEntity.Name = snapshot.Name;
            else
            {
                int end = packet.List.FindLastIndex(row => row.HasPowerEnd);
                packet.List.Insert(end, fault == "change" ? new() { ChangeEntity = snapshot } : new() { ShowEntity = snapshot });
                if (fault == "show-hide") packet.List.Insert(end + 1, new() { HideEntity = new() {
                    Entity = selected.Id, Zone = (int)Zone.SETASIDE } });
            }
            var client = session.Client.Copy();
            client.Apply(packet);
            SelfChecks.Check(client.Entities[alias].CardId == ""
                && client.Entities[selected.Id].Get(GameTag.ZONE) == (int)Zone.SETASIDE,
                "retired corruption leaves alias hidden and original in SETASIDE: " + fault);
            // Existing FULLs can be filtered by native rewind reconstruction; packet privacy must still reject them.
            if (fault is "show" or "change")
                SelfChecks.Check(Invariants.CompareReset(game, client, privateBurns: privateBurns)
                    .Any(v => v.Entity == selected.Id && v.Tag == "RESET_PRIVACY"),
                    "RESET_PRIVACY rejects named retired original: " + fault);
            var history = new HistoryShape(game, match.WireReturnId, match.EngineReturnId);
            history.Observe(packet); history.Finish();
            SelfChecks.Check(history.Findings.Any(f => f.Rule == "PRIVACY" && f.VictimCard == "CS2_024"),
                "packet privacy rejects retired identity even if later hidden: " + fault);
        }
        var namedState = session.Client.Copy();
        namedState.Entities[selected.Id].CardId = "CS2_024";
        SelfChecks.Check(Invariants.CompareReset(game, namedState, privateBurns: privateBurns)
            .Any(v => v.Entity == selected.Id && v.Tag == "RESET_PRIVACY"),
            "RESET_PRIVACY independently rejects named retired reconstructed state");
        Console.WriteLine($"PASS two-burn reset: retired={selected.Id} alias={alias}; SHOW/CHANGE rejected as RESET_PRIVACY; FULL/SHOW/CHANGE/SHOW-HIDE rejected as PRIVACY");
    }

    private static void Combat(string fault)
    {
        var filler = Enumerable.Repeat(Cards.FromId("CS2_182"), 29).ToList();
        var game = new Game(new GameConfig { RandomSeed = 23, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = filler, Player2Deck = filler.Append(Cards.FromId("JAIL_509")).ToList() });
        game.StartGame();
        NativeSession session = null;
        bool attacking = false;
        int alias = 0;
        var match = new NativeMatch((id, packet) => {
            if (attacking && packet is PegasusGame.PowerHistory history)
            {
                if (fault == "damage-tag") history.List.RemoveAll(row => row.HasTagChange
                    && row.TagChange.Entity == alias && row.TagChange.Tag == (int)GameTag.DAMAGE);
                foreach (var row in history.List.Where(row => row.HasMetaData
                    && row.MetaData.Type == HistoryMeta.Type.DAMAGE && row.MetaData.Info.Contains(alias)))
                {
                    if (fault == "damage-amount") row.MetaData.Data++;
                    if (fault == "damage-target") row.MetaData.Info = new() { game.Player1.Hero.Id };
                }
            }
            session.Receive(id, packet);
        });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.History.RequireCorrelation = true;
        void Flush() { session.Flush(); game.PowerHistory.Last.Clear(); }
        void Act(PlayerTask task) { Flush(); session.PublishDecision(task.Controller); session.Process(task); game.PowerHistory.Last.Clear(); }
        Flush();
        Act(EndTurnTask.Any(game.Player1));
        var owner = game.Player2;
        owner.BaseMana = 10;
        while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        var retained = (Minion)Entity.FromCard(owner, Cards.FromId("BE_093"), zone: owner.DeckZone);
        Flush(); Generic.Draw(owner, retained); Flush();
        owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
        alias = match.WireReturnId(retained.Id);
        SelfChecks.Check(alias != retained.Id && retained.Zone == owner.HandZone
            && session.Client.Entities[alias].CardId == ""
            && session.Client.Entities[retained.Id].Get(GameTag.ZONE) == (int)Zone.SETASIDE,
            "private return has a distinct anonymous wire actor and retired original");
        if (fault == "none")
        {
            owner.LastCardPlayed = retained.Id;
            Flush();
            ResetReferences(true);
        }
        Act(PlayCardTask.Any(owner, retained));
        retained.IsExhausted = false;
        var target = (Minion)Entity.FromCard(game.Player1, Cards.FromId("BE_093"));
        Generic.SummonBlock(game, target, -1, null);
        Flush();
        SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "returned board card retains strict parity");
        if (fault == "none")
        {
            ResetReferences(false);
            var metadata = new PegasusGame.PowerHistory { List = new() {
                new() { MetaData = new() { Type = HistoryMeta.Type.TARGET, Data = retained.Id, Info = new() { target.Id } } },
                new() { MetaData = new() { Type = HistoryMeta.Type.TARGET, Data = 0, Info = new() { target.Id } } },
                new() { MetaData = new() { Type = HistoryMeta.Type.JOUST, Data = retained.Id, Info = new() { retained.Id } } },
                new() { MetaData = new() { Type = HistoryMeta.Type.JOUST, Data = 0, Info = new() { target.Id } } },
                new() { MetaData = new() { Type = HistoryMeta.Type.DAMAGE, Data = retained.Id, Info = new() { retained.Id } } },
                new() { MetaData = new() { Type = HistoryMeta.Type.HEALING, Data = retained.Id, Info = new() { retained.Id } } }
            } };
            session.Invoke("RemapPrivateReturnPacket", metadata);
            SelfChecks.Check(metadata.List[0].MetaData.Data == alias && metadata.List[1].MetaData.Data == 0
                && metadata.List[2].MetaData.Data == alias && metadata.List[2].MetaData.Info.Single() == alias
                && metadata.List[3].MetaData.Data == 0
                && metadata.List.Skip(4).All(row => row.MetaData.Data == retained.Id && row.MetaData.Info.Single() == alias),
                "TARGET source and JOUST winner remap; zero IDs and damage/healing amounts stay numeric");
            foreach (var tag in new[] { GameTag.ZONE, GameTag.ZONE_POSITION, GameTag.COST, GameTag.EXHAUSTED,
                GameTag.ATK, GameTag.HEALTH, GameTag.CARDRACE })
            {
                var broken = session.Client.Copy(); broken.Entities[alias].Tags[(int)tag]++;
                SelfChecks.Check(Invariants.Compare(game, broken).Any(v => v.Entity == retained.Id && v.Tag == tag.ToString()),
                    "alias rejects " + tag + " corruption");
            }
            var wrongCard = session.Client.Copy(); wrongCard.Entities[alias].CardId = "CS2_182";
            SelfChecks.Check(Invariants.Compare(game, wrongCard).Any(v => v.Tag == "CARD_ID"), "alias rejects wrong identity");
            var missing = session.Client.Copy(); missing.Entities.Remove(alias);
            SelfChecks.Check(Invariants.Compare(game, missing).Any(v => v.Tag == "ENTITY"), "alias rejects missing actor");
            var anonymous = session.Client.Copy(); anonymous.Entities[alias].ActorCardId = "";
            SelfChecks.Check(Invariants.Compare(game, anonymous).Any(v => v.Tag == "ANON_ACTOR"), "alias rejects anonymous actor");
            var ghost = session.Client.Copy(); ghost.Entities[retained.Id].CardId = retained.Card.Id;
            ghost.Entities[retained.Id].Tags[(int)GameTag.ZONE] = (int)Zone.PLAY;
            SelfChecks.Check(Invariants.Compare(game, ghost).Any(v => v.Tag == "ENTITY" && v.Shown == "visible"),
                "retired original still rejects public ghost");
        }
        void ResetReferences(bool hidden)
        {
            var privateBurns = hidden ? new HashSet<int> { retained.Id } : new HashSet<int>();
            var reset = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, owner.Id, "", -1, 0),
                SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(game)
            };
            reset.AddRange(game.IdEntityDic.Values.Select(entity => SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true)));
            reset.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            session.Invoke("SendHistory", reset);
            var errors = Invariants.CompareReset(game, session.Client, privateBurns: privateBurns).ToArray();
            SelfChecks.Check(errors.Length == 0, "reset references preserve privacy and public aliases hidden=" + hidden + " card=" + session.Client.Entities[alias].CardId + " zone=" + session.Client.Entities[alias].Get(GameTag.ZONE) + ": " + string.Join("; ", errors.Select(e => e.ToString())));
            foreach (var tag in new[] { GameTag.LAST_CARD_DRAWN, GameTag.LAST_CARD_PLAYED })
            {
                SelfChecks.Check(owner[tag] == retained.Id && session.Client.Entities[owner.Id].Get(tag) == (hidden ? 0 : alias),
                    "reset reference fixture exercises " + tag);
                foreach (int corrupt in (hidden ? new[] { retained.Id, alias } : new[] { 0, retained.Id }).Append(game.Player1.Hero.Id))
                {
                    var mutant = session.Client.Copy(); mutant.Entities[owner.Id].Tags[(int)tag] = corrupt;
                    SelfChecks.Check(Invariants.CompareReset(game, mutant, privateBurns: privateBurns).Any(v => v.Entity == owner.Id && v.Tag == tag.ToString()),
                        "reset rejects leaked, missing or wrong reference " + tag);
                }
            }
        }
        attacking = true;
        bool unmatched = false;
        try { Act(MinionAttackTask.Any(owner, retained, target)); }
        catch (InvalidOperationException error) when (fault == "damage-tag" && error.Message.Contains("unmatched hook hit"))
        { unmatched = true; }
        if (fault == "damage-tag") SelfChecks.Check(unmatched, "alias missing damage must fail hook correlation");
        else
        {
            session.History.Finish();
            SelfChecks.Check(fault == "none" ? session.History.Findings.Count == 0
                : session.History.Findings.Any(f => f.Severity == "error"), "alias history sensitivity " + fault);
        }
    }
}
