using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16SwapDeferredProof
{
    [GroupProof("b16-swap-deferred")]
    internal static void Run()
    {
        WarptoothCanonicalHistory();
        var g = DiscoverRealProof.New(); var p = g.Player1;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var waiting = Add(p, "CS2_200");
        SwapRules.DeferSummon(waiting);
        DiscoverRealProof.Saved(g, next => {
            var owner = next.Player1;
            next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(next.IdEntityDic[waiting.Id].Zone == owner.HandZone, "full board retains pending entity");
            Kill(next, owner.BoardZone.First());
            Core2026Proof.Check(next.IdEntityDic[waiting.Id].Zone == owner.BoardZone && owner.BoardZone.Count == 7,
                "post-death vacancy summons exact pending entity on original/clone/load");
        });
        PendingLifetime();
        RewindPendingAndRefill();
        NestedDeathrattle();
        ReentrantWaiter();
        PendingResetAndTransform();
        Console.WriteLine("PASS b16 deferred facility clone/snapshot");
    }

    private static void WarptoothCanonicalHistory()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        var held = Add(p, "JAIL_421");
        var victims = Enumerable.Range(0, 3).Select(_ => Summon(p, "CS2_200")).ToArray();
        var distinct = g.CloneExact(history: true); var repeated = g.CloneExact(history: true);
        for (int i = 0; i < 3; i++)
            foreach (var game in new[] { distinct, repeated })
            {
                var victim = (Minion)game.IdEntityDic[victims[game == distinct ? i : 0].Id];
                victim.TakeDamage(game.Player2.Hero, 1);
                victim.TakeHeal(game.Player2.Hero, 1);
            }
        distinct.DeathProcessingAndAuraUpdate(); repeated.DeathProcessingAndAuraUpdate();
        string a = HsSim.Canonical.State(distinct), b = HsSim.Canonical.State(repeated);
        Core2026Proof.Check(a.Split("|history-facts-v1:")[0] == b.Split("|history-facts-v1:")[0],
            "damaged/healed victim fixtures have identical current board and tags");
        Core2026Proof.Check(HsSim.Canonical.Sha(a) != HsSim.Canonical.Sha(b),
            "Warptooth canonical hash distinguishes distinct damaged-victim histories");
        foreach (var game in new[] { distinct, repeated })
            DiscoverRealProof.Saved(game, next => {
                Core2026Proof.Check(HsSim.Canonical.State(next) == HsSim.Canonical.State(game),
                    "Warptooth canonical history survives clone/snapshot");
                next.Player1.Hero.TakeDamage(next.Player2.Hero, 1);
                next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check((next.IdEntityDic[held.Id].Zone == next.Player1.BoardZone) == (game == distinct),
                    "same next hit summons only with three distinct past victims on original/clone/load");
            });
    }

    private static void PendingLifetime()
    {
        foreach (bool deck in new[] { false, true })
        foreach (bool departure in new[] { false, true })
        {
            var g = DiscoverRealProof.New(); var p = g.Player1;
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            var waiting = deck ? DiscoverRealProof.Deck(p, "CS2_200").Single() : Add(p, "CS2_200");
            SwapRules.DeferSummon(waiting);
            if (deck) { p.DeckZone.Remove(waiting); p.HandZone.Add(waiting); }
            if (departure) { p.SetasideZone.Add(p.HandZone.Remove(waiting)); p.HandZone.Add(p.SetasideZone.Remove(waiting)); }
            DiscoverRealProof.Saved(g, next => {
                End(next);
                Kill(next, next.Player1.BoardZone.First());
                Core2026Proof.Check((next.IdEntityDic[waiting.Id].Zone == next.Player1.BoardZone) != departure,
                    "pending survives deck-to-hand/later opponent turn; actual departure cancels even after reentry");
            });
        }
    }

    private static void RewindPendingAndRefill()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        var magmaw = Summon(p, "CATA_550");
        var waiting = Add(p, "JAIL_421"); SwapRules.DeferSummon(waiting);
        var fireball = Add(p, "CS2_029"); fireball[GameTag.REWIND] = 1;
        PlayEntity(g, p, fireball, p.BoardZone.First(m => m.Id != magmaw.Id));
        Core2026Proof.Check(g.Rewind != null && waiting.Zone == p.BoardZone, "attempt frees slot and consumes pending summon");
        var baseline = Game.LoadSnapshotWithPools(g.Rewind.Baseline, g.OfflinePools);
        Core2026Proof.Check(baseline.IdEntityDic[waiting.Id][SwapRules.PendingSummon] > 0
            && baseline.IdEntityDic[magmaw.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 93,
            "Rewind baseline retains pending queue and partly used refill counter");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, g.Rewind.RewindEntityId)), "Rewind accepted");
        Core2026Proof.Check(g.IdEntityDic[waiting.Id].Zone == g.Player1.BoardZone
            && g.Player1.BoardZone.Count == 7 && g.IdEntityDic[magmaw.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 93,
            "retry restores then consumes same pending entity once, without an extra refill");
    }

    private static void NestedDeathrattle()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        var definition = Cards.FromId("EX1_029").Clone();
        definition.Power = new Power { DeathrattleTask = new CustomTask((game, owner, source, target, stack) => {
            game.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.BoardZone.Count == 6,
                "nested Deathrattle checkpoint must not admit deferred summons before its parent finishes");
            Generic.SummonBlock(game, (Minion)Entity.FromCard(owner, Cards.FromId("CS2_231")), -1, source);
        }) };
        var victim = (Minion)Entity.FromCard(p, definition); Generic.SummonBlock(g, victim, -1, null);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var waiting = Add(p, "JAIL_421"); SwapRules.DeferSummon(waiting);
        Kill(g, victim);
        Core2026Proof.Check(waiting.Zone == p.HandZone, "Deathrattle summon takes available slot before deferred waiter");
    }

    private static void ReentrantWaiter()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var first = Add(p, "CS2_200"); var second = Add(p, "CS2_200");
        g.TriggerManager.AfterSummonTrigger += card => { if (card.Id == first.Id) SwapRules.DeferSummon(second); };
        SwapRules.DeferSummon(first); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(first.Zone == p.BoardZone && second.Zone == p.BoardZone,
            "a summon reaction can enqueue another waiter at the same stable checkpoint");
    }

    private static void PendingResetAndTransform()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var waiting = Add(p, "JAIL_421"); SwapRules.DeferSummon(waiting);
        new MoveToDeck(EntityType.TARGET).Process(g, p, p.Hero, waiting);
        Core2026Proof.Check(waiting[SwapRules.PendingSummon] > 0 && waiting.Zone == p.DeckZone,
            "backward hand-to-deck reset does not cancel pending hidden-zone request");
        DiscoverRealProof.Saved(g, next => {
            var card = next.IdEntityDic[waiting.Id];
            Generic.ChangeEntityBlock(next.Player1, card, Cards.FromId("CS2_200"), true);
            Core2026Proof.Check(next.IdEntityDic[waiting.Id][SwapRules.PendingSummon] == 0,
                "transform replaces pending card identity and cancels its summon");
            Kill(next, next.Player1.BoardZone.First());
            Core2026Proof.Check(next.IdEntityDic[waiting.Id].Zone == next.Player1.DeckZone,
                "transformed hidden card cannot inherit prior pending summon");
        });
        var bodyGame = DiscoverRealProof.New(); var body = Summon(bodyGame.Player1, "CATA_550");
        Generic.ChangeEntityBlock(body.Controller, body, Cards.FromId("CATA_580t"), true);
        Core2026Proof.Check(body.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0 && body[GameTag.TAG_SCRIPT_DATA_NUM_1] == body.Card[GameTag.TAG_SCRIPT_DATA_NUM_1],
            "transform drops old refill display so new definition reads its own printed script counter");
        Core2026Proof.Check(body[SwapRules.RefillActive] == 0, "transform cancels old Colossal refill ability");
        Kill(bodyGame, bodyGame.Player1.BoardZone.First(m => m.Id != body.Id));
        Core2026Proof.Check(bodyGame.Player1.BoardZone.Count == 6, "transformed body cannot refill former appendages");
    }
}
