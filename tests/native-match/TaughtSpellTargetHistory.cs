using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("taught-spell-target", ProofKind.Native)]
    private static void TaughtSpellTargetHistory()
    {
        foreach (string id in new[] { "MEND_046t", "TIME_704t" })
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var g = match.Game; var p = g.CurrentPlayer;
            foreach (var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
            var friendly = EngineEntity.FromCard(p, Cards.FromId("CS2_182"), zone: p.BoardZone);
            var chosen = EngineEntity.FromCard(p.Opponent, Cards.FromId("CS2_182"), zone: p.Opponent.BoardZone);
            var token = EngineEntity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
            var taught = PayloadRules.StoreCard(token, 1, Cards.FromId("EX1_246"), PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
            token[GameTag.TAG_SCRIPT_DATA_NUM_1] = taught.Id;
            FlushFieldHistory(match);
            int cursor = capture.WirePackets.Count;
            var task = p.Options().FirstOrDefault(t => t.Source?.Id == token.Id && t.Target?.Id == chosen.Id);
            Check(task != null, id + " NPC/human options choose carved Hex target");
            if (bot) InvokeProcess(match, task);
            else
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int index = options.Options.FindIndex(o => o.MainOption?.Id == token.Id);
                Check(index >= 0 && options.Options[index].MainOption.Targets.Select(t => t.Id).ToHashSet()
                    .SetEquals(new[] { friendly.Id, chosen.Id }), id + " native options carry exactly carved spell targets");
                match.SendOption(options.Id, index, chosen.Id, -1, 1);
            }
            FlushFieldHistory(match);
            Check(chosen.Card.Id == "hexfrog" && friendly.Card.Id == "CS2_182", id + " native chosen Hex resolves");
            var cast = p.GraveyardZone.Single(c => c.Card.Id == "EX1_246");
            var wire = capture.HistorySnapshots(cursor).SelectMany(h => h.List).ToArray();
            Check(wire.Any(e => SnapshotEntity(e) == cast.Id && SnapshotTag(e, GameTag.CARD_TARGET) == chosen.Id)
                || wire.Any(e => e.HasTagChange && e.TagChange.Entity == cast.Id
                    && e.TagChange.Tag == (int)GameTag.CARD_TARGET && e.TagChange.Value == chosen.Id),
                id + " cast Hex CARD_TARGET reaches serialized wire for " + (bot ? "NPC" : "human"));
            capture.SerializeAll();
        }
        Console.WriteLine("PASS taught spell native options: Treant/Pupil chosen Hex, human/NPC");
    }
}
