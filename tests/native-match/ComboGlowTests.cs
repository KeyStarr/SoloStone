using System;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// The client draws the Combo glow on a hand card only from the COMBO tag (220) the server sends; it never reads the
// card definition. Combo cards such as Nightmare Fuel never glowed because we sent the tag only on the printed
// card, not on revealed snapshots. Revealed snapshots now carry the printed value and COMBO_ACTIVE (266) is set
// on the player after a play and cleared at end of turn, as in retail. Hidden opponent cards must leak neither
// identity nor COMBO, and an identity change (CHANGE_ENTITY) must reset COMBO.
internal static class ComboGlowTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("TEST Combo: " + message); }

    private static PegasusGame.PowerHistoryEntity Snapshot(PowerHistoryData entry) =>
        entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
        : entry.HasChangeEntity ? entry.ChangeEntity : null;

    internal static void Run()
    {
        var fixture = new PoweredUpGlowTests.Fixture();
        var player = fixture.Player;
        var cards = new[] { "EDR_528", "END_032", "JAIL_909", "TIME_710" }
            .Select(fixture.Draw).ToArray();
        var plain = fixture.Draw("CS2_231");
        var enemy = SabberStoneCore.Actions.Generic.DrawCard(fixture.Game.Player2, Cards.FromId("EDR_528"));
        foreach (var card in cards) fixture.Game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(card));
        fixture.Flush();

        int cursor = fixture.Entries.Count();
        fixture.Process(PlayCardTask.Minion(player, (Minion)plain));
        Check(player.IsComboActive, "first play did not activate engine Combo");
        Check(fixture.Entries.Skip(cursor).Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == player.Id && entry.TagChange.Tag == 266 && entry.TagChange.Value == 1),
            "first play must send COMBO_ACTIVE=1 on Player1 entity");
        var snapshots = fixture.Entries.Select(Snapshot).Where(entity => entity != null).ToArray();
        var hidden = snapshots.Where(entity => entity.Entity == enemy.Id).ToArray();
        Check(hidden.Length > 0 && hidden.All(entity => entity.Name.Length == 0
            && entity.Tags.All(tag => tag.Name != 220)), "opponent hand leaked Combo or identity");
        Check(snapshots.Where(entity => entity.Entity == plain.Id).All(entity => entity.Tags.All(tag => tag.Name != 220)),
            "non-Combo card acquired COMBO");
        foreach (var card in cards)
        {
            Check(card.Card[GameTag.COMBO] == 1, "fixture lacks printed Combo: " + card.Card.Id);
            var revealed = snapshots.Where(entity => entity.Entity == card.Id && entity.Name.Length > 0).ToArray();
            Check(revealed.Length > 0 && revealed.All(entity => entity.Tags.Count(tag => tag.Name == 220 && tag.Value == 1) == 1),
                card.Card.Id + " revealed snapshot lacks COMBO=1");
            Check(fixture.Match.SnapshotVisibleTransportTags(card).TryGetValue(220, out int combo) && combo == 1,
                card.Card.Id + " live audit lacks COMBO=1");
        }

        cursor = fixture.Entries.Count();
        fixture.Process(EndTurnTask.Any(player));
        Check(fixture.Entries.Skip(cursor).Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == player.Id && entry.TagChange.Tag == 266 && entry.TagChange.Value == 0),
            "end turn must send COMBO_ACTIVE=0 on Player1 entity");

        // Historical identities must win over the current engine card definition.
        var changed = cards[1];
        foreach (bool hideFirst in new[] { false, true })
        {
            fixture.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(changed));
            fixture.Flush();
            if (hideFirst)
            {
                fixture.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(changed));
                fixture.Flush();
            }
            var snapshot = PowerHistoryBuilder.FullEntity(changed).Entity;
            snapshot.Name = "CS2_231";
            cursor = fixture.Entries.Count();
            fixture.Game.PowerHistory.Add(new PowerHistoryChangeEntity { Entity = snapshot, CardId = snapshot.Name });
            fixture.Flush();
            Check(fixture.Entries.Skip(cursor).Where(entry => entry.HasChangeEntity).Select(Snapshot)
                .Any(entity => entity.Entity == changed.Id && entity.Tags.Any(tag => tag.Name == 220 && tag.Value == 0)),
                "changed identity retained Combo, hideFirst=" + hideFirst);
        }
        foreach (var packet in fixture.Entries)
            using (var stream = new System.IO.MemoryStream())
            {
                packet.Serialize(stream);
                Check(stream.Length == packet.GetSerializedSize(), "packet serialization size");
            }
        Console.WriteLine("PASS Combo hand glow: four printed definitions, player activation/reset, hidden hand privacy, audit and identity clearing");
    }
}
