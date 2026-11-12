using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;

internal static partial class Program
{
    private static void NativeAdmissionControls()
    {
        ControllerAdmissionControls();
        PowerHistoryData Full(int id, string card, int type, int zone = 1, int owner = 1, int secret = 0) => new() {
            FullEntity = new() { Entity = id, Name = card, Tags = new() {
                new() { Name = (int)GameTag.CARDTYPE, Value = type }, new() { Name = (int)GameTag.ZONE, Value = zone },
                new() { Name = (int)GameTag.CONTROLLER, Value = owner }, new() { Name = (int)GameTag.SECRET, Value = secret } } } };
        PowerHistoryData Start(int id, BlockType type = BlockType.POWER) => new() { PowerStart = new() { Source = id, Type = (HistoryBlock.Type)(int)type } };
        PowerHistoryData Tag(int id, GameTag tag, int value) => new() { TagChange = new() { Entity = id, Tag = (int)tag, Value = value } };
        PowerHistoryData Target(int data = 0, int id = 10) => new() { MetaData = new() { Type = (HistoryMeta.Type)0, Data = data, Info = new() { id } } };
        var end = new PowerHistoryData { PowerEnd = new() };
        var prefix = new[] { Full(10, "RLK_708", 5), Full(20, "", 0, 2, 2) };
        int cases = 0;
        void Assert(string name, IEnumerable<PowerHistoryData> body, string rule, int count)
        {
            var faults = NativeAdmission.Validate(prefix.Concat(body).ToArray());
            Check(faults.Count(f => f.Rule == rule) == count, "admission control " + name + ": " + string.Join(";", faults)); cases++;
        }
        Assert("pending SHOW cannot type an impact", new[] { Start(10), Tag(20, GameTag.ZONE, 3), new PowerHistoryData { ShowEntity = Full(20, "later", 4, 3, 2).FullEntity }, end }, "R1", 1);
        Assert("early TARGET prevents inference", new[] { Start(10), Target(), Tag(20, GameTag.ZONE, 3), end }, "R1", 0);
        Assert("Data=source accepted", new[] { Start(10), Target(10), Tag(20, GameTag.ZONE, 3), end }, "R1", 0);
        Assert("another source metadata cannot protect block", new[] { Start(10), Target(30), Tag(20, GameTag.ZONE, 3), end }, "R1", 1);
        Assert("late metadata protects entire segment", new[] { Start(10), Tag(20, GameTag.ZONE, 3), Target(), end }, "R1", 0);
        Assert("hidden metadata target rejected", new[] { Start(10), Target(id: 20), Tag(20, GameTag.ZONE, 3), end }, "R1", 1);
        Assert("missing metadata target cannot bypass fallback", new[] { Start(10), Target(id: 999), Tag(20, GameTag.ZONE, 3), end }, "R1", 2);
        Assert("mixed safe and hidden metadata rejected", new[] { Start(10), Target(), Target(id: 20), end }, "R1", 1);
        var sub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 10, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21" } };
        var subEnd = new PowerHistoryData { SubSpellEnd = new() };
        Assert("parent metadata cannot protect subspell", new[] { Start(10), Target(), sub, Tag(20, GameTag.ZONE_POSITION, 1), subEnd, end }, "R1", 1);
        Assert("subspell hidden metadata target rejected", new[] { Start(10), sub, Target(id: 20), subEnd, end }, "R1", 1);
        Assert("subspell missing metadata target rejected", new[] { Start(10), sub, Target(id: 999), subEnd, end }, "R1", 1);
        Assert("subspell metadata cannot stop controller inference", new[] { Start(10), sub, Target(), Tag(20, GameTag.ZONE_POSITION, 1), subEnd, end }, "R1", 1);
        Assert("post-subspell segment cannot borrow parent metadata", new[] { Start(10), Target(), sub, subEnd, Tag(20, GameTag.ZONE_POSITION, 1), end }, "R1", 1);
        Assert("post-child segment cannot borrow early metadata", new[] { Start(10), Target(), Start(10), end, Tag(20, GameTag.ZONE_POSITION, 1), end }, "R1", 1);
        Assert("typed tasks before metadata cannot infer an untyped target", new[] { Start(10), Tag(10, GameTag.COST, 0), Target(), Tag(20, GameTag.ZONE, 3), end }, "R1", 0);
        Assert("nested metadata belongs to child", new[] { Start(10), Start(10), Target(), end, Tag(20, GameTag.ZONE, 3), end }, "R1", 1);
        Assert("nested tags belong to child", new[] { Start(10), Target(), Start(10, BlockType.TRIGGER), Tag(20, GameTag.ZONE, 3), end, end }, "R1", 1);
        Assert("SETASIDE is excluded", new[] { Tag(20, GameTag.ZONE, 6), Start(10), Tag(20, GameTag.ZONE, 3), end }, "R1", 0);
        Assert("enchantment is excluded", new[] { Full(20, "enchantment", 6), Start(10), Tag(20, GameTag.ZONE, 3), end }, "R1", 0);
        Assert("HIDE retains logical type", new[] { Full(20, "known", 4), new PowerHistoryData { HideEntity = new() { Entity = 20, Zone = 3 } }, Start(10), Tag(20, GameTag.ZONE_POSITION, 1), end }, "R1", 0);
        Assert("hidden PLAY requires class", new[] { Start(20, BlockType.PLAY), end }, "R2", 1);
        Assert("typed anonymous Secret allowed", new[] { Full(20, "", 5, 3, 2, 1), Start(20, BlockType.PLAY), end }, "R2", 0);
        Assert("public move requires identity first", new[] { Tag(20, GameTag.ZONE, 1), new PowerHistoryData { ShowEntity = Full(20, "late", 4).FullEntity }, end }, "R3", 1);
        Assert("public controller transfer requires identity", new[] { Tag(20, GameTag.ZONE, 3), Tag(20, GameTag.CONTROLLER, 1) }, "R3", 1);
        Assert("Jaraxxus cannot borrow parent FULLs", new[] { Full(10, "CORE_EX1_323", 3), Start(10, BlockType.PLAY), Full(30, "power", 10), Full(31, "weapon", 7), Start(10), end, end }, "R4", 1);
        Assert("Jaraxxus owns both FULLs", new[] { Full(10, "CORE_EX1_323", 3), Start(10), Full(30, "power", 10), Full(31, "weapon", 7), end }, "R4", 0);
        Assert("game/player phase blocks have no SuperSpell", new[] { Full(10, "", 2), Start(10, BlockType.TRIGGER), Tag(20, GameTag.ZONE_POSITION, 1), end }, "R1", 0);
        Assert("new hidden FULL exists before impact", new[] { Start(10), Full(30, "", 0, 2, 2), Tag(30, GameTag.ZONE_POSITION, 1), end }, "R1", 1);
        Assert("new typed FULL has realtime logical tags", new[] { Start(10), Full(30, "new", 4, 1, 2), Tag(30, GameTag.ZONE_POSITION, 1), end }, "R1", 0);
        var beforeReceipt = prefix.Concat(new[] { Start(10), Tag(30, GameTag.ZONE_POSITION, 1), end }).ToArray();
        Check(!NativeAdmission.ValidatePackets(new IReadOnlyList<PowerHistoryData>[] { beforeReceipt, new[] { Full(30, "", 0, 2, 2) } })
            .Any(f => f.Rule == "R1"), "future packet FULL cannot become an earlier inferred target"); cases++;
        Check(NativeAdmission.ValidatePackets(new IReadOnlyList<PowerHistoryData>[] {
            prefix.Concat(new[] { Start(10), Target() }).ToArray(), new[] { Tag(20, GameTag.ZONE_POSITION, 1), end } })
            .Count(f => f.Rule == "R1") == 1, "packet continuation cannot borrow previous segment metadata"); cases++;
        var capture = new Capture();
        var sentSub = new PowerHistoryData { SubSpellStart = new() { SourceEntityId = 10, SpellPrefabGuid = "2084d102cd356b04ca792130b749cc21", TargetEntityIds = new() { 30 } } };
        capture.Send(19, new PowerHistory { List = prefix.Concat(new[] { Full(30, "retiring", 4), Start(10), sentSub,
            Tag(30, GameTag.ZONE, (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME) }).ToList() });
        capture.Send(19, new PowerHistory { List = new() { Tag(20, GameTag.ZONE_POSITION, 1), subEnd, end } });
        sentSub.SubSpellStart.TargetEntityIds = new() { 10 };
        Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List))
            .Any(f => f.Rule == "R1" && f.Entity == 30), "rewritten object cannot conceal missing target in bytes already sent"); cases++;
        Assert("unknown FX accepts hidden authored target", new[] { Full(10, "JAIL_407", 4), Start(10), Target(id: 20), end }, "R1", 0);
        Assert("unknown FX still rejects missing authored target", new[] { Full(10, "JAIL_407", 4), Start(10), Target(id: 999), end }, "R1", 1);
        Console.WriteLine($"PASS native admission controls: {cases} positive/negative cases");
    }
}
