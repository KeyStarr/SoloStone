using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void SmolderingBurnHistory()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "FIR_911", "FIR_914", "FIR_916" })
        {
            var (match, capture) = AnimationMatch(opponent);
            var game = match.Game;
            var owner = game.CurrentPlayer;
            var card = Generic.DrawCard(owner, Cards.FromId(id));
            game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            var first = PowerHistoryBuilder.FullEntity(card);
            for (int remaining = 3; remaining >= 1; remaining--)
            {
                Check(card[GameTag.TAG_SCRIPT_DATA_NUM_2] == remaining, id + " countdown fixture");
                var tags = ReplayNativeEntityTags(NativeHistory(capture), card.Id);
                if (!opponent)
                {
                    Check(tags.GetValueOrDefault(2505) == 1 && tags.GetValueOrDefault(2600) == remaining,
                        id + " native hand fire must track remaining owner turns, including last-turn stage 1");
                    Check(tags.GetValueOrDefault(4213) == 1, id + " retail opponent suppression retained");
                    Check(match.SnapshotVisibleTransportTags(card).GetValueOrDefault(2600) == remaining,
                        id + " endpoint audit agrees with fire packets");
                }
                else
                    Check(!tags.ContainsKey(2505) && !tags.ContainsKey(2600) && !tags.ContainsKey(4213),
                        id + " hidden opponent hand leaks no burn state");
                Check(!card.NativeTags.ContainsKey((GameTag)2505) && !card.NativeTags.ContainsKey((GameTag)2600),
                    "Fire projection must not write engine state");
                if (remaining == 1) break;
                InvokeProcess(match, EndTurnTask.Any(owner));
                FlushFieldHistory(match);
                InvokeProcess(match, EndTurnTask.Any(game.CurrentPlayer));
                FlushFieldHistory(match);
            }
            if (!opponent)
            {
                int cursor = capture.Packets.Count;
                SendHistoryEntries(match, new List<IPowerHistoryEntry> { first });
                var historical = NativeHistory(capture, cursor).First(e => e.HasFullEntity && e.FullEntity.Entity == card.Id);
                Check(SnapshotTag(historical, (GameTag)2600) == 3,
                    id + " historical snapshot must not borrow today's last-turn stage");
                FlushFieldHistory(match);
            }
            InvokeProcess(match, EndTurnTask.Any(owner));
            FlushFieldHistory(match);
            Check(card.Zone == owner.GraveyardZone && owner.DiscardedEntities.Contains(card.Id),
                id + " third owner end still discards");
            var discarded = ReplayNativeEntityTags(NativeHistory(capture), card.Id);
            Check(discarded.GetValueOrDefault(2505) == 0 && discarded.GetValueOrDefault(2600) == 0,
                id + " discard clears hand fire rather than retaining definition defaults");
            owner.HandZone.Add(owner.GraveyardZone.Remove(card));
            game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            Check(card[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3, id + " reentry resets countdown");
            if (!opponent)
                Check(ReplayNativeEntityTags(NativeHistory(capture), card.Id).GetValueOrDefault(2600) == 3,
                    id + " reentry resets fire stage");
            game.AllOptionsMap.Clear();
            var state = game.SaveSnapshot();
            string history = game.PowerHistory.Print(false);
            FlushFieldHistory(match);
            Check(state.SequenceEqual(game.SaveSnapshot()) && history == game.PowerHistory.Print(false),
                "Fire projection leaves engine snapshot, RNG and history unchanged");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Smoldering burn: all three cards, stages 3/2/1, both seats/privacy, historical snapshots, discard/reentry, read-only projection");
    }
}
