using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class Program
{
    // Garona's King Llane was moved into the opponent's deck by its Start of Game effect, with the engine Controller
    // changed but the CONTROLLER tag left at the old player. When Portal Vanguard drew him, SHOW_ENTITY reassigned the
    // actor to the other side and dropped the buff, so the drawn minion appeared on the wrong side. The `transferred`
    // cases cover that.
    private static void VanguardDrawHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool transferred in new[] { false, true })
        {
            Capture capture;
            NativeMatch match;
            var scenario = DebugScenario.Parse(bot ? "npcHand=TIME_003\nnpcMana=3" : "humanHand=TIME_003\nhumanMana=3");
            if (transferred)
            {
                (match, capture) = SogStart(bot ? new[] { "TIME_875" } : Array.Empty<string>(),
                    bot ? Array.Empty<string>() : new[] { "TIME_875" });
                match.DebugScenario = scenario;
            }
            else
            {
                capture = new Capture();
                match = new NativeMatch(capture.Send, "mage", "mage", 8427) { DebugScenario = scenario };
                match.Start();
            }
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            if (bot) { InvokeProcess(match, EndTurnTask.Any(match.Game.Player1)); FlushFieldHistory(match); }
            var owner = match.Game.CurrentPlayer;
            if (transferred)
            {
                Check(owner.DeckZone.Any(c => c.Card.Id == "TIME_875t"), "Llane fixture remains in recipient deck after turn draw");
                foreach (var card in owner.DeckZone.Where(c => c.Card.Id != "TIME_875t").ToArray())
                    owner.SetasideZone.Add(owner.DeckZone.Remove(card));
                FlushFieldHistory(match);
            }
            int hand = owner.HandZone.Count, deck = owner.DeckZone.Count, opponentHand = owner.Opponent.HandZone.Count;
            var source = owner.HandZone.Single(c => c.Card.Id == "TIME_003");
            var held = owner.HandZone.Select(c => c.Id).ToHashSet();
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            Inspect("first", cursor);
            Check(match.Game.Rewind != null, "Vanguard offers Rewind after its first draw");
            cursor = capture.Packets.Count;
            InvokeProcess(match, ChooseTask.Pick(owner, match.Game.Rewind.RewindEntityId));
            FlushFieldHistory(match);
            Check(History(capture, cursor).Any(e => e.HasResetGame), "Vanguard replay emits GAME_RESET");
            Inspect("replay", cursor);
            capture.SerializeAll();

            void Inspect(string phase, int from)
            {
                owner = bot ? match.Game.Player2 : match.Game.Player1;
                var drawn = owner.HandZone.Single(c => !held.Contains(c.Id));
                string name = $"vanguard-{(transferred ? "llane" : "ordinary")}-{(bot ? "opponent" : "owner")}-{phase}";
                DumpDrawHistory(name, capture, from);
                var rows = capture.HistorySnapshots(from).SelectMany(p => p.List).ToList();
                Check(owner.HandZone.Count == hand && owner.DeckZone.Count == deck - 1
                    && owner.Opponent.HandZone.Count == opponentHand, name + " draw belongs only to source owner");
                if (transferred) Check(drawn.Card.Id == "TIME_875t", name + " tutors the transferred physical Llane");
                var show = rows.Last(e => e.HasShowEntity && e.ShowEntity.Entity == drawn.Id);
                Check(SnapshotTag(show, GameTag.CONTROLLER) == owner.PlayerId
                    && SnapshotTag(show, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.HAND
                    && rows.Any(e => e.HasTagChange && e.TagChange.Entity == drawn.Id
                        && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value == drawn.ZonePosition + 1),
                    name + " draw SHOW carries owner/HAND/position");
                Check(bot ? string.IsNullOrEmpty(show.ShowEntity.Name) : show.ShowEntity.Name == drawn.Card.Id,
                    name + " draw identity follows viewer");
                if (transferred && phase == "replay")
                {
                    var restored = rows.Single(e => e.HasFullEntity && e.FullEntity.Entity == drawn.Id);
                    Check(SnapshotTag(restored, GameTag.CONTROLLER) == owner.PlayerId
                        && SnapshotTag(restored, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.DECK
                        && SnapshotTag(restored, GameTag.ZONE_POSITION) == 0 && string.IsNullOrEmpty(restored.FullEntity.Name),
                        name + " reset FULL retains destination owner and hidden deck residence");
                }
                var buffs = rows.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
                Check(bot ? buffs.Length == 0 : buffs.Length == 1 && buffs[0].SourceEntityId == owner.Hero.Id
                    && buffs[0].TargetEntityIds.SequenceEqual(new[] { drawn.Id }), name + " buff targets owner's drawn hand card only");
                Check(drawn[GameTag.ATK] == drawn.Card.ATK + 2 && drawn[GameTag.HEALTH] == drawn.Card.Health + 2,
                    name + " drawn minion receives +2/+2");
            }
        }
        Console.WriteLine("PASS Vanguard draw: debug scenario, both viewers, first resolution and Rewind replay");
    }

    // Same stale-CONTROLLER class: a stolen final portal demon returned Broxigar (TIME_020) to the other player's hand
    // with CONTROLLER unchanged. The engine Controller and the CONTROLLER tag must agree for every viewer.
    private static void BroxigarReturnHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool transferred in new[] { false, true })
        {
            bool originalBot = bot != transferred;
            var (match, capture) = SogStart(originalBot ? Array.Empty<string>() : new[] { "TIME_020" },
                originalBot ? new[] { "TIME_020" } : Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game;
            var originalOwner = originalBot ? game.Player2 : game.Player1;
            var recipient = bot ? game.Player2 : game.Player1;
            var original = originalOwner.SetasideZone.Single(c => c.Card.Id == "TIME_020");
            Check(original[PayloadRules.Stored] != 0, "Broxigar fixture retains vanished physical original");
            if (originalBot) InvokeProcess(match, EndTurnTask.Any(game.Player1));
            originalOwner.BaseMana = 10; originalOwner.UsedMana = 0;
            var portal = Generic.DrawCard(originalOwner, Cards.FromId("TIME_020t5"));
            InvokeProcess(match, PlayCardTask.Any(originalOwner, portal));
            var demon = originalOwner.Opponent.BoardZone.Single(c => c.Card.Id == "TIME_020t5t");
            if (transferred)
                new ControlTask(EntityType.TARGET).Process(game, originalOwner, original, demon);
            Check(demon.Controller == recipient.Opponent, "final portal demon belongs to recipient's opponent after transfer");
            FlushFieldHistory(match);
            int hand = recipient.HandZone.Count, otherHand = recipient.Opponent.HandZone.Count;
            int cursor = capture.Packets.Count;
            demon.Destroy();
            game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            string name = $"broxigar-{(transferred ? "stolen" : "ordinary")}-{(bot ? "opponent" : "owner")}";
            Check(original.Controller == recipient && original[GameTag.CONTROLLER] == recipient.PlayerId,
                name + " engine Controller and CONTROLLER tag agree with recipient");
            Check(ControllerAdmissionInvariant.Faults(game).Count == 0,
                name + " every HAND/DECK/PLAY admission keeps engine Controller and CONTROLLER tag equal");
            Check(original.Zone == recipient.HandZone && recipient.HandZone.Count == hand + 1
                && recipient.Opponent.HandZone.Count == otherHand && original[PayloadRules.Stored] == 0,
                name + " returns same physical Broxigar only to recipient's hand");
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            DumpDrawHistory(name, capture, cursor);
            var shows = rows.Where(e => e.HasShowEntity && e.ShowEntity.Entity == original.Id
                && SnapshotTag(e, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.HAND).ToArray();
            Check(shows.Length > 0, name + " publishes hand return SHOW");
            foreach (var show in shows) Inspect(show, "SHOW");
            Check(rows.Where(e => SnapshotEntity(e) == original.Id && (e.HasShowEntity || e.HasFullEntity || e.HasChangeEntity))
                .All(e => !bot || string.IsNullOrEmpty(SnapshotName(e))), name + " opposing hand stays private throughout return");
            cursor = capture.Packets.Count;
            SendHistoryEntries(match, new() { SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(original) });
            var full = capture.HistorySnapshots(cursor).SelectMany(p => p.List)
                .Single(e => e.HasFullEntity && e.FullEntity.Entity == original.Id);
            Inspect(full, "FULL");
            capture.SerializeAll();

            void Inspect(PowerHistoryData row, string kind)
            {
                Check(SnapshotTag(row, GameTag.CONTROLLER) == recipient.PlayerId
                    && SnapshotTag(row, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.HAND,
                    name + " " + kind + " carries recipient/HAND");
                Check(bot ? string.IsNullOrEmpty(SnapshotName(row)) : SnapshotName(row) == "TIME_020",
                    name + " " + kind + " identity follows viewer");
                if (bot) Check(HasOnlyHiddenTags(row.HasShowEntity ? row.ShowEntity.Tags : row.FullEntity.Tags),
                    name + " " + kind + " exposes no opposing-hand definition tags");
            }
        }
        Console.WriteLine("PASS Broxigar return: ordinary/stolen final portal demon, both seats/viewers, engine owner/tag and SHOW/FULL privacy");
    }
}
