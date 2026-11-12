using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14K5LostCityProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_223", VolcanicThrasher);

    // PINNED-RULE-ONLY: data/rules-evidence/fabled-spelldamage/spell-damage-copy.yaml
    // PINNED-RULE-ONLY: data/rules-evidence/fabled-spelldamage/spell-damage-stacking.yaml
    // PINNED: data/rules-evidence/fabled-spelldamage/spell-damage-healing.yaml. Tier A hand-copy rule;
    // tier C/E additive damage; ordinary healing never reads spell-local Spell Damage.
    private static void VolcanicThrasher(Game g, Controller p, string id)
    {
        foreach (bool kindred in new[] { false, true })
        foreach (bool reverse in new[] { false, true })
        {
            var next = g.CloneExact(history: true); var owner = next.Player1;
            if (kindred) { Play(next, owner, "GIL_681"); End(next); End(next); }
            foreach (var c in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(c));
            var first = Entity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var second = Entity.FromCard(owner, Cards.FromId("CORE_CS2_029"), zone: owner.DeckZone);
            var duplicate = Entity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var frost = Entity.FromCard(owner, Cards.FromId("CS2_024"), zone: owner.DeckZone);
            if (reverse) { owner.DeckZone.Remove(first); owner.DeckZone.Add(first); }
            var candidates = owner.DeckZone.Where(c => c.Card.Id is "CS2_029" or "CORE_CS2_029").ToArray();
            var oracle = next.CloneExact(); int expected = candidates[oracle.Random.Next(candidates.Length)].Id;
            var source = (Minion)Play(next, owner, id);
            var drawn = owner.HandZone.Single(c => c.Id == expected);
            Core2026Proof.Check(frost.Zone == owner.DeckZone && owner.DeckZone.Count == 3 && drawn.Card.Type == CardType.SPELL
                && drawn[GameTag.SPELLPOWER] == (kindred ? 2 : 0) && owner.CurrentSpellPower == 0,
                "seeded Fire-only physical draw preserves duplicate weighting, reordered deck and conditional local +2");
            Core2026Proof.Check((drawn.AppliedEnchantments?.Count(e => e.Card.Id == "TLC_223e") ?? 0) == (kindred ? 1 : 0), "exact Volcanic enchantment");
            if (kindred)
            {
                var copy = Generic.Copy(owner.Opponent, source, drawn, Zone.HAND);
                var backward = Generic.Copy(owner, source, drawn, Zone.DECK);
                Core2026Proof.Check(copy[GameTag.SPELLPOWER] == 2 && copy.AppliedEnchantments.Single().Card.Id == "TLC_223e"
                    && copy.AppliedEnchantments.Single().Id != drawn.AppliedEnchantments.Single().Id
                    && backward[GameTag.SPELLPOWER] == 0, "Volcanic hand copy retains independent +2; backward copy drops grant");
            }
            source.Silence(); Kill(next, source);
            foreach (var fork in B14K5ShamanHunterProof.Forks(next))
            {
                var spell = fork.IdEntityDic[drawn.Id];
                End(fork); End(fork);
                if (kindred) Summon(fork.Player1, "EX1_012");
                int before = fork.Player2.Hero.Damage;
                PlayEntity(fork, fork.Player1, spell, fork.Player2.Hero);
                Core2026Proof.Check(fork.Player2.Hero.Damage == before + (kindred ? 9 : 6),
                    "drawn bonus stacks with controller Spell Damage and survives source removal, turn end and saved futures");
            }
        }
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Fatigue == 0 && p.HandZone.Count == 0, "no Fire match neither draws nor fatigues");
        Play(g, p, "GIL_681"); End(g); End(g);
        var burn = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var sourceFull = Add(p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        // A draw reaction fills the slot vacated by playing Thrasher only in an externally filled task fixture.
        p[GameTag.MAXHANDSIZE] = 9;
        PlayEntity(g, p, sourceFull);
        Core2026Proof.Check(burn.Zone == p.GraveyardZone && burn[GameTag.SPELLPOWER] == 0 && p.HandZone.Count == 9,
            "full hand burns drawn spell without buffing unrelated cards");
    }
}
