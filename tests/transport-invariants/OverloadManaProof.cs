using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

// Regression for a playtest bug: with Overload locked mana, a Shaman deck showed 8 available mana in the native client
// while the engine allowed 7. The client computes available mana as RESOURCES + TEMP_RESOURCES - RESOURCES_USED and
// expects locked Overload to be inside RESOURCES_USED, but the engine keeps OVERLOAD_LOCKED separate. The transport now
// projects spent plus locked mana into RESOURCES_USED. Checked for both seats, live updates, CreateGame snapshots,
// Rewind and unlock.
internal static class OverloadManaProof
{
    internal static void Run()
    {
        foreach (bool bot in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            void Flush() { session.Flush(); game.PowerHistory.Last.Clear(); }
            void AssertMana(Controller owner, int expected, string phase)
            {
                var player = session.Client.Entities[owner.Id];
                int shown = Math.Max(0, player.Get(GameTag.RESOURCES) + player.Get(GameTag.TEMP_RESOURCES) - player.Get(GameTag.RESOURCES_USED));
                SelfChecks.Check(shown == expected, $"Overload {phase}: player={owner.PlayerId} expected={expected} shown={shown}");
                SelfChecks.Check(!Invariants.CompareMana(game, session.Client).Any(), "Overload mana invariant " + phase);
            }
            Flush();
            var owner = bot ? game.Player2 : game.Player1;
            owner.BaseMana = 8; owner.UsedMana = 0; owner.OverloadLocked = 1;
            Flush();
            AssertMana(owner, 7, "locked crystal");
            owner.UsedMana = 2; owner.TemporaryMana = 1;
            Flush();
            AssertMana(owner, 6, "spent plus temp");

            // Frozen initial/reset snapshots must project their own values, never today's getters.
            var frozen = PowerHistoryBuilder.CreateGame(game, new[] { game.Player1, game.Player2 });
            owner.UsedMana = 5; owner.OverloadLocked = 3;
            game.PowerHistory.Last.Clear();
            session.Invoke("SendHistory", new List<IPowerHistoryEntry> { frozen });
            var snapshot = session.Client.Entities[owner.Id];
            SelfChecks.Check(snapshot.Get(GameTag.RESOURCES_USED) == 3 && snapshot.Get(GameTag.OVERLOAD_LOCKED) == 1,
                "Overload frozen CreateGame includes exactly its spent plus locked mana");
            // Flush later events against the frozen cursor while live getters hold different values.
            session.Invoke("SendHistory", new List<IPowerHistoryEntry> {
                PowerHistoryBuilder.TagChange(owner.Id, GameTag.RESOURCES_USED, 3),
                PowerHistoryBuilder.TagChange(owner.Id, GameTag.OVERLOAD_LOCKED, 0) });
            SelfChecks.Check(snapshot.Get(GameTag.RESOURCES_USED) == 3, "Overload historical unlock uses historical expenditure");

            owner.UsedMana = 2; owner.OverloadLocked = 1;
            var reset = PowerHistoryBuilder.ResetGame(game);
            var fulls = game.IdEntityDic.Values.Select(entity => PowerHistoryBuilder.FullEntity(entity)).ToArray();
            owner.UsedMana = 4; owner.OverloadLocked = 2;
            Flush();
            var history = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, owner.Id, "", -1, 0), reset };
            history.AddRange(fulls);
            history.Add(PowerHistoryBuilder.BlockEnd());
            // Rewind restores the engine before sending its frozen reset batch.
            owner.UsedMana = 2; owner.OverloadLocked = 1;
            game.PowerHistory.Last.Clear();
            session.Invoke("SendHistory", history);
            SelfChecks.Check(snapshot.Get(GameTag.RESOURCES_USED) == 3 && snapshot.Get(GameTag.OVERLOAD_LOCKED) == 1,
                "Overload Rewind restores projected frozen expenditure");
            AssertMana(owner, 6, "Rewind baseline");
            owner.OverloadLocked = 0;
            Flush();
            AssertMana(owner, 7, "unlock after Rewind");
        }
        Console.WriteLine("PASS overload mana transport: both players; immutable CreateGame/history/Rewind; unlock");
    }
}
