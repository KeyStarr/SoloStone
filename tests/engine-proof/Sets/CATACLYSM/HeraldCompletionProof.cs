using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HeraldCompletionProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "Herald completion: " + message);
    internal static void Run()
    {
        PinnedRule_OnyxiaSinestraTierMagnitude();
        StandardSetsProof.Case("CATA_561", Lean_RitualHeraldThenTwoBreezlings);
        StandardSetsProof.Case("CATA_722", Battlecry);
        StandardSetsProof.Case("CATA_780", Battlecry);
        StandardSetsProof.Case("CATA_158", Lean_FollowerDeathSlot);
        StandardSetsProof.Case("CATA_156", Lean_AnimationHeraldThenEnemyDamage);
        StandardSetsProof.Case("CATA_785", Lean_RiteAlwaysHeraldComboAddsDamage);
    }
    // H1: https://hearthstone.wiki.gg/wiki/Herald, coordinator web-findings 2026-10-20:
    // "doubling their stats and effects with two Heralds and quadrupling them after four".
    private static void PinnedRule_OnyxiaSinestraTierMagnitude()
    {
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.ROGUE })
        for (int seed = 1; seed <= 3; seed++)
        for (int prior = 0; prior <= 5; prior++)
        {
            var g = HeraldProof.New(cls, seed); var p = g.Player1;
            PinnedPoolFixture.Use(g);
            Check(p.HeroClass == cls, "tier fixture uses requested hero class");
            p[HeraldRules.Progress] = prior;
            Check(HeraldRules.Class(p) == cls, "DK/Rogue own Soldier class");
            var copy = g.CloneExact(history: true); var restored = Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools);
            var soldier = HeraldProof.Herald(g, p);
            HeraldProof.Herald(copy, copy.Player1); HeraldProof.Herald(restored, restored.Player1);
            int factor = new[] { 1, 1, 2, 2, 4, 4 }[prior];
            Check(soldier.Card.Id == (cls == CardClass.ROGUE ? "CATA_158t" : "CATA_780t")
                && soldier.AttackDamage == factor && soldier.BaseHealth == factor && soldier.Cost == 1, "pinned Soldier identity/body");
            Check(soldier[GameTag.TAG_SCRIPT_DATA_NUM_1] == (cls == CardClass.ROGUE ? factor : 2 * factor)
                && HeraldRules.Count(p) == prior + 1 && HeraldRules.Count(p.Opponent) == 0, "old-tier magnitude, owner-only count");
            Check(p.HandZone.Count == 1, "one generated card per self summon");
            var generated = (Playable)p.HandZone.Single();
            if (cls == CardClass.DEATHKNIGHT)
                Check(generated.Card.Type == CardType.MINION && generated.Card.Cost == 2 * factor
                    && generated.Cost == 2 * factor && CostRules.PaymentKind(generated) == CostPaymentKind.Health, "exact printed cost and Health payment");
            else
                Check(generated.Card.Type == CardType.SPELL && generated.Card.Class != CardClass.NEUTRAL
                    && !CardClassRules.IsAllowedInDeck(generated.Card, p.HeroClass)
                    && generated.Cost == Math.Max(0, generated.Card.Cost - factor), "OtherClass current hero, permanent discount");
            Check(Canonical.State(g) == Canonical.State(copy) && Canonical.State(g) == Canonical.State(restored)
                && g.Random.Next() == copy.Random.Next(), "seeded generation clone/snapshot equivalence");
            soldier.Silence(); Kill(g, soldier);
            Check(p.HandZone.Count == 1 && generated.Cost == Math.Max(0, generated.Card.Cost - (cls == CardClass.ROGUE ? factor : 0)), "source Silence/death never retracts grant");
            End(g);
            Check(CostRules.PaymentKind(generated) == CostPaymentKind.Mana, "Health expires, Rogue stays Mana");
            if (cls == CardClass.ROGUE) Check(generated.Cost == Math.Max(0, generated.Card.Cost - factor), "discount survives turn end");
        }
        Lean_GenerationBoundaries();
        Lean_SoldierLifecycle();
        CrossClassSoldiers();
    }
    // Soldiers summoned by a class with no Soldier of its own (the card's class decides): tiers, generated cards and the
    // current hero class (Priest/Mage/Druid) as the OtherClass/exact-cost reference all work; nothing is stored on the controller.
    private static void CrossClassSoldiers()
    {
        foreach (var cls in new[] { CardClass.PRIEST, CardClass.MAGE, CardClass.DRUID })
        foreach (var (cardId, soldierId) in new[] { ("CATA_780", "CATA_780t"), ("CATA_785", "CATA_158t"),
            ("CATA_525", "CATA_525t"), ("CATA_725", "CATA_725t"), ("CATA_565", "CATA_565t"), ("CATA_580", "CATA_580t") })
        for (int prior = 0; prior <= 5; prior++)
        {
            var g = HeraldProof.New(cls, 1); var p = g.Player1;
            PinnedPoolFixture.Use(g);
            p[HeraldRules.Progress] = prior;
            var source = Entity.FromCard(p, Cards.FromId(cardId), zone: p.SetasideZone);
            var soldier = HeraldProof.Herald(g, p, source);
            int factor = new[] { 1, 1, 2, 2, 4, 4 }[prior];
            Check(soldier != null && soldier.Card.Id == soldierId && soldier[HeraldRules.RealizedFactor] == factor
                && HeraldRules.Count(p) == prior + 1 && p[HeraldRules.Profile] == 0, $"{cls}/{cardId}/{prior}: card-class Soldier at tier factor {factor}, no lock-in");
            var generated = p.HandZone.OfType<Playable>().ToArray();
            if (soldierId == "CATA_780t")
                Check(generated.Length == 1 && generated[0].Card.Type == CardType.MINION && generated[0].Card.Cost == 2 * factor
                    && CostRules.PaymentKind(generated[0]) == CostPaymentKind.Health, $"{cls}: Onyxia exact-cost Health minion");
            if (soldierId == "CATA_158t")
                Check(generated.Length == 1 && generated[0].Card.Type == CardType.SPELL
                    && !CardClassRules.IsAllowedInDeck(generated[0].Card, p.HeroClass)
                    && generated[0].Cost == Math.Max(0, generated[0].Card.Cost - factor), $"{cls}: Sinestra other-class spell with permanent discount");
            soldier.Silence(); Kill(g, soldier); End(g);
        }
    }
    private static void Lean_GenerationBoundaries()
    {
        // Declarations record the printed class domain (OfflinePoolAdmissionProof); runtime coverage of
        // every current hero class is proven below.
        foreach (var (id, printed) in new[] { ("CATA_780t", "DEATHKNIGHT"), ("CATA_158t", "ROGUE") })
        {
            var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id);
            Check(declaration.Pools.All(r => r.ClassDomain.SequenceEqual(new[] { printed })),
                id + " declares its printed class domain");
        }
        foreach (string hero in PoolRequirement.PlayableClasses)
        for (int seed = 1; seed <= 3; seed++)
        {
            var cls = hero == "DEMONHUNTER" ? (CardClass)14 : Enum.Parse<CardClass>(hero);
            var g = HeraldProof.New(cls, seed); var p = g.Player1;
            Check(p.HeroClass == cls, "all-class fixture uses requested hero class");
            // Directly summoned Soldiers work for every current hero, irrespective of selected Herald profile.
            Summon(p, "CATA_158t");
            var card = (Playable)p.HandZone.Single();
            var member = g.OfflinePools.Standard.Single(c => c.CardId == card.Card.Id);
            // Own adaptive costs (e.g. JAIL_514) still apply after Sinestra's discount.
            var baseline = g.CloneExact(history: true); var owner = baseline.Player1;
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone.Single()));
            var undiscounted = Add(owner, card.Card.Id);
            // Shatter inherits the discount on both halves before recombination.
            int expectedCost = Math.Max(0, undiscounted.Cost - (card.Card.Shatter == null ? 1 : 2));
            Check(!member.Classes.Contains(hero) && !member.Classes.Contains("NEUTRAL")
                && !CardClassRules.IsAllowedInDeck(card.Card, p.HeroClass) && card.Card.Type == CardType.SPELL
                && card.Cost == expectedCost, "all-class OtherClass filter and discount");
            var filter = new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.SPELL);
            Check(OfflineCardPools.Candidates(g, p.HeroClass, filter, 127003).Count >= 3, "all current classes have admitted spells");
            foreach (int tier in new[] { 0, 1, 2 })
            {
                int cost = 2 << tier;
                Check(OfflineCardPools.Candidates(g, p.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost), 126983).Count >= 3,
                    "all realized Onyxia costs have admitted minions for every hero");
                p[HeraldRules.Progress] = tier * 2;
                Summon(p, "CATA_780t");
                var minion = (Playable)p.HandZone.Last();
                Check(minion.Card.Type == CardType.MINION && minion.Card.Cost == cost && minion.Cost == cost
                    && CostRules.PaymentKind(minion) == CostPaymentKind.Health, "all-class Onyxia summon generates exact-cost Health minion");
            }
        }
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.ROGUE })
        foreach (int held in new[] { 8, 9, 10 })
        {
            var g = HeraldProof.New(cls); var p = g.Player1;
            for (int i = 0; i < held; i++) Add(p, "CS2_182");
            var m = HeraldProof.Herald(g, p);
            Check(p.HandZone.Count == Math.Min(10, held + 1), "generator hand overflow");
            int before = p.HandZone.Count;
            Summon(p.Opponent, "CS2_182");
            Check(p.HandZone.Count == before, "opponent summon never triggers Soldier");
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            Check(HeraldProof.Herald(g, p) == null && p.HandZone.Count == before && HeraldRules.Count(p) == 2, "full board no generation, still progresses");
        }
        foreach (bool opponentTurn in new[] { false, true })
        {
            var g = HeraldProof.New(CardClass.DEATHKNIGHT); var p = g.Player1;
            if (opponentTurn) End(g);
            var m = HeraldProof.Herald(g, p); var generated = (Playable)p.HandZone.Single();
            p.Hero.Damage = p.Hero.BaseHealth - generated.Cost; p.Hero.Armor = 20;
            Check(!CostRules.CanPay(p, CostRules.Quote(p, generated)), "Health equal cost rejected despite Armor");
            p.Hero.Damage--;
            Check(CostRules.CanPay(p, CostRules.Quote(p, generated)), "Health greater than cost accepted");
            End(g); Check(CostRules.PaymentKind(generated) == CostPaymentKind.Mana, "expiry on either player's turn");
        }
    }
    private static void Lean_SoldierLifecycle()
    {
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.ROGUE })
        {
            var g = HeraldProof.New(cls); var p = g.Player1; p[HeraldRules.Progress] = 4;
            string id = cls == CardClass.ROGUE ? "CATA_158t" : "CATA_780t";
            var held = (Minion)Add(p, id);
            Check(p.HandZone.Count == 1 && p.BoardZone.SlotCount == 0, "hand entry never triggers generation");
            PlayEntity(g, p, held);
            Check(held.AttackDamage == 4 && held.BaseHealth == 4 && p.HandZone.Count == 1, "hand play configures before summon trigger");
            p[HeraldRules.Progress] = 0;
            var copy = (Minion)Generic.Copy(p, held, held, Zone.PLAY); g.DeathProcessingAndAuraUpdate();
            Check(copy.BaseHealth == 4 && copy[HeraldRules.RealizedFactor] == 4 && p.HandZone.Count == 2,
                "copy keeps creation tier and repeats its own summon ability");
            copy.Silence(); var silentCopy = (Minion)Generic.Copy(p, copy, copy, Zone.PLAY); g.DeathProcessingAndAuraUpdate();
            Check(silentCopy.IsSilenced && p.HandZone.Count == 2, "silenced copy generates nothing");
            new ReturnHandTask(EntityType.TARGET).Process(g, p, p.Hero, held, new TaskStack());
            PlayEntity(g, p, held);
            Check(held.BaseHealth == 1 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == (cls == CardClass.ROGUE ? 1 : 2)
                && p.HandZone.Count == 3 && HeraldRules.Count(p) == 0, "bounce/replay realizes current tier without Herald progression");
            Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshot(g.SaveSnapshot())), "generated grants survive lifecycle snapshot");
        }
    }
    private static void Lean_RitualHeraldThenTwoBreezlings(Game g, Controller p, string id)
    {
        p[HeraldRules.Profile] = (int)CardClass.SHAMAN;
        Play(g, p, id);
        Check(HeraldRules.Count(p) == 1 && p.BoardZone.Single().Card.Id == "CATA_565t", "Ritual Herald once");
        Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card.Id == "CATA_561t" && c.Card.Cost == 1
            && c.Card.ATK == 1 && c.Card.Health == 1 && c.Card.Races.Contains(Race.ELEMENTAL) && c.Card[GameTag.RUSH] == 1), "two exact Breezlings in hand");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        var spell = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        int tokens = p.HandZone.Count(c => c.Card.Id == "CATA_561t");
        new PlayTask(PlayType.SPELL).Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable> { spell } });
        Check(HeraldRules.Count(p) == 2 && p.BoardZone.SlotCount == 7 && p.HandZone.Count == 10
            && p.HandZone.Count(c => c.Card.Id == "CATA_561t") == tokens + 1, "full board retains recipe tail; one free hand slot");
        foreach (int held in new[] { 7, 8, 9 })
        {
            var generating = HeraldProof.New(CardClass.ROGUE); var owner = generating.Player1;
            for (int i = 0; i < held; i++) Add(owner, "CS2_182");
            Play(generating, owner, id);
            var rewards = owner.HandZone.Skip(held).ToArray();
            Check(rewards.Length == Math.Min(3, 10 - held) && rewards[0].Card.Type == CardType.SPELL
                && rewards[0].Card.Id != "CATA_561t" && rewards.Skip(1).All(c => c.Card.Id == "CATA_561t"),
                "generating Soldier takes first free hand slot before Breezlings, including overflow");
            var entries = generating.HistoryFacts.Query.HandEntries().Where(f => rewards.Any(c => c.Id == f.EntityId)).ToArray();
            Check(entries.Length == rewards.Length && entries.Select(f => f.EntityId).SequenceEqual(rewards.Select(c => c.Id)),
                "generation history precedes Breezling hand entries");
        }
        Countered(id);
        CrossClassFallback(id);
    }
    private static void Battlecry(Game g, Controller p, string id)
    {
        p[HeraldRules.Profile] = (int)(id == "CATA_780" ? CardClass.DEATHKNIGHT : CardClass.WARRIOR);
        var location = LocationProofs.Location(p); var right = Summon(p, "CS2_182");
        var source = StandardSetsProof.PlayAt(g, p, id, 1);
        Check(HeraldRules.Count(p) == 1 && p.BoardZone.GetSlot(2).Card.Id == (id == "CATA_780" ? "CATA_780t" : "CATA_580t")
            && p.BoardZone.GetSlot(3) == right, "Battlecry physical-slot placement");
        Check(id == "CATA_780" ? source.HasLifeSteal : source.HasTaunt, "printed keyword");
        int before = HeraldRules.Count(p); var summoned = Summon(p, id);
        Check(HeraldRules.Count(p) == before, "summon does not Battlecry");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Check(HeraldRules.Count(p) == before + 1 && p.BoardZone.SlotCount == 7, "full board progresses");
        CrossClassFallback(id);
    }
    private static void Lean_FollowerDeathSlot(Game g, Controller p, string id)
    {
        Minion source = null;
        var board = StandardSetsProof.DeathrattleSlotBoard(p, () => source = StandardSetsProof.PlayAt(g, p, id, 2));
        Check(source.HasStealth && HeraldRules.Count(p) == 0, "Stealth; no Battlecry");
        Kill(g, source);
        var soldier = p.BoardZone.Single(m => m.Card.Id == "CATA_158t");
        StandardSetsProof.CheckDeathrattleSlot(p, board, new[] { soldier }, "Follower dead slot");
        Check(HeraldRules.Count(p) == 1, "death Herald once");
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Check(HeraldRules.Count(p) == 1, "Silence suppresses Deathrattle");
        source = Summon(p, id); var right = Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        Check(p.BoardZone.GetSlot(source.ZonePosition + 1).Card.Id == "CATA_158t"
            && right.ZonePosition == source.ZonePosition + 2, "living Deathrattle summons right");
    }
    private static void Lean_AnimationHeraldThenEnemyDamage(Game g, Controller p, string id)
    {
        p[HeraldRules.Profile] = (int)CardClass.DEATHKNIGHT;
        var ally = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var big = Summon(p.Opponent, "CS2_186"); var location = LocationProofs.Location(p.Opponent);
        var spellPower = Summon(p, "CS2_052");
        Check(p.CurrentSpellPower == 1, "Spell Damage fixture");
        Play(g, p, id);
        Check(enemy.Zone == p.Opponent.GraveyardZone && big.Damage == 5 && ally.Damage == 0
            && p.Opponent.Hero.Damage == 0 && location.Zone == p.Opponent.BoardZone, "five spell damage to enemy minions only");
        var soldier = p.BoardZone.Single(m => m.Card.Id == "CATA_780t");
        var summon = g.HistoryFacts.Query.Summons().Single(f => f.EntityId == soldier.Id);
        var damage = g.HistoryFacts.Query.Damage().Where(f => f.SourceId == p.GraveyardZone.Last(c => c.Card.Id == id).Id).ToArray();
        Check(HeraldRules.Count(p) == 1 && p.HandZone.Single().Card.Type == CardType.MINION
            && damage.Length == 2 && damage.All(f => summon.Sequence < f.Sequence), "Soldier summon history precedes every damage fact");
        var empty = HeraldProof.New(CardClass.DEATHKNIGHT); var owner = empty.Player1;
        while (!owner.BoardZone.IsFull) Summon(owner, "CS2_182");
        var victim = Summon(owner.Opponent, "CS2_186"); victim.HasStealth = true; victim.CantBeTargetedBySpells = true;
        Play(empty, owner, id);
        Check(victim.Damage == 4 && owner.BoardZone.SlotCount == 7 && HeraldRules.Count(owner) == 1, "full board still damages Stealth/Elusive enemies");
        Countered(id);
        CrossClassFallback(id);
    }
    private static void Lean_RiteAlwaysHeraldComboAddsDamage(Game g, Controller p, string id)
    {
        Play(g, p, id);
        Check(HeraldRules.Count(p) == 1 && p.Opponent.Hero.Damage == 0, "first play Herald only");
        Play(g, p, id, p.Opponent.Hero);
        Check(HeraldRules.Count(p) == 2 && p.Opponent.Hero.Damage == 3, "Combo Herald once plus damage");
        foreach (bool combo in new[] { false, true })
        {
            p.IsComboActive = combo;
            var spell = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            new PlayTask(PlayType.SPELL).Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable> { spell } });
            Check(HeraldRules.Count(p) == (combo ? 4 : 3) && p.Opponent.Hero.Damage == 3
                && spell.Zone == p.GraveyardZone, "indirect casts never Combo, no target needed");
        }
        End(g); End(g);
        Play(g, p, id);
        Check(HeraldRules.Count(p) == 5 && p.Opponent.Hero.Damage == 3, "previous turn cannot enable Combo");
        foreach (var kind in new[] { HistoryCastKind.Effect, HistoryCastKind.Drawn, HistoryCastKind.Repeat })
        {
            var autoGame = HeraldProof.New(CardClass.ROGUE); var owner = autoGame.Player1; owner.IsComboActive = true;
            var auto = Entity.FromCard(owner, Cards.FromId(id));
            new EffectContext(autoGame, owner.Id, auto.Id).CastSpell(auto.Id, kind, owner.Opponent.Hero.Id);
            Check(HeraldRules.Count(owner) == 1 && owner.Opponent.Hero.Damage == 0, "explicit indirect target never activates Combo");
        }
        var targeted = HeraldProof.New(CardClass.ROGUE); var player = targeted.Player1;
        Summon(player, "CS2_052"); Play(targeted, player, "CS2_182");
        var friendly = Summon(player, "CS2_186"); var hidden = Summon(player.Opponent, "CS2_182"); hidden.HasStealth = true;
        var elusive = Summon(player.Opponent, "EDR_598"); var rite = Add(player, id);
        var options = player.Options().Where(t => t.Source?.Id == rite.Id).ToArray();
        Check(options.Any(t => t.Target?.Id == friendly.Id) && options.Any(t => t.Target?.Id == player.Hero.Id)
            && !options.Any(t => t.Target?.Id == hidden.Id || t.Target?.Id == elusive.Id), "Combo targets either side, respects Stealth/Elusive");
        PlayEntity(targeted, player, rite, friendly);
        Check(friendly.Damage == 4 && HeraldRules.Count(player) == 1, "Combo damage includes Spell Damage");
        var forced = HeraldProof.New(CardClass.ROGUE); var forcedOwner = forced.Player1; forcedOwner.IsComboActive = true;
        var deckSpell = Entity.FromCard(forcedOwner, Cards.FromId(id), zone: forcedOwner.DeckZone);
        Check(forced.Process(PlayCardTask.Any(forcedOwner, deckSpell, forcedOwner.Opponent.Hero, skipPrePhase: true)), "forced deck-play fixture");
        Check(HeraldRules.Count(forcedOwner) == 1 && forcedOwner.Opponent.Hero.Damage == 0, "non-hand player cast never activates Combo");
        Countered(id);
        CrossClassFallback(id);
    }
    private static void Countered(string id)
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        End(g); Play(g, p.Opponent, "EX1_287"); End(g);
        var spell = (Spell)Play(g, p, id);
        Check(spell.IsCountered && HeraldRules.Count(p) == 0 && p.BoardZone.SlotCount == 0, "countered spell has no Herald or recipe tail");
    }
    // Herald cards are playable by every class (wiki, tier C): a class without a Soldier resolves the source card's own class
    // (b), then the opponent's (c, Mage here: none). Neutral multi-class cards have no (b): no Soldier, no progress (d, unpinned).
    private static void CrossClassFallback(string id)
    {
        var card = Cards.FromId(id); string expected = HeraldRules.Soldier(card.Class);
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST })
        {
            var g = HeraldProof.New(cls); var p = g.Player1;
            if (card.Power.PowerTask == null) { Kill(g, Summon(p, id)); }
            else
            {
                var source = Add(p, id);
                Check(source.IsPlayableByPlayer && g.Process(PlayCardTask.Any(p, source)), $"{cls} plays Herald card {id}");
            }
            Check(HeraldRules.Count(p) == (expected != null ? 1 : 0) && p[HeraldRules.Profile] == 0
                && p.BoardZone.Count(m => m.Card.Id == expected && expected != null) == (expected != null ? 1 : 0),
                $"{cls}/{id}: fallback Soldier {expected ?? "none"}, no lock-in");
        }
    }
}
