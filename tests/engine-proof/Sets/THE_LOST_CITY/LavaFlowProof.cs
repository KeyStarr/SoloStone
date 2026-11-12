using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LavaFlowProof
{
    // STILL-UNPINNED: data/rules-evidence/tie-break/{lowest-health-hero-lava-flow,lowest-health-repeat,lowest-health-ties}.yaml.
    // Tier C Target/Random effect/Advanced rulebook: include hero, ignore Armor, uniform ties; deaths deferred through all three hits.
    internal static void Run() => StandardSetsProof.Case("TLC_227", Unpinned_LavaFlow);
    private static void Unpinned_LavaFlow(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        var friend = Summon(p, "CS2_231");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Hero.Damage == 6 && p.Hero.Damage == 0 && friend.Damage == 0 && p.OverloadOwed == 1,
            "three two-damage hits, enemy hero included, friendly side excluded, Overload one");
        enemy.Hero.Damage = 27; enemy.Hero.Armor = 20;
        var high = Summon(enemy, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(enemy.Hero.Health == 3 && enemy.Hero.Armor == 14 && high.Damage == 0,
            "lowest current Health ignores Armor and keeps selecting hero");
        enemy.Hero.Damage = 0; enemy.Hero.Armor = 0; ParkBoard(enemy);
        var golem = Summon(enemy, "EX1_556"); var other = Summon(enemy, "CS2_182");
        Play(g, p, id);
        var replacement = enemy.BoardZone.Single(m => m.Card.Id == "skele21");
        Core2026Proof.Check(golem.Zone == enemy.GraveyardZone && replacement.Health == 1 && replacement.Damage == 0
            && other.Damage == 2 && enemy.Hero.Damage == 0, "reselect after lethal, skip mortally wounded; Deathrattle token cannot be hit by same cast");
        ParkBoard(enemy); ParkBoard(p);
        Summon(p, "CS2_142"); var shield = Summon(enemy, "EX1_008");
        Play(g, p, id);
        Core2026Proof.Check(shield.Zone == enemy.GraveyardZone && enemy.Hero.Damage == 3,
            "Spell Damage applies to each hit; Divine Shield consumes a hit before lethal and hero fallback");
        ParkBoard(enemy); ParkBoard(p); enemy.Hero.Damage = 0;
        var sleeping = Summon(enemy, "EDR_416t");
        var target = Summon(enemy, "CS2_182");
        Core2026Proof.Check(sleeping.Untouchable, "Dormant fixture");
        Play(g, p, id);
        Core2026Proof.Check(sleeping.Damage == 0 && target.Zone == enemy.GraveyardZone && enemy.Hero.Damage == 0,
            "Dormant excluded even when its Health is lowest");
        ParkBoard(enemy);
        Core2026Proof.Check(Enumerable.Range(1, 3).Any(seed => new Util.DeepCloneableRandom(seed).Next(3) != 0),
            "tie fixtures include a nonfirst seeded selection");
        foreach (bool reversed in new[] { false, true })
        {
            var board = new[] { Summon(enemy, "CS2_182"), Summon(enemy, "CS2_200"), Summon(enemy, "CS2_120") };
            foreach (var minion in board) { minion.BaseHealth = 10; minion.Damage = 0; }
            if (reversed) { foreach (var minion in board) enemy.SetasideZone.Add(enemy.BoardZone.Remove(minion)); foreach (var minion in board.Reverse()) enemy.BoardZone.Add((Minion)enemy.SetasideZone.Remove(minion)); }
            var ordered = enemy.BoardZone.ToArray(); g.Random.SetSeed(g.Seed);
            int selected = new Util.DeepCloneableRandom(g.Seed).Next(3);
            PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
            Core2026Proof.Check(ordered.Select(m => m.Damage).SequenceEqual(Enumerable.Range(0, 3).Select(i => i == selected ? 6 : 0)),
                "seeded uniform tie selection over reordered physical boards, then lowest reselection");
            ParkBoard(enemy);
        }
    }
}
