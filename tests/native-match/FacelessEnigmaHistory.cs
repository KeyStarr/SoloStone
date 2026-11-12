using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void FacelessEnigmaHistory()
    {
        foreach (int pick in new[] { 0, 1 })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "test-standard-batch-2-opp-hunter", 1337);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var owner = game.Player1;
            owner.BaseMana = 10; owner.UsedMana = 0;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
            var source = Generic.DrawCard(owner, Cards.FromId("TIME_860"));
            FlushFieldHistory(match);
            var choice = ChoiceRetirementPlay(match, capture, source, 1);
            Check(choice.Entities.Count == 2 && !choice.HideChosen, "Enigma opens native two-Secret prompt");
            int selected = choice.Entities[pick], rejected = choice.Entities[1 - pick];
            string otherCard = game.IdEntityDic[rejected].Card.Id;
            int answerStart = capture.Packets.Count;
            match.SendChoices(choice.Id, new List<int> { selected });
            var enemySecret = game.Player2.SecretZone.Single();
            Check(owner.SecretZone.Single().Id == selected && enemySecret.Card.Id == otherCard
                && enemySecret.Id != rejected && owner.Choice == null && !game.Resolution.HasSuspendedEffect,
                "Enigma resolves both owners and completes choice");
            Check(enemySecret.Card.Class != game.Player2.HeroClass, "fixture gives Hunter an off-class Secret");
            var answer = History(capture, answerStart);
            int play = AssertHiddenSecretPlay(capture, answerStart, enemySecret.Id);
            int create = answer.FindIndex(e => e.HasFullEntity && SnapshotEntity(e) == enemySecret.Id);
            int setaside = answer.FindIndex(e => IsZone(e, enemySecret.Id, EngineZone.SETASIDE));
            int install = answer.FindIndex(e => IsZone(e, enemySecret.Id, EngineZone.SECRET));
            Check(create >= 0 && create < setaside && setaside < play && install > play,
                "Enigma creates anonymous actor, sets it aside, admits PLAY, then installs Secret");
            int ownPlay = answer.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == selected
                && e.PowerStart.Type == HistoryBlock.Type.PLAY);
            Check(ownPlay >= 0 && ownPlay < play && !answer[ownPlay].PowerStart.ForceShowBigCard,
                "chosen Secret casts first without forcing friendly big card");
            Check(!answer.Any(e => IsZone(e, selected, EngineZone.REMOVEDFROMGAME)),
                "installed choice actor is preserved for Secret presentation");
            // Suspended prompt and resumed casts must each close their own blocks.
            int depth = 0;
            foreach (var row in History(capture, 0))
            {
                if (row.HasPowerStart) depth++;
                if (row.HasPowerEnd) Check(--depth >= 0, "Enigma never closes an unopened block");
            }
            Check(depth == 0, "Enigma prompt and answer leave every history block balanced");
            int acknowledged = capture.Packets.FindIndex(answerStart, p => p.Id == 13);
            int resumed = capture.Packets.FindIndex(answerStart, p => p.Id == 19);
            Check(acknowledged >= 0 && resumed > acknowledged, "EntitiesChosen precedes resumed cast history");
            capture.SerializeAll();
        }
        UnseenSaboteurSecretHistory();
        Console.WriteLine("PASS TIME_860: both picks, off-class opponent Secret, big-card admission, privacy and balanced blocks");
    }

    private static void UnseenSaboteurSecretHistory()
    {
        var (capture, match) = ChoiceRetirementMatch(CardClass.MAGE);
        var game = match.Game; var opponent = game.Player2;
        foreach (var old in opponent.HandZone.ToArray()) opponent.SetasideZone.Add(old.Zone.Remove(old));
        var secret = SabberStoneCore.Model.Entities.Entity.FromCard(opponent, Cards.FromId("CORE_EX1_287"), zone: opponent.HandZone);
        var saboteur = Generic.DrawCard(game.Player1, Cards.FromId("DAL_538"));
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(game.Player1, saboteur));
        FlushFieldHistory(match);
        Check(opponent.SecretZone.Single().Id == secret.Id, "Unseen Saboteur casts opponent's hand Secret");
        AssertHiddenSecretPlay(capture, cursor, secret.Id);
        capture.SerializeAll();
        Console.WriteLine("PASS DAL_538: opponent hand Secret big-card admission stays private");
    }

    private static int AssertHiddenSecretPlay(Capture capture, int cursor, int id)
    {
        var answer = History(capture, cursor);
        int play = answer.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == id
            && e.PowerStart.Type == HistoryBlock.Type.PLAY);
        Check(play >= 0, "hidden Secret has PLAY block");
        // PowerProcessor.ShouldShowPlayedBigCard always returns true for the opposing seat, regardless of ForceShowBigCard.
        // HistoryInfo.CanDuplicateEntity accepts a still-hidden actor only with SECRET (or another special presentation tag).
        foreach (var expected in new[] { (GameTag.SECRET, 1), (GameTag.CARDTYPE, (int)CardType.SPELL) })
            Check(answer.Take(play).Any(e => e.HasTagChange && e.TagChange.Entity == id
                && e.TagChange.Tag == (int)expected.Item1 && e.TagChange.Value == expected.Item2),
                "hidden Secret publishes " + expected.Item1 + " before PLAY/big-card preparation");
        Check(!answer[play].PowerStart.ForceShowBigCard && !answer.Any(e => SnapshotEntity(e) == id
            && !string.IsNullOrEmpty(SnapshotName(e))), "Secret admission never reveals card identity or forces big card");
        return play;
    }
}
