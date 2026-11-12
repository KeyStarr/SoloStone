using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static BEPoolProof;

internal static class EncircleProof
{
    private static Minion[] Claws(Controller p, IPlayable spell) =>
        p.BoardZone.Where(m => m.Card.Id == "BE_873t" && m.Id > spell.Id).ToArray();

    private static void EnemyOutput(Game g, Controller p, IPlayable spell, int expected)
    {
        var enemy = p.Opponent.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id).ToArray();
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, Declared("BE_265"), Cards.FromId("BE_265").AssetId);
        Core2026Proof.Check(enemy.Length == expected, "opponent summon count " + expected);
        foreach (var m in enemy)
            Core2026Proof.Check(pool.Any(c => c.Id == m.Card.Id) && m.Card.Type == CardType.MINION && m.Card.Cost == 1
                && m.Controller == p.Opponent && m.Zone == p.Opponent.BoardZone && m.ZonePosition == p.Opponent.BoardZone.SlotCount - 1
                && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health
                && Claws(p, spell).All(c => c.Id < m.Id), "random 1-Cost minion for the opponent, after the Shredclaws, at its far right");
    }

    internal static void Run() => StandardSetsProof.Case("BE_265", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_10"));
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); report.ValidateRegistered();
        var requirement = report.Cards.Single().PoolRequirements.Single();
        Core2026Proof.Check(requirement.Class == "DEMONHUNTER" && requirement.Count >= OfflinePoolAdmission.MinimumCandidates, "1-Cost minion pool >= 3");

        var existing = Summon(p, "CS2_182"); var enemyMinion = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        Resume(g, other => PlayEntity(other, other.Player1, other.IdEntityDic[spell.Id]));
        var claws = Claws(p, spell);
        Core2026Proof.Check(claws.Length == 5 && claws.Select(c => c.ZonePosition).SequenceEqual(Enumerable.Range(1, 5))
            && claws.All(c => c.AttackDamage == 3 && c.Health == 1 && c.IsRush && c.Card.IsRace(Race.BEAST) && c.Controller == p),
            "five 3/1 Rush Shredclaws appended right of the existing minion in creation order");
        EnemyOutput(g, p, spell, 1);
        // Rush: minions only on the summon turn.
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == claws[0].Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == enemyMinion.Id) && options.All(t => t.Target is Minion), "Rush: attack minions, not heroes");
        Core2026Proof.Check(existing.ZonePosition == 0, "existing minion keeps its slot");

        // Mixed board with a location: only free physical slots are filled.
        ParkBoard(p); ParkBoard(p.Opponent);
        Location(p); Summon(p, "CS2_182"); Summon(p, "CS2_182");
        spell = Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && Claws(p, spell).Length == 4, "location occupies a slot: four Shredclaws fit");
        EnemyOutput(g, p, spell, 1);

        // Full own board: no Shredclaws, the opponent still gets its minion.
        spell = Play(g, p, id);
        Core2026Proof.Check(Claws(p, spell).Length == 0, "full board summons no Shredclaws");
        EnemyOutput(g, p, spell, 1);

        // Full enemy board: no pick, no RNG.
        ParkBoard(p); ParkBoard(p.Opponent);
        while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
        long draws = g.Random.DrawCount;
        spell = Play(g, p, id);
        Core2026Proof.Check(Claws(p, spell).Length == 5 && g.Random.DrawCount == draws && p.Opponent.BoardZone.All(m => m.Card.Id == "CS2_182"),
            "full enemy board: Shredclaws only, no RNG");

        // Empty and singleton pools.
        ParkBoard(p); ParkBoard(p.Opponent);
        Restricted(g, id, 0, () => {
            var rng = g.Random.CaptureState();
            var s = Play(g, p, id);
            Core2026Proof.Check(Claws(p, s).Length == 5 && p.Opponent.BoardZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty pool: Shredclaws only");
        });
        ParkBoard(p);
        Restricted(g, id, 1, () => { var s = Play(g, p, id); EnemyOutput(g, p, s, 1); });
    });
}
