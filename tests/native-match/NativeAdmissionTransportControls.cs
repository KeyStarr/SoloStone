using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void NativeAdmissionTransportControls()
    {
        NativeAdmissionContinuationControls();
        NativeGeneratorTargetControls();
        int cases = 0;
        foreach (BlockType type in new[] { BlockType.POWER, BlockType.TRIGGER })
        foreach (bool sourceOnly in new[] { false, true })
        foreach (bool explicitTarget in new[] { false, true })
        foreach (bool subspell in new[] { false, true })
        foreach (bool lateTarget in new[] { false, true })
        foreach (bool newHidden in new[] { false, true })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            // These are outbound snapshots, deliberately absent from engine state. The repair must use only the wire.
            PowerHistoryData Full(int id, int cardType, int zone, string name) => new() { FullEntity = new() {
                Entity = id, Name = name, Tags = new() {
                    new() { Name = (int)GameTag.CARDTYPE, Value = cardType },
                    new() { Name = (int)GameTag.ZONE, Value = zone },
                    new() { Name = (int)GameTag.CONTROLLER, Value = 2 } } } };
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            var hidden = Full(103, 0, 2, "");
            hidden.FullEntity.Tags.RemoveAll(t => t.Name == (int)GameTag.CARDTYPE);
            var setup = new PegasusGame.PowerHistory { List = new() { Full(100, 5, 1, "RLK_708"), Full(101, 4, 1, "CS2_182"),
                Full(102, 3, 1, "HERO_08") } };
            if (!newHidden) setup.List.Add(hidden);
            Send(setup);
            var start = new PowerHistoryData { PowerStart = new() { Source = 100, Type = (HistoryBlock.Type)(int)type } };
            var packet = new PegasusGame.PowerHistory { List = new() { start } };
            var expected = new List<int> { 102, 101 };
            var authored = new PowerHistoryData { MetaData = new() { Type = (HistoryMeta.Type)0, Data = 100, Info = expected } };
            if (explicitTarget && !lateTarget) packet.List.Add(authored);
            if (newHidden) packet.List.Add(hidden);
            var sub = new PowerHistoryData { SubSpellStart = new() { SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21", SourceEntityId = 100 } };
            if (subspell) packet.List.Add(sub);
            foreach (int id in sourceOnly ? new[] { 103, 100, 103 } : new[] { 103, 102, 101, 102, 100 })
                packet.List.Add(new() { TagChange = new() { Entity = id, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } });
            if (subspell) packet.List.Add(new() { SubSpellEnd = new() });
            if (explicitTarget && lateTarget) packet.List.Add(authored);
            packet.List.Add(new() { PowerEnd = new() });
            Send(packet);
            var packets = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            Check(NativeAdmission.ValidatePackets(packets).Count == 0, "transport repairs untyped inference without engine identity");
            var target = subspell ? packets[^1].Single(e => e.HasSubSpellStart) : packets[^1].Single(e => e.HasMetaData);
            var safe = sourceOnly ? new[] { 100 } : expected.ToArray();
            Check(subspell ? sub.SubSpellStart.TargetEntityIds.SequenceEqual(safe)
                : target.MetaData.Info.SequenceEqual(explicitTarget ? expected : safe), "segment preserves typed target order / source fallback");
            if (explicitTarget)
                Check(packet.List.Contains(authored) && authored.MetaData.Data == 100 && authored.MetaData.Info.SequenceEqual(expected)
                    && (lateTarget ? packet.List[^2] == authored : packet.List[1] == authored), "authored late/early TARGET remains in place with no extra source impact");
            Check(packet.List.Count(e => e.HasMetaData) == (subspell ? explicitTarget ? 1 : 0 : 1), "no redundant metadata across subspell boundary");
            Check(!packet.List.Any(e => e.HasShowEntity || e.HasChangeEntity || e.HasFullEntity && !ReferenceEquals(e, hidden)
                || e.HasTagChange && e.TagChange.Tag == (int)GameTag.CARDTYPE)
                && hidden.FullEntity.Name == "" && hidden.FullEntity.Tags.All(t => t.Name != (int)GameTag.CARDTYPE),
                "repair never reveals private type or identity");
            if (subspell)
            {
                var saved = target.SubSpellStart.TargetEntityIds;
                target.SubSpellStart.TargetEntityIds = new();
                Check(NativeAdmission.ValidatePackets(packets).Any(f => f.Rule == "R1"), "empty subspell IDs restore inference even with parent metadata");
                target.SubSpellStart.TargetEntityIds = saved;
            }
            else if (!explicitTarget)
                Check(NativeAdmission.ValidatePackets(packets.Select(p => (IReadOnlyList<PowerHistoryData>)p.Where(e => !ReferenceEquals(e, target)).ToArray())).Any(f => f.Rule == "R1"),
                    "TARGET removal mutation must restore R1 violation");
            foreach (int unsafeId in new[] { 103, 999 })
            {
                var saved = subspell ? target.SubSpellStart.TargetEntityIds : target.MetaData.Info;
                if (subspell) target.SubSpellStart.TargetEntityIds = new() { unsafeId };
                else target.MetaData.Info = new() { unsafeId };
                Check(NativeAdmission.ValidatePackets(packets).Any(f => f.Rule == "R1"), "hidden/missing target replacement rejected");
                if (subspell) target.SubSpellStart.TargetEntityIds = saved;
                else target.MetaData.Info = saved;
            }
            cases++;
        }
        foreach (BlockType type in new[] { BlockType.POWER, BlockType.TRIGGER })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            Send(new() { List = new() {
                new() { FullEntity = new() { Entity = 300, Name = "RLK_708", Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = 5 } } } },
                new() { FullEntity = new() { Entity = 301, Name = "", Tags = new() { new() { Name = (int)GameTag.ZONE, Value = 2 } } } } } });
            var opening = new PegasusGame.PowerHistory { List = new() {
                new() { PowerStart = new() { Source = 300, Type = (HistoryBlock.Type)(int)type } } } };
            Send(opening);
            var continuation = new PegasusGame.PowerHistory { List = new() {
                new() { TagChange = new() { Entity = 301, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } }, new() { PowerEnd = new() } } };
            Send(continuation);
            var packets = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            Check(NativeAdmission.ValidatePackets(packets).Count == 0, "continuation segment protected in its own packet");
            Check(opening.List.Count == 1 && continuation.List[0].HasMetaData && continuation.List[0].MetaData.Info.SequenceEqual(new[] { 300 }),
                "continuation receives own TARGET without changing already sent opening packet");
            var target = packets[^1][0];
            Check(NativeAdmission.ValidatePackets(packets.Select(p => (IReadOnlyList<PowerHistoryData>)p.Where(e => !ReferenceEquals(e, target)).ToArray()))
                .Any(f => f.Rule == "R1"), "removing opening TARGET exposes continuation's untyped target");
            cases++;
        }
        foreach (string boundary in new[] { "child", "sub-end", "flush", "packet", "open-subspell", "equal-anchor" })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            PowerHistoryData Target() => new() { MetaData = new() { Type = (HistoryMeta.Type)0, Info = new() { 300 } } };
            PowerHistoryData Start() => new() { PowerStart = new() { Source = 300, Type = (HistoryBlock.Type)(int)BlockType.POWER } };
            Send(new() { List = new() {
                new() { FullEntity = new() { Entity = 300, Name = "RLK_708", Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = 5 } } } },
                new() { FullEntity = new() { Entity = 301, Name = "", Tags = new() { new() { Name = (int)GameTag.ZONE, Value = 2 } } } },
                new() { FullEntity = new() { Entity = 302, Name = "HERO_08", Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = 3 } } } } } });
            var sub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 302, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21" } };
            var packet = new PegasusGame.PowerHistory { List = new() { Start(), Target() } };
            if (boundary is "child" or "equal-anchor") packet.List.AddRange(new[] { Start(), Target(), new PowerHistoryData { PowerEnd = new() } });
            if (boundary == "sub-end")
            {
                sub.SubSpellStart.TargetEntityIds = new() { 300 };
                packet.List.AddRange(new[] { sub, new PowerHistoryData { SubSpellEnd = new() } });
            }
            if (boundary == "flush") packet.List.Add(new() { MetaData = new() { Type = (HistoryMeta.Type)21 } });
            if (boundary == "open-subspell") packet.List.Add(sub);
            if (boundary is "packet" or "open-subspell")
            {
                Send(packet);
                Check(boundary != "open-subspell" || sub.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { 302 }),
                    "open subspell binds its own typed source before packet continuation");
                packet = new() { List = new() };
            }
            packet.List.Add(new() { TagChange = new() { Entity = 301, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } });
            if (boundary == "open-subspell") packet.List.Add(new() { SubSpellEnd = new() });
            if (boundary == "equal-anchor")
            {
                packet.List.AddRange(new[] { Start(), Target(), new PowerHistoryData { PowerEnd = new() },
                    new PowerHistoryData { TagChange = new() { Entity = 301, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } }, Target() });
            }
            packet.List.Add(new() { PowerEnd = new() });
            Send(packet);
            var packets = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            Check(NativeAdmission.ValidatePackets(packets).Count == 0, "repair respects native list boundary: " + boundary);
            if (boundary == "open-subspell") packets.SelectMany(p => p).Single(e => e.HasSubSpellStart).SubSpellStart.TargetEntityIds = new();
            else
            {
                var sent = packets[^1].ToList();
                var target = boundary == "equal-anchor" ? sent[sent.FindIndex(e => e.HasTagChange && e.TagChange.Entity == 301) - 1] : sent.Last(e => e.HasMetaData && (int)e.MetaData.Type == 0);
                sent.Remove(target); packets[^1] = sent;
            }
            Check(NativeAdmission.ValidatePackets(packets).Any(f => f.Rule == "R1"), "boundary protection removal rejected: " + boundary);
            cases++;
        }
        foreach (bool subspell in new[] { false, true })
        foreach (int invalid in new[] { 401, 999 })
        {
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            var metadata = new PowerHistoryData { MetaData = new() { Type = (HistoryMeta.Type)0, Info = new() { invalid, 400 } } };
            var sub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 400, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21", TargetEntityIds = new() { invalid, 400 } } };
            var packet = new PegasusGame.PowerHistory { List = new() {
                new() { FullEntity = new() { Entity = 400, Name = "RLK_708", Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = 5 } } } },
                new() { FullEntity = new() { Entity = 401, Name = "", Tags = new() { new() { Name = (int)GameTag.ZONE, Value = 2 } } } },
                new() { PowerStart = new() { Source = 400, Type = (HistoryBlock.Type)(int)BlockType.POWER } }, subspell ? sub : metadata,
                new() { TagChange = new() { Entity = 401, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } } } };
            if (subspell) packet.List.Add(new() { SubSpellEnd = new() });
            packet.List.Add(new() { PowerEnd = new() });
            typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { packet });
            Check(NativeAdmission.Validate(capture.HistorySnapshots().Single().List).Count == 0 && (subspell ? sub.SubSpellStart.TargetEntityIds : metadata.MetaData.Info).SequenceEqual(new[] { 400 }),
                "invalid authored IDs removed while safe authored target retained");
            cases++;
        }
        foreach (int sourceType in new[] { 0, 6 })
        {
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            PowerHistoryData Full(int id, int type, string name) => new() { FullEntity = new() { Entity = id, Name = name,
                Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = type }, new() { Name = (int)GameTag.CONTROLLER, Value = 2 }, new() { Name = (int)GameTag.ZONE, Value = 1 } } } };
            var packet = new PegasusGame.PowerHistory { List = new() { Full(500, sourceType, sourceType == 0 ? "" : "enchantment"),
                Full(501, 3, "HERO_08"), Full(502, 0, ""),
                new() { PowerStart = new() { Source = 500, Type = (HistoryBlock.Type)(int)BlockType.TRIGGER } },
                new() { TagChange = new() { Entity = 502, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } },
                new() { PowerEnd = new() } } };
            typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { packet });
            Check(NativeAdmission.Validate(capture.HistorySnapshots().Single().List).Count == 0 && !packet.List.Any(e => e.HasMetaData),
                "unknown anonymous/enchantment FX retains inference instead of inventing a hero anchor");
            cases++;
        }
        foreach (bool sameOwner in new[] { false, true })
        foreach (bool usesPower in new[] { false, true })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send);
            PowerHistoryData Full(int id, string card, int type, int owner) => new() { FullEntity = new() {
                Entity = id, Name = card, Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = type },
                    new() { Name = (int)GameTag.CONTROLLER, Value = owner } } } };
            var power = Full(201, "EX1_tk33", 10, sameOwner ? 1 : 2);
            var start = new PowerHistoryData { PowerStart = new() { Source = 200, Type = (HistoryBlock.Type)(int)BlockType.POWER } };
            var packet = new PegasusGame.PowerHistory { List = new() {
                Full(200, "CORE_EX1_323", 3, 1),
                new() { PowerStart = new() { Source = 200, Type = (HistoryBlock.Type)(int)BlockType.PLAY } }, power,
                new() { TagChange = new() { Entity = usesPower ? 201 : 200, Tag = (int)GameTag.COST, Value = 0 } }, start,
                Full(202, "EX1_323w", 7, 1), new() { PowerEnd = new() }, new() { PowerEnd = new() } } };
            typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { packet });
            var sent = capture.HistorySnapshots().Single().List;
            Check(NativeAdmission.Validate(sent).Count(f => f.Rule == "R4") == (sameOwner && !usesPower ? 0 : 1),
                "hero power FULL ownership repair requires same controller");
            if (sameOwner && !usesPower)
            {
                var mutant = sent.ToList();
                var sentPower = mutant.Single(e => e.HasFullEntity && e.FullEntity.Entity == 201);
                var sentStart = mutant.Single(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.POWER);
                mutant.Remove(sentPower); mutant.Insert(mutant.IndexOf(sentStart), sentPower);
                Check(NativeAdmission.Validate(mutant).Any(f => f.Rule == "R4"), "returning hero-power FULL to parent must fail strict scanner");
            }
            cases++;
        }
        Console.WriteLine($"PASS native admission transport: {cases} cases; both block kinds, ordered typed targets/source fallback, authored metadata, privacy, removal mutations");
    }

    private static void NativeAdmissionContinuationControls()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (BlockType kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        foreach (string boundary in new[] { "child", "sub-end", "flush", "packet" })
        {
            var capture = new Capture();
            var match = StartFixture(capture, "CS2_023", null);
            var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player2,
                SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: match.Game.Player2.DeckZone);
            FlushFieldHistory(match);
            int source = match.Game.Player2.Hero.Id;
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            PowerHistoryData Start() => new() { PowerStart = new() { Source = source, Type = (HistoryBlock.Type)(int)kind, EffectCardId = "RLK_708" } };
            var packet = new PegasusGame.PowerHistory { List = new() { Start() } };
            if (boundary == "child") packet.List.AddRange(new[] { Start(), new PowerHistoryData { PowerEnd = new() } });
            if (boundary == "sub-end") packet.List.AddRange(new[] {
                new PowerHistoryData { SubSpellStart = new() { SourceEntityId = source, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21", TargetEntityIds = new() { source } } },
                new PowerHistoryData { SubSpellEnd = new() } });
            if (boundary == "flush") packet.List.Add(new() { MetaData = new() { Type = (HistoryMeta.Type)21 } });
            if (boundary == "packet") { Send(packet); packet = new() { List = new() }; }
            packet.List.AddRange(new[] {
                new PowerHistoryData { TagChange = new() { Entity = hidden.Id, Tag = (int)GameTag.ZONE, Value = (int)EngineZone.PLAY } },
                new PowerHistoryData { ShowEntity = new() { Entity = hidden.Id, Name = "CS2_182", Tags = new() {
                    new() { Name = (int)GameTag.CARDTYPE, Value = (int)CardType.MINION } } } },
                new PowerHistoryData { PowerEnd = new() } });
            Send(packet);
            var snapshots = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            var faults = NativeAdmission.ValidatePackets(snapshots);
            if (faults.Count != 0) failures.Add(kind + " hoisted reveal after " + boundary + ": " + string.Join(";", faults));
            var sent = snapshots[^1];
            int move = sent.ToList().FindIndex(e => e.HasTagChange && e.TagChange.Entity == hidden.Id);
            Check(move > 0 && sent[move - 1].HasShowEntity, "hoisted SHOW still precedes public move");
            var target = sent.LastOrDefault(e => e.HasMetaData && (int)e.MetaData.Type == 0);
            if (target != null)
                Check(NativeAdmission.ValidatePackets(snapshots.Select(p => (IReadOnlyList<PowerHistoryData>)p.Where(e => !ReferenceEquals(e, target)).ToArray()))
                    .Any(f => f.Rule == "R1"), "removing continuation TARGET exposes pre-SHOW logical type");
            cases++;
        }
        foreach (BlockType kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        foreach (bool authored in new[] { false, true })
        foreach (bool survivingTarget in new[] { false, true })
        {
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            PowerHistoryData Full(int id, int type) => new() { FullEntity = new() { Entity = id, Name = type == 0 ? "" : "public",
                Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = type }, new() { Name = (int)GameTag.ZONE, Value = 2 } } } };
            Send(new() { List = new() { Full(600, 5), Full(601, 4), Full(602, 0), Full(603, 4) } });
            var sub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 600, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21",
                TargetEntityIds = authored ? survivingTarget ? new() { 601, 603 } : new() { 601 } : new() } };
            var opening = new PegasusGame.PowerHistory { List = new() {
                new() { PowerStart = new() { Source = 600, Type = (HistoryBlock.Type)(int)kind } }, sub,
                new() { TagChange = new() { Entity = 601, Tag = (int)GameTag.ZONE, Value = (int)EngineZone.REMOVEDFROMGAME } } } };
            if (survivingTarget) opening.List.Add(new() { TagChange = new() { Entity = 603, Tag = (int)GameTag.DAMAGE, Value = 1 } });
            Send(opening);
            var before = sub.SubSpellStart.TargetEntityIds.ToArray();
            var openingBytes = capture.WirePackets[^1].Bytes.ToArray();
            Send(new() { List = new() {
                new() { TagChange = new() { Entity = 602, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } },
                new() { SubSpellEnd = new() }, new() { PowerEnd = new() } } });
            if (!sub.SubSpellStart.TargetEntityIds.SequenceEqual(before)) failures.Add(kind + " rewrote sent subspell; authored=" + authored);
            var snapshots = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            var faults = NativeAdmission.ValidatePackets(snapshots);
            if (faults.Count != 0) failures.Add(kind + " retired subspell anchor; authored=" + authored + ": " + string.Join(";", faults));
            if (!before.SequenceEqual(new[] { survivingTarget ? 603 : 600 })) failures.Add(kind + " failed to select surviving anchor");
            using var stream = new System.IO.MemoryStream(); opening.Serialize(stream);
            if (!stream.ToArray().SequenceEqual(openingBytes)) failures.Add(kind + " changed opening bytes after send; authored=" + authored);
            // The oracle must keep judging the bytes sent, even if someone rewrites the retained object.
            sub.SubSpellStart.TargetEntityIds = new() { 999 };
            Check(capture.HistorySnapshots().ElementAt(1).List.Single(e => e.HasSubSpellStart).SubSpellStart.TargetEntityIds.SequenceEqual(before),
                "sent history snapshots ignore subsequent in-memory rewrites");
            cases++;
        }
        foreach (BlockType kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        foreach (int seat in new[] { 1, 2 })
        foreach (bool removeBeforeFlush in new[] { false, true })
        foreach (bool authored in new[] { false, true })
        foreach (bool survivingTarget in new[] { false, true })
        {
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            void Send(PegasusGame.PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            PowerHistoryData Full(int id, int type) => new() { FullEntity = new() { Entity = id,
                Name = type == 0 ? "" : type == (int)CardType.SPELL ? "RLK_708" : "CS2_182",
                Tags = new() { new() { Name = (int)GameTag.CONTROLLER, Value = seat },
                    new() { Name = (int)GameTag.ZONE, Value = (int)EngineZone.DECK } } } };
            var setup = new PegasusGame.PowerHistory { List = new() { Full(700, (int)CardType.SPELL),
                Full(701, (int)CardType.MINION), Full(702, 0), Full(703, (int)CardType.MINION) } };
            foreach (var entry in setup.List.Where(e => e.FullEntity.Entity != 702))
                entry.FullEntity.Tags.Add(new() { Name = (int)GameTag.CARDTYPE,
                    Value = entry.FullEntity.Entity == 700 ? (int)CardType.SPELL : (int)CardType.MINION });
            Send(setup);
            var sub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 700, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21",
                TargetEntityIds = authored ? survivingTarget ? new() { 701, 703 } : new() { 701 } : new() } };
            var remove = new PowerHistoryData { TagChange = new() { Entity = 701,
                Tag = (int)GameTag.ZONE, Value = (int)EngineZone.REMOVEDFROMGAME } };
            var packet = new PegasusGame.PowerHistory { List = new() {
                new() { PowerStart = new() { Source = 700, Type = (HistoryBlock.Type)(int)kind } }, sub } };
            if (removeBeforeFlush) packet.List.Add(remove);
            packet.List.Add(new() { ShowEntity = new() { Entity = 702, Name = "CS2_182",
                Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = (int)CardType.MINION } } } });
            packet.List.Add(new() { MetaData = new() { Type = (HistoryMeta.Type)21 } });
            if (!removeBeforeFlush) packet.List.Add(remove);
            packet.List.Add(new() { TagChange = new() { Entity = 702, Tag = (int)GameTag.DAMAGE, Value = 1 } });
            if (survivingTarget) packet.List.Add(new() { TagChange = new() { Entity = 703, Tag = (int)GameTag.DAMAGE, Value = 1 } });
            packet.List.AddRange(new[] { new PowerHistoryData { SubSpellEnd = new() }, new PowerHistoryData { PowerEnd = new() } });
            Send(packet);
            var snapshots = capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List).ToArray();
            var sentSub = snapshots[^1].Single(e => e.HasSubSpellStart).SubSpellStart;
            string label = $"{kind} seat={seat} removeBeforeFlush={removeBeforeFlush} authored={authored} survivingTarget={survivingTarget}";
            var faults = NativeAdmission.ValidatePackets(snapshots);
            if (faults.Count != 0) failures.Add(label + " same-packet continuation: " + string.Join(";", faults));
            if (!sentSub.TargetEntityIds.SequenceEqual(new[] { survivingTarget ? 703 : 700 }))
                failures.Add(label + " opening did not retain its surviving typed anchor: " + string.Join(",", sentSub.TargetEntityIds));
            Check(snapshots[0].Single(e => e.FullEntity.Entity == 702).FullEntity.Name == ""
                && snapshots[0].Single(e => e.FullEntity.Entity == 702).FullEntity.Tags.All(t => t.Name != (int)GameTag.CARDTYPE)
                && snapshots[^1].Count(e => e.HasShowEntity) == 1
                && snapshots[^1].ToList().FindIndex(e => e.HasShowEntity) > snapshots[^1].ToList().FindIndex(e => e.HasSubSpellStart),
                "opening repair preserves hidden snapshot and deferred SHOW");
            // Reproduce the unsafe rewrite on decoded bytes: H is typed only after the opening starts.
            sentSub.TargetEntityIds = new() { 702 };
            Check(NativeAdmission.ValidatePackets(snapshots).Any(f => f.Rule == "R1" && f.Entity == 702
                && f.Detail.Contains("CARDTYPE=0")), "same-packet hidden-target mutation fails at opening");
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS native admission continuations: {cases} hoisted-reveal / retired-anchor cases; immutable sent snapshots");
    }
}
