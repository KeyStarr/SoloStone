using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using L = LocationProofs;

internal static class SpireOfSolitudeProof
{
    // forced-attack/forced-budget,forced-readiness.yaml PINNED-RULE-ONLY (C rulebook).
    // forced-zero-cant-attack,spire-immune-enchant,spire-no-enemy.yaml STILL-UNPINNED (D/C; owner assumptions):
    // normal retaliation, no JAIL_511e grant, no enemy still summons, no manual attack-budget consumption.
    internal static void Run() => StandardSetsProof.Case("JAIL_511", Unpinned_SpireOfSolitude);

    private static void Unpinned_SpireOfSolitude(Game fixture, Controller owner, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var left = Summon(p, "CS2_182"); var spire = L.Play(p, id); var right = Summon(p, "CS2_182");
            for (int i = 0; i < 4; i++) Add(p, "CS2_182");
            var enemies = (reverse ? new[] { "CS2_200", "CS2_182", "CS2_179" } : new[] { "CS2_179", "CS2_182", "CS2_200" })
                .Select(card => Summon(p.Opponent, card)).ToArray();
            foreach (var enemy in enemies) { enemy.AttackDamage = 1; enemy[GameTag.HEALTH] = 20; }
            enemies[0].HasStealth = true;
            var expected = enemies[g.Random.Clone().Next(enemies.Length)];
            PoolPilotProof.Resume(g, branch => {
                L.Use((BoardObject)branch.IdEntityDic[spire.Id]);
                var demon = branch.Player1.BoardZone.Single(m => m.Card.Id == "JAIL_511t");
                Core2026Proof.Check(demon.AttackDamage == 4 && demon.BaseHealth == 4 && demon.Damage == 1 && demon.IsRace(Race.DEMON)
                    && !demon.IsImmune && !(demon.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_511e") ?? false)
                    && demon.NumAttacksThisTurn == 0 && demon.IsExhausted,
                    "exact hand-size Demon takes retaliation, no immunity, manual budget/readiness preserved");
                Core2026Proof.Check(branch.Player1.BoardZone.Slots.Select(e => e.Id)
                    .SequenceEqual(new[] { left.Id, spire.Id, demon.Id, right.Id }), "Demon beside living location in physical slot");
                Core2026Proof.Check(enemies.All(e => branch.IdEntityDic[e.Id][GameTag.DAMAGE] == (e.Id == expected.Id ? 4 : 0)),
                    "seeded random enemy minion, reordered board, random attack includes Stealth");
            });
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var spire = L.Play(p, id); Add(p, "CS2_182"); var rng = g.Random.DrawCount;
            L.Use(spire);
            Core2026Proof.Check(p.BoardZone.Single().Card.Id == "JAIL_511t" && spire.Durability == 1
                && p.Opponent.Hero.Damage == 0 && g.Random.DrawCount == rng, "no enemy minion: summon, consume use, no attack or RNG");
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            for (int i = 0; i < 4; i++) End(g);
            L.Use(spire);
            Core2026Proof.Check(spire.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 7
                && p.BoardZone.Count(m => m.Card.Id == "JAIL_511t") == 2,
                "final charge clears its physical slot before full-board summon");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var spire = L.Play(p, id);
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            Core2026Proof.Check(!LocationRules.CanActivate(p, spire, null), "non-final full-board activation illegal");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var spire = L.Play(p, id); var enemy = Summon(p.Opponent, "CS2_200"); var rng = g.Random.DrawCount;
            L.Use(spire);
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.GraveyardZone.Any(c => c.Card.Id == "JAIL_511t")
                && enemy.Damage == 0 && g.Random.DrawCount == rng && spire.Durability == 1,
                "empty hand makes a mortal zero-Health Demon; no forced attack or RNG after death");
        }
    }
}
