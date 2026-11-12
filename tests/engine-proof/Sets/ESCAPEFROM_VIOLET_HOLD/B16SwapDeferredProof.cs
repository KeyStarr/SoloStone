using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using static Core2026Proof;
internal static class B16SwapDeferredJailProof
{
    [GroupProof("b16-swap-deferred-cards", ProofKind.Cards)]
    internal static void Run()
    {
        B16SwapDeferredCataclysmProof.Run();
        StandardSetsProof.Case("JAIL_421", Unpinned_WarptoothDeferred);
        StandardSetsProof.Case("JAIL_502", Unpinned_AlarmSwap);
        StandardSetsProof.Case("JAIL_852", Unpinned_TogwaggleTransfer);
    }
    private static void Unpinned_WarptoothDeferred(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        var victims = Enumerable.Range(0, 3).Select(_ => Summon(p, "CS2_200")).ToArray();
        p.Hero.TakeDamage(p.Opponent.Hero, 1);
        foreach (var victim in victims) victim.TakeDamage(p.Opponent.Hero, 1);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Zone == p.BoardZone && ((Minion)held).HasCharge, "hero plus three distinct friendly victims summon Warptooth");
        WarptoothDistinctAndTurns(g.Seed);
        WarptoothFullBoard(g.Seed);
    }

    private static void WarptoothDistinctAndTurns(long seed)
    {
        foreach (bool ownTurn in new[] { true, false })
        {
            var g = DiscoverRealProof.New(seed); var p = g.Player1;
            var held = Add(p, "JAIL_421");
            var victims = Enumerable.Range(0, 3).Select(_ => Summon(p, "CS2_200")).ToArray();
            if (!ownTurn) End(g);
            for (int hit = 0; hit < 4; hit++) p.Hero.TakeDamage(p.Opponent.Hero, 1);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(held.Zone == p.HandZone && held[SwapRules.PendingSummon] == 0, "four hits on one character do not qualify");
            foreach (var victim in victims) victim.TakeDamage(p.Opponent.Hero, 1);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check((held.Zone == p.BoardZone) == ownTurn, "distinct friendly damage qualifies only on owner turn");
        }
        var turns = DiscoverRealProof.New(seed); var owner = turns.Player1;
        var waiting = Add(owner, "JAIL_421"); var a = Summon(owner, "CS2_200");
        a.TakeDamage(owner.Opponent.Hero, 1); owner.Hero.TakeDamage(owner.Opponent.Hero, 1);
        End(turns); End(turns);
        var b = Summon(owner, "CS2_200"); var c = Summon(owner, "CS2_200");
        b.TakeDamage(owner.Opponent.Hero, 1); c.TakeDamage(owner.Opponent.Hero, 1);
        turns.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(waiting.Zone == owner.HandZone, "victims from previous owner turn do not accumulate");
        var prevented = DiscoverRealProof.New(seed); var me = prevented.Player1;
        var card = Add(me, "JAIL_421"); var ms = Enumerable.Range(0, 3).Select(_ => Summon(me, "CS2_200")).ToArray();
        me.Hero.Armor = 3;
        me.Hero.TakeDamage(me.Opponent.Hero, 1); ms[0].TakeDamage(me.Opponent.Hero, 1); ms[1].TakeDamage(me.Opponent.Hero, 1);
        ms[2].HasDivineShield = true; ms[2].TakeDamage(me.Opponent.Hero, 1);
        prevented.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(card.Zone == me.HandZone, "Divine Shield excluded");
        ms[2].IsImmune = true; ms[2].TakeDamage(me.Opponent.Hero, 1);
        prevented.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(card.Zone == me.HandZone, "Immune excluded");
    }

    private static void WarptoothFullBoard(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        var first = Add(p, "JAIL_421");
        var second = DiscoverRealProof.Deck(p, "JAIL_421").Single();
        var board = Enumerable.Range(0, 7).Select(_ => Summon(p, "CS2_200")).ToArray();
        p.Hero.TakeDamage(p.Opponent.Hero, 1);
        foreach (var m in board.Take(3)) m.TakeDamage(p.Opponent.Hero, 1);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(first[SwapRules.PendingSummon] > 0 && second[SwapRules.PendingSummon] > 0,
            "full-board threshold independently latches both hand/deck entities");
        p.DeckZone.Remove(second); p.HandZone.Add(second);
        DiscoverRealProof.Saved(g, next => {
            End(next);
            Kill(next, (Minion)next.IdEntityDic[board[0].Id]);
            Core2026Proof.Check(next.IdEntityDic[first.Id].Zone == next.Player1.BoardZone
                && next.IdEntityDic[second.Id].Zone == next.Player1.HandZone && next.IdEntityDic[second.Id][SwapRules.PendingSummon] > 0,
                "first free slot consumes one entity, keeps other pending across later turn/deck-to-hand");
            Kill(next, (Minion)next.IdEntityDic[board[1].Id]);
            Core2026Proof.Check(next.IdEntityDic[second.Id].Zone == next.Player1.BoardZone
                && next.IdEntityDic[second.Id][SwapRules.PendingSummon] == 0, "second vacancy consumes remaining latch once");
        });
        var lethal = DiscoverRealProof.New(seed); var owner = lethal.Player1; var held = Add(owner, "JAIL_421");
        var victims = Enumerable.Range(0, 7).Select(_ => Summon(owner, "CS2_200")).ToArray();
        owner.Hero.TakeDamage(owner.Opponent.Hero, 1);
        victims[0].TakeDamage(owner.Opponent.Hero, 1); victims[1].TakeDamage(owner.Opponent.Hero, 1);
        victims[2].TakeDamage(owner.Opponent.Hero, 100);
        Core2026Proof.Check(held.Zone == owner.HandZone, "fourth lethal hit does not summon inside damage callback");
        lethal.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Zone == owner.BoardZone && victims[2].Zone == owner.GraveyardZone,
            "same damage batch death opens slot before deferred summon");
    }
    private static void Unpinned_AlarmSwap(Game g, Controller p, string id)
    {
        var bot = Summon(p, id); var chosen = Add(p.Opponent, "CS2_200");
        DiscoverRealProof.Deck(p.Opponent, "CS2_029");
        End(g); End(g);
        Core2026Proof.Check(bot.Zone == p.Opponent.HandZone && chosen.Zone == p.BoardZone,
            "Alarm reciprocally exchanges controllers and zones at owner start");
        AlarmCapacityBuffsAndRng(g.Seed);
        AlarmNegative(g.Seed);
    }

    private static void AlarmCapacityBuffsAndRng(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        LocationProofs.Location(p); Summon(p, "CS2_122");
        var bot = Summon(p, "JAIL_502"); Summon(p, "CS2_182");
        int slot = bot.ZonePosition;
        Summon(p.Opponent, "EX1_315");
        while (p.Opponent.HandZone.Count < 10) Add(p.Opponent, "EX1_015");
        var candidates = p.Opponent.HandZone.OfType<Minion>().Reverse().ToArray();
        foreach (var held in candidates) p.Opponent.HandZone.Swap(held, p.Opponent.HandZone[0]);
        candidates = p.Opponent.HandZone.OfType<Minion>().ToArray();
        foreach (var held in candidates) Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_550e1"), p.Hero, held);
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_550e1"), p.Hero, bot); bot.TakeDamage(p.Opponent.Hero, 1);
        g.DeathProcessingAndAuraUpdate();
        var expected = g.CloneExact(history: true); int selected = candidates[expected.Random.Next(candidates.Length)].Id;
        int deck = p.DeckZone.Count;
        DiscoverRealProof.Saved(g, next => {
            var source = next.IdEntityDic[bot.Id];
            source.Power.Trigger.SingleTask.Process(next, next.Player1, source, null, new TaskStack());
            next.DeathProcessingAndAuraUpdate();
            var incoming = (Minion)next.IdEntityDic[selected]; var outgoing = (Minion)next.IdEntityDic[bot.Id];
            Core2026Proof.Check(incoming.Zone == next.Player1.BoardZone && incoming.ZonePosition == slot
                && outgoing.Zone == next.Player2.HandZone && next.Player2.HandZone.Count == 10,
                "uniform entity pick on reordered full opposing hand; reciprocal physical slot, no burned card");
            Core2026Proof.Check(outgoing.Damage == 0 && outgoing.AttackDamage == 0 && (outgoing.AppliedEnchantments?.Count ?? 0) == 0
                && incoming.AttackDamage == 4 && incoming.BaseHealth == incoming.Card.Health,
                "outgoing reset; incoming permanent +2 hand buff plus new Raid Leader aura once");
            Core2026Proof.Check(next.Player1.DeckZone.Count == deck && outgoing.Cost == 1,
                "incoming Battlecry suppressed; outgoing hand reevaluates new controller Summoning Portal");
            Core2026Proof.Check(next.Random.CaptureState().Equals(expected.Random.CaptureState()), "Alarm uses one seeded entity roll");
        });
    }

    private static void AlarmNegative(long seed)
    {
        foreach (bool silence in new[] { false, true })
        {
            var g = DiscoverRealProof.New(seed); var p = g.Player1;
            var bot = Summon(p, "JAIL_502");
            DiscoverRealProof.Deck(p.Opponent, "CS2_029");
            if (silence) { Add(p.Opponent, "CS2_200"); bot.Silence(); }
            var rng = g.Random.CaptureState();
            End(g);
            Core2026Proof.Check(bot.Zone == p.BoardZone, "Alarm does not trigger on opponent turn");
            End(g);
            Core2026Proof.Check(bot.Zone == p.BoardZone && g.Random.CaptureState().Equals(rng),
                "Silence or spells-only opposing hand leaves bot, no random roll");
        }
    }
    private static void Unpinned_TogwaggleTransfer(Game g, Controller p, string id)
    {
        Unpinned_TogwaggleRecipientEntries();
        Add(p, "CS2_200"); Add(p, "CS2_182");
        for (int i = 0; i < 7; i++) Add(p.Opponent, "CS2_231");
        var source = Add(p, id);
        var pooled = p.HandZone.Where(c => c != source).Concat(p.Opponent.HandZone).ToArray();
        foreach (var card in pooled)
        {
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_550e1"), p.Hero, card);
            CostRules.AddModifier((Playable)card, EffectOperator.SUB, 1);
        }
        var expected = g.CloneExact(history: true);
        var shuffled = pooled.ToArray();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = expected.Random.Next(i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        var selected = shuffled.Take(2).Select(c => c.Id).ToArray();
        var oldEntries = pooled.ToDictionary(c => c.Id, c => g.HistoryFacts.Query.LatestHandEntry(c.Id));
        DiscoverRealProof.Saved(g, next => {
            var entryEvents = new List<int>();
            void OnEntry(IEntity card) { if (oldEntries.ContainsKey(card.Id)) entryEvents.Add(card.Id); }
            next.TriggerManager.ZoneTrigger += OnEntry;
            PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]);
            next.TriggerManager.ZoneTrigger -= OnEntry;
            Core2026Proof.Check(next.Player1.HandZone.Select(c => c.Id).Order().SequenceEqual(selected.Order())
                && next.Player2.HandZone.Count == 7, "seeded uniform partition uses post-play counts");
            foreach (var old in pooled)
            {
                var card = (Minion)next.IdEntityDic[old.Id];
                Core2026Proof.Check(card.Cost == Math.Max(0, card.Card.Cost - 1) && card.AttackDamage == card.Card.ATK + 2
                    && card.AppliedEnchantments.Count(e => e.Card.Id == "CATA_550e1") == 1,
                    "hand-to-hand movement preserves persistent discounts and exact buff attachments");
                var entry = next.HistoryFacts.Query.LatestHandEntry(old.Id);
                bool retained = card.Controller.Id == oldEntries[old.Id].ControllerId;
                Core2026Proof.Check(retained ? entry.Sequence == oldEntries[old.Id].Sequence
                    : entry.Sequence > oldEntries[old.Id].Sequence && entry.ControllerId == card.Controller.Id
                        && entry.Turn == next.Turn && entry.FromZone == Zone.HAND && entry.EntryKind == HistoryHandEntryKind.Other,
                    "only transferred cards publish genuine recipient hand entries; retained cards stay continuous");
            }
            Core2026Proof.Check(entryEvents.Order().SequenceEqual(pooled.Where(card =>
                next.IdEntityDic[card.Id].Controller.Id != oldEntries[card.Id].ControllerId).Select(card => card.Id).Order()),
                "transferred cards get exactly one entry event; retained cards get none");
            Core2026Proof.Check(next.Random.CaptureState().Equals(expected.Random.CaptureState()), "partition consumes exact seeded shuffle draws");
        });
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 7, "post-play hand counts retained");
        TogwaggleExpiryAndCapacity(g.Seed);
        TogwaggleControllerAuras(g.Seed);
        TogwaggleBranches(g.Seed);
    }

    private static void Unpinned_TogwaggleRecipientEntries()
    {
        var g = DiscoverRealProof.New(1); var p = g.Player1;
        var mine = Add(p, "CS2_200"); var theirs = Add(p.Opponent, "CS2_182");
        Summon(p, "JAIL_205");
        var source = Add(p, "JAIL_852");
        DiscoverRealProof.Saved(g, next => {
            var entries = new List<int>();
            void OnEntry(IEntity entity)
            {
                if (entity.Id != mine.Id && entity.Id != theirs.Id) return;
                var card = (IPlayable)entity;
                Core2026Proof.Check(next.Player1.HandZone.Count == 1 && next.Player2.HandZone.Count == 1
                    && next.IdEntityDic[mine.Id].Controller == next.Player2
                    && next.IdEntityDic[theirs.Id].Controller == next.Player1,
                    "recipient entry observers see both final hands and controllers after atomic commit");
                foreach (var id in new[] { mine.Id, theirs.Id })
                    Core2026Proof.Check(next.HistoryFacts.Query.LatestHandEntry(id).ControllerId == next.IdEntityDic[id].Controller.Id,
                        "all recipient entry facts committed before first observer");
                Core2026Proof.Check(card.Zone == card.Controller.HandZone, "entry sender belongs to recipient hand");
                entries.Add(card.Id);
            }
            next.TriggerManager.ZoneTrigger += OnEntry;
            PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]);
            next.TriggerManager.ZoneTrigger -= OnEntry;
            Core2026Proof.Check(next.IdEntityDic[mine.Id].Controller == next.Player2, "seed 1 transfers own minion to opponent");
            End(next);
            Core2026Proof.Check(next.IdEntityDic[mine.Id].Zone == next.Player1.HandZone,
                "Rat Burglar steals minion genuinely entering opponent hand via Togwaggle this turn");
            Core2026Proof.Check(entries.Order().SequenceEqual(new[] { mine.Id, theirs.Id }.Order()),
                "each transferred card publishes one recipient entry event on original/clone/load");
        });
    }

    private static void TogwaggleExpiryAndCapacity(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        var cards = new List<IPlayable>();
        foreach (var owner in new[] { p, p.Opponent })
            while (owner.HandZone.Count < 10)
            {
                var card = Add(owner, "CS2_200"); cards.Add(card);
                Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_046e"), owner.Hero, card);
            }
        DiscoverRealProof.Saved(g, next => {
            SwapRules.ShuffleHands(next.Player1);
            Core2026Proof.Check(next.Player1.HandZone.Count == 10 && next.Player2.HandZone.Count == 10
                && cards.All(c => next.IdEntityDic[c.Id].Zone?.Type == Zone.HAND), "two full hands redistribute without overflow/lost entities");
            End(next);
            Core2026Proof.Check(cards.All(c => ((Minion)next.IdEntityDic[c.Id]).AttackDamage == c.Card.ATK),
                "transferred and retained temporary buffs preserve original end-turn expiry");
        });
    }

    private static void TogwaggleControllerAuras(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        Summon(p, "EX1_608");
        foreach (var owner in new[] { p, p.Opponent })
            for (int i = 0; i < 3; i++)
            {
                var card = Add(owner, "CS2_025");
                CostRules.AddModifier((Playable)card, EffectOperator.SUB, 1);
            }
        g.DeathProcessingAndAuraUpdate();
        DiscoverRealProof.Saved(g, next => {
            SwapRules.ShuffleHands(next.Player1);
            foreach (var card in next.Player1.HandZone.Concat(next.Player2.HandZone))
                Core2026Proof.Check(card.Cost == (card.Controller == next.Player1 ? 0 : 1),
                    "persistent -1 retained; old Apprentice aura removed, new controller aura applied exactly once");
        });
    }
    private static void TogwaggleBranches(long seed)
    {
        var g = DiscoverRealProof.New(seed);
        foreach (var owner in new[] { g.Player1, g.Player2 })
            for (int i = 0; i < 3; i++) Add(owner, "EX1_160");
        DiscoverRealProof.Saved(g, next => {
            SwapRules.ShuffleHands(next.Player1);
            foreach (var card in next.Player1.HandZone.Concat(next.Player2.HandZone))
                Core2026Proof.Check(card.ChooseOnePlayables != null && card.ChooseOnePlayables.All(option =>
                    option.Controller == card.Controller && option.Zone == card.Controller.SetasideZone),
                    "Choose One branch actors follow transferred parent controller without a prompt");
        });
    }

}
