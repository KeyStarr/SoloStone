using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using SabberStoneCore.Tasks.PlayerTasks;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    // A trigger block with no visible content (no tasks, metadata, sub-spell, keyword or Secret) makes the client
    // animate the card as if it fired. Wrathspike Brute did this on every friendly attack (seen in a playtest) and
    // Chillmaw did it when its condition failed. The transport now drops such empty trigger blocks, but must keep
    // anything that still means something: nested content, metadata, keyword boundaries (Poisonous, Lifesteal, empty
    // Deathrattle), Secrets, split packets. The private-effect cases check that hiding a private effect looks the
    // same to the opponent as an activation that did nothing.
    [GroupProof("empty-triggers", ProofKind.Native)]
    private static void EmptyTriggerPresentation()
    {
        foreach (string content in new[] { "empty", "metadata", "subspell", "child", "child-trigger", "poisonous", "lifesteal", "deathrattle", "deathrattle-keyword", "secret", "tag", "split-packet" })
        {
            var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null);
            string id = content == "secret" ? "EX1_287" : content == "deathrattle" ? "EX1_029" : "CORE_BT_510";
            var owner = match.Game.Player1;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var start = PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0);
            if (content is "poisonous" or "lifesteal") start.TriggerKeyword = (int)(content == "poisonous" ? GameTag.POISONOUS : GameTag.LIFESTEAL);
            if (content == "deathrattle-keyword") start.TriggerKeyword = (int)GameTag.DEATHRATTLE;
            var entries = new List<IPowerHistoryEntry> { start };
            if (content == "metadata") entries.Add(new SabberStoneCore.Kettle.PowerHistoryMetaData { Type = MetaDataType.TARGET, Info = new() { source.Id } });
            if (content == "subspell") entries.AddRange(new IPowerHistoryEntry[] {
                new SabberStoneCore.Kettle.PowerHistorySubSpellStart { Source = source.Id, Targets = new() { source.Id }, SpellPrefabGuid = "test-subspell" },
                new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
            if (content is "child" or "child-trigger") entries.AddRange(new IPowerHistoryEntry[] {
                PowerHistoryBuilder.BlockStart(content == "child-trigger" ? BlockType.TRIGGER : BlockType.POWER, source.Id, "", -1, 0), PowerHistoryBuilder.BlockEnd() });
            if (content == "tag") entries.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.ATK, 4));
            if (content == "split-packet") { SendHistoryEntries(match, entries); entries = new(); }
            entries.Add(PowerHistoryBuilder.BlockEnd()); SendHistoryEntries(match, entries);
            var rows = History(capture, cursor);
            Check(rows.Any(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.TRIGGER) == (content is not ("empty" or "deathrattle")), "empty-trigger projection: " + content);
            if (content == "deathrattle-keyword")
            {
                var retained = rows.Single(e => e.HasPowerStart).PowerStart;
                Check(retained.TriggerKeyword == (int)GameTag.DEATHRATTLE && retained.EffectIndex == -1
                    && string.IsNullOrEmpty(retained.EffectCardId) && rows.Count(e => e.HasPowerEnd) == 1,
                    "empty Deathrattle retains balanced keyword boundary without selecting its card effect");
            }
            if (content == "child-trigger") Check(rows.Count(e => e.HasPowerStart) == 1, "taskless parent survives empty child removal");
        }
        foreach (int seat in new[] { 1, 2 })
        {
            var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null); var g = match.Game;
            var owner = seat == 1 ? g.Player1 : g.Player2;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CORE_BT_510"), zone: owner.BoardZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.HandZone);
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            // A filtered private effect must be indistinguishable from an empty activation.
            var entries = new List<IPowerHistoryEntry> {
                PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0),
                PowerHistoryBuilder.TagChange(hidden.Id, (GameTag)33060, 1), PowerHistoryBuilder.BlockEnd() };
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { entries });
            var rows = History(capture, cursor);
            Check(!rows.Any(e => e.HasPowerStart && e.PowerStart.Source == source.Id)
                && !rows.Any(e => e.HasTagChange && e.TagChange.Tag == 33060), "private-only trigger suppressed with payload, seat " + seat);
        }
        EmptyTriggerHiddenIdentityPrivacy();
        EmptyDeathrattleBoundaryPrivacy();
        EmptyTriggerRootSegmentBoundaries();
        DinositterEmptyTriggerPrivacy();
        EmptyTriggerSegmentBoundaries();
        Console.WriteLine("PASS empty trigger projection: viewer-wire-empty effects suppressed; lifecycle, nested boundaries, metadata, subspells, other keywords, visible secrets and split packets retained");
    }

    private static void EmptyDeathrattleBoundaryPrivacy()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            byte[][] Run(bool hiddenEffect)
            {
                var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null); var g = match.Game;
                var owner = seat == 1 ? g.Player1 : g.Player2;
                var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CORE_AT_123"), zone: owner.BoardZone);
                var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                var entries = new List<IPowerHistoryEntry> {
                    PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0),
                    PowerHistoryBuilder.TagChange(source.Id, GameTag.ATK, 4),
                    PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "CORE_AT_123", -1, 0, triggerKeyword: (int)GameTag.DEATHRATTLE)
                };
                if (hiddenEffect) entries.Add(PowerHistoryBuilder.TagChange(hidden.Id, (GameTag)33060, 1));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                entries.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.DAMAGE, 1));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                SendHistoryEntries(match, entries);
                var rows = History(capture, cursor);
                var start = rows.Single(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.TRIGGER).PowerStart;
                Check(start.EffectIndex == -1 && string.IsNullOrEmpty(start.EffectCardId) && start.TriggerKeyword == (int)GameTag.DEATHRATTLE,
                    "viewer-empty Deathrattle keeps boundary/keyword but disables explicit and fallback card effects");
                return capture.WirePackets.Skip(cursor).Select(p => p.Bytes).ToArray();
            }
            var empty = Run(false); var filtered = Run(true);
            Check(empty.Length == filtered.Length && empty.Zip(filtered).All(p => p.First.SequenceEqual(p.Second)),
                "empty and private-only Deathrattles have identical serialized effect selectors, seat " + seat);
        }
    }

    private static void EmptyTriggerHiddenIdentityPrivacy()
    {
        byte[][] Run(string card, bool privateBody, int lifecycle = -1)
        {
            var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null); var g = match.Game;
            var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(g.Player2, Cards.FromId(card), zone: g.Player2.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            int source = lifecycle < 0 ? hidden.Id : new[] { g.Id, g.Player1.Id, g.Player2.Id }[lifecycle];
            var entries = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source, "", -1, 0) };
            if (privateBody) entries.Add(PowerHistoryBuilder.TagChange(hidden.Id, (GameTag)33060, 1));
            entries.Add(PowerHistoryBuilder.BlockEnd());
            if (lifecycle >= 0) SendHistoryEntries(match, entries);
            else
            {
                // Actual Secret activation reveals its source before pruning; isolate pruning with identical post-privacy rows.
                var packet = new PegasusGame.PowerHistory { List = new() {
                    new PowerHistoryData { PowerStart = new PowerHistoryStart { Type = HistoryBlock.Type.TRIGGER, Source = source } },
                    new PowerHistoryData { PowerEnd = new PowerHistoryEnd() } } };
                typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { packet });
            }
            var rows = History(capture, cursor);
            Check(rows.Any(e => e.HasPowerStart && e.PowerStart.Source == source) == (lifecycle >= 0),
                "hidden source traits never retain empty trigger; lifecycle always retained: " + card);
            return capture.WirePackets.Skip(cursor).Select(p => p.Bytes).ToArray();
        }
        var empty = Run("CORE_BT_510", false);
        foreach (string card in new[] { "CORE_BT_510", "EX1_287", "EX1_029" })
        {
            var actual = Run(card, false);
            Check(empty.Length == actual.Length && empty.Zip(actual).All(pair => pair.First.SequenceEqual(pair.Second)),
                "hidden Secret/Deathrattle identity cannot change pruning of identical wire rows: " + card);
        }
        foreach (int lifecycle in new[] { 0, 1, 2 })
        {
            var before = Run("CORE_BT_510", false, lifecycle); var after = Run("EX1_029", true, lifecycle);
            Check(before.Length == after.Length && before.Zip(after).All(pair => pair.First.SequenceEqual(pair.Second)),
                "lifecycle retained identically with hidden private effect: " + lifecycle);
        }
    }

    private static void EmptyTriggerRootSegmentBoundaries()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool before in new[] { false, true })
        foreach (bool after in new[] { false, true })
        foreach (int pairs in new[] { 1, 2 })
        {
            var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null);
            var owner = seat == 1 ? match.Game.Player1 : match.Game.Player2;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var entries = new List<IPowerHistoryEntry>();
            if (before) entries.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.ATK, 4));
            for (int i = 0; i < pairs; i++)
            {
                entries.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0));
                entries.Add(PowerHistoryBuilder.BlockEnd());
            }
            if (after) entries.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.DAMAGE, 1));
            SendHistoryEntries(match, entries);
            var rows = History(capture, cursor);
            string label = $"root empty triggers: seat={seat}, before={before}, after={after}, pairs={pairs}";
            Check(rows.Count(e => e.HasPowerStart) == (before && after ? 1 : 0), label);
            Check(rows.Count(e => e.HasPowerEnd) == (before && after ? 1 : 0), label + " balanced");
            var tags = rows.Where(e => e.HasTagChange && e.TagChange.Entity == source.Id).Select(e => e.TagChange.Tag);
            Check(tags.SequenceEqual((before ? new[] { (int)GameTag.ATK } : Array.Empty<int>())
                .Concat(after ? new[] { (int)GameTag.DAMAGE } : Array.Empty<int>())), label + " loose tasks preserved");
            if (before && after)
            {
                Check(rows.Single(e => e.HasPowerStart).PowerStart.EffectIndex == -1,
                    label + " retained empty separator has no effect spell");
                Check(rows.FindIndex(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.ATK)
                    < rows.FindIndex(e => e.HasPowerStart)
                    && rows.FindIndex(e => e.HasPowerEnd)
                    < rows.FindIndex(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.DAMAGE), label + " separates loose tasks");
            }
        }
    }

    private static void DinositterEmptyTriggerPrivacy()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            (int Id, byte[] Bytes)[] Run(bool discount)
            {
                var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null); var g = match.Game;
                var owner = seat == 1 ? g.Player1 : g.Player2;
                if (g.CurrentPlayer != owner) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "Dinositter owner turn");
                foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
                var sitter = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("TLC_822"), zone: owner.BoardZone);
                // Same hidden hand size/entity IDs: only the hidden identity and its private COST effect differ.
                var heldCard = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(discount ? "CS2_172" : "GAME_005"), zone: owner.HandZone);
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                Check(g.Process(EndTurnTask.Any(owner)), "Dinositter owner end");
                Check(!discount || heldCard.Cost == 1, "Dinositter real discount");
                if (discount)
                {
                    int activation = g.PowerHistory.Last.FindIndex(e => e is PowerHistoryBlockStart start && start.Source == sitter.Id);
                    Check(activation >= 0 && g.PowerHistory.Last[activation + 1] is SabberStoneCore.Kettle.PowerHistoryTagChange cost
                        && cost.EntityId == heldCard.Id && cost.Tag == GameTag.COST && cost.Value == 1,
                        "Dinositter freezes successful COST inside its engine activation");
                }
                FlushFieldHistory(match);
                var rows = History(capture, cursor);
                Check(rows.Any(e => e.HasPowerStart && e.PowerStart.Source == sitter.Id) == (discount && seat == 1),
                    "Dinositter activation matches viewer-visible effect, seat " + seat + ", discount=" + discount);
                Check(rows.Any(e => e.HasTagChange && e.TagChange.Entity == heldCard.Id && e.TagChange.Tag == (int)GameTag.COST && e.TagChange.Value == 1)
                    == (discount && seat == 1), "only owner receives Dinositter discount COST");
                if (seat == 2)
                    Check(!rows.Any(e => e.HasShowEntity && e.ShowEntity.Entity == heldCard.Id && e.ShowEntity.Name.Length > 0),
                        "opponent keeps hand identity hidden");
                return capture.WirePackets.Skip(cursor).ToArray();
            }
            var noBeast = Run(false); var hiddenBeast = Run(true);
            if (seat == 2)
                Check(noBeast.Length == hiddenBeast.Length && noBeast.Zip(hiddenBeast).All(pair =>
                    pair.First.Id == pair.Second.Id && pair.First.Bytes.SequenceEqual(pair.Second.Bytes)),
                    "opponent packets byte-identical for hidden Beast discount versus no Beast in hand");
        }
    }

    private static void EmptyTriggerSegmentBoundaries()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string content in new[] { "tags", "show", "target-before", "target-after", "subspell", "open-subspell", "packet-parent" })
        {
            // A filtered private body and no body must preserve identical task boundaries.
            byte[][] Run(bool reference)
            {
                var capture = new Capture(); var match = StartFixture(capture, "CS2_182", null); var g = match.Game;
                var owner = seat == 1 ? g.Player1 : g.Player2;
                var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
                var visible = owner.Hero;
                var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(g.Player2, Cards.FromId("CS2_172"), zone: g.Player2.HandZone);
                FlushFieldHistory(match); int cursor = capture.Packets.Count;
                var prefix = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
                var parent = PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "RLK_708", -1, 0, effectIndex: 0);
                var child = PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0);
                IPowerHistoryEntry Target() => new SabberStoneCore.Kettle.PowerHistoryMetaData { Type = MetaDataType.TARGET, Info = new() { visible.Id } };
                var entries = new List<IPowerHistoryEntry> { parent, PowerHistoryBuilder.TagChange(visible.Id, GameTag.DAMAGE, 0) };
                if (content == "target-before") entries.Add(Target());
                if (content is "subspell" or "open-subspell") entries.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart {
                    Source = source.Id, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21" });
                if (content is "open-subspell" or "packet-parent") { SendHistoryEntries(match, entries); entries = new(); }
                entries.Add(child);
                if (reference) entries.Add(PowerHistoryBuilder.TagChange(hidden.Id, (GameTag)33060, 1));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                if (content == "show") entries.Add(PowerHistoryBuilder.ShowEntity(hidden));
                entries.Add(PowerHistoryBuilder.TagChange(hidden.Id, GameTag.ZONE_POSITION, 1));
                if (content == "target-after") entries.Add(Target());
                if (content is "subspell" or "open-subspell") entries.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
                entries.Add(PowerHistoryBuilder.BlockEnd()); SendHistoryEntries(match, entries);
                var packets = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
                Check(NativeAdmission.ValidatePackets(packets).Count == 0, "empty child native admission: " + content);
                var sent = capture.HistorySnapshots(cursor).ToArray(); var rows = sent.SelectMany(p => p.List).ToList();
                int at = rows.FindIndex(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.TRIGGER && e.PowerStart.Source == source.Id);
                Check(at >= 0 && rows[at + 1].HasPowerEnd, "empty child preserves task boundary: " + content);
                if (content == "show") Check(rows.Any(e => e.HasShowEntity && e.ShowEntity.Entity == hidden.Id), "deferred SHOW remains in continuation");
                if (content is "subspell" or "open-subspell")
                {
                    var sub = rows.Single(e => e.HasSubSpellStart).SubSpellStart;
                    Check(sub.TargetEntityIds.SequenceEqual(new[] { source.Id }), "subspell freezes typed source across empty child: " + content);
                    var saved = sub.TargetEntityIds; sub.TargetEntityIds = new();
                    Check(NativeAdmission.ValidatePackets(prefix.Concat(sent.Select(p => (IReadOnlyList<PowerHistoryData>)p.List))).Any(f => f.Rule == "R1"), "subspell descriptor mutation exposes hidden target");
                    sub.TargetEntityIds = saved;
                }
                else if (content != "target-after")
                {
                    var target = rows[at + 2];
                    Check(target.HasMetaData && (int)target.MetaData.Type == 0 && target.MetaData.Info.SequenceEqual(new[] { source.Id }),
                        "continuation gets independent TARGET before deferred SHOW/hidden tags: " + content);
                    foreach (var packet in sent) packet.List.Remove(target);
                    Check(NativeAdmission.ValidatePackets(prefix.Concat(sent.Select(p => (IReadOnlyList<PowerHistoryData>)p.List))).Any(f => f.Rule == "R1"), "continuation TARGET mutation rejected");
                    sent = capture.HistorySnapshots(cursor).ToArray();
                }
                return sent.Select(packet => { using var stream = new MemoryStream(); packet.Serialize(stream); return stream.ToArray(); }).ToArray();
            }
            var expected = Run(true); var actual = Run(false);
            Check(actual.Length == expected.Length && actual.Zip(expected).All(pair => pair.First.SequenceEqual(pair.Second)),
                "empty child preserves serialized native tasks, TARGETs and frozen descriptors: " + content + ", seat " + seat);
        }
    }

}
