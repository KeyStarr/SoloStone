using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14K5LocationsProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_810", Unpinned_SilvermoonPreventionSpill);

    // STILL-UNPINNED (owner-approved): data/rules-evidence/locations/loc-gnomeregan-advance.yaml
    // and data/rules-evidence/locations/silvermoon-prevention-spill.yaml.
    // Tier C/E prevention analogies; no Silvermoon retail trace. Spill uses pre-hit Health, not damage dealt.
    private static void Unpinned_SilvermoonPreventionSpill(Game g, Controller p, string id)
    {
        foreach (string stage in new[] { id, "TIME_810t1", "TIME_810t2" })
        foreach (bool reverse in new[] { false, true })
        foreach (int protection in new[] { 0, 1, 2 })
        {
            var next = g.CloneExact(history: true); var owner = next.Player1;
            var left = Summon(owner, "CS2_182");
            var location = LocationProofs.Location(owner, stage); var right = Summon(owner, "CS2_182");
            var a = Summon(owner.Opponent, "CS2_182"); var b = Summon(owner.Opponent, "CS2_200");
            a.Damage = a.BaseHealth - 3; b.Damage = b.BaseHealth - 3;
            if (reverse) { owner.Opponent.BoardZone.Remove(a); owner.Opponent.BoardZone.Add(a); }
            foreach (var minion in new[] { a, b })
            { minion.HasDivineShield = protection == 1; minion.IsImmune = protection == 2; }
            owner.Opponent.Hero.Armor = 1;
            Summon(owner, "EX1_012"); // Spell Damage never scales location damage.
            var oracle = next.CloneExact();
            var candidates = owner.Opponent.BoardZone.ToArray();
            var expected = candidates[oracle.Random.Next(candidates.Length)];
            foreach (var fork in B14K5ShamanHunterProof.Forks(next))
            {
                var source = (BoardObject)fork.IdEntityDic[location.Id];
                LocationProofs.Use(source);
                var target = (Minion)fork.IdEntityDic[expected.Id];
                var other = (Minion)fork.IdEntityDic[candidates.Single(m => m.Id != expected.Id).Id];
                Core2026Proof.Check((protection == 0 ? target.Zone == fork.Player2.GraveyardZone : target.Health == 3)
                    && (protection != 1 || !target.HasDivineShield) && other.Health == 3
                    && (protection != 1 || other.HasDivineShield), "seeded target follows physical ordering; prevention applies normally");
                Core2026Proof.Check(fork.Player2.Hero.Damage == (stage == id ? 0 : 1) && fork.Player2.Hero.Armor == (stage == id ? 1 : 0),
                    "allocated two excess damage goes through hero Armor regardless of prevention");
                Core2026Proof.Check(source.Durability == 2 && source.IsExhausted && source.ZonePosition == 1
                    && fork.Player1.BoardZone.GetSlot(0).Id == left.Id && fork.Player1.BoardZone.GetSlot(2).Id == right.Id
                    && source.Card.Id == (stage == id ? "TIME_810t1" : "TIME_810t2"), "stage advances without recharging or moving");
            }
        }
        var empty = LocationProofs.Location(p, id);
        LocationProofs.Use(empty);
        Core2026Proof.Check(empty.Card.Id == "TIME_810t1" && empty.Durability == 2 && p.Opponent.Hero.Damage == 0,
            "no enemy consumes use and advances with no damage or spill");
        End(g); End(g); End(g); End(g); LocationProofs.Use(empty);
        Core2026Proof.Check(empty.Card.Id == "TIME_810t2" && empty.Durability == 1, "second charge carries spent durability");
        End(g); End(g); End(g); End(g); LocationProofs.Use(empty);
        Core2026Proof.Check(empty.Zone == p.GraveyardZone, "third total charge removes location");

        var high = Summon(p.Opponent, "CS2_200"); var low = Summon(p.Opponent, "CS2_182"); low.Damage = low.BaseHealth - 1;
        var final = LocationProofs.Location(p, "TIME_810t2"); LocationProofs.Use(final);
        Core2026Proof.Check(low.Zone == p.Opponent.GraveyardZone && high.Damage == 0 && p.Opponent.Hero.Damage == 4,
            "Future targets lowest current Health and spills exactly four");
        while (!p.BoardZone.IsFull) LocationProofs.Location(p);
        End(g); End(g); End(g); End(g);
        LocationProofs.Use(final);
        Core2026Proof.Check(high.Damage == 5 && p.BoardZone.SlotCount == 7, "full friendly physical board does not block activation");

        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var dormant = Summon(p.Opponent, "CORE_BT_156");
        Core2026Proof.Check(dormant.IsDormant && dormant.Untouchable, "Dormant/untouchable exclusion fixture");
        var excluded = LocationProofs.Location(p, id);
        long draws = g.Random.DrawCount; int damage = p.Opponent.Hero.Damage;
        LocationProofs.Use(excluded);
        Core2026Proof.Check(dormant.Damage == 0 && p.Opponent.Hero.Damage == damage && g.Random.DrawCount == draws
            && excluded.Durability == 2 && excluded.Card.Id == "TIME_810t1", "Dormant/untouchable enemy is excluded without RNG, damage or spill");
    }
}
