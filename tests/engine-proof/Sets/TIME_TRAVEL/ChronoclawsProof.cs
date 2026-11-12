using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ChronoclawsProof
{
    // PINNED-RULE-ONLY: NOTES/rules-evidence/highest-cost-in-hand-tiebreak.yaml
    // Tier C Discard/Clumsy Courier notes + tier E outof.games: effective Cost, random ties.
    internal static void Run()
    {
        var discarded = new HashSet<string>();
        int scenarios = 0;
        StandardSetsProof.Case("END_016", (g, p, id) => {
            var weapon = (Weapon)Play(g, p, id);
            Core2026Proof.Check(weapon.AttackDamage == 4 && weapon.Durability == 3, "4/3 weapon");
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.HandZone.Count == 0,
                "empty hand attack completes without a discard");
            End(g); End(g); PoolPilotProof.EmptyHand(p);
            var first = Add(p, "CS2_182"); var second = Add(p, "CS2_029");
            var discounted = (Playable)Add(p, "CS2_186");
            CostRules.AddModifier(discounted, EffectOperator.SUB, 4);
            var low = Add(p, "CS2_168"); var enemy = Add(p.Opponent, "CS2_186");
            Core2026Proof.Check(first.Cost == 4 && second.Cost == 4 && discounted.Card.Cost == 7 && discounted.Cost == 3,
                "tied effective maximum beats discounted printed maximum");
            PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "resumed hero attack"));
            var gone = new[] { first, second }.Where(c => c.Zone.Type == Zone.GRAVEYARD).ToArray();
            Core2026Proof.Check(gone.Length == 1 && discounted.Zone == p.HandZone && low.Zone == p.HandZone && enemy.Zone == p.Opponent.HandZone,
                "exactly one maximum discarded; lower Cost and opponent cards untouched");
            discarded.Add(gone.Single().Card.Id);
            if (++scenarios == 3)
                Core2026Proof.Check(discarded.SetEquals(new[] { "CS2_182", "CS2_029" }), "seeds 1-3 select both tied cards");
            int held = p.HandZone.Count;
            Attack(g, Summon(p, "CS2_182"), p.Opponent.Hero);
            Core2026Proof.Check(p.HandZone.Count == held, "friendly minion attack does not discard");
            End(g); Play(g, p.Opponent, "CS2_082");
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && p.HandZone.Count == held,
                "opponent hero attack does not discard");
            End(g); PoolPilotProof.EmptyHand(p);
            var highest = Add(p, "CS2_186"); var smaller = Add(p, "CS2_168");
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "last durability attack");
            Core2026Proof.Check(highest.Zone == p.GraveyardZone && smaller.Zone == p.HandZone && weapon.Zone == p.GraveyardZone,
                "last durability still discards unique maximum");
            Play(g, p, id); Play(g, p, "CS2_082"); End(g); End(g);
            held = p.HandZone.Count;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.HandZone.Count == held,
                "replaced weapon loses its trigger");
        });
    }
}
