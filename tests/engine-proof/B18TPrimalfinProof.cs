using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B18TPrimalfinProof
{
    internal static int Pending(Controller p) => p.AppliedEnchantments?.Count(e => e.Card.Id == "TLC_251e") ?? 0;
    internal static void Match(Controller p, string id)
    {
        p.CardsPlayedThisTurn.Add(Cards.FromId(id));
        p.SnapshotCardsPlayedThisTurn();
    }
    private static void EmptyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
    }
    private static void Futures(Game g, Action<Game> check)
    {
        foreach (var future in new[] { g.CloneExact(history: g.History), Game.LoadSnapshot(g.SaveSnapshot()), g }) check(future);
    }
    [GroupProof("b18-t-primalfin")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            Unpinned_EmptyChillspineConsumes(history);
            Unpinned_CoConsumptionAndOrder(history);
            Unpinned_PassivePayment(history);
            Unpinned_EmptyAndCountered(history);
            Unpinned_RecipientAndReplacement(history);
            Unpinned_SeededSummons(history);
            Unpinned_ExistingClauses(history);
            Unpinned_ClauseAdapters(history);
            Unpinned_AtomicBoundary(history);
            Unpinned_DiscoverRecipient(history);
        }
        Unpinned_Rewind();
        Console.WriteLine("PASS Unpinned_Primalfin: co-consumption, clause boundaries, payment, lifecycle, RNG, clone/load/Rewind");
    }
    private static void Unpinned_EmptyChillspineConsumes(bool history)
    {
        foreach (bool matched in new[] { true, false })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Play(g, p, matched ? "CS2_171" : "CS2_182"); End(g); End(g);
            Play(g, p, "TLC_251"); Play(g, p, "TLC_251");
            Futures(g, future => {
                var owner = future.Player1; long draws = future.Random.DrawCount;
                int friendlyDamage = owner.BoardZone.Sum(m => m.Damage), heroDamage = owner.Opponent.Hero.Damage;
                Play(future, owner, "DINO_413");
                Core2026Proof.Check(Pending(owner) == (matched ? 0 : 2),
                    "Unpinned_EmptyChillspine: matched empty-recipient clause co-consumes; unmatched preserves, history=" + history);
                Core2026Proof.Check(future.Random.DrawCount == draws && owner.Opponent.BoardZone.IsEmpty
                    && owner.Opponent.Hero.Damage == heroDamage && owner.BoardZone.Sum(m => m.Damage) == friendlyDamage
                    && owner.BoardZone.All(m => !m.IsFrozen), "empty selection uses no RNG, damage or stale friendly recipients");
                Match(owner, "TLC_903"); Play(future, owner, "TLC_903");
                Core2026Proof.Check(Pending(owner) == 0 && owner.Hero.AttackDamage == (matched ? 5 : 10),
                    "Queen receives only pending modifier left after empty Chillspine");
            });
        }
        Console.WriteLine("PASS Unpinned_EmptyChillspineConsumes history=" + history + ": original/clone/load, matched/unmatched, co-consumption, Queen follow-up");
    }
    private static void Unpinned_CoConsumptionAndOrder(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var first = (Minion)Play(g, p, "TLC_251"); Play(g, p, "TLC_251");
        Core2026Proof.Check(Pending(p) == 2 && first.Card[(GameTag)4028] == 0, "Primalfin installs separate pending player enchantments, not Kindred membership");
        first.Silence(); new ReturnHandTask(EntityType.TARGET).Process(g, p, p.Hero, first);
        End(g); End(g);
        Core2026Proof.Check(Pending(p) == 2, "Silence, bounce and turn end never expire the player's modifier");
        var unmatched = Add(p, "TLC_440");
        for (int i = 0; i < 3; i++) { _ = unmatched.Cost; _ = p.Options(); _ = PoweredUp.IsMet(unmatched); }
        Generic.Copy(p, p.Hero, unmatched, Zone.HAND); Summon(p, "TLC_251");
        Core2026Proof.Check(Pending(p) == 2, "quotes, previews, copying, generation and summons preserve pending modifiers");
        PlayEntity(g, p, unmatched, p.Opponent.Hero);
        Core2026Proof.Check(Pending(p) == 2, "unmatched Kindred does not consume");
        EmptyHand(p); Match(p, "TLC_440");
        Futures(g, future => {
            var owner = future.Player1; int deck = owner.DeckZone.Count, damage = owner.Opponent.Hero.Damage;
            int plays = future.HistoryFacts.Query.Plays().Count();
            Play(future, owner, "TLC_440", owner.Opponent.Hero);
            Core2026Proof.Check(owner.DeckZone.Count == deck - 3 && owner.Opponent.Hero.Damage == damage + 4,
                "Unpinned_CoConsumption: ordinary damage/draw once, bonus draw twice total");
            Core2026Proof.Check(Pending(owner) == 0 && future.HistoryFacts.Query.Plays().Count() == plays + 1,
                "all modifiers co-consume, one committed play");
            deck = owner.DeckZone.Count;
            Play(future, owner, "TLC_440", owner.Opponent.Hero);
            Core2026Proof.Check(owner.DeckZone.Count == deck - 2, "second match has no queued modifier");
        });
    }
    private static void Unpinned_PassivePayment(bool history)
    {
        foreach (string id in new[] { "TLC_816", "TLC_366", "TLC_600" })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Play(g, p, "TLC_251"); Play(g, p, "TLC_251"); Match(p, id);
            var card = Add(p, id); var other = Add(p, id);
            int cost = Math.Max(0, card.Card.Cost - 4);
            Core2026Proof.Check(card.Cost == cost && other.Cost == cost && Pending(p) == 2, "passive previews discount four without consumption: " + id);
            if (cost > 0)
            {
                p.BaseMana = 0; p.UsedMana = 0;
                Core2026Proof.Check(!Generic.PlayCard(p, card, id == "TLC_600" ? p.Opponent.Hero : null), "unaffordable play rejected");
                Core2026Proof.Check(Pending(p) == 2 && card.Zone == p.HandZone, "failed payment keeps both modifiers");
            }
            Futures(g, future => {
                var owner = future.Player1; var source = future.IdEntityDic[card.Id];
                PlayEntity(future, owner, source, id == "TLC_600" ? owner.Opponent.Hero : null);
                Core2026Proof.Check(owner.UsedMana == cost && Pending(owner) == 0 && future.IdEntityDic[other.Id].Cost == Math.Max(0, card.Card.Cost - 2),
                    "commit pays doubled reduction once; other hand costs revert: " + id);
            });
        }
    }
    private static void Unpinned_EmptyAndCountered(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        Play(g, p, "TLC_251"); Match(p, "TLC_519");
        var spell = (Spell)Add(p, "TLC_519");
        TriggerManager.TriggerHandler counter = e => { if (e.Id == spell.Id) ((Spell)e).IsCountered = true; };
        g.TriggerManager.CastSpellTrigger += counter;
        PlayEntity(g, p, spell); g.TriggerManager.CastSpellTrigger -= counter;
        Core2026Proof.Check(Pending(p) == 1, "countered active clause never activates or consumes");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "TLC_519"); Core2026Proof.Check(Pending(p) == 0 && p.BoardZone.SlotCount == 7, "matched full-board clause consumes even without recipients");
        StandardSetsProof.ParkBoard(p); Play(g, p, "TLC_251");
        End(g); Match(p.Opponent, "TLC_519"); Play(g, p.Opponent, "TLC_519");
        Core2026Proof.Check(Pending(p) == 1 && p.Opponent.BoardZone.Count == 2, "opponent Kindred cannot consume friendly modifier");
        End(g); Match(p, "TLC_519");
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Play(g, p, "TLC_519"); Core2026Proof.Check(Pending(p) == 0 && p.BoardZone.IsFull, "each repeated summon checks capacity");
        StandardSetsProof.ParkBoard(p); EmptyHand(p); Play(g, p, "TLC_251"); Match(p, "TLC_440");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        int deck = p.DeckZone.Count;
        var cryo = p.HandZone[0]; p.SetasideZone.Add(p.HandZone.Remove(cryo)); cryo = Add(p, "TLC_440");
        PlayEntity(g, p, cryo, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 3 && Pending(p) == 0, "three draws use ordinary overflow/burn");
        // Passive clauses commit before countering, unlike active clauses.
        EmptyHand(p); Play(g, p, "TLC_251"); Match(p, "TLC_816");
        spell = (Spell)Add(p, "TLC_816"); deck = p.DeckZone.Count;
        g.TriggerManager.CastSpellTrigger += counter; PlayEntity(g, p, spell); g.TriggerManager.CastSpellTrigger -= counter;
        Core2026Proof.Check(Pending(p) == 0 && p.UsedMana == 0 && p.DeckZone.Count == deck, "countered passive card already paid and consumed");
    }
    private static void Unpinned_RecipientAndReplacement(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        foreach (var entity in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(entity));
        var deathrattle = Entity.FromCard(p, Cards.FromId("EX1_029"), zone: p.DeckZone);
        Play(g, p, "TLC_251"); Match(p, "TLC_432");
        Play(g, p, "TLC_432");
        Core2026Proof.Check(Pending(p) == 0 && deathrattle.Zone == p.HandZone && deathrattle.Cost == 0 && p.DeckZone.IsEmpty,
            "Dread Raptor draws once, repeats SET zero on same captured card");
        EmptyHand(p); StandardSetsProof.ParkBoard(p); Play(g, p, "TLC_251"); Match(p, "TLC_463");
        Add(p.Opponent, "CS2_182"); Add(p.Opponent, "CS2_200"); Add(p.Opponent, "CS2_125");
        int hand = p.Opponent.HandZone.Count;
        Play(g, p, "TLC_463");
        Core2026Proof.Check(Pending(p) == 0 && p.Opponent.HandZone.Count == hand - 2, "Razidir replaces friendly discard, repeats enemy discard only");
    }
    private static void Unpinned_SeededSummons(bool history)
    {
        foreach (int seed in new[] { 3, 11, 29 })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Play(g, p, "TLC_251"); Match(p, "TLC_815");
            g.SetRandomSeed(seed);
            var expected = g.CloneExact();
            var filter = Batch9PoolProof.Filter("TLC_815");
            string[] ids = Enumerable.Range(0, 3).Select(_ => OfflineCardPools.Pick(expected, filter, p.HeroClass, 117316).Id).ToArray();
            Futures(g, future => {
                var owner = future.Player1; long before = future.Random.DrawCount;
                Play(future, owner, "TLC_815");
                Core2026Proof.Check(owner.BoardZone.Skip(1).Select(m => m.Card.Id).SequenceEqual(ids)
                    && future.Random.DrawCount == before + 3 && Pending(owner) == 0,
                    "seeded ordinary + two independent Kindred summons, exact RNG order seed=" + seed);
            });
        }
    }
    private static void Unpinned_ExistingClauses(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        Play(g, p, "TLC_251"); Match(p, "TLC_903");
        Play(g, p, "TLC_903");
        Core2026Proof.Check(p.Hero.AttackDamage == 10 && Pending(p) == 0, "Silithid Queen grants two independent +5 Attack buffs");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "ordinary buff layers expire at turn end"); End(g);
        StandardSetsProof.ParkBoard(p); Play(g, p, "TLC_251"); Match(p, "TLC_226");
        Play(g, p, "TLC_226");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TLC_226") == 3 && Pending(p) == 0, "Bookkeeper copies twice; summoned copies never recursively activate");
    }
    private static void Unpinned_Rewind()
    {
        var g = HistoryFactsProof.Fixture(true); var p = g.Player1;
        Play(g, p, "TLC_251"); Match(p, "TLC_903"); Play(g, p, "TIME_003");
        Core2026Proof.Check(g.Rewind != null && Pending(p) == 1, "Rewind baseline includes pending player enchantment");
        var r = g.Rewind;
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(r.OfferChoiceId, p.Id, new[] { r.RewindEntityId }), out var error), "Rewind answer: " + error);
        Core2026Proof.Check(Pending(g.Player1) == 1, "Rewind restores pending modifier");
        Play(g, g.Player1, "TLC_903"); Core2026Proof.Check(Pending(g.Player1) == 0 && g.Player1.Hero.AttackDamage == 10, "restored modifier consumes once");
    }

    private static void Unpinned_AtomicBoundary(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        Play(g, p, "TLC_251"); Match(p, "TLC_903");
        var source = Entity.FromCard(p, Cards.FromId("TLC_903")); int calls = 0;
        KindredRules.Clause(new CustomTask((game, owner, s, t, stack) => {
            calls++;
            if (calls == 1)
            {
                Core2026Proof.Check(Pending(owner) == 0 && KindredRules.BeginMatchedClause((IPlayable)s) == 1,
                    "consume before resolving; recursive clause cannot reuse grant");
                Enchantment.GetInstance(owner, (IPlayable)s, owner, Cards.FromId("TLC_251e"));
            }
        })).Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(calls == 2 && Pending(p) == 1, "repetitions frozen before callback; newly created grant remains pending");
    }

    private static void Unpinned_ClauseAdapters(bool history)
    {
        foreach (string id in new[] { "TLC_454", "TLC_223", "TLC_429", "TLC_428", "TLC_243", "TLC_107",
            "DINO_413", "DINO_404", "TLC_825", "TLC_829", "DINO_138", "DINO_435", "TLC_482", "END_015", "TLC_236", "TLC_447" })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Play(g, p, "TLC_251"); Match(p, id);
            var low = Summon(p.Opponent, "CS2_120");
            var high = Summon(p.Opponent, "CS2_182");
            SabberStoneCore.Enchants.Effects.Health_N(20).ApplyTo(low);
            SabberStoneCore.Enchants.Effects.Health_N(20).ApplyTo(high);
            var fire = Entity.FromCard(p, Cards.FromId("CORE_CS2_029"), zone: p.DeckZone);
            int hand = p.HandZone.Count, enemyDamage = p.Opponent.Hero.Damage;
            int victimAttack = high.AttackDamage, victimHealth = high.Health;
            var played = Play(g, p, id, id is "TLC_825" or "TLC_829" or "TLC_447" ? high : null);
            var source = played as Minion;
            Core2026Proof.Check(Pending(p) == 0, "explicit clause adapter consumes: " + id);
            if (id == "TLC_454") Core2026Proof.Check(low.Zone == p.Opponent.GraveyardZone && high.Zone == p.Opponent.GraveyardZone, "Kodo destroys two remaining highest, no base third kill");
            if (id == "TLC_223") Core2026Proof.Check(fire.Zone == p.HandZone && fire[GameTag.SPELLPOWER] == 4, "Thrasher draws once, two Spell Damage buffs on captured draw");
            if (id == "TLC_429") Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TLC_429t") == 4, "Steamfin summons four tokens");
            if (id == "TLC_428") Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "TLC_428e") == 1
                && p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_428e").ScriptTag1 == 1, "Glider one base discount grant, idempotent Shield");
            if (id == "TLC_243") { Core2026Proof.Check(source.IsImmune, "Stormdrake immune"); End(g); Core2026Proof.Check(!source.IsImmune, "Stormdrake ordinary expiry"); }
            if (id == "TLC_107") Core2026Proof.Check(source.IsRush, "Stormbrewer idempotent Rush");
            if (id == "DINO_413") Core2026Proof.Check(low.Damage == 2 && high.Damage == 2 && low.IsFrozen && high.IsFrozen, "Stegodon base damage once, Freeze same recipients");
            if (id == "DINO_404") Core2026Proof.Check(p.BoardZone.First(m => m.Card.Id == "TLC_251").IsRush, "Firegill Rush grant");
            if (id == "TLC_825") Core2026Proof.Check(high.Damage == source.AttackDamage * 2, "Matriarch hits same target twice");
            if (id == "TLC_829") Core2026Proof.Check(source.AttackDamage == source.Card.ATK + 2 * victimAttack
                && source.Health == source.Card.Health + 2 * victimHealth, "Devilsaur captures victim stats once, gains them twice");
            if (id == "DINO_138") Core2026Proof.Check(low.Damage == 12 && high.Damage == 12, "Diabolus two edge-hit clauses");
            if (id == "DINO_435") Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 3, "Crater copies twice without recursion");
            if (id == "TLC_482") Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TLC_249") == 2
                && low.Damage + high.Damage + p.Opponent.Hero.Damage - enemyDamage == 8, "Slagclaw summons once, two rounds of both Cinder Deathrattles");
            if (id == "END_015") Core2026Proof.Check(p.HandZone.Count == hand + 2, "Triennium generates twice on Kindred only");
            if (id == "TLC_236") Core2026Proof.Check(p.HandZone.Single(m => m.Card.Id == "CS2_182").Cost == 2, "Hybridization draws once and discounts captured cost-four by two");
            if (id == "TLC_447") Core2026Proof.Check(low.Damage == 4 && high.Zone == p.Opponent.GraveyardZone, "Fumes destroys once, two area damage clauses");
        }
    }

    private static void Unpinned_DiscoverRecipient(bool history)
    {
        foreach (string id in new[] { "CORE_EDR_004", "CORE_EDR_004_2026" })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Play(g, p, "TLC_251"); Match(p, id); Play(g, p, id);
            var offers = GiftedDiscoverProof.Offers(g, id, OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.BEAST));
            int index = Array.FindIndex(offers, offer => offer.Power.Aura == null);
            Core2026Proof.Check(index >= 0 && Pending(p) == 1, "pending modifier survives actual Discover prompt");
            var selected = offers[index]; int cost = selected.Cost;
            GiftedDiscoverProof.Pair(g, selected, "EDR_100t1");
            Futures(g, future => {
                var ids = offers.Select(offer => future.IdEntityDic[offer.Id]).ToArray();
                GiftedDiscoverProof.Choose(future, ids, index);
                Core2026Proof.Check(Pending(future.Player1) == 0 && future.IdEntityDic[selected.Id].Cost == Math.Max(0, cost - 2),
                    "Raptor Herald keeps one Discover recipient, discounts it twice after resumed choice");
            });
        }
    }
}
