using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    private static void AutomaticQuestHistory()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "TLC_426", "UNG_942" })
        {
            var capture = new Capture();
            var match = StartFixture(capture, "CS2_231", null);
            var game = match.Game; var p = opponent ? game.Player2 : game.Player1;
            var quest = (SabberStoneCore.Model.Entities.Spell)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            FlushAutomaticHistory(match);
            int cursor = capture.Packets.Count;
            new SabberStoneCore.Tasks.SimpleTasks.PlayTask(SabberStoneCore.Tasks.SimpleTasks.PlayType.SPELL)
                .Process(game, p, p.Hero, null, new TaskStack { Playables = new[] { (IPlayable)quest } });
            FlushAutomaticHistory(match);
            var history = History(capture, cursor);
            int reveal = history.FindIndex(e => e.HasShowEntity && SnapshotEntity(e) == quest.Id && SnapshotName(e) == id);
            int publicEntry = history.FindIndex(e => e.HasTagChange && e.TagChange.Entity == quest.Id
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.SECRET);
            Check(reveal >= 0 && (publicEntry < 0 || reveal < publicEntry),
                "automatic Quest reveal precedes public entry: " + id + "/" + p.PlayerId);
            Check(quest.Zone == p.SecretZone && history.Any(e => e.HasShowEntity
                && SnapshotEntity(e) == quest.Id && SnapshotName(e) == id
                && SnapshotTag(e, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.SECRET
                && SnapshotTag(e, GameTag.ZONE_POSITION) == quest.ZonePosition + 1),
                "automatic deck Quest public on either seat: " + id + "/" + p.PlayerId);
            capture.SerializeAll();
        }
        Console.WriteLine("PASS automatic Quest projection: deck cast; modern/legacy; both seats");
    }

    private static void AutomaticSecretHistory()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        var game = match.Game; var opponent = game.Player2;
        var secret = (SabberStoneCore.Model.Entities.Spell)SabberStoneCore.Model.Entities.Entity.FromCard(opponent, Cards.FromId("CORE_EX1_287"));
        int cursor = capture.Packets.Count;
        Generic.CastSpell(opponent, game, secret, null, 0);
        FlushAutomaticHistory(match);
        var history = History(capture, cursor);
        AssertHiddenSecretPlay(capture, cursor, secret.Id);
        Check(secret.Zone == opponent.SecretZone && !EmittedIds(history).Contains("CORE_EX1_287"),
            "automatic opponent Secret stays hidden on install");
        Check(history.Any(e => e.HasTagChange && e.TagChange.Entity == secret.Id
            && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value == 1), "automatic Secret has native slot");
        cursor = capture.Packets.Count;
        var duplicate = (SabberStoneCore.Model.Entities.Spell)SabberStoneCore.Model.Entities.Entity.FromCard(opponent, Cards.FromId("CORE_EX1_287"));
        Generic.CastSpell(opponent, game, duplicate, null, 0);
        FlushAutomaticHistory(match);
        history = History(capture, cursor);
        Check(opponent.SecretZone.Single().Id == secret.Id && duplicate.Zone == opponent.GraveyardZone,
            "failed duplicate preserves installed Secret");
        Check(history.Any(e => e.HasShowEntity && SnapshotEntity(e) == duplicate.Id && SnapshotName(e) == "CORE_EX1_287"),
            "failed automatic Secret has named public SHOW_ENTITY");
        Check(!history.Any(e => SnapshotEntity(e) == secret.Id && SnapshotName(e) == "CORE_EX1_287"),
            "duplicate failure does not reveal active Secret actor");
        var hiddenDeckSecret = (SabberStoneCore.Model.Entities.Spell)SabberStoneCore.Model.Entities.Entity.FromCard(
            opponent, Cards.FromId("CORE_EX1_289"), zone: opponent.DeckZone);
        FlushAutomaticHistory(match);
        cursor = capture.Packets.Count;
        Generic.CastSpell(opponent, game, hiddenDeckSecret, null, 0);
        FlushAutomaticHistory(match);
        AssertHiddenSecretPlay(capture, cursor, hiddenDeckSecret.Id);
        Check(hiddenDeckSecret.Zone == opponent.SecretZone && !EmittedIds(History(capture, cursor)).Contains("CORE_EX1_289"),
            "successful opponent deck Secret reveal remains redacted");
        capture.SerializeAll();
        foreach (string spellId in new[] { "CS2_004", "CORE_EX1_610" })
        {
            var deckCapture = new Capture();
            int deckSpellId = 0;
            var deckMatch = StartFixture(deckCapture, "CS2_231", null);
            var treasuregill = Generic.DrawCard(deckMatch.Game.Player1, Cards.FromId("TLC_438"));
            deckSpellId = SabberStoneCore.Model.Entities.Entity.FromCard(deckMatch.Game.Player1, Cards.FromId(spellId),
                zone: deckMatch.Game.Player1.DeckZone).Id;
            FlushHistory(deckMatch);
            int deckCursor = deckCapture.Packets.Count;
            deckMatch.Game.Player1.BaseMana = 10; deckMatch.Game.Player1.UsedMana = 0;
            InvokeProcess(deckMatch, SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Minion(deckMatch.Game.Player1, treasuregill));
            FlushHistory(deckMatch);
            var deckHistory = History(deckCapture, deckCursor);
            Check(deckHistory.Any(e => e.HasShowEntity && SnapshotEntity(e) == deckSpellId && SnapshotName(e) == spellId),
                "automatic deck cast reveals original spell actor: " + spellId);
            if (spellId == "CS2_004")
                Check(deckHistory.Any(e => SnapshotEntity(e) == deckSpellId && SnapshotTag(e, GameTag.CARD_TARGET) != 0)
                    || deckHistory.Any(e => e.HasTagChange && e.TagChange.Entity == deckSpellId
                        && e.TagChange.Tag == (int)GameTag.CARD_TARGET && e.TagChange.Value != 0),
                    "automatic deck spell projects preferred target");
            deckCapture.SerializeAll();
        }
        Console.WriteLine("PASS automatic Secret projection: hidden install, native slot, public failed attempt");
    }

    private static void FlushAutomaticHistory(NativeMatch match)
    {
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(match, new object[] { match.Game.PowerHistory.Last });
        match.Game.PowerHistory.Last.Clear();
    }
}
