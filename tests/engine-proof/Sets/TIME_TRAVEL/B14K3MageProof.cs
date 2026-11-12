using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static B14K3BEProof;

internal static class B14K3TIMETRAVELProof
{
    internal static void CardsProof() => StandardSetsProof.Case("TIME_859", Unpinned_Anomalize);
    // auto-cast/anomalize-{partial-summon-boundary,stat-permutation,zero-health-avoidance}.yaml;
    // C wiki permutation + E Dormant observation, owner-approved finite health-first uniform fallback.
    private static void Unpinned_Anomalize(Game g, Controller p, string id)
    {
        CheckPoolBuckets(g, id, 2);
        Unpinned_AnomalizeSummonSelection(g, id);
        var ten = OfflinePoolFilter.Random(type: CardType.MINION, minCost: 10, maxCost: 10);
        var one = OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1);
        Core2026Proof.Check(new[] { ten, one }.All(f => OfflineCardPools.Candidates(g, p.HeroClass, f, Cards.FromId(id).AssetId).Count >= 3), "both all-class cost buckets admitted");
        var outcomes = new HashSet<string>();
        foreach (int slots in new[] { 0, 1, 2 })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            Pool(branch, new[] { "EDR_209", "CS3_007" }, () => {
                var location = LocationProofs.Location(owner);
                while (owner.BoardZone.SlotCount < 7 - slots) Summon(owner, seed % 2 == 0 ? "CS2_231" : "CS2_182");
                var old = owner.BoardZone.Slots.Select(c => c.Id).ToArray();
                branch.SetRandomSeed(seed);
                var random = new Util.DeepCloneableRandom(seed);
                random.Next(1); random.Next(1);
                int[][] derangements = {
                    new[] { 8, 5, 2, 3 }, new[] { 8, 3, 2, 5 }, new[] { 8, 2, 5, 3 },
                    new[] { 3, 5, 2, 8 }, new[] { 3, 2, 5, 8 }, new[] { 3, 2, 8, 5 },
                    new[] { 2, 3, 8, 5 }, new[] { 2, 3, 5, 8 }, new[] { 2, 5, 8, 3 }
                };
                int[] expected = slots == 0 ? Array.Empty<int>() : slots == 1
                    ? new[] { new[] { 8, 5 } }[random.Next(1)] : derangements[random.Next(9)];
                Future(branch, next => {
                    Play(next, next.Player1, id);
                    var generated = next.Player1.BoardZone.Where(m => !old.Contains(m.Id)).ToArray();
                    Core2026Proof.Check(generated.Length == slots && generated.Select(m => m.Card.Id).SequenceEqual(new[] { "EDR_209", "CS3_007" }.Take(slots))
                        && next.Player1.BoardZone.Slots.Take(old.Length).Select(c => c.Id).SequenceEqual(old), "ten then one; only successful summons participate; physical limit respected");
                    if (slots == 0) return;
                    int[] before = generated.SelectMany(m => new[] { m.Card.ATK, m.Card.Health }).ToArray();
                    int[] after = generated.SelectMany(m => new[] { m.AttackDamage, m.BaseHealth }).ToArray();
                    Core2026Proof.Check(after.SequenceEqual(expected), "seeded uniform best-assignment choice, including singleton RNG draws");
                    Core2026Proof.Check(before.Order().SequenceEqual(after.Order()) && after.Where((v,i) => i % 2 == 1).All(v => v > 0), "four individual values permuted, no new points or lethal Health");
                    Core2026Proof.Check(after.Where((v,i) => v == before[i]).Count() == 0 && generated.All(m => m.AppliedEnchantments.Single().Card.Id == "TIME_859e"), "best derangement applied through exact named attachments");
                    outcomes.Add(string.Join(",", after));
                    var filler = next.Player1.BoardZone.First(m => old.Contains(m.Id));
                    next.Player1.SetasideZone.Add(next.Player1.BoardZone.Remove(filler));
                    var copy = Generic.Copy(next.Player1, next.Player1.Hero, generated[0], Zone.PLAY);
                    Core2026Proof.Check(((Minion)copy).AttackDamage == generated[0].AttackDamage && ((Minion)copy).BaseHealth == generated[0].BaseHealth, "forward copy retains realized scramble");
                    foreach (var minion in generated) { minion.Silence(); Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.BaseHealth == minion.Card.Health, "Silence restores base stats"); }
                });
            });
        }
        Core2026Proof.Check(outcomes.Count >= 3, "different seeds do not always select first assignment");
        bool dormantContributed = false;
        // No native Dormant one-drop is admitted on this head; external sleep exercises the same recipient boundary.
        foreach (int seed in new[] { 1, 2, 3 })
        Pool(g, new[] { "EDR_209", "CS3_007" }, () => {
            ParkBoard(p); g.SetRandomSeed(seed);
            void Sleep(IEntity entity)
            {
                if (entity is Minion m && m.Card.Id == "CS3_007") DormantRules.Make(m, 2, "TIME_046e", p.Hero);
            }
            g.TriggerManager.AfterSummonTrigger += Sleep;
            try { Play(g, p, id); }
            finally { g.TriggerManager.AfterSummonTrigger -= Sleep; }
            var dormant = p.BoardZone.Single(m => m.Card.Id == "CS3_007"); var active = p.BoardZone.Single(m => m.Card.Id == "EDR_209");
            dormantContributed |= new[] { active.AttackDamage, active.BaseHealth }.Any(v => v == 2 || v == 3);
            Core2026Proof.Check(dormant.IsDormant && dormant.AttackDamage == 3 && dormant.BaseHealth == 2
                && dormant.AppliedEnchantments.All(e => e.Card.Id != "TIME_859e")
                && new[] { active.AttackDamage, active.BaseHealth }.All(v => new[] { 2, 3, 5, 8 }.Contains(v)), "Dormant sampled but unchanged");
        });
        Core2026Proof.Check(dormantContributed, "Dormant values must actually reach the active participant across seeded assignments");
        Pool(g, new[] { "EDR_209", "CATA_210" }, () => {
            ParkBoard(p); Play(g, p, id);
            var generated = p.BoardZone.ToArray();
            Core2026Proof.Check(generated.Length == 2 && generated.All(m => m.Health > 0)
                && generated.SelectMany(m => new[] { m.AttackDamage, m.BaseHealth }).Order().SequenceEqual(new[] { 0, 2, 5, 8 }),
                "zero Attack never enters writable Health when living assignment exists");
        });
        Pool(g, new[] { "EDR_209", "CS3_007" }, () => {
            ParkBoard(p);
            void Buff(IEntity entity)
            {
                if (entity is Minion m && m.Card.Id == "EDR_209") { m.AttackDamage = 6; m.BaseHealth = 9; }
            }
            g.TriggerManager.AfterSummonTrigger += Buff;
            try { Play(g, p, id); }
            finally { g.TriggerManager.AfterSummonTrigger -= Buff; }
            Core2026Proof.Check(p.BoardZone.SelectMany(m => new[] { m.AttackDamage, m.BaseHealth }).Order().SequenceEqual(new[] { 2, 3, 6, 9 }),
                "summon-trigger stat changes settle before scramble snapshot");
        });
        Pool(g, new[] { "EDR_209", "CS3_007" }, () => {
            ParkBoard(p); var aura = Summon(p, "CORE_CS2_122");
            Play(g, p, id);
            var participants = p.BoardZone.Where(m => m.Id != aura.Id).ToArray();
            Core2026Proof.Check(participants.SelectMany(m => new[] { m.AttackDamage - 1, m.BaseHealth }).Order().SequenceEqual(new[] { 2, 4, 6, 8 }),
                "aura-inclusive values sampled once, then ongoing Attack aura applies again");
        });
        Pool(g, new[] { "EDR_209", "CS3_007" }, () => {
            ParkBoard(p); ParkBoard(p.Opponent);
            void Steal(IEntity entity)
            {
                if (entity is Minion m && m.Card.Id == "CS3_007")
                    new ControlTask(EntityType.TARGET, opposite: true).Process(g, p, p.Hero, m);
            }
            g.TriggerManager.AfterSummonTrigger += Steal;
            try { Play(g, p, id); }
            finally { g.TriggerManager.AfterSummonTrigger -= Steal; }
            var participants = p.BoardZone.Concat(p.Opponent.BoardZone).ToArray();
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1
                && participants.SelectMany(m => new[] { m.AttackDamage, m.BaseHealth }).Order().SequenceEqual(new[] { 2, 3, 5, 8 })
                && participants.All(m => m.AppliedEnchantments.Any(e => e.Card.Id == "TIME_859e")),
                "successful surviving participant stays in scramble after a summon-trigger control transfer");
        });
    }

    private static void Unpinned_AnomalizeSummonSelection(Game game, string id)
    {
        // Same approved anomalize-partial-summon-boundary rule; independently index multi-card cost buckets.
        string[] fixture = { "EDR_209", "EDR_471", "CORE_YOP_034", "CS3_007", "CORE_EX1_011", "CATA_210" };
        var buckets = new[] { 10, 1 }.Select(cost => fixture.Select(Cards.FromId).Where(c => c.Cost == cost)
            .OrderBy(c => c.AssetId).Select(c => c.Id).ToArray()).ToArray();
        Core2026Proof.Check(buckets.All(b => b.Length == 3), "three independent candidates per summon bucket");
        var tens = new HashSet<string>(); var ones = new HashSet<string>();
        foreach (bool reverse in new[] { false, true })
        foreach (int slots in new[] { 0, 1, 2 })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            Pool(branch, reverse ? fixture.Reverse().ToArray() : fixture, () => {
                var fillers = new[] { "CS2_182", "CS2_231", "CS3_007" };
                foreach (string filler in reverse ? fillers.Reverse() : fillers) Summon(owner, filler);
                LocationProofs.Location(owner);
                while (owner.BoardZone.SlotCount < 7 - slots) Summon(owner, "CS2_182");
                var before = owner.BoardZone.Slots.Select(c => c.Id).ToArray();
                branch.SetRandomSeed(seed);
                var random = new Util.DeepCloneableRandom(seed);
                string[] expected = buckets.Select(bucket => bucket[random.Next(bucket.Length)]).Take(slots).ToArray();
                Play(branch, owner, id);
                var generated = owner.BoardZone.Where(m => !before.Contains(m.Id)).ToArray();
                Core2026Proof.Check(generated.Select(m => m.Card.Id).SequenceEqual(expected)
                    && owner.BoardZone.Slots.Take(before.Length).Select(c => c.Id).SequenceEqual(before),
                    $"seeded ordered multi-candidate summons, seed={seed}, reversed={reverse}, slots={slots}");
                if (slots == 2) { tens.Add(generated[0].Card.Id); ones.Add(generated[1].Card.Id); }
            });
        }
        Core2026Proof.Check(tens.Count > 1 && ones.Count > 1, "both summon buckets select beyond first candidate across seeds");
    }
}
