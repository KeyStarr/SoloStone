using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Replay of a real playtest game: Holy Bola played between the two halves of a split (Shatter) card merged them
    // before its draw. The native client ignores replacements inside the PLAY zone list, so the halves stayed, the
    // merged card never appeared and the draw stalled. The seed, actions, pool snapshot and final hand ids are the
    // recorded ones; the merge must now sit in its own TRIGGER block and the retired halves must be hidden.
    private static void HolyBolaLiveReplay()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "showcase-paladin-pure-holy", "showcase-ai-warrior-armor", 1014061229, startingStars: 15);
        match.Start();
        // Release-24's recorded actions depend on the accepted pool membership.
        const string poolHash = "29ea592977fe61745aacb70dfe5f511ab240e9b051df570bdb2495b7b704b4d0";
        string poolPath = "tests/goldens/pools/" + poolHash + ".xml";
        byte[] poolBytes = File.ReadAllBytes(poolPath);
        Check(Convert.ToHexString(SHA256.HashData(poolBytes)).ToLowerInvariant() == File.ReadAllText(poolPath + ".sha256").Trim(),
            "release-24 pool snapshot checksum");
        var policy = (OfflinePoolPolicy)typeof(OfflinePoolPolicy).GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { XDocument.Load(new MemoryStream(poolBytes)) });
        var pools = policy.BuildManifest();
        Check(pools.Hash == poolHash, "release-24 pool manifest");
        typeof(SabberStoneCore.Model.Game).GetProperty("OfflinePools").SetValue(match.Game, pools);
        match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int> { 11, 14 });
        foreach (var action in new[] { (0, 0, 0), (3, 0, 1), (0, 0, 0), (1, 8, 0), (1, 0, 0), (1, 42, 0), (0, 0, 0), (2, 0, 0) })
        {
            int cursor = capture.Packets.Count;
            match.SendOption(capture.Last<PegasusGame.AllOptions>(14).Id, action.Item1, action.Item2, -1, action.Item3);
            if (match.Game.Player1.Choice != null)
                match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int> { 73 });
            DumpDrawHistory("live-" + cursor, capture, cursor);
            if (action == (2, 0, 0))
            {
                var data = History(capture, cursor);
                int full = data.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == 77);
                Check(full > 0 && data[full - 1].HasPowerStart && (int)data[full - 1].PowerStart.Type == (int)BlockType.TRIGGER,
                    "release-24 merged entity 77 belongs to its own TRIGGER before Nozdormu draw");
                foreach (int half in new[] { 69, 70 })
                    Check(data.Any(e => e.HasHideEntity && e.HideEntity.Entity == half && e.HideEntity.Zone == 5), "release-24 retired half " + half);
                Check(data.Count(e => e.HasShowEntity && e.ShowEntity.Entity == 34) == 1, "release-24 Nozdormu drawn once");
            }
        }
        Check(match.Game.Player1.HandZone.Select(c => c.Id).SequenceEqual(new[] { 77, 31, 73, 33, 13, 10, 34 }), "release-24 exact final hand ids");
        capture.SerializeAll();
        Console.WriteLine("PASS Holy Bola live replay: release-24 seed/actions/exact entity ids");
    }

    private static void HolyBolaHistory()
    {
        HolyBolaLiveReplay();
        var failures = new List<string>();
        foreach (bool bot in new[] { false, true })
        foreach (int cost in new[] { 5, 2, 0 })
        foreach (bool separator in new[] { true, false })
        {
            var (match, capture) = AnimationMatch(bot, "showcase-paladin-pure-holy");
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            var left = EngineEntity.FromCard(owner, Cards.FromId("CATA_479t"), zone: owner.HandZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId("JAIL_377"), zone: owner.HandZone);
            if (!separator) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var right = EngineEntity.FromCard(owner, Cards.FromId("CATA_479t2"), zone: owner.HandZone);
            foreach (string id in new[] { "CATA_478", "JAIL_326", "CATA_475", "TIME_015", "CORE_BT_292" })
                EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            var second = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var first = EngineEntity.FromCard(owner, Cards.FromId("CATA_473"), zone: owner.DeckZone);
            first.Cost = cost;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            // An unrelated draw cannot join halves with a separator still between them.
            Generic.Draw(owner, second);
            Check(left.Zone == owner.HandZone && right.Zone == owner.HandZone, "unrelated draw preserves separated Shatter halves");
            owner.DeckZone.Add(owner.HandZone.Remove(second));
            owner.DeckZone.Remove(first); owner.DeckZone.Add(first);
            FlushFieldHistory(match);
            cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            string name = $"bola-{cost}-{separator}-{bot}";
            DumpDrawHistory(name, capture, cursor);
            var data = History(capture, cursor);
            int power = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)BlockType.POWER);
            Check(power >= 0 && data.Count(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && (int)e.PowerStart.Type == (int)BlockType.POWER) == 1, name + " both draws share one POWER");
            Check(first.Zone == owner.HandZone && (cost <= 2 ? second.Zone == owner.HandZone : second.Zone == owner.DeckZone), name + " conditional draw count");
            if (separator)
            {
                var combined = owner.HandZone.Single(c => c.Card.Id == "CATA_479");
                Check(owner.HandZone[0] == combined && combined[ShatterRules.AlreadyCombined] == 1 && combined[(GameTag)4239] == 0,
                    name + " removing Bola joins at lower slot with permanent no-Shatter state");
                int full = data.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == combined.Id);
                // Pinned client contract (docs/design/shatter-presentation.md): the replacement stages in SETASIDE so the
                // CombinedSpawn override is installed before HAND entry consumes it; all of it precedes the draw POWER.
                int spawn = data.FindIndex(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == SabberStoneCore.Presentation.ShatterHistory.CombinedSpawn
                    && e.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { combined.Id }));
                int hand = data.FindIndex(e => e.HasTagChange && e.TagChange.Entity == combined.Id
                    && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.HAND);
                Check(full >= 0 && full < spawn && spawn < hand && hand < power && SnapshotTag(data[full], GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.SETASIDE
                    && SnapshotName(data[full]) == (bot ? "" : "CATA_479"), name + " merge FULL, spawn override and HAND entry precede draw POWER with correct privacy");
                var blocks = new Stack<BlockType>();
                foreach (var e in data.Take(hand))
                {
                    if (e.HasPowerStart) blocks.Push((BlockType)e.PowerStart.Type);
                    if (e.HasPowerEnd) blocks.Pop();
                }
                // ZoneMgr ignores the entire PLAY change list when acknowledging local prediction.
                if (blocks.Count == 0 || blocks.Peek() == BlockType.PLAY)
                    failures.Add(name + ": Shatter replacement must have its own unpredicted task list");
                foreach (var half in new[] { left, right })
                    if (!data.Any(e => e.HasHideEntity && e.HideEntity.Entity == half.Id && e.HideEntity.Zone == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME))
                        failures.Add(name + ": retired half HIDE must agree with REMOVEDFROMGAME tag");
            }
            else Check(left.Zone == owner.HandZone && right.Zone == owner.HandZone, name + " other separator prevents join");
            foreach (var drawn in new[] { first, second }.Where(c => c.Zone == owner.HandZone))
                Check(data.Any(e => e.HasShowEntity && e.ShowEntity.Entity == drawn.Id && SnapshotTag(e, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.HAND), name + " draw destination SHOW");
            capture.SerializeAll();
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS Holy Bola history: 12 cases; expensive/cheap/zero, merge/no merge, both players");
    }
}
