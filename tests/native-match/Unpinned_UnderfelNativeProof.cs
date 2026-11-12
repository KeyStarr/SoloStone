using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("b15-underfel-native", ProofKind.Native)]
    private static void Unpinned_UnderfelNative()
    {
        Check(Cards.All.Any(c => c.Id == "TLC_446" && c.Implemented), "Escape the Underfel native recipe missing");
        var capture = new Capture(); var match = StartAtHumanTurn(capture); var g = match.Game; var p = g.Player1;
        p.BaseMana = 10; p.UsedMana = 0;
        QuestNativeProof.Play(g, p, "TLC_446t");
        var rift = p.BoardZone.Single(m => m.Card.Id == "TLC_446t1");
        var hand = QuestNativeProof.Add(p, "CATA_499"); TemporaryRules.Make(hand);
        void Publish() => typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        FlushFieldHistory(match); Publish();
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(o => o.MainOption?.Id == rift.Id);
        Check(index >= 0 && options.Options[index].Type_ == PegasusGame.Option.Type.POWER
            && options.Options[index].MainOption.PowerKeyword == 4089
            && options.Options[index].MainOption.Targets.Any(t => t.Id == hand.Id)
            && options.Options[index].SubOptions.Count == 0, "Rift retail interactable POWER with hand targets and no prompt");
        var tags = match.SnapshotVisibleTransportTags(rift);
        Check(tags[4089] == 1 && (!tags.TryGetValue(4090, out int activationCost) || activationCost == 0)
            && tags[1508] == 1 && tags[4257] == 1 && tags[448] == 1,
            "exact interactable minion capabilities projected");
        Check(match.ValidateOption(options.Id, index, hand.Id, -1, 0) is ActivateLocationTask,
            "interactable reuses activation task");
        foreach (int bad in new[] { 0, rift.Id, p.Opponent.HandZone[0].Id, p.DeckZone[0].Id })
            AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, bad, -1, 0));
        Generic.DiscardBlock(p, hand);
        AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, hand.Id, -1, 0));
        hand = QuestNativeProof.Add(p, "CATA_499"); Publish(); options = capture.Last<AllOptions>(14);
        index = options.Options.FindIndex(o => o.MainOption?.Id == rift.Id);
        match.SendOption(options.Id, index, hand.Id, -1, 0);
        Check(hand[GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME
            && g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect, "native throw resolves and returns options");
        Check(!capture.Last<AllOptions>(14).Options.Any(o => o.MainOption?.Id == rift.Id), "used Rift option removed");
        Check(match.SnapshotVisibleTransportTags(rift)[(int)GameTag.EXHAUSTED] == 1, "used Rift readiness projected");
        Check(!match.SnapshotVisibleTransportTags(rift).ContainsKey(33067), "private use stamp filtered");
        capture.SerializeAll();

        var (enemyMatch, enemyCapture) = AnimationMatch(botTurn: true);
        var enemyGame = enemyMatch.Game; var owner = enemyGame.Player2;
        owner.BaseMana = 10; owner.UsedMana = 0;
        QuestNativeProof.Play(enemyGame, owner, "TLC_446t");
        var enemyRift = owner.BoardZone.Single(m => m.Card.Id == "TLC_446t1");
        var thrown = QuestNativeProof.Add(owner, "CATA_499"); TemporaryRules.Make(thrown);
        var attachments = thrown.AppliedEnchantments.Select(e => e.Id).ToArray();
        FlushFieldHistory(enemyMatch); int cursor = enemyCapture.Packets.Count;
        var task = owner.Options().Single(t => t is ActivateLocationTask && t.Source.Id == enemyRift.Id && t.ActionTarget?.Id == thrown.Id);
        Check(enemyGame.Process(task), "opponent throw succeeds"); FlushFieldHistory(enemyMatch);
        Check(History(enemyCapture, cursor).Any(e => e.HasHideEntity && e.HideEntity.Entity == thrown.Id
            && e.HideEntity.Zone == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME),
            "opponent sees anonymous thrown card leave hand");
        var hidden = History(enemyCapture, cursor).Where(e => new[] { thrown.Id }.Concat(attachments).Contains(SnapshotEntity(e))).ToArray();
        Check(hidden.All(e => string.IsNullOrEmpty(SnapshotName(e))), "opponent never receives thrown identity or attachment identity");
        Check(!History(enemyCapture, cursor).Any(e => e.HasTagChange && (e.TagChange.Tag == 33067 || e.TagChange.Tag == 3630)),
            "private stamp and hidden Temporary cleanup never leak");
        var fair = FairBotState.Create(enemyGame, enemyGame.Player1.PlayerId, 123, DeckStyle.Tempo);
        var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        FairViewHiddenOfferProof.AssertHiddenActors(enemyGame, view, thrown.Controller.PlayerId, thrown.Id);
        enemyCapture.SerializeAll();
        Console.WriteLine("PASS Unpinned Underfel native: keyword 4089, hand targets, stale rejection, no stall, opponent throw privacy");
    }
}
