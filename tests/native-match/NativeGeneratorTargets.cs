using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void NativeGeneratorTargetControls()
    {
        int cases = 0;
        var failures = new List<string>();
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot, "showcase-demon-hunter-outcast");
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            var vanessa = EngineEntity.FromCard(owner, Cards.FromId("JAIL_407"), zone: owner.HandZone);
            vanessa[GameTag.COST] = 0;
            var played = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            FlushFieldHistory(match);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == vanessa.Id));
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var before = owner.HandZone.Select(c => c.Id).ToHashSet();
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == played.Id));
            FlushFieldHistory(match);
            var generated = owner.HandZone.Where(c => !before.Contains(c.Id)).ToArray();
            Check(generated.Length == 1, "Vanessa generated one hand card on seat " + owner.PlayerId);
            var packets = capture.HistorySnapshots().ToArray();
            var history = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            int trigger = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == vanessa.Id
                && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
            Check(trigger >= 0, "real Vanessa TRIGGER emitted");
            var tasks = history.Skip(trigger + 1).TakeWhile(e => !e.HasPowerEnd).ToArray();
            if (tasks.Any(e => e.HasMetaData && (int)e.MetaData.Type == 0))
                failures.Add("Vanessa seat " + owner.PlayerId + " received invented TARGET");
            if (tasks.Any(e => e.HasMetaData && (int)e.MetaData.Type == 0 && e.MetaData.Info.Contains(vanessa.Id)))
                failures.Add("Vanessa seat " + owner.PlayerId + " targeted its source");
            var snapshots = history.Where(e => e.HasFullEntity || e.HasShowEntity)
                .Select(e => e.FullEntity ?? e.ShowEntity).Where(e => e.Entity == generated[0].Id).ToArray();
            Check(snapshots.Length > 0 && (bot
                ? snapshots.All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => t.Name != (int)GameTag.CARDTYPE))
                : snapshots.Any(e => e.Name == generated[0].Card.Id)), "generator preserves each seat's hand privacy");
            failures.AddRange(NativeAdmission.ValidatePackets(packets.Select(p => (IReadOnlyList<PowerHistoryData>)p.List))
                .Select(f => "Vanessa: " + f));
            cases++;
        }

        // Inspect additions to the actual normalizer input, so authored recipe TARGETs are not mistaken for insertions.
        foreach (string card in new[] { "JAIL_407", "CS2_023", "CORE_BT_035", "CORE_BT_491", "CORE_BT_351", "RLK_708" })
        foreach (BlockType kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        foreach (bool typedCandidate in new[] { false, true })
        foreach (string prefab in new[] { "", "unknown-spawn-to-hand", "4694fc2ee619cd54c9352938dbf84940", "2084d102cd356b04ca792130b749cc21" })
        {
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            void Send(PowerHistory packet) => typeof(NativeMatch).GetMethod("SendPowerHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { packet });
            PowerHistoryData Full(int id, string name, int type) => new() { FullEntity = new() { Entity = id, Name = name,
                Tags = new() { new() { Name = (int)GameTag.CARDTYPE, Value = type },
                    new() { Name = (int)GameTag.CONTROLLER, Value = 2 }, new() { Name = (int)GameTag.ZONE, Value = 3 } } } };
            Send(new() { List = new() { Full(71, card, 4), Full(72, "", 0), Full(73, "HERO_10", 3) } });
            var start = new PowerHistoryStart { Source = 71, Type = (HistoryBlock.Type)(int)kind, EffectIndex = 0 };
            var sub = prefab.Length == 0 ? null : new PowerHistorySubSpellStart { SourceEntityId = 71, SpellPrefabGuid = prefab };
            var packet = new PowerHistory { List = new() { new() { PowerStart = start } } };
            if (sub != null) packet.List.Add(new() { SubSpellStart = sub });
            packet.List.Add(new() { TagChange = new() { Entity = 72, Tag = (int)GameTag.ZONE_POSITION, Value = 1 } });
            if (typedCandidate) packet.List.Add(new() { TagChange = new() { Entity = 73, Tag = (int)GameTag.ATK, Value = 2 } });
            if (sub != null) packet.List.Add(new() { SubSpellEnd = new() });
            packet.List.Add(new() { PowerEnd = new() });
            bool risky = NativeAdmission.RequiresTypedImpact(start, card, sub);
            var original = packet.List.ToArray();
            // Negative R1 control: only unknown, untyped-only inference is allowed.
            Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List)
                .Append(original)).Any(f => f.Rule == "R1") == (risky || typedCandidate), "R1 distinguishes inferred-target risk");
            Send(packet);
            int[] targets = sub != null ? sub.TargetEntityIds.ToArray()
                : packet.List.Where(e => !original.Any(old => ReferenceEquals(old, e)) && e.HasMetaData && (int)e.MetaData.Type == 0)
                    .SelectMany(e => e.MetaData.Info).ToArray();
            string label = card + "/" + kind + "/" + prefab + "/typed=" + typedCandidate;
            if (targets.Contains(start.Source) && !risky) failures.Add("inserted TARGET names non-risky block source: " + label);
            int[] expected = typedCandidate ? new[] { 73 } : risky ? new[] { 71 } : Array.Empty<int>();
            if (!targets.SequenceEqual(expected)) failures.Add("wrong inserted targets: " + label);
            Check(!NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List))
                .Any(f => f.Rule == "R1"), "repaired or native-inference segment admitted: " + label);
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS generator targeting: {cases} cases; real Vanessa both seats, source-target guard, risky/unknown FX and typed candidates");
    }
}
