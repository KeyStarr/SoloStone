using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class OutcastGlowTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("TEST Outcast: " + message); }

    internal static void Run()
    {
        var packets = new List<PegasusGame.PowerHistory>();
        var choices = new List<EntityChoices>();
        var match = new NativeMatch((id, message) => {
            if (id == 19) packets.Add((PegasusGame.PowerHistory)message);
            if (id == 17) choices.Add((EntityChoices)message);
        });
        match.Start(); match.SendChoices(choices.Last().Id, new List<int>());
        var game = match.Game; var player = game.Player1;
        void Flush() {
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { game.PowerHistory.Last });
            game.PowerHistory.Last.Clear();
        }
        void Process(PlayerTask task) { Check(game.Process(task), "fixture action failed: " + task); Flush(); }
        IPlayable Draw(string id) => SabberStoneCore.Actions.Generic.DrawCard(player, Cards.FromId(id));
        IEnumerable<PowerHistoryData> Entries() => packets.SelectMany(packet => packet.List);
        int Glow(int id) {
            int value = 0;
            foreach (var entry in Entries()) {
                var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                if (entity?.Entity == id && entity.Tags.Any(t => t.Name == (int)GameTag.POWERED_UP))
                    value = entity.Tags.Last(t => t.Name == (int)GameTag.POWERED_UP).Value;
                if (entry.HasTagChange && entry.TagChange.Entity == id && entry.TagChange.Tag == (int)GameTag.POWERED_UP)
                    value = entry.TagChange.Value;
            }
            return value;
        }
        void ClearHand() {
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            Flush();
        }
        void Expected(IPlayable card, int value, string scenario) {
            Check(Glow(card.Id) == value, scenario + " packet expected " + value + ", got " + Glow(card.Id));
            var snapshot = match.SnapshotVisibleTransportTags(card);
            Check((snapshot.TryGetValue((int)GameTag.POWERED_UP, out int expected) ? expected : 0) == value,
                scenario + " live audit expected " + value);
        }
        ClearHand(); player.BaseMana = 10; player.UsedMana = 0;
        var eye = Draw("CORE_BT_801");
        Check(eye.Card[(GameTag)1333] == 1, "fixture lacks printed Outcast tag");
        Flush(); Expected(eye, 1, "single card");
        int before = Entries().Count(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.POWERED_UP);
        Flush();
        Check(Entries().Count(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.POWERED_UP) == before,
            "unchanged highlight emitted duplicate tag");
        Draw("CS2_231"); Flush(); Expected(eye, 1, "left edge");
        ClearHand();
        var left = Draw("CS2_231"); eye = Draw("CORE_BT_801"); Flush(); Expected(eye, 1, "right edge");
        var right = Draw("CS2_231"); Flush(); Expected(eye, 0, "draw moves right edge to middle");
        Process(PlayCardTask.Minion(player, (Minion)left)); Expected(eye, 1, "play left neighbour");
        Process(EndTurnTask.Any(game.CurrentPlayer)); Expected(eye, 0, "opponent turn");
        Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "return to owner turn");
        Check(match.SnapshotVisibleTransportTags(eye).TryGetValue((int)GameTag.POWERED_UP, out int first) && first == 1,
            "audit must demand positive before packet publication");
        Flush(); Expected(eye, 1, "own turn again");
        player.BaseMana = 10; player.UsedMana = 0;
        Process(PlayCardTask.SpellTarget(player, eye, player.BoardZone.First())); Expected(eye, 0, "played/leaves hand");

        ClearHand(); eye = Draw("CORE_BT_801"); Flush(); Expected(eye, 1, "before shuffle");
        player.HandZone.Remove(eye);
        Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(player, player, eye), "shuffle fixture failed");
        Flush(); Check(Glow(eye.Id) == 0, "hidden shuffle retained hand highlight");
        ClearHand();
        var trade = Draw("CORE_SW_066"); eye = Draw("CORE_BT_801"); Draw("CS2_231"); Flush();
        Expected(eye, 0, "middle before Trade");
        player.UsedMana = 0; Process(TradeCardTask.Any(player, trade)); Expected(eye, 1, "Trade removes left neighbour");
        Check(Entries().Any(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DECK_ACTION),
            "Trade did not publish deck-action block");
        ClearHand(); Draw("CS2_231"); eye = Draw("CORE_BT_801"); trade = Draw("CORE_SW_066"); Flush();
        Expected(eye, 0, "middle before replacement Trade draw");
        player.UsedMana = 0; Process(TradeCardTask.Any(player, trade)); Expected(eye, 0, "Trade replacement retains middle");
        ClearHand(); eye = Draw("CORE_BT_801"); Flush(); Expected(eye, 1, "terminal fixture");
        var enemy = SabberStoneCore.Actions.Generic.DrawCard(game.Player2, Cards.FromId("CORE_BT_801")); Flush();
        Process(EndTurnTask.Any(game.CurrentPlayer));
        game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        var victim = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone); Flush();
        Process(PlayCardTask.SpellTarget(game.Player2, enemy, (Minion)victim));
        Check(Glow(enemy.Id) == 0 && !Entries().Any(e => e.HasTagChange && e.TagChange.Entity == enemy.Id && e.TagChange.Tag == (int)GameTag.POWERED_UP),
            "opponent Outcast received highlight tag");
        Process(EndTurnTask.Any(game.CurrentPlayer)); Expected(eye, 1, "own turn before game end");
        Process(ConcedeTask.Any(player)); Expected(eye, 0, "game end");
        foreach (var packet in packets)
            using (var stream = new System.IO.MemoryStream()) {
                packet.Serialize(stream); Check(stream.Length == packet.GetSerializedSize(), "packet serialization size");
            }
        Console.WriteLine("PASS Outcast hand glow: edges/single/middle, draw, play, turn, Trade, shuffle, game end, dedup, independent audit and opponent privacy");
    }
}
