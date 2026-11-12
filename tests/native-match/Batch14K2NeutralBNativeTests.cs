using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("b14-k2-neutral-b-native", ProofKind.Native)]
    private static void Batch14K2NeutralBPrivacy()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 3, StartPlayer = 1, SkipMulligan = true,
                Shuffle = false, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "privacy fixture owner turn");
            var owner = game.CurrentPlayer;
            foreach (var player in new[] { owner, owner.Opponent })
                foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            FlushHistory(match); game.PowerHistory.Last.Clear();
            var victim = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_200"));
            Generic.SummonBlock(game, victim, -1, null); victim.Damage = 2;
            var ooze = Generic.DrawCard(owner, Cards.FromId("TLC_252")); owner.BaseMana = 10; owner.UsedMana = 0;
            Check(game.Process(PlayCardTask.Any(owner, ooze, victim)), "Ooze native play");
            var bones = owner.HandZone.Single(); FlushHistory(match); game.PowerHistory.Last.Clear();
            var rows = NativeHistory(capture).SelectMany(h => new[] { h.FullEntity, h.ShowEntity, h.ChangeEntity })
                .Where(e => e != null && e.Entity == bones.Id).ToArray();
            Check(rows.Length > 0 && rows.All(e => opponent ? string.IsNullOrEmpty(e.Name) : e.Name == "TLC_829t"),
                "generated Bones identity private to owner");
            if (!opponent) Check(SentTextTag(capture, bones.Id, (int)GameTag.TAG_SCRIPT_DATA_NUM_1) == 6
                && SentTextTag(capture, bones.Id, (int)GameTag.TAG_SCRIPT_DATA_NUM_2) == 5,
                "Bones owner sees pinned Attack/Health script values");
            else Check(!NativeHistory(capture).Any(h => h.HasTagChange && h.TagChange.Entity == bones.Id
                && (h.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 || h.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2)),
                "opponent receives no hidden Bones stat payload");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS b14-k2-neutral-b native: Bones text and generated-hand privacy, both owners");
    }
}
