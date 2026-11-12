using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RenewingFlamesProof
{
    // tie-break/lowest-health-hero-pool.yaml PINNED (C); lowest-health-repeat.yaml and
    // lowest-health-ties.yaml STILL-UNPINNED (C): reselect uniformly; defer deaths until spell ends.
    internal static void Run() => StandardSetsProof.Case("EDR_255", Unpinned_RenewingFlames);
    private static void Unpinned_RenewingFlames(Game g, Controller p, string id)
    {
        p.Hero.Damage = 20;
        var weak = Summon(p.Opponent, "CS2_182"); weak.Health = 4;
        var next = Summon(p.Opponent, "CS2_182"); next.Health = 8;
        Play(g, p, id);
        Core2026Proof.Check(weak.Zone.Type == Zone.GRAVEYARD && next.Damage == 5 && p.Hero.Damage == 10,
            "Unpinned_RenewingFlames: dead first target excluded; second hit reselects; Lifesteal heals both hits");
        ParkBoard(p.Opponent);
        var wounded = Summon(p.Opponent, "CS2_182"); wounded.Health = 20; wounded.Damage = 15;
        var golem = Summon(p.Opponent, "EX1_556");
        p.Opponent.Hero.Damage = 0; Play(g, p, id);
        Core2026Proof.Check(golem.Zone.Type == Zone.GRAVEYARD && wounded.Zone.Type == Zone.GRAVEYARD
            && p.Opponent.BoardZone.Single().Card.Id == "skele21" && p.Opponent.BoardZone.Single().Damage == 0,
            "Unpinned_RenewingFlames: current Health used; Deathrattle replacement waits until both hits finish");
        ParkBoard(p.Opponent);
        var sleeper = Summon(p.Opponent, "EDR_840t1");
        var immune = Summon(p.Opponent, "CS2_182"); immune.Health = 1; immune.IsImmune = true;
        var ordinary = Summon(p.Opponent, "CS2_182");
        int health = p.Hero.Health; Play(g, p, id);
        Core2026Proof.Check(immune.Damage == 0 && ordinary.Damage == 0 && sleeper.Damage == 0 && p.Hero.Health == health,
            "lowest Immune enemy absorbs both attempts without Lifesteal; Dormant excluded");
        ParkBoard(p.Opponent);
        p.Hero.Damage = 20; p.Opponent.Hero.Damage = 25; p.Opponent.Hero.Armor = 20;
        ordinary = Summon(p.Opponent, "CS2_182"); ordinary.Health = 10;
        Summon(p, "CS2_052"); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Health == 5 && p.Opponent.Hero.Armor == 8 && ordinary.Damage == 0 && p.Hero.Damage == 8,
            "hero participates by current Health ignoring Armor; per-hit Spell Damage and Armor Lifesteal");
        ParkBoard(p); ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0; p.Opponent.Hero.Armor = 0;
        foreach (int seed in new[] { 1, 2, 3 })
        foreach (bool reversed in new[] { false, true })
        {
            ParkBoard(p.Opponent);
            var a = Summon(p.Opponent, "CS2_182"); a.Health = 4;
            var b = Summon(p.Opponent, "CS2_182"); b.Health = 4;
            if (reversed) { p.Opponent.BoardZone.Remove(a); p.Opponent.BoardZone.Add(a); }
            // Only first hit is tied; its victim must match seeded selection in physical board order.
            g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            int expected = oracle.Player2.BoardZone[oracle.Random.Next(2)].Id;
            var source = Play(g, p, id);
            var hits = g.HistoryFacts.Query.Damage().Where(f => f.SourceId == source.Id).ToArray();
            Core2026Proof.Check(hits.Length == 2 && hits[0].VictimId == expected && hits[1].VictimId != expected,
                "Unpinned_RenewingFlames: seeded ties honor reordered board, then exclude lethal victim");
        }
        bool nonFirst = false;
        foreach (int seed in new[] { 1, 2, 3 })
        foreach (bool reversed in new[] { false, true })
        {
            ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 16;
            var a = Summon(p.Opponent, "CS2_182"); a.Health = 14;
            var b = Summon(p.Opponent, "CS2_182"); b.Health = 14;
            if (reversed) { p.Opponent.BoardZone.Remove(a); p.Opponent.BoardZone.Add(a); }
            g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            var candidates = oracle.Player2.BoardZone.Select(m => m.Id).Append(p.Opponent.Hero.Id).ToArray();
            int index = oracle.Random.Next(3); nonFirst |= index != 0;
            var source = Play(g, p, id);
            var hits = g.HistoryFacts.Query.Damage().Where(f => f.SourceId == source.Id).ToArray();
            Core2026Proof.Check(hits.Length == 2 && hits.All(f => f.VictimId == candidates[index]),
                "Unpinned_RenewingFlames: hero/minion ties share seeded pool; newly wounded target is selected again");
        }
        Core2026Proof.Check(nonFirst, "tie regression exercises a non-first pick");
    }
}
