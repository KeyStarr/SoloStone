using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void CastTwiceMarkHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string enchantment in new[] { "JAIL_123e", "BE_801e" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            var spell = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone);
            var hidden = EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_029"), zone: game.Player2.HandZone);
            RepeatRules.Mark(spell, owner.Hero, enchantment);
            RepeatRules.Mark(hidden, game.Player2.Hero, enchantment);
            var mark = spell.AppliedEnchantments.Single();
            var hiddenMark = hidden.AppliedEnchantments.Single();
            FlushFieldHistory(match);
            if (bot)
                Check(!History(capture, 0).Any(e => SnapshotEntity(e) == mark.Id &&
                    (!string.IsNullOrEmpty(SnapshotName(e)) || SnapshotTag(e, GameTag.ATTACHED) != 0 ||
                        SnapshotTag(e, GameTag.CREATOR) != 0 || SnapshotTag(e, GameTag.CARDTYPE) != 0)),
                    "opponent's held repeat mark remains anonymous, without attachment or creator");
            int cursor = capture.Packets.Count;
            Check(game.Process(PlayCardTask.Any(owner, spell, owner.Opponent.Hero)), "marked Fireball plays");
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            var all = History(capture, 0);
            int trigger = all.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == mark.Id);
            int reveal = all.FindIndex(e => SnapshotEntity(e) == mark.Id && SnapshotName(e) == enchantment);
            Check(trigger >= 0 && reveal >= 0 && reveal < trigger,
                "public repeat mark identity reaches client before its TRIGGER");
            var snapshot = all[reveal];
            Check(SnapshotTag(snapshot, GameTag.CARDTYPE) == (int)CardType.ENCHANTMENT &&
                SnapshotTag(snapshot, GameTag.ATTACHED) == spell.Id && SnapshotTag(snapshot, GameTag.CREATOR) == owner.Hero.Id &&
                SnapshotTag(snapshot, GameTag.CONTROLLER) == owner.PlayerId,
                "repeat mark reveal carries pinned client's enchantment, attachment, creator and controller tags");
            Check(!data.Any(e => SnapshotEntity(e) == hiddenMark.Id &&
                (!string.IsNullOrEmpty(SnapshotName(e)) || SnapshotTag(e, GameTag.ATTACHED) != 0)),
                "public play never reveals another held spell's mark");
            Check(owner.Opponent.Hero.Damage == 12, "projection preserves both Fireball bodies");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS cast-twice mark reveal: hidden until public play; identity and retail attachment tags before TRIGGER");
    }
}
