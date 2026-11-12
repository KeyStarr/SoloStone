using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    [GroupProof("b15-quest-closure-native", ProofKind.Native)]
    private static void B15QuestClosureNative()
    {
        var (match, capture) = AnimationMatch(botTurn: true);
        var g = match.Game; var owner = g.Player2;
        foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        owner.BaseMana = 10; FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        var source = QuestNativeProof.Play(g, owner, "TLC_602t");
        var rewards = g.IdEntityDic.Values.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        FlushFieldHistory(match);
        Check(rewards.Length == 9 && History(capture, cursor)
            .Where(e => rewards.Any(r => r.Id == SnapshotEntity(e))).All(e => string.IsNullOrEmpty(SnapshotName(e))),
            "opponent Latorvius hand/deck identities remain hidden");
        foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        cursor = capture.Packets.Count;
        var life = QuestNativeProof.Add(owner, "TLC_817t3"); var death = QuestNativeProof.Add(owner, "GAME_005");
        Generic.ChangeEntityBlock(owner, death, Cards.FromId("TLC_817t4"), true);
        var combined = owner.HandZone.SingleOrDefault(c => c.Card.Id == "TLC_817t5"); FlushFieldHistory(match);
        Check(combined != null && owner.HandZone.Count == 1 && History(capture, cursor)
            .Where(e => new[] { life.Id, death.Id, combined.Id }.Contains(SnapshotEntity(e))).All(e => string.IsNullOrEmpty(SnapshotName(e))),
            "opponent combined reward identity remains hidden");
        var fair = FairBotState.Create(g, g.Player1.PlayerId, 123, DeckStyle.Tempo);
        var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fair);
        FairViewHiddenOfferProof.AssertHiddenActors(g, view, owner.PlayerId, life.Id, death.Id, combined.Id);
        QuestNativeProof.Play(g, owner, "TLC_817");
        FlushFieldHistory(match);
        var components = owner.SecretZone.ModernQuests;
        Check(components.Count == 2 && components.All(q => match.SnapshotVisibleTransportTags(q)[(int)GameTag.QUEST_PROGRESS_TOTAL] == 4),
            "native split quest actors carry retail progress totals");
        foreach (var q in components) q[(GameTag)33060] = 123;
        Check(components.All(q => !match.SnapshotVisibleTransportTags(q).ContainsKey(33060)), "private matching mask filtered");
        cursor = capture.Packets.Count;
        var queen = QuestNativeProof.Play(g, owner, "UNG_920t1");
        var brood = g.IdEntityDic.Values.Where(c => c[GameTag.DISPLAYED_CREATOR] == queen.Id).ToArray();
        FlushFieldHistory(match);
        Check(brood.Length == 20 && History(capture, cursor).Where(e => brood.Any(r => r.Id == SnapshotEntity(e)))
            .All(e => string.IsNullOrEmpty(SnapshotName(e))), "opponent Carnassa shuffled Brood identities remain hidden");
        cursor = capture.Packets.Count;
        QuestNativeProof.Play(g, owner, "UNG_028t"); FlushFieldHistory(match);
        Check(!History(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Tag == 33066), "once-per-game state never projected");
        capture.SerializeAll();
        Console.WriteLine("PASS b15 quest native: hidden generated rewards, fresh combination, component totals, private masks");
    }
}
