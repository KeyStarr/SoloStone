using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("dormant-copy-native", ProofKind.Native)]
    private static void DormantCopyHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            owner.BaseMana = 10; owner.UsedMana = 0;
            var source = (Minion)Generic.DrawCard(owner, Cards.FromId("EDR_469"));
            InvokeProcess(match, PlayCardTask.Minion(owner, source));
            Check(source.IsDormant, "Dormant copy source sleeps");
            source.HasTaunt = true;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var copy = (Minion)Generic.Copy(owner, owner.Hero, source, SabberStoneCore.Enums.Zone.PLAY);
            FlushFieldHistory(match);
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            int clear = rows.FindIndex(r => r.HasTagChange && r.TagChange.Entity == copy.Id
                && r.TagChange.Tag == (int)GameTag.TAUNT && r.TagChange.Value == 0);
            int enter = rows.FindIndex(r => r.HasTagChange && r.TagChange.Entity == copy.Id
                && r.TagChange.Tag == (int)GameTag.DORMANT && r.TagChange.Value == 1);
            Check(clear >= 0 && enter > clear, "Dormant copy emits entering TAUNT=0 before DORMANT=1, owner=" + owner.PlayerId);
            var full = rows.Single(r => r.HasFullEntity && r.FullEntity.Entity == copy.Id).FullEntity;
            Check(full.Tags.All(t => t.Name != (int)GameTag.DORMANT || t.Value == 0)
                && full.Tags.Any(t => t.Name == (int)GameTag.TAUNT && t.Value == 1),
                "Dormant copy FULL retains pre-entry sleep and keyword state");
            Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots()
                .Select(p => (System.Collections.Generic.IReadOnlyList<PegasusGame.PowerHistoryData>)p.List)).Count == 0,
                "Dormant copy native admission");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Dormant copy entering visuals for both owners");
    }
}
