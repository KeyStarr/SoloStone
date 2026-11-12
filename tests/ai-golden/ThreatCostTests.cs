using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static ReplyTests;

public static class ThreatCostTests
{
    public static void TestTauntAllocationMatchesExhaustiveTargets()
    {
        var random = new Random(20261031);
        for (int trial = 0; trial < 1000; trial++) {
            var game = Fixture();
            int bodies = random.Next(1, 4);
            for (int i = 0; i < bodies; i++) {
                var c = Board(game, false, "CS2_182", random.Next(4) != 0);
                c.AttackDamage = random.Next(1, 10); c.CantAttackHeroes = random.Next(3) == 0;
                c.HasWindfury = i == 0 && random.Next(2) == 0;
                if (bodies == 1 && random.Next(2) == 0) c[GameTag.MEGA_WINDFURY] = 1;
                c.IsFrozen = random.Next(10) == 0; c.NumAttacksThisTurn = random.Next(3);
                c.CantAttack = random.Next(12) == 0;
                c.IsRush = c.AttackableByRush = random.Next(4) == 0;
            }
            for (int i = 0, n = random.Next(1, 4); i < n; i++) {
                var c = Board(game, true, "CS2_182"); c.HasTaunt = true;
                c.Health = random.Next(1, 10); c.HasDivineShield = random.Next(2) == 0;
                c.HasStealth = random.Next(12) == 0; c.IsImmune = random.Next(12) == 0;
                if (random.Next(12) == 0) c[GameTag.DORMANT] = 1;
            }
            if (random.Next(2) == 0) {
                var weapon = (Weapon)Entity.FromCard(game.Player2, Cards.FromId("CS2_106"));
                game.Player2.Hero.AddWeapon(weapon); weapon.AttackDamage = random.Next(1, 6);
                weapon.Durability = random.Next(1, 3); game.Player2.Hero.HasWindfury = random.Next(2) == 0;
                game.Player2.Hero.IsExhausted = false; game.Player2.Hero.IsFrozen = random.Next(6) == 0;
            }
            var own = new FairPlayer(game.Player1, true); var enemy = new FairPlayer(game.Player2);
            int current = random.Next(2) == 0 ? own.PlayerId : enemy.PlayerId;
            int expected = Exhaustive(own, enemy, current);
            long actual = DeckPlanEvaluator.PublicFaceDamage(own, enemy, current);
            Check(actual == expected, "all-target exhaustive allocation trial " + trial + ": " + actual + " != " + expected);
            Check(actual == PreviousDamage(own, enemy, current), "previous-helper allocation trial " + trial);
            foreach (int life in new[] { expected, expected + 1, 100 }) {
                long bound = DeckPlanEvaluator.PublicFaceDamage(own, enemy, current, life);
                Check((bound < life) == (expected < life) && Penalty(bound, life) == Penalty(expected, life),
                    "threshold changed a consumer: trial " + trial + ", life " + life);
            }
        }
    }
    private static long Penalty(long damage, int life) => Math.Min(200, Math.Max(0, damage - life + 1) * 50);

