using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal static class OutcastProof
{
    internal static void Run()
    {
        NativeSession session = null;
        var choices = new List<EntityChoices>();
        var match = new NativeMatch((id, packet) => {
            session.Receive(id, packet);
            if (packet is EntityChoices choice) choices.Add(choice);
        });
        session = new NativeSession(match);
        match.Start(); match.SendChoices(choices.Last().Id, new());
        var game = match.Game; var player = game.Player1;
        foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        Generic.DrawCard(player, Cards.FromId("CS2_231"));
        var eye = Generic.DrawCard(player, Cards.FromId("CORE_BT_801"));
        var right = Generic.DrawCard(player, Cards.FromId("CS2_231"));
        session.Flush(); game.PowerHistory.Last.Clear();
        player.BaseMana = 10; player.UsedMana = 0;
        SelfChecks.Check(game.Process(PlayCardTask.Minion(player, right)), "Outcast edge-neighbour play");
        SelfChecks.Check(match.SnapshotVisibleTransportTags(eye).TryGetValue((int)GameTag.POWERED_UP, out int first) && first == 1,
            "live audit must demand first Outcast positive independently of publication");
        SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == eye.Id && v.Tag == "POWERED_UP" && v.Expected == "1"),
            "strict oracle must detect unsent first Outcast positive");
        session.Flush(); game.PowerHistory.Last.Clear();
        SelfChecks.Check(session.Client.Entities[eye.Id].Get(GameTag.POWERED_UP) == 1
            && !Invariants.Compare(game, session.Client).Any(), "Outcast production packet must satisfy strict oracle");
        var tags = session.Client.Entities[eye.Id].Tags;
        tags.Remove((int)GameTag.POWERED_UP);
        SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == eye.Id && v.Tag == "POWERED_UP"),
            "strict oracle must detect dropped Outcast highlight");
        tags[(int)GameTag.POWERED_UP] = 1;
        Generic.DrawCard(player, Cards.FromId("CS2_231"));
        SelfChecks.Check(match.SnapshotVisibleTransportTags(eye)[(int)GameTag.POWERED_UP] == 0,
            "live audit must independently demand stale Outcast reset");
        SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == eye.Id && v.Tag == "POWERED_UP" && v.Expected == "0"),
            "strict oracle must detect stale Outcast positive before reset publication");
        session.Flush(); game.PowerHistory.Last.Clear();
        SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "Outcast reset packet must satisfy strict oracle");
        player.SetasideZone.Add(player.HandZone.Remove(eye));
        tags[(int)GameTag.POWERED_UP] = 1;
        SelfChecks.Check(match.SnapshotVisibleTransportTags(eye)[(int)GameTag.POWERED_UP] == 0
            && Invariants.Compare(game, session.Client).Any(v => v.Entity == eye.Id && v.Tag == "POWERED_UP"),
            "audit and strict oracle must reject stale Outcast after leaving hand");
        tags[(int)GameTag.POWERED_UP] = 0; session.Flush();
        SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "Outcast leaving-hand packet invariants");
        game.PowerHistory.Last.Clear();
        var shuffled = Generic.DrawCard(player, Cards.FromId("CORE_BT_801")); session.Flush(); game.PowerHistory.Last.Clear();
        SelfChecks.Check(session.Client.Entities[shuffled.Id].Get(GameTag.POWERED_UP) == 1, "shuffle starts with active Outcast");
        player.HandZone.Remove(shuffled);
        SelfChecks.Check(Generic.ShuffleIntoDeck(player, player, shuffled), "Outcast shuffle"); session.Flush();
        SelfChecks.Check(session.Client.Entities[shuffled.Id].Get(GameTag.POWERED_UP) == 0
            && !Invariants.Compare(game, session.Client).Any(), "HIDE must not retain local Outcast highlight");
        session.Client.Entities[shuffled.Id].Tags[(int)GameTag.POWERED_UP] = 1;
        SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == shuffled.Id && v.Tag == "POWERED_UP"),
            "strict oracle must detect local Outcast highlight surviving HIDE");
        session.Client.Entities[shuffled.Id].Tags[(int)GameTag.POWERED_UP] = 0;
        // Dark Bribe draws a glowing Outcast, then hides it by giving the physical card away.
        foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        foreach (string id in new[] { "BE_264", "CS2_231", "CS2_231" })
            player.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(id)));
        var bribe = Generic.DrawCard(player, Cards.FromId("JAIL_206"));
        player.UsedMana = 0; session.Flush(); game.PowerHistory.Last.Clear();
        session.Process(PlayCardTask.Any(player, bribe));
        int givenId = player.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "BE_264");
        SelfChecks.Check(session.Client.Entities[givenId].Get(GameTag.POWERED_UP) == 1, "Dark Bribe offers glowing Harrower");
        session.PublishDecision(player);
        match.SendChoices(choices.Last().Id, new List<int> { givenId });
        SelfChecks.Check(game.IdEntityDic[givenId].Controller == player.Opponent
            && session.Client.Entities[givenId].CardId == ""
            && session.Client.Entities[givenId].Get(GameTag.POWERED_UP) == 0,
            "hidden hand transfer clears Outcast glow before forgetting visibility");
        session.Process(EndTurnTask.Any(player));
        player.Opponent.BaseMana = 10; player.Opponent.UsedMana = 0;
        session.Process(PlayCardTask.Any(player.Opponent, game.IdEntityDic[givenId]));
        session.Process(EndTurnTask.Any(player.Opponent));
        SelfChecks.Check(session.Client.Entities[givenId].Get(GameTag.POWERED_UP) == 0
            && !Invariants.Compare(game, session.Client).Any(), "given Harrower stays unlit after opponent play and next human turn");
        Console.WriteLine("PASS Outcast strict transport/live audit: first unsent positive, dropped highlight, stale middle/off-hand/hidden positive; violations=0");
    }
}
