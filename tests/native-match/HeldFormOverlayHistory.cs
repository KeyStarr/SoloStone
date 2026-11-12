using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("held-form-overlay", ProofKind.Native)]
    private static void HeldFormOverlayHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool play in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            var demise = Generic.DrawCard(owner, Cards.FromId("CORE_RLK_567"));
            FlushFieldHistory(match);
            var attachment = demise.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2");
            for (int cast = 0; cast < 2; cast++)
            {
                var coin = Generic.DrawCard(owner, Cards.FromId("GAME_005"));
                InvokeProcess(match, PlayCardTask.Spell(owner, coin));
                FlushFieldHistory(match);
                Check(demise.Card.Id == "GAME_005", "cast transforms the held Shadow in place");
                Check(demise.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2").Id == attachment.Id,
                    "recurrence attachment survives repeated transformations");
                var tags = ReplayNativeEntityTags(NativeHistory(capture), demise.Id);
                Check(bot ? !tags.ContainsKey((int)GameTag.SHIFTING_SPELL)
                    : tags.GetValueOrDefault((int)GameTag.SHIFTING_SPELL) == 1,
                    "owner receives shifting overlay tag; opponent hand stays private");
                var attached = ReplayNativeEntityTags(NativeHistory(capture), attachment.Id);
                var snapshots = NativeHistory(capture).Where(e => SnapshotEntity(e) == attachment.Id).ToArray();
                Check(bot ? snapshots.All(e => string.IsNullOrEmpty(SnapshotName(e)))
                    : snapshots.Any(e => SnapshotName(e) == "RLK_567e2"),
                    "recurrence uses its pinned CardDef identity only for visible owner");
                Check(bot ? !attached.ContainsKey((int)GameTag.ATTACHED)
                    : attached.GetValueOrDefault((int)GameTag.ATTACHED) == demise.Id
                        && attached.GetValueOrDefault((int)GameTag.CREATOR) == demise.Id,
                    "visible recurrence carries native ATTACHED and CREATOR");
                if (!bot)
                    Check(tags.GetValueOrDefault(1020) == 0 && tags.GetValueOrDefault(1544) == 0,
                        "copied spell does not retain original unplayable/retired printing tags");
            }
            if (play) InvokeProcess(match, PlayCardTask.Spell(owner, demise));
            else owner.SetasideZone.Add(owner.HandZone.Remove(demise));
            FlushFieldHistory(match);
            Check(!demise.AppliedEnchantments.Contains(attachment), "played copy loses held recurrence");
            Check(ReplayNativeEntityTags(NativeHistory(capture), demise.Id)
                .GetValueOrDefault((int)GameTag.SHIFTING_SPELL) == 0, "played copy clears overlay tag on wire");
            capture.SerializeAll();
        }
        foreach (string id in new[] { "DINO_407", "TIME_876" })
        {
            var (match, capture) = AnimationMatch();
            var owner = match.Game.CurrentPlayer;
            var held = Generic.DrawCard(owner, Cards.FromId(id));
            foreach (var card in owner.Opponent.HandZone.ToArray())
                owner.Opponent.SetasideZone.Add(owner.Opponent.HandZone.Remove(card));
            foreach (var card in owner.Opponent.DeckZone.ToArray())
                owner.Opponent.SetasideZone.Add(owner.Opponent.DeckZone.Remove(card));
            var minion = Generic.DrawCard(owner.Opponent, Cards.FromId("CS2_182"));
            InvokeProcess(match, EndTurnTask.Any(owner));
            if (id == "DINO_407")
            {
                owner.Opponent.BaseMana = 10; owner.Opponent.UsedMana = 0;
                InvokeProcess(match, PlayCardTask.Minion(owner.Opponent, minion));
            }
            else InvokeProcess(match, EndTurnTask.Any(owner.Opponent));
            FlushFieldHistory(match);
            Check(held.Card.Id == "CS2_182", id + " real trigger transforms held form");
            Check(ReplayNativeEntityTags(NativeHistory(capture), held.Id)
                .GetValueOrDefault((int)GameTag.SHIFTING_MINION) == 1, id + " native owner retains shifting overlay");
            owner.SetasideZone.Add(owner.HandZone.Remove(held)); FlushFieldHistory(match);
            Check(ReplayNativeEntityTags(NativeHistory(capture), held.Id)
                .GetValueOrDefault((int)GameTag.SHIFTING_MINION) == 0, id + " hand exit clears overlay");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS held-form overlay: repeated casts, attachment identity, owner/privacy and play cleanup");
    }
}
