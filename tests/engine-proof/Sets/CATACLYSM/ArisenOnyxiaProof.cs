using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ArisenOnyxiaProof
{
    internal static void Unpinned_HealthCostsAndWings(Game g, Controller p, string id)
    {
        for (int tier = 0; tier < 3; tier++)
        {
            var branch = DeathKnightC1Proof.NewGame(g.Seed); var owner = branch.Player1;
            owner[HeraldRules.Progress] = tier * 2;
            int cost = 2 << tier, factor = 1 << tier;
            var filter = OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost);
            var pool = OfflineCardPools.Candidates(branch, owner.HeroClass, filter, 124139);
            Core2026Proof.Check(pool.Count > 0, "current wing exact-cost bucket populated");
            Console.WriteLine($"Onyxia wing bucket {cost}: {pool.Count}");
            var random = branch.Random.Clone();
            var expected = Enumerable.Range(0, 2).Select(_ => pool[random.Next(pool.Count)].Id).ToArray();
            var held = Add(owner, id);
            PoolPilotProof.Resume(branch, next => {
                var c = next.Player1;
                Core2026Proof.Check(next.Process(PlayCardTask.Any(c, next.IdEntityDic[held.Id])), "Onyxia play");
                var body = (Minion)next.IdEntityDic[held.Id];
                var wings = ColossalRules.AppendagesOf(body).OrderBy(m => m.Id).ToArray();
                Core2026Proof.Check(wings.Select(m => m.Card.Id).SequenceEqual(new[] { "CATA_155t", "CATA_155t1" })
                    && wings.All(m => m.AttackDamage == factor && m.Health == factor && m[GameTag.TAG_SCRIPT_DATA_NUM_1] == cost), "exact wing identities and realized Herald tier");
                Core2026Proof.Check(c.HandZone.Select(m => m.Card.Id).SequenceEqual(expected)
                    && c.HandZone.All(m => m.Card.Cost == cost && CostRules.PaymentKind(m) == CostPaymentKind.Health), "seeded ordered exact-cost generation with temporary Health payment");
                Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState()), "two exact random draws and continuation");
                c.Hero.Damage = 29;
                var payment = c.HandZone.First();
                Core2026Proof.Check(CostRules.CanPay(c, CostRules.Quote(c, payment)), "PINNED Onyxia allows over-health costs");
                Core2026Proof.Check(Generic.PayPhase(c, payment) && c.Hero.BaseHealth == 30 + payment.Cost
                    && c.Hero.Health == 1 + payment.Cost, "Health payment replaced with max/current gain");
                Core2026Proof.Check(Canonical.State(next) == Canonical.State(Game.LoadSnapshot(next.SaveSnapshot())), "replaced Health payment snapshot validates");
                int max = c.Hero.BaseHealth;
                body.Silence(); next.DeathProcessingAndAuraUpdate();
                c.Hero.TakeDamage(c.Hero, 1);
                Core2026Proof.Check(c.Hero.BaseHealth == max, "Silence disables replacement without removing gained Health");
                End(next);
                Core2026Proof.Check(c.HandZone.All(m => CostRules.PaymentKind(m) == CostPaymentKind.Mana
                    && m.AppliedEnchantments.All(e => e.Card.Id != "CATA_155e2")), "wing Health cost and actor expire at turn end");
            });
        }
        // Ordinary damage and explicit setters remain independent of replacement.
        var body2 = Summon(p, id); p.Hero.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(p.Hero.Health == 32, "own-turn fatigue Health replacement");
        Effects.SetMaxHealth(20).ApplyTo(p.Hero);
        Core2026Proof.Check(p.Hero.Health == 20, "explicit Health setter unchanged");
        p.SetasideZone.Add(p.BoardZone.Remove(body2));
        p.Hero.TakeDamage(p.Hero, 1); Core2026Proof.Check(p.Hero.Health == 19, "leaving play removes replacement");
        foreach (bool full in new[] { false, true })
        {
            var branch = DeathKnightC1Proof.NewGame(g.Seed); var c = branch.Player1;
            if (full) PoolPilotProof.FillHand(c);
            else for (int i = 0; i < 6; i++) Summon(c, "CS2_182");
            var body = Summon(c, id);
            Core2026Proof.Check(full ? c.HandZone.Count == 10 : ColossalRules.AppendagesOf(body).Count() == 0,
                "full hand burns generated cards; full board has no wings");
        }
        var empty = DeathKnightC1Proof.NewGame(g.Seed); var player = empty.Player1;
        B14K3BEProof.Pool(empty, Array.Empty<string>(), () => {
            var cursor = empty.Random.CaptureState(); var wing = Summon(player, "CATA_155t");
            Core2026Proof.Check(player.HandZone.IsEmpty && cursor.Equals(empty.Random.CaptureState()), "empty exact-cost bucket yields nothing without RNG");
        });
        Unpinned_RewindReplacementState(g.Seed);
    }
    private static void Unpinned_RewindReplacementState(long seed)
    {
        var g = DeathKnightC1Proof.NewGame(seed); var p = g.Player1;
        Summon(p, "CORE_EDR_003"); Summon(p, "CATA_155");
        PoolPilotProof.EmptyHand(p);
        Play(g, p, "EDR_489");
        var source = Add(p, "TIME_001"); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)) && RewindTestHelpers.IsRewindOffered(g), "opponent-paid Rewind candidate offered");
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1;
            owner.Hero.TakeDamage(owner.Hero, 2); CorpseRules.Gain(owner, 2);
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(next.Player1.Hero.Health == 30 && CorpseRules.Available(next.Player1) == 0
                && next.Player1.Opponent.Hero.Health == 21, $"Rewind rolls back replacement gain/counters and pays opponent only once: health={next.Player1.Hero.Health}, corpses={CorpseRules.Available(next.Player1)}, opponent={next.Player1.Opponent.Hero.Health}");
            RewindTestHelpers.KeepIfOffered(next);
            next.Player1.Hero.TakeDamage(next.Player1.Hero, 1); CorpseRules.Gain(next.Player1, 1);
            Core2026Proof.Check(next.Player1.Hero.Health == 31 && CorpseRules.Available(next.Player1) == 2, "restored live replacement sources still work");
        });
    }
}
