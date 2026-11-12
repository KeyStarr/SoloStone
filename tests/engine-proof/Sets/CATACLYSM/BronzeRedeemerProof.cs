using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BronzeRedeemerProof
{
    [GroupProof("bronze-redeemer-dead-source")]
    internal static void QueuedDeadSourceSnapshot()
    {
        foreach (bool history in new[] { false, true })
        foreach (int health in new[] { 0, -2 }) {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var source = Summon(p, "CATA_478"); source.Health = health;
            if (health < 0) source.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();
            // An earlier end-turn trigger killed the source after its summon had already queued.
            source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack());
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "CATA_478t")
                && p.GraveyardZone.Any(m => m.Card.Id == "CATA_478t"), "zero-Health queued Bronze Brute dies before it can be attacked");
            Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)),
                "queued dead-source summon keeps valid history snapshot");
        }
        Console.WriteLine("PASS BronzeRedeemerProof.QueuedDeadSourceSnapshot");
    }
    internal static void Run() => StandardSetsProof.Case("CATA_478", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1;
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Redeemer play");
        source.AttackDamage = 7; source.BaseHealth = 9; source.Damage = 4;
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Any(m => m.Card.Id == "CATA_478t" && m.AttackDamage == 7 && m.Health == 5) && !p.BoardZone.Any(m => m.Card.Id == "CATA_478t"), "clone summon isolated");
        End(g); var token = p.BoardZone.Single(m => m.Card.Id == "CATA_478t");
        Core2026Proof.Check(token.AttackDamage == 7 && token.Health == 5 && token.Damage == 0 && token.IsRace(Race.DRAGON) && token.Card.ATK == 1 && token.Card.Health == 1, "pinned 1/1 Dragon initialized to current source stats");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, token.Id, right.Id }), "summon right of actual source including Dormant neighbour");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 4 && token.AttackDamage == 7, "opponent turn does not trigger; token persists");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 4, "Silence removes source trigger");
        left[GameTag.DORMANT] = 0; ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 0, "dead source stops triggering");
        source = (Minion)Play(g, p, id); Play(g, p, "EX1_366"); End(g);
        token = p.BoardZone.Single(m => m.Card.Id == "CATA_478t");
        Core2026Proof.Check(token.AttackDamage == 4 && token.Health == 4 && p.Hero.Weapon.Durability == 4 && source.AttackDamage == 3, "stats initialized before Sword of Justice summon buff");
        End(g); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_478t") == 1, "full board prevents extra token");
        End(g); ClearBoard(g, p); left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); Kill(g, source);
        source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        token = p.BoardZone.Last(); Core2026Proof.Check(token.Card.Id == "CATA_478t" && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, token.Id }), "removed queued source appends token");
    });
}
