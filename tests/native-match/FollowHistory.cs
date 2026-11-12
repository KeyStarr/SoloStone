using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    [GroupProof("b15-follow-native", ProofKind.Native)]
    private static void FollowHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CAP_002", "CAP_402", "CAP_802" }) {
            var (match, capture) = AnimationMatch(bot);
            var g = match.Game; var p = g.CurrentPlayer;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var bearer = EngineEntity.FromCard(p, Cards.FromId("CS2_231"), zone: p.HandZone);
            var source = EngineEntity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            int deckCount = p.Opponent.DeckZone.Count;
            InvokeProcess(match, p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            if (p.Choice != null) {
                Check(id == "CAP_002", "only Footsteps opens choices");
                InvokeProcess(match, ChooseTask.Pick(p, p.Choice.Choices.First()));
            }
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var marked = p.HandZone.Single(c => c.AppliedEnchantments?.Count > 0);
            var actor = marked.AppliedEnchantments.Single();
            Check(actor.Card.AssetId > 0 && actor[GameTag.ATTACHED] == marked.Id
                && actor[GameTag.CREATOR] != 0 && actor[GameTag.TAG_ONE_TURN_EFFECT] == 1,
                "retail Follow attachment shape");
            if (bot) {
                foreach (int hidden in new[] { marked.Id, actor.Id })
                    Check(entries.Where(e => SnapshotEntity(e) == hidden).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                        "opponent never sees hidden Follow hand/attachment identities");
            }
            else Check(entries.Any(e => SnapshotEntity(e) == actor.Id && SnapshotName(e) == actor.Card.Id),
                "owner sees named Follow attachment");
            if (id == "CAP_402") {
                var imp = p.Opponent.DeckZone.Single(c => c.Card.Id == "CAP_400t2t");
                Check(p.Opponent.DeckZone.Count == deckCount + 1, "Evidence inserts Imp");
                // Generated-to-deck identity is deliberately private to both views.
                Check(entries.Where(e => SnapshotEntity(e) == imp.Id).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                    "generated enemy-deck identity stays private");
            }
            if (id == "CAP_802") {
                var ghost = p.BoardZone.Single();
                Check(entries.Any(e => SnapshotEntity(e) == ghost.Id && SnapshotName(e) == "CAP_802t"
                    && SnapshotTag(e, GameTag.REBORN) == 1), "public Ghost carries exported Reborn");
            }
            Check(entries.Where(e => e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity)
                .All(e => !(e.HasFullEntity ? e.FullEntity.Tags : e.HasShowEntity ? e.ShowEntity.Tags : e.ChangeEntity.Tags)
                    .Any(t => t.Name >= 30000)), "internal attachment/choice state filtered");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Follow native actors, private hand/deck/Discover, both sides and serialization");
    }
}
