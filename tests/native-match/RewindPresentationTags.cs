using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    private static void RewindPresentationTags()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var player = match.Game.CurrentPlayer;
            foreach (string id in new[] { "FIR_911", "FIR_914", "FIR_916" })
            {
                var card = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(id), zone: player.SetasideZone);
                FlushFieldHistory(match);
                var ordinary = PowerHistoryBuilder.FullEntity(card).Entity.Tags;
                var reset = PowerHistoryBuilder.FullEntity(card, reset: true).Entity.Tags;
                Check(card.Card[(GameTag)2505] == 1 && !card.NativeTags.ContainsKey((GameTag)2505),
                    "Smoldering fixture has definition-only Immolating");
                Check(!reset.ContainsKey((GameTag)2505) && !reset.ContainsKey((GameTag)2600),
                    "reset must not turn Smoldering definition metadata into live Immolate state");
                Check(reset.Keys.All(tag => ordinary.ContainsKey(tag) || tag == GameTag.COST),
                    "reset must not add arbitrary definition-only actor state");
                if (!bot)
                    Check(!match.SnapshotVisibleTransportTags(card).ContainsKey(2505),
                        "endpoint audit agrees with ordinary Smoldering transport");
                // Real instance state, including an explicit off, must survive reset and save/load.
                foreach (int value in new[] { 1, 0 })
                {
                    card[(GameTag)2505] = value;
                    card[(GameTag)2600] = value * 3;
                    var restored = SabberStoneCore.Model.Game.LoadSnapshot(match.Game.SaveSnapshot()).IdEntityDic[card.Id];
                    var tags = PowerHistoryBuilder.FullEntity(restored, reset: true).Entity.Tags;
                    Check(tags[(GameTag)2505] == value && tags[(GameTag)2600] == value * 3,
                        "reset preserves explicit Immolate instance state and zero across persistence");
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Rewind presentation tags: both seats, all Smoldering definitions, ordinary/reset parity, instance on/off persistence");
    }
}
