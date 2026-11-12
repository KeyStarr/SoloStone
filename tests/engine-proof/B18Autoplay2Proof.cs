using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

internal static class B18Autoplay2Proof
{
    [GroupProof("b18-t-autoplay2")]
    internal static void Run()
    {
        Unpinned_KeepAllStoneCharge();
        Unpinned_StoneCopies();
        Unpinned_NavigatorCompositeActivationOrder();
        Unpinned_ComponentRecipes();
        Unpinned_GiftsDeckAndNestedChoices();
        Unpinned_CommittedTargetAndRewind();
        DeferredDeathBeforeSandsRewind();
        UnexpectedEvidenceShapeDuringSands();
        var pools = L.Game();
        Console.WriteLine("POOLS Runic=" + string.Join(",", new[] { CardClass.DEATHKNIGHT, CardClass.DEMONHUNTER, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR }
            .Select(c => c + ":" + OfflineCardPools.Candidates(pools, c, CompositeLocationRules.Spells, 117762).Count)));
        Console.WriteLine("PASS b18-t-autoplay2 captured autoplay, ordered composite, persistence and capacity");
    }
    internal static IPlayable[] Offers(Controller p, IEntity source, bool discover, params string[] ids)
    {
        var offers = ids.Select(id => Entity.FromCard(p, Cards.FromId(id), zone: p.SetasideZone)).ToArray();
        p.Choice = new Choice(p) { SourceId = source.Id, ChoiceType = ChoiceType.GENERAL,
            ChoiceAction = ChoiceAction.HAND, Choices = offers.Select(o => o.Id).ToList() };
        if (discover) foreach (var offer in offers) DiscoverRealRules.PrepareDiscover(source, offer);
        return offers;
    }
    internal static Weapon Stone(Controller p)
    {
        var stone = (Weapon)Entity.FromCard(p, Cards.FromId("TLC_460t"), zone: p.SetasideZone);
        AutoPlayRules.Play(stone, p.Hero); return stone;
    }
    private static void Unpinned_KeepAllStoneCharge()
    {
        // Catch missing keep-all charges, per-card charging, and autoplay of retained options.
        foreach (string producer in new[] { "paleomancy", "discover-task", "masters-three", "masters-many", "hero-power" })
        foreach (int hand in new[] { 0, 10 })
        foreach (int charges in new[] { 1, 8 })
        {
            var game = DeathKnightC1Proof.NewGame(1); var p = game.Player1;
            var quest = (Spell)Core2026Proof.Play(game, p, "TLC_460");
            var stone = Stone(p); stone.Damage = 8 - charges;
            Stone(p.Opponent);
            PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, hand);
            B16AutoplayProof.EmptyDeck(p);
            foreach (string id in new[] { "CS2_171", "CS2_172", "CS2_203", "EX1_531" }.Take(producer == "masters-many" ? 4 : 3))
                Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            p[GameTag.CORPSES] = 5;
            var source = producer == "hero-power" ? (IPlayable)p.Hero.HeroPower :
                Entity.FromCard(p, Cards.FromId(producer == "paleomancy" ? "TLC_434" : "TRL_339"), zone: p.SetasideZone);
            foreach (var next in B16AutoplayProof.Futures(game))
            {
                var c = next.Player1; var effect = next.IdEntityDic[source.Id];
                if (producer == "paleomancy") PoolPilotProof.Resolve(next, effect);
                else if (producer.StartsWith("masters-"))
                    SabberStoneCore.Tasks.SpecificTask.MastersCall.Process(next, c, effect, null);
                else new SabberStoneCore.Tasks.SimpleTasks.DiscoverTask(CardType.MINION, CardClass.DRUID, keepAllCondition: cards => true)
                    .Process(next, c, effect, null);
                next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
                var weapon = (Weapon)next.IdEntityDic[stone.Id];
                int expected = charges - (producer == "hero-power" ? 0 : 1);
                L.Check(weapon.Durability == expected && (expected == 0 ? c.Hero.Weapon == null : c.Hero.Weapon == weapon),
                    producer + ": one eligible keep-all Discover consumes exactly one Stone charge");
                L.Check(c.Opponent.Hero.Weapon.Durability == 8, "opponent Stone does not charge");
                L.Check(next.HistoryFacts.Query.Discoveries().Single().KeepAll && ((Spell)next.IdEntityDic[quest.Id]).QuestProgress == 3,
                    "one keep-all completion still advances quest by three cards");
                L.Check(c.HandZone.Count == (hand == 0 ? 3 : 10) && c.BoardZone.Count == 0
                    && PayloadRules.Entries(c, 118191).Length == 0 && c.Choice == null && !next.Resolution.HasSuspendedEffect,
                    "retained cards use hand capacity without autoplay or pending work");
                B16AutoplayProof.AssertSaved(next);
            }
        }
    }
    internal static BoardObject Composite(Controller p, int tier, int first, int second)
    {
        var card = CompositeLocationRules.Compose(Cards.FromId("TLC_100t" + tier), Cards.FromId("TLC_100t" + tier + first), Cards.FromId("TLC_100t" + tier + second));
        return (BoardObject)Entity.FromCard(p, card, zone: p.BoardZone);
    }
    internal static void Unpinned_StoneCopies()
    {
        foreach (int seed in new[] { 1, 8, 19 })
        {
            var g = L.Game(seed); var p = g.Player1;
            var stone = Stone(p);
            var source = Entity.FromCard(p, Cards.FromId("CS2_023"), zone: p.SetasideZone);
            var offers = Offers(p, source, true, "CS2_182", "CS2_029", "CS2_189");
            var rng = g.Random.Clone();
            var spell = (Spell)offers[1];
            var targets = spell.ValidPlayTargets.Where(spell.IsValidPlayTarget).ToArray();
            int expectedTarget = targets[rng.Next(targets.Length)].Id;
            foreach (var next in B16AutoplayProof.Futures(g))
            {
                B16AutoplayProof.Answer(next, offers[0].Id);
                L.Check(((ICharacter)next.IdEntityDic[expectedTarget]).Damage == 6 && next.Random.CaptureState().Equals(rng.CaptureState()), "seeded target order and exact RNG consumption");
                var c = next.Player1;
                L.Check(c.Hero.Weapon.Durability == 7 && c.BoardZone.Concat(c.GraveyardZone).Any(m => m.Card.Id == "CS2_189"), "Stone plays minion copy and spends one charge");
                L.Check(next.IdEntityDic[offers[1].Id].Zone == c.SetasideZone && next.IdEntityDic[offers[2].Id].Zone == c.SetasideZone, "rejected originals never consumed");
                L.Check(c.Hero.Damage + c.Opponent.Hero.Damage + c.BoardZone.Sum(m => m.Damage) == 6, "automatic spell resolves; minion uses the shared summon-without-Battlecry policy");
                L.Check(c.Choice == null && !next.Resolution.HasSuspendedEffect, "autoplay cannot stall a decision");
                B16AutoplayProof.AssertSaved(next);
            }
        }
        var hpGame = L.Game(); var owner = hpGame.Player1; var weapon = Stone(owner);
        var hpOffers = Offers(owner, owner.Hero.HeroPower, true, "CS2_182", "CS2_189");
        B16AutoplayProof.Answer(hpGame, hpOffers[0].Id);
        L.Check(weapon.Durability == 8 && owner.BoardZone.Count == 0, "Hero Power Discover excludes Stone");
        var sourceCard = Entity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.SetasideZone);
        var ordinary = Offers(owner, sourceCard, false, "CS2_182", "CS2_189");
        B16AutoplayProof.Answer(hpGame, ordinary[0].Id);
        L.Check(weapon.Durability == 8 && owner.BoardZone.Count == 0, "lookalike choice excludes Stone");
        while (!owner.BoardZone.IsFull) L.Minion(owner);
        var blocked = Offers(owner, sourceCard, true, "CS2_182", "CS2_189", "CORE_REV_990");
        B16AutoplayProof.Answer(hpGame, blocked[0].Id);
        L.Check(weapon.Durability == 7 && owner.BoardZone.Count == 7, "full-board copies fizzle without replacing originals");
    }
    internal static void Unpinned_NavigatorCompositeActivationOrder()
    {
        foreach (bool reversed in new[] { false, true })
        {
            var g = L.Game(); var p = g.Player1;
            var location = Composite(p, 2, reversed ? 7 : 5, reversed ? 5 : 7);
            L.Check(!LocationRules.CanActivate(p, location, null), "Crystals cannot target a future Raptor");
            var target = L.Minion(p); target.AttackDamage = 9;
            g.DeathProcessingAndAuraUpdate();
            L.Check(!LocationRules.CanActivate(p, location, p.Opponent.Hero), "friendly minion target union");
            foreach (var next in B16AutoplayProof.Futures(g))
            {
                var c = next.Player1; var loc = (BoardObject)next.IdEntityDic[location.Id];
                L.Use(loc, next.IdEntityDic[target.Id]);
                var copies = c.BoardZone.Where(m => m.Id != target.Id && m.Card.Id == target.Card.Id).ToArray();
                L.Check(copies.Length == 1 && copies[0].AttackDamage == 3 && copies[0].Health == 3, "Crystals sets copied state through buff layer");
                L.Check(c.BoardZone.Count(m => m.Card.Id == "TLC_101t") == 2 && loc.Durability == 1, "each component executes once");
                copies[0].Silence();
                L.Check(copies[0].AttackDamage == copies[0].Card.ATK, "Silence removes copy-size enchantment");
                B16AutoplayProof.AssertSaved(next);
            }
        }
        foreach (bool plantsFirst in new[] { false, true })
        {
            var order = L.Game(); var owner = order.Player1;
            var pair = Composite(owner, 2, plantsFirst ? 4 : 5, plantsFirst ? 5 : 4);
            L.Use(pair);
            L.Check(owner.BoardZone.All(m => m.AttackDamage == (plantsFirst ? 2 : 4)), "noncommuting component order: Plants buffs only existing Raptors");
        }
        var full = L.Game(); var cfull = full.Player1;
        var last = Composite(cfull, 1, 5, 1);
        while (!cfull.BoardZone.IsFull) L.Minion(cfull);
        L.Check(!LocationRules.CanActivate(cfull, last, null), "non-final summoning activation blocked at full board");
        last[GameTag.DAMAGE] = 1;
        L.Use(last);
        L.Check(last.Zone == cfull.GraveyardZone && cfull.BoardZone.Count == 7 && cfull.Opponent.Hero.Damage == 1, "last charge frees slot and then Deathrattle");
        var discover = L.Game(); var d = discover.Player1; var ordered = Composite(d, 1, 3, 6);
        L.Use(ordered);
        L.Check(d.Choice != null && d.Hero.Armor == 0 && ordered.Durability == 2, "Discover suspends before later component and charge");
        foreach (var next in B16AutoplayProof.Futures(discover))
        {
            B16AutoplayProof.Answer(next);
            var loc = (BoardObject)next.IdEntityDic[ordered.Id];
            L.Check(next.Player1.Hero.Armor == 3 && loc.Durability == 1 && next.Player1.HandZone.Any(x => x.Cost < x.Card.Cost || x.Card.Cost == 0), "resume once in selected order");
            var copied = Generic.Copy(next.Player1, loc, loc, Zone.HAND);
            L.Check(copied.Card[GameTag.MODULAR_ENTITY_PART_1] == 117779 && copied.Card[GameTag.MODULAR_ENTITY_PART_2] == 117793, "backwards copy preserves immutable recipe");
            B16AutoplayProof.AssertSaved(next);
        }
    }
    private static void Unpinned_ComponentRecipes()
    {
        foreach (bool history in new[] { true, false })
        {
            var g = L.Game(history: history); var p = g.Player1; var body = L.Minion(p);
            var location = Composite(p, 2, 2, 4);
            L.Use(location);
            B16AutoplayProof.AssertSaved(g);
            L.Check(body.AttackDamage == body.Card.ATK + 2 && p.Hero.AttackDamage == 2 && p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_100t22e"), $"Moonlight and Plants magnitudes: body={body.AttackDamage}/{body.Card.ATK} hero={p.Hero.AttackDamage} spellpower={p.CurrentSpellPower}");
            var spell = (Spell)Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.HandZone);
            L.Check(g.Process(PlayCardTask.Any(p, spell, p.Opponent.Hero)), "cast next spell");
            L.Check(p.Opponent.Hero.Damage == 8 && p.CurrentSpellPower == 0, "next-spell bonus transfers and is consumed");
            L.End(g);
            L.Check(body.AttackDamage == body.Card.ATK && p.Hero.AttackDamage == 0, "Plants expires at turn end");
            L.Check(!LocationRules.CanActivate(p, location, null), "opponent cannot activate");
        }
    }
    private static void Unpinned_GiftsDeckAndNestedChoices()
    {
        var g = L.Game(19); var p = g.Player1; Stone(p);
        var source = Entity.FromCard(p, Cards.FromId("CS2_023"), zone: p.SetasideZone);
        var executor = Entity.FromCard(p, Cards.FromId("EDR_102t"), new EntityData { [GameTag.CREATOR] = source.Id }, p.SetasideZone);
        var offers = Offers(p, executor, true, "CS2_182", "CS2_172", "CS2_171");
        p.DeckZone.Add(p.SetasideZone.Remove(offers[1]));
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_477e"), source, offers[1]);
        GiftedDiscoverProof.Pair(g, offers[1], "EDR_100t3");
        GiftedDiscoverProof.Pair(g, offers[2], "EDR_100t");
        g.DeathProcessingAndAuraUpdate();
        foreach (var next in B16AutoplayProof.Futures(g))
        {
            B16AutoplayProof.Answer(next, offers[0].Id);
            var c = next.Player1;
            var copy = c.BoardZone.Single(m => m.Card.Id == "CS2_172");
            L.Check(copy.Id != offers[1].Id && copy.HasTaunt && copy.AttackDamage == copy.Card.ATK + 2 && copy.Health == copy.Card.Health + 6,
                $"full deck payload and assigned Dark Gift reach the played copy: atk={copy.AttackDamage}, hp={copy.Health}, taunt={copy.HasTaunt}, gifts={DarkGifts.GiftsOf(copy).Count}");
            L.Check(next.IdEntityDic[offers[1].Id].Zone == c.DeckZone && !DarkGifts.Has(next.IdEntityDic[offers[1].Id]), "deck original and its ungranted gift remain untouched");
            L.Check(c.BoardZone.Single(m => m.Card.Id == "CS2_171").HasLifeSteal, "second displayed offer retains independent gift");
            B16AutoplayProof.AssertSaved(next);
        }
        var bound = L.Game(); var chooser = bound.Player1; Stone(chooser);
        var rivalOriginal = Entity.FromCard(chooser.Opponent, Cards.FromId("CS2_172"), zone: chooser.Opponent.DeckZone);
        Generic.AddEnchantmentBlock(bound, Cards.FromId("CATA_477e"), chooser.Opponent.Hero, rivalOriginal);
        var boundSource = Entity.FromCard(chooser, Cards.FromId("CS2_023"), zone: chooser.SetasideZone);
        var boundOffers = Offers(chooser, boundSource, true, "CS2_182", "CS2_172");
        boundOffers[1][DiscoverRealRules.BoundEntity] = rivalOriginal.Id;
        foreach (var next in B16AutoplayProof.Futures(bound))
        {
            B16AutoplayProof.Answer(next, boundOffers[0].Id);
            var copy = next.Player1.BoardZone.Single();
            L.Check(copy.Controller == next.Player1 && copy.AttackDamage == copy.Card.ATK + 2
                && next.IdEntityDic[rivalOriginal.Id].Zone == next.Player2.DeckZone && next.Player2.BoardZone.Count == 0,
                "bound opposing-deck offer copies full payload to chooser without moving the original");
            B16AutoplayProof.AssertSaved(next);
        }
        var nested = L.Game(); var owner = nested.Player1; var last = Stone(owner); last.Damage = 7;
        var real = Entity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.SetasideZone);
        var pending = Offers(owner, real, true, "CS2_182", "MEND_307", "CS2_172");
        foreach (var next in B16AutoplayProof.Futures(nested))
        {
            B16AutoplayProof.Answer(next, pending[0].Id);
            L.Check(next.Player1.Hero.Weapon == null && next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect,
                "last-charge nested Discover finishes without reusing Stone");
            L.Check(next.Player1.BoardZone.Count == 2, "nested Discover summon followed by remaining outer copy");
            B16AutoplayProof.AssertSaved(next);
        }
        var queued = GiftedDiscoverProof.New(1, CardClass.MAGE); var q = queued.Player1; var two = Stone(q); two.Damage = 6;
        Core2026Proof.Play(queued, q, "TIME_857");
        B16AutoplayProof.Answer(queued);
        L.Check(q.Choice != null && q.Hero.Weapon.Durability == 1 && PayloadRules.Entries(q, 118191).Length == 2,
            "first of two Discovers retains full pending copy queue at a saved decision");
        foreach (var next in B16AutoplayProof.Futures(queued))
        {
            B16AutoplayProof.Answer(next);
            L.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && PayloadRules.Entries(next.Player1, 118191).Length == 0,
                "cloned/restored second Discover drains queued copies exactly once");
            B16AutoplayProof.AssertSaved(next);
        }
        var mixed = L.Game(); var m = mixed.Player1; Stone(m);
        var trigger = Entity.FromCard(m, Cards.FromId("CS2_023"), zone: m.SetasideZone);
        var pair = Offers(m, trigger, true, "CS2_182", "CS2_106", "CORE_REV_990");
        B16AutoplayProof.Answer(mixed, pair[0].Id);
        L.Check(m.Hero.Weapon.Card.Id == "CS2_106" && m.BoardZone.GetLocations().Single().Card.Id == "CORE_REV_990", "weapon replacement cannot cancel later captured location");
    }
    private static void Unpinned_CommittedTargetAndRewind()
    {
        var g = L.Game(); var p = g.Player1; var target = L.Minion(p);
        var location = Composite(p, 2, 3, 7);
        L.Use(location, target);
        target.Destroy(); g.DeathProcessingAndAuraUpdate();
        while (!p.HandZone.IsFull) Core2026Proof.Add(p, "CS2_182");
        foreach (var next in B16AutoplayProof.Futures(g))
        {
            B16AutoplayProof.Answer(next);
            L.Check(next.Player1.BoardZone.Count == 0 && next.Player1.HandZone.Count == 10
                && ((BoardObject)next.IdEntityDic[location.Id]).Durability == 1, "lost committed target skips copy; full-hand Discover burns; charge completes");
        }
        var rewind = L.Game(); var c = rewind.Player1;
        var recipe = Composite(c, 2, 2, 6); L.Use(recipe);
        var stone = Stone(c); int armor = c.Hero.Armor;
        Core2026Proof.Play(rewind, c, "TIME_001");
        L.Check(rewind.Rewind != null, "Rewind starts with composed definition and Moonlight state");
        var decision = rewind.GetPendingDecisions().Single();
        L.Check(rewind.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { rewind.Rewind.RewindEntityId }), out var error), "rewind composed state: " + error);
        var restored = (BoardObject)rewind.IdEntityDic[recipe.Id];
        L.Check(restored.Durability == 1 && restored.Card[GameTag.MODULAR_ENTITY_PART_1] == 117778
            && rewind.Player1.Hero.Armor == armor && rewind.Player1.Hero.Weapon.Durability == 8,
            "Rewind retains location recipe/readiness and Stone; retries consume restored Moonlight once");
        if (rewind.Rewind != null)
        {
            decision = rewind.GetPendingDecisions().Single();
            L.Check(rewind.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
                new[] { rewind.Rewind.KeepEntityId }), out error), "keep after rewind");
        }
        B16AutoplayProof.AssertSaved(rewind);
    }

    private static void DeferredDeathBeforeSandsRewind()
    {
        foreach (string lethal in new[] { "BOT_453", "CORE_CS2_076" }) DeferredDeathBeforeSandsRewind(lethal);
    }
    // Live incident 9747d030: a mis-shaped achievement fact made every Sands Keep throw. Gameplay must continue and the
    // match's achievement evidence becomes invalid; external snapshot loads still fail closed.
    private static void UnexpectedEvidenceShapeDuringSands()
    {
        foreach (string forge in new[] { "death-cast-link", "damage-shape" })
        foreach (bool retry in new[] { false, true })
        {
            // This death-evidence regression needs stable seeded Discover contents, not the expanding release pool.
            var game = PinnedPoolFixture.Use(L.Game()); var owner = game.Player1; Stone(owner);
            var victim = L.Minion(owner.Opponent); victim.Damage = victim.Health - 1;
            var source = Entity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.SetasideZone);
            var offers = Offers(owner, source, true, "CS2_182", "BOT_453", "GAME_005");
            B16AutoplayProof.Answer(game, offers[0].Id);
            var death = game.HistoryFacts.Query.Deaths().Single(d => d.EntityId == victim.Id);
            if (forge == "death-cast-link")
            {
                var unrelated = game.HistoryFacts.Query.Casts().Last(c => c.Outcome == HistoryCastOutcome.Completed && c.CastId != death.CastId);
                typeof(MinionDiedFact).GetField("<CastId>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(death, unrelated.CastId);
            }
            else
            {
                var fatal = game.HistoryFacts.Records.OfType<DamageResolvedHistoryFact>().Single(d => d.Sequence == death.FatalDamageSequence);
                typeof(DamageResolvedHistoryFact).GetField("<ResolutionId>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(fatal, -1L);
            }
            bool strictRejects = false;
            try { Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools); }
            catch (System.Runtime.Serialization.SerializationException) { strictRejects = true; }
            L.Check(strictRejects && game.HistoryFacts.EvidenceValid, forge + ": external snapshot load still rejects the forged evidence; saving does not judge it");
            int stone = owner.Hero.Weapon.Durability;
            Core2026Proof.Play(game, owner, "TIME_EVENT_999");
            B16AutoplayProof.Answer(game);
            L.Check(game.Rewind != null, forge + ": Sands offers Rewind over a mis-shaped evidence baseline");
            B16AutoplayProof.Answer(game, retry ? game.Rewind.RewindEntityId : game.Rewind.KeepEntityId);
            if (retry) B16AutoplayProof.Answer(game);
            L.Check(game.Rewind == null && game.Player1.Choice == null && game.Player2.Choice == null && game.State == State.RUNNING
                && game.Player1.Hero.Weapon.Durability == stone - 1,
                forge + (retry ? " retry" : " keep") + ": Sands answer clears the choice and the game continues");
            L.Check(!game.HistoryFacts.EvidenceValid && game.HistoryFacts.EvidenceFailure != null,
                forge + (retry ? " retry" : " keep") + ": match achievement evidence is marked invalid");
            int mover = game.CurrentPlayer.Id;
            Core2026Proof.End(game);
            L.Check(game.CurrentPlayer.Id != mover && game.State == State.RUNNING && !game.HistoryFacts.EvidenceValid,
                forge + ": play continues and the verdict is sticky (" + game.CurrentPlayer.Id + "/" + mover + "/" + game.State + "/" + game.HistoryFacts.EvidenceValid + ")");
        }
    }
    private static void DeferredDeathBeforeSandsRewind(string lethal)
    {
        // Keep the exact Stone-charge oracle independent of release pool admissions.
        var game = PinnedPoolFixture.Use(L.Game()); var owner = game.Player1; Stone(owner);
        var victim = L.Minion(owner.Opponent); victim.Damage = victim.Health - 1;
        var source = Entity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.SetasideZone);
        var offers = Offers(owner, source, true, "CS2_182", lethal, "GAME_005");
        B16AutoplayProof.Answer(game, offers[0].Id);
        var death = game.HistoryFacts.Query.Deaths().Single(d => d.EntityId == victim.Id);
        var end = game.HistoryFacts.Query.Casts().Single(c => c.CastId == death.CastId && c.Outcome == HistoryCastOutcome.Completed);
        L.Check(end.Sequence < death.Sequence && (lethal == "BOT_453" ? death.FatalDamageSequence > 0 && death.FatalDamageSequence < end.Sequence : death.Cause == HistoryDeathCause.Destroy && death.FatalDamageSequence == 0),
            "Stone lethal hit belongs to the completed cast before deferred death cleanup");
        B16AutoplayProof.AssertSaved(game);
        var castField = typeof(MinionDiedFact).GetField("<CastId>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var sourceField = typeof(MinionDiedFact).GetField("<SourceId>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var unrelatedCast = game.HistoryFacts.Query.Casts().Last(c => c.Outcome == HistoryCastOutcome.Completed && c.CastId != death.CastId);
        foreach (bool forgeSource in new[] { false, true })
        {
            bool rejected = false; int originalSource = death.SourceId;
            try
            {
                if (forgeSource) sourceField.SetValue(death, owner.Hero.Id);
                else castField.SetValue(death, unrelatedCast.CastId);
                try { Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools); }
                catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
            }
            finally { castField.SetValue(death, end.CastId); sourceField.SetValue(death, originalSource); }
            L.Check(rejected, "deferred death still rejects a forged source or cast link");
        }
        foreach (var future in B16AutoplayProof.Futures(game))
        {
            Core2026Proof.Play(future, future.Player1, "TIME_EVENT_999");
            B16AutoplayProof.Answer(future);
            L.Check(future.Rewind != null, "Sands settles Discover and Stone copies before Rewind");
            foreach (bool retry in new[] { false, true })
            {
                var next = Game.LoadSnapshotWithPools(future.SaveSnapshot(), future.OfflinePools);
                B16AutoplayProof.Answer(next, retry ? next.Rewind.RewindEntityId : next.Rewind.KeepEntityId);
                if (retry) B16AutoplayProof.Answer(next);
                L.Check(next.Rewind == null && next.Player1.Choice == null && next.Player1.Hero.Weapon.Durability == 6,
                    "Sands Keep/retry commits exactly one Discover and Stone charge after an earlier lethal cast: "
                    + lethal + "/retry=" + retry + "/rewind=" + (next.Rewind != null)
                    + "/choice=" + (next.Player1.Choice != null) + "/durability=" + next.Player1.Hero.Weapon?.Durability);
                B16AutoplayProof.AssertSaved(next);
            }
        }
    }

}
