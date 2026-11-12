using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Pinned Player.GetNumAvailableResources; settled prediction counters are zero.
    private static Dictionary<int, int> NativeManaTags(Capture capture, int playerId)
    {
        var tags = new Dictionary<int, int>();
        foreach (var entry in History(capture, 0))
        {
            var create = entry.HasCreateGame ? entry.CreateGame : entry.HasResetGame ? entry.ResetGame.CreateGame : null;
            var player = create?.Players.Single(p => p.Entity.Id == playerId);
            if (player != null)
                foreach (var tag in player.Entity.Tags) tags[tag.Name] = tag.Value;
            if (entry.HasTagChange && entry.TagChange.Entity == playerId)
                tags[entry.TagChange.Tag] = entry.TagChange.Value;
        }
        return tags;
    }

    // Playtest (Herald Shaman): after Overload the client showed 8 available mana while the engine allowed 7. The client
    // computes available mana as RESOURCES + TEMP_RESOURCES - RESOURCES_USED and expects the overload-locked part inside
    // RESOURCES_USED; the engine keeps OVERLOAD_LOCKED separate. Transport therefore projects spent plus locked mana into
    // RESOURCES_USED (snapshots, Rewind resets and unlocks too).
    private static void OverloadManaParity()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-shaman-herald");
            var game = match.Game;
            var owner = game.CurrentPlayer;
            game.Turn = bot ? 12 : 11;
            owner.BaseMana = 6;
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            FlushFieldHistory(match);

            void AssertMana(int expected, string phase)
            {
                var tags = NativeManaTags(capture, owner.Id);
                int Get(GameTag tag) => tags.TryGetValue((int)tag, out int value) ? value : 0;
                int native = Math.Max(0, Get(GameTag.RESOURCES) + Get(GameTag.TEMP_RESOURCES) - Get(GameTag.RESOURCES_USED));
                Check(owner.RemainingMana == expected, "Overload engine " + phase);
                Check(native == expected, $"Overload native {phase}: expected={expected}, client={native}, engine={owner.RemainingMana}, bot={bot}");
                Check(Get(GameTag.OVERLOAD_LOCKED) == owner.OverloadLocked && Get(GameTag.OVERLOAD_OWED) == owner.OverloadOwed,
                    "Overload lock/owed presentation " + phase);
            }
            void Play(string id)
            {
                var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
                FlushFieldHistory(match);
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
                FlushFieldHistory(match);
            }
            void NextOwnTurn()
            {
                InvokeProcess(match, game.CurrentPlayer.Options().Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
                InvokeProcess(match, game.CurrentPlayer.Options().Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
                FlushFieldHistory(match);
            }

            Check(Cards.FromId("DINO_413").Overload == 0 && Cards.FromId("CATA_561").Overload == 0
                && Cards.FromId("CATA_569").Overload == 1, "L9 printed Overload belongs to Ceremonial Clash");
            Play("DINO_413");
            Play("CATA_561");
            AssertMana(0, "turn 11 Stegodon/Ritual");
            NextOwnTurn();
            AssertMana(7, "turn 13 refill");
            Play("CATA_569");
            AssertMana(3, "Clash owes one next turn");
            NextOwnTurn();
            Check(owner.BaseMana == 8 && owner.OverloadLocked == 1 && owner.OverloadOwed == 0,
                "L9 turn 15 has eight crystals and one locked");
            AssertMana(7, "turn 15 locked crystal");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER));
            FlushFieldHistory(match);
            AssertMana(5, "hero power under overload");
            Play("GAME_005");
            AssertMana(6, "temporary mana under overload");
            Play("JAIL_802");
            AssertMana(4, "spending temporary then permanent mana");
            owner.UsedMana -= 2;
            FlushFieldHistory(match);
            AssertMana(6, "refresh spent crystals keeps lock");
            owner.BaseMana--;
            FlushFieldHistory(match);
            AssertMana(5, "destroy crystal keeps lock");
            owner.BaseMana++;
            FlushFieldHistory(match);
            AssertMana(6, "gain filled crystal keeps lock");
            // A second overloaded turn must retain the lock even when its tag value does not change.
            owner.OverloadOwed = 1;
            NextOwnTurn();
            AssertMana(8, "consecutive overloaded turn");
            owner.OverloadLocked = 0;
            FlushFieldHistory(match);
            AssertMana(9, "unlock without a spending tag");
            NextOwnTurn();
            AssertMana(10, "ordinary refill after overload");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS overload mana parity: L9 turn 15, both players, spending/temp/refresh/gain/destroy/consecutive lock/unlock");
    }
}
