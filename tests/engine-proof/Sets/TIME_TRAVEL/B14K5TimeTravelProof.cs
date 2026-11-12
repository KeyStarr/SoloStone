using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14K5TimeTravelProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_217", Stormrook);

    // Tier D: pinned 37.0.0.254477 export, TIME_217. Same replacement boundary as Flux Revenant.
    private static void Stormrook(Game g, Controller p, string id)
    {
        var filter = PoolPilotProof.Filter(id);
        var candidates = OfflineCardPools.Candidates(g, CardClass.SHAMAN, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.Cost == 5 && c.Id != id),
            "declared any-class exact-five pool has three admitted Shaman candidates and excludes source");
        Console.WriteLine($"Stormrook pool: SHAMAN={candidates.Count}; any-class exact-Cost=5 minions");
        foreach (bool reverse in new[] { false, true })
        {
            var next = g.CloneExact(history: true); var owner = next.Player1;
            var location = LocationProofs.Location(owner);
            var left = Summon(owner, "CS2_182");
            var rook = StandardSetsProof.PlayAt(next, owner, id, 2);
            var right = Summon(owner, "CS2_200");
            if (reverse) { owner.BoardZone.Remove(left); owner.BoardZone.Add(left); }
            rook.Damage = 4;
            var before = owner.BoardZone.Slots.Select(m => m.Id).ToArray();
            int position = rook.ZonePosition + 1;
            // Oracle consumes precisely one pool choice from the same RNG state, independent of board order.
            var oracle = next.CloneExact();
            var eligible = OfflineCardPools.Candidates(oracle, owner.HeroClass, filter, Cards.FromId(id).AssetId);
            string expected = eligible[oracle.Random.Next(eligible.Count)].Id;
            long draws = next.Random.DrawCount;
            foreach (var fork in B14K5ShamanHunterProof.Forks(next))
            {
                var source = (Minion)fork.IdEntityDic[rook.Id];
                Play(fork, fork.Player1, "EX1_238", source);
                var generated = fork.Player1.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
                Core2026Proof.Check(source.Damage == 4 && source.Zone == fork.Player1.BoardZone && generated.Card.Id == expected
                    && generated.Card.AssetId == Cards.FromId(expected).AssetId
                    && generated.Card.Type == CardType.MINION && generated.Card.Cost == 5
                    && generated.AttackDamage == generated.Card.ATK && generated.Health == generated.Card.Health,
                    "lethal friendly Nature damage is replaced with seeded exact-identity five-Cost summon");
                Core2026Proof.Check(fork.Player1.BoardZone.Slots.Select(m => m.Id).SequenceEqual(before.Take(position)
                    .Append(generated.Id).Concat(before.Skip(position))) && fork.Random.DrawCount == draws + 1,
                    "one RNG draw; summon right of living source using physical slots, including reordered board");
            }
        }

        var body = (Minion)Play(g, p, id);
        Play(g, p, "CS2_024", body);
        Core2026Proof.Check(body.Damage == 3 && p.BoardZone.Count == 1, "non-Nature spell deals normal damage without summon");
        body.Damage = 0;
        End(g); Play(g, p.Opponent, "EX1_238", body);
        Core2026Proof.Check(body.Damage == 3 && p.BoardZone.Count == 1, "opponent Nature spell does not trigger replacement");
        End(g); body.Damage = 0;
        body.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Damage == 1 && p.BoardZone.Count == 1, "non-spell damage is not replaced");
        var nature = Entity.FromCard(p, Cards.FromId("EX1_238"));
        long beforeDraws = g.Random.DrawCount;
        body.TakeDamage(nature, 0); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Damage == 1 && p.BoardZone.Count == 1 && g.Random.DrawCount == beforeDraws,
            "zero damage neither summons nor consumes RNG");
        body.IsImmune = true;
        Play(g, p, "EX1_238", body);
        Core2026Proof.Check(body.Damage == 1 && p.BoardZone.Count == 1 && g.Random.DrawCount == beforeDraws,
            "Immune prevents damage before replacement");
        body.IsImmune = false; body.HasDivineShield = true;
        Play(g, p, "EX1_238", body);
        Core2026Proof.Check(!body.HasDivineShield && body.Damage == 1 && p.BoardZone.Count == 1 && g.Random.DrawCount == beforeDraws,
            "Divine Shield prevents damage before replacement");
        body.Damage = 0;
        End(g); End(g);
        Play(g, p, "CORE_EX1_238", body);
        Core2026Proof.Check(body.Damage == 0 && p.BoardZone.Count == 2, "replacement persists across turns and accepts current Nature printing");

        StandardSetsProof.ParkBoard(p);
        body = (Minion)Play(g, p, id);
        PoolPilotProof.Pool(g, id, 0, () => {
            long draws = g.Random.DrawCount;
            Play(g, p, "EX1_238", body);
            Core2026Proof.Check(body.Damage == 0 && p.BoardZone.Count == 1 && g.Random.DrawCount == draws,
                "empty exact-five pool still replaces damage, creates nothing, consumes no RNG");
        });
        LocationProofs.Location(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        beforeDraws = g.Random.DrawCount;
        Play(g, p, "EX1_238", body);
        Core2026Proof.Check(body.Damage == 0 && p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6
            && g.Random.DrawCount == beforeDraws + 1, "full physical board samples but cannot summon; damage remains replaced");
        StandardSetsProof.ParkBoard(p);
        body = (Minion)Play(g, p, id); body.Silence();
        beforeDraws = g.Random.DrawCount;
        Play(g, p, "EX1_238", body);
        Core2026Proof.Check(body.Damage == 3 && p.BoardZone.Count == 1 && g.Random.DrawCount == beforeDraws,
            "Silence removes replacement and RNG consumption");
        StandardSetsProof.ParkBoard(p);
        body = (Minion)Play(g, p, id);
        var ordinary = Summon(p, "CS2_200");
        Play(g, p, "EX1_238", ordinary);
        Core2026Proof.Check(ordinary.Damage == 3 && body.Damage == 0 && p.BoardZone.Count == 2,
            "friendly Nature damage to another minion does not trigger Stormrook");
        p.HandZone.Add(p.BoardZone.Remove(body));
        ordinary.Damage = 0;
        Play(g, p, "EX1_238", ordinary);
        Core2026Proof.Check(ordinary.Damage == 3 && p.BoardZone.Count == 1, "departed source has no trigger on another minion");
        body.TakeDamage(nature, 3);
        Core2026Proof.Check(body.Damage == 0 && p.BoardZone.Count == 1, "offboard source cannot replace damage or summon");
    }
}
