using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CloudSerpentProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_888", (g, p, id) => {
        var eligible = (Minion)Add(p, id);
        new AddEnchantmentTask("TLC_110e", EntityType.TARGET).Process(g, p, eligible, eligible);
        eligible.Cost = 1;
        var unrelated = Add(p, "CS2_182");
        var source = Add(p, id);
        var clone = g.Clone();
        PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.HandZone.Count == 3 && p.HandZone.Count == 3
            && source.Zone == p.HandZone, "clone hand copy isolated");
        PlayEntity(g, p, source);
        var copy = (Minion)p.HandZone.Single(c => c.Id != eligible.Id && c.Id != unrelated.Id);
        Core2026Proof.Check(source.Card.IsRace(Race.ELEMENTAL) && source.Card.IsRace(Race.DRAGON)
            && copy.Card.Id == id && copy.Id != eligible.Id && copy.AttackDamage == 5 && copy.Health == 5
            && copy.Cost == 1, "dual-type candidate copied once with hand buffs and cost");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Summon(p, id); Add(p.Opponent, id);
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "no copy from board, opponent hand, or played source");
        Add(p, "CS2_182"); Add(p, "CS2_029"); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 2, "untyped minions and spells excluded");
        ClearBoard(g, p);
        foreach (var candidateId in new[] { "CORE_EX1_043", "EX1_250" })
        {
            var single = g.CloneExact(history: true); var owner = single.Player1;
            var candidate = Add(owner, candidateId);
            Core2026Proof.Check(candidate.Card.IsRace(Race.DRAGON) != candidate.Card.IsRace(Race.ELEMENTAL),
                "single-race eligibility fixture " + candidateId);
            Play(single, owner, id);
            Core2026Proof.Check(owner.HandZone.Count == 4 && owner.HandZone.Last().Card.Id == candidateId
                && owner.HandZone.Last().Id != candidate.Id && candidate.Zone == owner.HandZone,
                "copies sole eligible " + candidateId);
        }
        var dragon = Add(p, "CORE_EX1_043");
        var elemental = Add(p, "EX1_250");
        source = Add(p, id);
        g.SetRandomSeed(g.Seed);
        var before = g.Random.CaptureState();
        var exact = g.CloneExact(history: true);
        PlayEntity(g, p, source);
        // First Next(2) at seeds 1, 2, 3 selects indices 0, 0, 1.
        var expectedId = g.Seed == 3 ? elemental.Card.Id : dragon.Card.Id;
        Core2026Proof.Check(p.HandZone.Count == 5 && p.HandZone.Last().Card.Id == expectedId,
            "seeded choice selects expected eligible card");
        var after = g.Random.CaptureState();
        Core2026Proof.Check(after.DrawCount == before.DrawCount + 1
            && (after.State0 != before.State0 || after.State1 != before.State1),
            "random copy advances game RNG exactly once");
        PlayEntity(exact, exact.Player1, (IPlayable)exact.IdEntityDic[source.Id]);
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(exact), "exact clone repeats seeded copy");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        ((Minion)source).Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10, "full hand receives no copy");
        p.HandZone.Remove(unrelated = p.HandZone.Last()); p.SetasideZone.Add(unrelated);
        source = Add(p, id); ClearBoard(g, p); PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Count == 10, "playing from full hand leaves exactly one copy slot");
    });
}
