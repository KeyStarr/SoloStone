using System.Runtime.Serialization;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using static Core2026Proof;

internal static class B14F1HistoryDefinitionProof
{
    // Detached EDR_461 emitted by the batch-13 codec (77d1080f), retained verbatim from review.
    private const string Batch13Ritual = "/wN5AAAAAZwBAAAAAAAAAAAAAAGcAQAAfL4BAAAAAAABkAEAAAABkAEAAAABkAEAAAABkAEAAAABPAAAAAYAAAAAAAAAAZABAAAAAZABAAAAAZABAAAAAZwBAAAFAAAAAAAAAAGQAQAAAAGQAQAAAAABkAEAAAAE/////6EBAAAAAAAAAZABAAABAZABAAAAAZABAAAAA78BAAABnAEAAAMAAAAAAAAAAcABAAAAAAAAAAAAAAGhAQAACEVEUl80NjF0AZABAAAAAZwBAAAAAAAAAAAAAAGQAQAAAAGhAQAAB0VEUl80NjEBkAEAAAEBkAEAAAABkAEAAAABkAEAAAABkAEAAAABkAEAAAABkAEAAAABoQEAABZSaXR1YWwgb2YgdGhlIE5ldyBNb29uAZABAAAAAZwBAAAAAAAAAAAAAAAEkgEAAE0AAACcAQAAAAAAAAGQAQAAAAMyAAAAAAEwAAAAAAAAAAAAAAAAAAAAAAAHIGxlZ2FjeS10YXNrL0VmZmVjdENvbnRleHRUYXNrL3YxAZABAAAAAYoBAAAAAAAAAAAAAAUBAAAASmNhcmQvRURSXzQ2MS92MS88UG93ZXI+a19fQmFja2luZ0ZpZWxkLzxQb3dlclRhc2s+a19fQmFja2luZ0ZpZWxkL19yZXNvbHZlAAAABJgBAABTAAAAAAAAAAGQAQAAAASSAQAARQAAAJwBAAAAAAAAAZABAAAAAAGcAQAAAAAAAAAAAAABkAEAAAAEkgEAAEUAAACcAQAACAAAAAOUAQAARQAAAJwBAAABRQAAAAIAAAAAAAAAAZwBAAADAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAADAAAAAAAAAAAZwBAAAFAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAALcAAAAAAAAAAZwBAACaBwAAAAAAAAOUAQAARQAAAJwBAAABRQAAAMcAAAAAAAAAAZwBAAAGAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAAMoAAAAAAAAAAZwBAAAFAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAAMsAAAAAAAAAAZwBAAAEAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAAEEBAAAAAAAAAZwBAAABAAAAAAAAAAOUAQAARQAAAJwBAAABRQAAAGMGAAAAAAAAAZwBAAABAAAAAAAAAAAAAVkAAAAAAAAAAAAAAAGQAQAAAAGhAQAAaFt4XVN1bW1vbiB0d28KcmFuZG9tIDMtQ29zdCBtaW5pb25zLgo8aT4oQ2FzdCBAIHw0KHNwZWxsLCBzcGVsbHMpIHRvIHN1bW1vbgo2LUNvc3QgbWluaW9ucyBpbnN0ZWFkLik8L2k+AZABAAAAAT4AAAAFAAAAAAAAAAGQAQAAAAGQAQAAAAFTAAAAAAAAAAAAAAAA";

    internal static void Run()
    {
        foreach (string id in new[] { "CORE_RLK_567", "TIME_876e", "DINO_407e" })
        {
            var original = Cards.FromId(id);
            string encoded = SnapshotCodec.EncodeDefinition(original, detached: true);
            var bytes = Convert.FromBase64String(encoded);
            Core2026Proof.Check(bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 4,
                "held Power fields use version 4: " + id);
            var restored = SnapshotCodec.DecodeDefinition(encoded);
            Core2026Proof.Check((restored.Power.HandEntryTask != null) == (original.Power.HandEntryTask != null)
                && restored.Power.RetainInHeldForm == original.Power.RetainInHeldForm,
                "held Power fields survive detached definition: " + id);
            Core2026Proof.Check(SnapshotCodec.EncodeDefinition(restored) == encoded,
                "held definition round-trip bytes: " + id);
        }
        var ritual = SnapshotCodec.DecodeDefinition(Batch13Ritual);
        Core2026Proof.Check(ritual.Id == "EDR_461" && ritual.AssetId == 114300 && !ReferenceEquals(ritual, Cards.FromId(ritual.Id)),
            "batch-13 detached definition retains identity");
        Core2026Proof.Check(ritual.HealthCostCondition == null && ritual.HandProgress.TargetId == "EDR_461t"
            && ritual.HandProgress.Initial == 3 && ritual.HandProgress.Mode == HandProgressMode.OwnerSpells,
            "batch-13 extended fields decode without a Health-cost predicate");
        Core2026Proof.Check(ritual.Power.HandEntryTask == null && !ritual.Power.RetainInHeldForm,
            "batch-13 definition defaults new held-form fields");
        Core2026Proof.Check(SnapshotCodec.EncodeDefinition(ritual) == Batch13Ritual, "batch-13 extended bytes stay byte-identical");
        var game = HistoryFactsProof.Fixture();
        var spell = Entity.FromCard(game.Player1, ritual);
        game.Player1.HandZone.Add(spell);
        PlayEntity(game, game.Player1, spell);
        Core2026Proof.Check(game.Player1.BoardZone.Count == 2 && game.Player1.BoardZone.All(m => m.Card.Cost == 3),
            "batch-13 decoded spell retains executable summon recipe");
        var knight = SnapshotCodec.DecodeDefinition(SnapshotCodec.EncodeDefinition(Cards.FromId("CORE_ETC_523"), detached: true));
        Core2026Proof.Check(knight.HealthCostCondition != null, "versioned definition retains Health-cost predicate");
        var held = Entity.FromCard(game.Player1, knight);
        Core2026Proof.Check(!knight.HealthCostCondition(held), "decoded Health-cost predicate initially false");
        game.Player1.Hero.TakeDamage(game.Player1.Hero, 2);
        game.Player1.Hero.TakeHeal(game.Player1.Hero, 1);
        Core2026Proof.Check(knight.HealthCostCondition(held), "decoded Health-cost predicate responds to actual healing");
        var unsupported = Convert.FromBase64String(SnapshotCodec.EncodeDefinition(knight));
        unsupported[2] = 99;
        try { SnapshotCodec.DecodeDefinition(Convert.ToBase64String(unsupported)); throw new Exception("unknown definition version accepted"); }
        catch (SerializationException) { }
        Console.WriteLine("PASS B14 F1 definition compatibility: real batch-13 bytes, executable recipe, versioned predicate");
    }
}