    public static void TestCappedTauntAllocationKeepsPreviousFallback()
    {
        var random = new Random(71);
        for (int trial = 0; trial < 100; trial++) {
            var game = Busy(); game.Player1.Hero.Health = 1;
            foreach (var c in game.Player2.BoardZone) {
                c.AttackDamage = random.Next(1, 10);
                if (trial % 2 == 0) c[GameTag.MEGA_WINDFURY] = 1;
            }
            var f = View(game).Facts;
            Check(DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId)
                == PreviousDamage(f.Own, f.Opponent, f.CurrentPlayerId), "capped allocation changed: " + trial);
        }
    }

    public static void TestThreatCacheSharesScoringScreenAndEndpoint()
    {
        var game = Fixture(); game.Player1.Hero.Health = 3;
        Board(game, true, "CS2_179"); Board(game, false, "CS2_200"); Board(game, false, "CS2_120").AttackDamage = 3;
        var state = View(game); var f = state.Facts;
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        state.Evaluate(evaluator); int generation = SearchGeneration();
        Check(f.OwnThreatBound == 3 && !DeckPlanEvaluator.PublicBoardSurvives(f), "scoring did not cache lethal bound");
        state.Evaluate(evaluator);
        Check(SearchGeneration() == generation, "screen/scoring repeated allocation for same facts");
        var endpoint = state.ForReplyEndTurn(); endpoint.Evaluate(evaluator); generation = SearchGeneration();
        Check(endpoint.ForReplyEndTurn() == endpoint && !DeckPlanEvaluator.PublicBoardSurvives(endpoint.Facts), "endpoint fixture");
        endpoint.Evaluate(evaluator);
        Check(SearchGeneration() == generation && endpoint.Facts != f, "endpoint repeated allocation or shared stale snapshot");
        var reply = endpoint.Determinize(new[] { "CS2_182" }, 71).ForReply();
        evaluator.ReplyValue(reply.Facts, f, reply.ForReplyHand(f)); generation = SearchGeneration();
        evaluator.ReplyValue(reply.Facts, f, reply.ForReplyHand(f));
        Check(reply.Facts.OpponentThreatBound == 3 && !reply.Facts.OwnThreatBound.HasValue
            && SearchGeneration() == generation, "reversed reply cache repeated or mixed perspectives");
        var untouched = View(game); var beam = new TurnBeam(untouched, s => 0, 30, 4, true);
        Run(beam);
        Check(!untouched.Facts.OwnThreatBound.HasValue && beam.Leaves.All(n => !n.State.Facts.OwnThreatBound.HasValue),
            "reply beam computed unused survival screen");
    }
    private static int SearchGeneration()
    {
        var search = typeof(DeckPlanEvaluator).GetField("threatSearch", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        return (int)search.GetType().GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(search);
    }

    public static void TestBusyThreatAllocatesZeroBytes()
    {
        var f = View(Busy()).Facts;
        for (int i = 0; i < 64; i++) {
            DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId, 100);
            DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId);
        }
        long sum = 0; long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) sum += DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId, 100);
        long skipped = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) sum += DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId);
        long searched = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(sum == 512 * 42 && skipped == 0 && searched == 0,
            "busy threat bytes/256 calls: threshold=" + skipped + ", exact/capped=" + searched);
        Console.WriteLine("busy threat: threshold=0 B/call; exact/capped=0 B/call (after thread-local warmup)");
    }

    public static void TestBusyThreatTimingVersusGreedy()
    {
        var f = View(Busy()).Facts; const int repeats = 10000;
        Func<long> fast = () => DeckPlanEvaluator.PublicFaceDamage(f.Own, f.Opponent, f.CurrentPlayerId, 100);
        Func<long> greedy = () => Greedy(f);
        for (int i = 0; i < 1000; i++) { fast(); greedy(); }
        long fastTicks = long.MaxValue, greedyTicks = long.MaxValue; long sum = 0;
        for (int sample = 0; sample < 5; sample++) {
            // Alternate order and use best samples to avoid turning a shared-machine pause into a failure.
            if (sample % 2 == 0) { greedyTicks = Math.Min(greedyTicks, Time(greedy, repeats, ref sum)); fastTicks = Math.Min(fastTicks, Time(fast, repeats, ref sum)); }
            else { fastTicks = Math.Min(fastTicks, Time(fast, repeats, ref sum)); greedyTicks = Math.Min(greedyTicks, Time(greedy, repeats, ref sum)); }
        }
        Check(sum > 0 && fastTicks <= greedyTicks * 1.15 + Stopwatch.Frequency / 1000,
            "sublethal busy threat exceeded greedy +15% +1ms jitter margin: " + fastTicks + "/" + greedyTicks);
        Console.WriteLine("busy threat timing/" + repeats + ": threshold=" + fastTicks * 1000.0 / Stopwatch.Frequency
            + "ms, greedy=" + greedyTicks * 1000.0 / Stopwatch.Frequency + "ms");
    }
    private static long Time(Func<long> call, int count, ref long sum)
    {
        long start = Stopwatch.GetTimestamp(); for (int i = 0; i < count; i++) sum += call();
        return Stopwatch.GetTimestamp() - start;
    }
    private static Game Busy()
    {
        var game = Fixture(); game.Turn = 12;
        foreach (var p in new[] { game.Player1, game.Player2 }) {
            p.Hero.Health = 100;
            for (int i = 0; i < 6; i++) {
                var c = Board(game, p == game.Player1, "CS2_182", true);
                c.AttackDamage = i + 1; c.Health = 3 + i; c.HasWindfury = true;
                c.HasTaunt = i < 3; c.HasDivineShield = i < 2;
            }
        }
        return game;
    }

    // Previous main's greedy bound, retained only as the timing baseline.
    private static int Greedy(FairFacts f)
    {
        var attacks = f.Opponent.Board.Where(c => !c.Frozen && c.Attack > 0).Select(c => c.Attack)
            .Concat(f.Opponent.Weapon == null ? new int[0] : new[] { f.Opponent.Weapon.Attack }).OrderBy(a => a).ToList();
        foreach (var c in f.Own.Board.Where(c => c.Taunt)) {
            int health = c.Health; bool shield = c.DivineShield;
            while (health > 0 && attacks.Count > 0) {
                var hit = attacks[0]; attacks.RemoveAt(0);
                if (shield) shield = false; else health -= hit;
            }
        }
        return attacks.Sum();
    }
    private static (int Damage, bool Face)[] Hits(FairPlayer enemy, int current)
    {
        var hits = new List<(int, bool)>(); bool active = current == enemy.PlayerId;
        foreach (var c in enemy.Board) {
            if (c.CantAttack || c.Untouchable || c.Frozen || c.Attack <= 0 || active && c.Exhausted) continue;
            for (int j = active ? c.AttacksThisTurn : 0; j < c.AttackLimit; j++)
                hits.Add((c.Attack, !c.CantAttackHeroes && !(active && c.RushOnly)));
        }
        var hero = enemy.Hero; int damage = active ? hero.Attack : enemy.Weapon?.Attack ?? 0;
        if (!hero.CantAttack && !hero.Frozen && !(active && hero.Exhausted)) {
            int count = Math.Max(0, hero.AttackLimit - (active ? hero.AttacksThisTurn : 0));
            if (enemy.Weapon != null) count = Math.Min(count, enemy.Weapon.Durability);
            for (int i = 0; i < count && damage > 0; i++) hits.Add((damage, !hero.CantAttackHeroes));
        }
        return hits.ToArray();
    }
    // Independent oracle: every unused hit can target ANY surviving Taunt, including interleaved orders.
    private static int Exhaustive(FairPlayer own, FairPlayer enemy, int current)
    {
        var hits = Hits(enemy, current); var blockers = own.Board.Where(c => c.Taunt && !c.Stealth && !c.Immune && !c.Untouchable).ToArray();
        var health = blockers.Select(c => c.Health).ToArray(); var shield = blockers.Select(c => c.DivineShield).ToArray();
        var memo = new Dictionary<(int, int), int>();
        int Search(int used)
        {
            int packed = 0; bool alive = false;
            for (int j = 0; j < health.Length; j++) { packed |= (health[j] | (shield[j] ? 16 : 0)) << (j * 5); alive |= health[j] > 0; }
            if (!alive) return hits.Where((h, i) => (used & (1 << i)) == 0 && h.Face).Sum(h => h.Damage);
            int cached; if (memo.TryGetValue((used, packed), out cached)) return cached;
            int best = 0;
            for (int i = 0; i < hits.Length; i++) if ((used & (1 << i)) == 0)
                for (int j = 0; j < health.Length; j++) if (health[j] > 0) {
                    int hp = health[j]; bool ds = shield[j];
                    if (ds) shield[j] = false; else health[j] = Math.Max(0, hp - hits[i].Damage);
                    best = Math.Max(best, Search(used | (1 << i)));
                    health[j] = hp; shield[j] = ds;
                }
            return memo[(used, packed)] = best;
        }
        return Search(0);
    }
    // Round-2 helper, including its exact traversal/state cap: protects conservative fallback identity.
    private static int PreviousDamage(FairPlayer own, FairPlayer enemy, int current)
    {
        var attacks = Hits(enemy, current).OrderBy(a => a.Face).ThenBy(a => a.Damage).ToArray();
        int face = attacks.Where(a => a.Face).Sum(a => a.Damage);
        var taunts = own.Board.Where(c => c.Taunt && !c.Stealth && !c.Untouchable && !c.Immune).ToArray();
        if (face == 0 || taunts.Length == 0 || attacks.Length > 63) return face;
        var memo = new Dictionary<(ulong, int, int, bool), int>(); int visits = 0; bool capped = false;
        int Search(ulong used, int blocker, int health, bool shield, int remainingFace)
        {
            if (health <= 0) {
                if (++blocker == taunts.Length) return remainingFace;
                health = taunts[blocker].Health; shield = taunts[blocker].DivineShield;
            }
            if (remainingFace == 0) return 0;
            var key = (used, blocker, health, shield); int cached;
            if (memo.TryGetValue(key, out cached)) return cached;
            if (++visits > 2048) { capped = true; return remainingFace; }
            int best = 0;
            for (int i = 0; i < attacks.Length; i++) {
                if ((used & (1UL << i)) != 0) continue;
                if (i > 0 && attacks[i].Damage == attacks[i-1].Damage && attacks[i].Face == attacks[i-1].Face && (used & (1UL << (i-1))) == 0) continue;
                best = Math.Max(best, Search(used | (1UL << i), blocker, shield ? health : health - attacks[i].Damage,
                    false, remainingFace - (attacks[i].Face ? attacks[i].Damage : 0)));
                if (capped || best == remainingFace) break;
            }
            return memo[key] = best;
        }
        int maximum = Search(0, 0, taunts[0].Health, taunts[0].DivineShield, face);
        return capped ? face : maximum;
    }
}
