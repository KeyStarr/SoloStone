using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MalevolentMutantProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_697", (g, p, id) => {
        foreach (bool hasFel in new[] { false, true })
        foreach (bool brann in new[] { false, true })
        {
            var replay = g.CloneExact(history: true); var owner = replay.Player1;
            if (brann) Summon(owner, "LOE_077");
            if (hasFel) Add(owner, "CORE_BT_801");
            Play(replay, owner, id);
            int repetitions = brann ? 2 : 1;
            for (int i = 0; hasFel && i < repetitions; i++)
            {
                Core2026Proof.Check(owner.Choice != null && owner.HandZone.Count == 1 + i,
                    "Mutant direct Battlecry waits for each separate answer");
                PlayerDecisionProof.Answer(replay, owner.Choice.Choices[0]);
            }
            Core2026Proof.Check(owner.Choice == null && owner.HandZone.Count == (hasFel ? 1 + repetitions : 0),
                "Mutant initial Battlecry completes exactly once per activation");
            Play(replay, owner, "GIL_820");
            Core2026Proof.Check(owner.Choice == null && !replay.Resolution.HasSuspendedEffect &&
                owner.HandZone.Count == (hasFel ? 1 + 2 * repetitions : 0) &&
                owner.HandZone.All(c => c.Card.Id == "CORE_BT_801"),
                "Mutant Shudderwock auto-resolves empty/nonempty hand, including Brann, without reentering queued recipe");
            Core2026Proof.Check(Canonical.State(replay) == Canonical.State(Game.LoadSnapshot(replay.SaveSnapshot())),
                "Mutant replay leaves a settled snapshot boundary");
        }
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "Mutant empty hand no prompt");
        ClearBoard(g, p);
        var nonFel = Add(p, "CS2_029"); var minion = Add(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null, "Mutant no Fel candidate");
        ClearBoard(g, p);
        var chosen = Add(p, "CORE_BT_801");
        new EffectContext(g, p.Id, p.Hero.Id).GiveBuff(chosen.Id, "BE_036e2", 2);
        var originalEnchant = chosen.AppliedEnchantments.Single(e => e.Card.Id == "BE_036e2");
        Core2026Proof.Check(chosen.Cost == 1 && originalEnchant.ScriptTag1 == 2, "Mutant real cost enchant fixture");
        var source = Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { chosen.Id }), "Mutant singleton Fel spell still prompts");
        var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var candidate in new[] { clone, restored, g }) PlayerDecisionProof.Answer(candidate, chosen.Id);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored), "Mutant saved future");
        var copy = p.HandZone.Last();
        var copyEnchant = copy.AppliedEnchantments.Single(e => e.Card.Id == "BE_036e2");
        Core2026Proof.Check(copyEnchant.Id != originalEnchant.Id && !ReferenceEquals(copyEnchant, originalEnchant) &&
            copyEnchant.Target == copy && originalEnchant.Target == chosen && copyEnchant.ScriptTag1 == 2 &&
            originalEnchant.ScriptTag1 == 2 && copy.Cost == 1 && chosen.Cost == 1,
            "Mutant copy retains independent enchant entity, amount and effective cost; original unchanged");
        copyEnchant[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Core2026Proof.Check(copyEnchant.ScriptTag1 == 1 && chosen.Cost == 1 && chosen.AppliedEnchantments.Contains(originalEnchant) &&
            originalEnchant.ScriptTag1 == 2, "Mutant changing copy enchant cannot change original");
        copyEnchant[GameTag.TAG_SCRIPT_DATA_NUM_1] = 2;
        Core2026Proof.Check(copy.Id != chosen.Id && copy.Card.Id == chosen.Card.Id && copy.Cost == chosen.Cost &&
            p.HandZone.Contains(chosen) && nonFel.Card.Id == "CS2_029" && minion.Card.Id == "CS2_182", "Mutant copies selected modified card; original remains");
        ClearBoard(g, p); Summon(p, "LOE_077"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, chosen.Id);
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Contains(p.HandZone.Last().Id), "Mutant Brann fresh offer includes first copy");
        PlayerDecisionProof.Answer(g, p.HandZone.Last().Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count(c => c.Card.Id == "CORE_BT_801") == 4, "Mutant Brann copies twice");
        ClearBoard(g, p);
        // Fill after commitment: ordinary full-hand copy is lost, without replacing an existing card.
        Play(g, p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_171");
        var hand = p.HandZone.Select(c => c.Id).ToArray();
        PlayerDecisionProof.Answer(g, chosen.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Select(c => c.Id).SequenceEqual(hand), "Mutant full hand loses copy");
    });
}
