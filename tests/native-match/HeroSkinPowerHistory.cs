using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class Program
{
    private static void HeroSkinPowerHistory()
    {
        HeroSkinPowerTransfers();
        var project = typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (bool bot in new[] { false, true })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "warrior", "warrior", 8427) {
                DebugScenario = DebugScenario.Parse("humanSkin=HERO_01bb\nnpcSkin=HERO_01bb\nhumanHand=TIME_003\nhumanMana=10") };
            match.Start(); match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            match.StopNpc();
            var owner = bot ? match.Game.Player2 : match.Game.Player1;
            var original = owner.Hero.HeroPower;
            var frozen = PowerHistoryBuilder.ShowEntity(original).Entity;
            var frozenTags = frozen.Tags.OrderBy(p => p.Key).ToArray();
            string Project(PowerHistoryEntity snapshot) => ((PegasusGame.PowerHistoryEntity)project.Invoke(match,
                new object[] { snapshot, true, true, null, false })).Name;
            Check(Project(frozen) == "HERO_01bbhp", "frozen base power starts with skin identity");
            new ReplaceHeroPower(Cards.FromAssetId(original.Card[(GameTag)1086])).Process(match.Game, owner, owner.Hero, null);
            var upgraded = owner.Hero.HeroPower;
            FlushFieldHistory(match);
            Check(original.Zone == owner.SetasideZone && Project(frozen) == "HERO_01bbhp"
                && Project(PowerHistoryBuilder.ShowEntity(upgraded).Entity) == "HERO_01bbhp2", "upgrade keeps old entity identity and projects new upgraded entity");
            BasicHeroPowerRules.SwapForTwoUses(owner, owner.Hero);
            int temporaryId = owner.Hero.HeroPower.Id;
            FlushFieldHistory(match);
            Check(Project(PowerHistoryBuilder.ShowEntity(owner.Hero.HeroPower).Entity) == "TLC_632t"
                && Project(PowerHistoryBuilder.FullEntity(upgraded, reset: true).Entity) == "HERO_01bbhp2",
                "temporary power retains own identity; SETASIDE upgraded power retains cosmetic identity");

            var human = match.Game.Player1;
            var portal = human.HandZone.Single(c => c.Card.Id == "TIME_003");
            InvokeProcess(match, human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == portal.Id));
            FlushFieldHistory(match);
            Check(match.Game.Rewind != null, "Portal Vanguard creates actual Rewind checkpoint");
            // Mutate after the checkpoint to prove projection does not read the current engine power.
            new ReplaceHeroPower(Cards.FromId("EX1_625t")).Process(match.Game, owner, owner.Hero, null);
            var replacement = PowerHistoryBuilder.ShowEntity(owner.Hero.HeroPower).Entity;
            Check(Project(replacement) == NativeMatch.TransportCardId("EX1_625t") && Project(frozen) == "HERO_01bbhp",
                "replacement cannot change frozen earlier snapshot");
            int cursor = capture.Packets.Count;
            InvokeProcess(match, ChooseTask.Pick(human, match.Game.Rewind.RewindEntityId));
            FlushFieldHistory(match);
            var rows = capture.HistorySnapshots(cursor).SelectMany(packet => packet.List).ToArray();
            Check(rows.Any(row => row.HasResetGame), "power history regression runs real GAME_RESET");
            owner = bot ? match.Game.Player2 : match.Game.Player1;
            Check(owner.Hero.HeroPower.Id == temporaryId && owner.Hero.HeroPower.Card.Id == "TLC_632t",
                "Rewind restores the temporary power entity");
            foreach (var expected in new[] { (original.Id, "HERO_01bbhp"), (upgraded.Id, "HERO_01bbhp2"), (temporaryId, "TLC_632t") })
            {
                var restored = match.Game.IdEntityDic[expected.Item1];
                Check(Project(PowerHistoryBuilder.FullEntity(restored, reset: true).Entity) == expected.Item2,
                    "restored power snapshot retains entity presentation identity " + expected.Item2);
                Check(rows.Any(row => row.HasFullEntity && row.FullEntity.Entity == expected.Item1 && row.FullEntity.Name == expected.Item2),
                    "GAME_RESET wire retains power identity including SETASIDE " + expected.Item2);
            }
            BasicHeroPowerRules.AfterUse(owner.Hero.HeroPower);
            Check(Project(PowerHistoryBuilder.ShowEntity(owner.Hero.HeroPower).Entity) == "TLC_632t2", "temporary CHANGE_ENTITY keeps second-use identity");
            BasicHeroPowerRules.AfterUse(owner.Hero.HeroPower);
            FlushFieldHistory(match);
            Check(owner.Hero.HeroPower.Id == upgraded.Id && Project(PowerHistoryBuilder.ShowEntity(owner.Hero.HeroPower).Entity) == "HERO_01bbhp2",
                "temporary expiry restores same upgraded cosmetic power entity");
            Check(Project(frozen) == "HERO_01bbhp" && Project(replacement) == NativeMatch.TransportCardId("EX1_625t")
                && frozen.Name == original.Card.Id && frozen.Tags.OrderBy(p => p.Key).SequenceEqual(frozenTags),
                "base and replacement snapshots remain stable across reset/restore without snapshot mutation");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS frozen hero powers: both seats, upgrade, replacement, temporary use/restore, Vanguard GAME_RESET and SETASIDE");
    }

    private static void HeroSkinPowerTransfers()
    {
        var project = typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (bool sameClass in new[] { true, false })
        foreach (bool transferBeforeCheckpoint in new[] { true, false })
        {
            var capture = new Capture();
            string npcSkin = sameClass ? "HERO_01bd" : HeroSkinData.All.Values.First(s => s.BaseHero == "HERO_08" && s.Power.Length != 0 && s.Id != "HERO_08").Id;
            var match = new NativeMatch(capture.Send, "warrior", sameClass ? "warrior" : "mage", 8427) {
                DebugScenario = DebugScenario.Parse("humanSkin=HERO_01bb\nnpcSkin=" + npcSkin + "\nhumanHand=TIME_003\nhumanMana=10") };
            match.Start(); match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>()); match.StopNpc();
            var human = match.Game.Player1; var bot = match.Game.Player2;
            var a = human.Hero.HeroPower; var b = bot.Hero.HeroPower;
            var frozen = PowerHistoryBuilder.ShowEntity(a).Entity;
            string Project(PowerHistoryEntity entity) => ((PegasusGame.PowerHistoryEntity)project.Invoke(match,
                new object[] { entity, true, true, null, false })).Name;
            Check(Project(frozen) == "HERO_01bbhp" && Project(PowerHistoryBuilder.ShowEntity(b).Entity) == HeroSkinData.All[npcSkin].Power,
                "different seat skins initially name their own powers");
            bool transferred = false;
            void Swap()
            {
                // Exercise the actual controller TAG_CHANGE stream; Grizzled Wizard itself is not implemented by this engine.
                var left = human.Hero.HeroPower; var right = bot.Hero.HeroPower;
                left[GameTag.COST] = 1; left[GameTag.EXHAUSTED] = 1;
                left.Controller = bot; left[GameTag.CONTROLLER] = bot.PlayerId;
                right.Controller = human; right[GameTag.CONTROLLER] = human.PlayerId;
                human.Hero.HeroPower = right; bot.Hero.HeroPower = left;
                left[GameTag.COST] = 3; // A later delta must not leak backward into the transfer SHOW.
                int cursor = capture.Packets.Count;
                FlushFieldHistory(match);
                var delta = capture.HistorySnapshots(cursor).SelectMany(h => h.List).ToArray();
                Check(new[] { left.Id, right.Id }.All(id => delta.Any(row => row.HasTagChange && row.TagChange.Entity == id
                    && row.TagChange.Tag == (int)GameTag.CONTROLLER)), "transfer emits controller delta for both entities");
                var identities = capture.HistorySnapshots().SelectMany(h => h.List)
                    .Where(row => row.HasFullEntity || row.HasShowEntity || row.HasChangeEntity)
                    .Select(row => row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.ChangeEntity)
                    .GroupBy(row => row.Entity).ToDictionary(group => group.Key, group => group.Last().Name);
                foreach (var power in new[] { left, right })
                {
                    string expected = power.Id == a.Id || sameClass ? "HERO_01bp" : "HERO_08bp";
                    Check(identities[power.Id] == expected,
                        "client identity becomes base immediately after controller transfer without manual SHOW");
                    CheckSkinAuditIdentity(match, capture, power, expected, "transferred power");
                    var packet = capture.HistorySnapshots(cursor).Single(h => h.List.Any(row => row.HasTagChange
                        && row.TagChange.Entity == power.Id && row.TagChange.Tag == (int)GameTag.CONTROLLER));
                    var shown = packet.List.Where(row => row.HasShowEntity && row.ShowEntity.Entity == power.Id)
                        .Select(row => row.ShowEntity).ToArray();
                    Check(shown.Length == (transferred ? 0 : 1), "only first transfer reloads power identity in controller packet");
                    if (!transferred)
                        Check(shown[0].Name == expected
                            && shown[0].Tags.Any(tag => tag.Name == (int)GameTag.CONTROLLER && tag.Value == power.Controller.PlayerId)
                            && shown[0].Tags.Any(tag => tag.Name == (int)GameTag.CARDTYPE && tag.Value == (int)CardType.HERO_POWER)
                            && shown[0].Tags.Any(tag => tag.Name == (int)GameTag.ZONE && tag.Value == (int)SabberStoneCore.Enums.Zone.PLAY),
                            "first-transfer SHOW preserves public power tags and new controller");
                    if (!transferred && power.Id == left.Id)
                        Check(shown[0].Tags.Any(tag => tag.Name == (int)GameTag.COST && tag.Value == 1)
                            && shown[0].Tags.Any(tag => tag.Name == (int)GameTag.EXHAUSTED && tag.Value == 1),
                            "transfer SHOW carries tags at transfer, not later endpoint values");
                }
                Check(!delta.Any(row => row.HasChangeEntity || row.HasFullEntity), "transfer does not transform or recreate power");
                transferred = true;
            }
            // A no-op controller tag is not a transfer.
            int noOpCursor = capture.Packets.Count;
            match.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(a.Id, GameTag.CONTROLLER, human.PlayerId));
            FlushFieldHistory(match);
            Check(Project(frozen) == "HERO_01bbhp", "same controller delta retains cosmetic power");
            Check(!capture.HistorySnapshots(noOpCursor).SelectMany(h => h.List)
                .Any(row => row.HasShowEntity && row.ShowEntity.Entity == a.Id
                    || row.HasChangeEntity && row.ChangeEntity.Entity == a.Id || row.HasFullEntity && row.FullEntity.Entity == a.Id),
                "same controller delta does not reload power identity");
            if (transferBeforeCheckpoint) Swap();
            var portal = human.HandZone.Single(c => c.Card.Id == "TIME_003");
            InvokeProcess(match, human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == portal.Id));
            FlushFieldHistory(match);
            Check(match.Game.Rewind != null, "transfer regression creates real Vanguard checkpoint");
            Swap(); // Transfer after checkpoint, or transfer back after checkpoint.
            Check(Project(frozen) == NativeMatch.TransportCardId(a.Card.Id), "transferred entity stays base even in an earlier frozen snapshot");
            int resetCursor = capture.Packets.Count;
            InvokeProcess(match, ChooseTask.Pick(human, match.Game.Rewind.RewindEntityId)); FlushFieldHistory(match);
            var reset = capture.HistorySnapshots(resetCursor).SelectMany(h => h.List).ToArray();
            Check(reset.Any(row => row.HasResetGame), "transfer regression emits real GAME_RESET");
            foreach (var power in new[] { a, b })
            {
                string expected = NativeMatch.TransportCardId(power.Card.Id);
                Check(reset.Any(row => row.HasFullEntity && row.FullEntity.Entity == power.Id && row.FullEntity.Name == expected),
                    "GAME_RESET FULL cannot restore transferred cosmetic power");
                var restored = match.Game.IdEntityDic[power.Id];
                CheckSkinAuditIdentity(match, capture, restored, expected, "transferred power after Rewind");
                Check(Project(PowerHistoryBuilder.ShowEntity(restored).Entity) == expected
                    && Project(PowerHistoryBuilder.FullEntity(restored, reset: true).Entity) == expected,
                    "post-Rewind SHOW/FULL agree on permanent base power identity");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS power transfers: same-class different skins, cross-class, swap back, transfer before/after Vanguard checkpoint");
    }

    private static void HeroSkinConcedeBoundaries()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior", "mage", 4242);
        match.Start();
        Check(match.Game.Player1.Choice?.ChoiceType == ChoiceType.MULLIGAN, "concede boundary starts during real mulligan");
        match.Concede();
        Check(match.Game.State == State.COMPLETE && match.HumanConceded, "mulligan concede reaches authoritative terminal state");
        int cursor = capture.Packets.Count;
        bool terminalRejected = false;
        try { match.Concede(); } catch (InvalidOperationException) { terminalRejected = true; }
        Check(terminalRejected && capture.Packets.Count == cursor, "complete match rejects duplicate engine concession without more history");
        Console.WriteLine("PASS actual engine mulligan and game-over concession boundaries");
    }
}
