using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using static Core2026Proof;

internal static class TriggerDefinitionVersionProof
{
    [GroupProof("trigger-definition-version")]
    internal static void Run()
    {
        string historical = File.ReadAllText("tests/engine-proof/definition-fixtures/attack-trigger-v0-JAIL_453.b64");
        var old = SnapshotCodec.DecodeDefinition(historical);
        Core2026Proof.Check(old.Id == "JAIL_453" && SnapshotCodec.EncodeDefinition(old) == historical,
            "historical trigger definitions keep exact bytes with no owner predicate");
        string encoded = SnapshotCodec.EncodeDefinition(Cards.FromId("END_026"), detached: true);
        Core2026Proof.Check(Convert.FromBase64String(encoded).Take(3).SequenceEqual(new byte[] { 255, 255, 6 }), "owner predicate requires version 6");
        var restored = SnapshotCodec.DecodeDefinition(encoded);
        Core2026Proof.Check(restored.Power.Trigger.OwnerCondition != null && SnapshotCodec.EncodeDefinition(restored) == encoded,
            "owner predicate definition round trip");
        var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
        var source = Entity.FromCard(p, restored); Generic.SummonBlock(g, (Minion)source, -1, null);
        var target = Summon(p.Opponent, "CS2_182"); int hand = p.HandZone.Count;
        Play(g, p, "CS2_008", target);
        Core2026Proof.Check(p.HandZone.Count == hand + 1, "decoded owner predicate executes matching spell-target effect");
        hand = p.HandZone.Count; Play(g, p, "GAME_005");
        Core2026Proof.Check(p.HandZone.Count == hand, "decoded owner predicate rejects untargeted spell");
        Console.WriteLine("PASS trigger definition version: historical bytes unchanged, v6 owner predicate executable");
    }
}
