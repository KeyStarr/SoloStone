using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KragwatheFrogProof
{
    // STILL-UNPINNED: data/rules-evidence/deck/w18-kragwa-{eligibility,order}.yaml.
    // Tier C Krag'wa Notes; tier D played wording; owner assumes zone-independent plays and random multiplicity-preserving return.
    internal static void Run() => StandardSetsProof.Case("CORE_TRL_345", Unpinned_KragwatheFrog);
    private static void Unpinned_KragwatheFrog(Game g, Controller p, string id)
    {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 6 && source.Card.IsRace(Race.BEAST)
            && p.HandZone.IsEmpty, "4/6 Beast; no previous own turn returns nothing");
        ParkBoard(p);
        Play(g, p, "GAME_005"); // Older own turn must be excluded.
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        string[] history = g.Seed == 2 ? new[] { "CS2_029", "GAME_005", "EX1_289", "GAME_005" }
            : new[] { "GAME_005", "EX1_289", "GAME_005", "CS2_029" };
        IPlayable secret = null;
        foreach (string spell in history)
        {
            var played = Play(g, p, spell, spell == "CS2_029" ? p.Opponent.Hero : null);
            if (spell == "EX1_289") secret = played;
        }
        var indirect = Entity.FromCard(p, Cards.FromId("CS2_024"));
        new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, indirect.Id).CastSpell(indirect.Id, HistoryCastKind.Effect, p.Opponent.Hero.Id);
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        PoolPilotProof.EmptyHand(p);
        var before = g.CloneExact(history: true);
        var rng = g.Random.Clone(); var expected = history.ToList(); expected.Shuffle(rng);
        Core2026Proof.Check(!expected.SequenceEqual(history), "seeded return permutation changes ordered play entries");
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(expected)
            && p.HandZone.Count(c => c.Card.Id == "GAME_005") == 2
            && p.HandZone.All(c => c[GameTag.DISPLAYED_CREATOR] == source.Id && c.Controller == p && c.Cost == c.Card.Cost),
            "seeded return keeps duplicate plays, excludes older/opponent/effect casts, creates fresh base copies");
        Core2026Proof.Check(secret.Zone == p.SecretZone && p.HandZone.Any(c => c.Card.Id == "EX1_289" && c.Id != secret.Id),
            "active Secret remains in place while new hand copy is generated");
        foreach (int slots in new[] { 1, 2 })
        {
            var branch = before.CloneExact(history: true); var owner = branch.Player1;
            var body = Add(owner, id); PoolPilotProof.FillHand(owner, 11 - slots);
            PlayEntity(branch, owner, body);
            Core2026Proof.Check(owner.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == body.Id).Select(c => c.Card.Id)
                .SequenceEqual(expected.Take(slots)) && owner.HandZone.Count == 10, "overflow receives random prefix, not chronological or deduplicated entries");
        }
        PoolPilotProof.Resume(before, next => Play(next, next.Player1, id));
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(before), "history shuffle and generated identities survive clone/snapshot");
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        var returned = g.IdEntityDic[history[0] == "GAME_005" ? p.GraveyardZone.First(c => c.Card.Id == "GAME_005").Id
            : p.GraveyardZone.First(c => c.Card.Id == "CS2_029").Id];
        p.SetasideZone.Add(p.GraveyardZone.Remove(returned)); // Present zone must never determine future eligibility.
        End(g); End(g); PoolPilotProof.EmptyHand(p);
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty, "previous turn with only minion play returns no spell from older turns");
    }
}
