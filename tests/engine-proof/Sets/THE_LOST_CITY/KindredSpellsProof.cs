using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KindredSpellsProof
{
    // Behavioural leans, not retail pins: data/rules-evidence/kindred/kindred-{card,turn-window,school-match}.yaml.
    private static void EmptyDeck(Controller p)
    {
        foreach (var entity in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(entity));
    }
    private static void EmptyHand(Controller p)
    {
        foreach (var entity in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(entity));
    }
    private static IPlayable Deck(Controller p, string id) => Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
    private static void NextOwnTurn(Game g) { End(g); End(g); }
    private static void Resolve(Game g, Controller p, string id)
    {
        var source = Entity.FromCard(p, Cards.FromId(id));
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
    }

    private static void Counter(Game g, Controller p, string id, ICharacter target = null)
    {
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, health = p.Hero.Health, enemyHealth = p.Opponent.Hero.Health;
        var board = p.BoardZone.Slots.Concat(p.Opponent.BoardZone.Slots).Select(x => x.Id).ToArray();
        var source = (Spell)Add(p, id);
        TriggerManager.TriggerHandler intercept = entity => { if (entity.Id == source.Id) ((Spell)entity).IsCountered = true; };
        g.TriggerManager.CastSpellTrigger += intercept;
        PlayEntity(g, p, source, target);
        g.TriggerManager.CastSpellTrigger -= intercept;
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.HandZone.Count == hand && p.DeckZone.Count == deck
            && p.Hero.Health == health && p.Opponent.Hero.Health == enemyHealth
            && p.BoardZone.Slots.Concat(p.Opponent.BoardZone.Slots).Select(x => x.Id).SequenceEqual(board), "Lean_: counter prevents all spell effects");
    }

    internal static void Run()
    {
        KindredRulesProof.Run();
        StandardSetsProof.Case("TLC_236", Lean_HybridizationCapturedDraws);
        StandardSetsProof.Case("TLC_440", Lean_CryosleepOrderAndTargets);
        StandardSetsProof.Case("TLC_447", Lean_CausticFumesDestroyThenArea);
        StandardSetsProof.Case("TLC_519", Lean_AmbushSpitter);
        StandardSetsProof.Case("TLC_815", Lean_VoidbulbIndependentSummons);
        StandardSetsProof.Case("TLC_816", Lean_SunbloomAdaptiveCost);
    }

    // data/rules-evidence/kindred/kindred-hybridization.yaml (supports lean).
    private static void Lean_HybridizationCapturedDraws(Game g, Controller p, string id)
    {
        Counter(g, p, id);
        EmptyDeck(p);
        var old = Add(p, "CS2_182");
        var ids = new[] { "CS2_171", "CS2_120", "CS2_125", "CS2_182" };
        var draws = ids.Select(x => Deck(p, x)).ToArray();
        var spell = Deck(p, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(draws.All(x => x.Zone == p.HandZone) && draws.Select(x => x.Cost).SequenceEqual(new[] { 1, 2, 3, 4 }), "Lean_: unmatched draws all four in cost order without discount");
        Core2026Proof.Check(p.HandZone.Skip(1).Select(x => x.Id).SequenceEqual(draws.Select(x => x.Id)) && spell.Zone == p.DeckZone, "Lean_: tutor order and minion filter");
        NextOwnTurn(g); EmptyHand(p); EmptyDeck(p);
        old = Add(p, "CS2_182");
        draws = ids.Select(x => Deck(p, x)).ToArray();
        var clone = g.CloneExact(history: true);
        Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(draws.All(x => x.Zone == p.HandZone) && draws.Select(x => x.Cost).SequenceEqual(new[] { 0, 1, 2, 3 }) && old.Cost == 4, "Lean_: only captured draws discounted with visible zero floor");
        Core2026Proof.Check(draws.All(x => x[GameTag.COST] == x.Cost) && p.HandZone.Select(x => x.Card.Id).SequenceEqual(clone.Player1.HandZone.Select(x => x.Card.Id)), "Lean_: visible costs and seeded clone agree");
        EmptyHand(p); EmptyDeck(p);
        var modified = (Playable)Deck(p, "CS2_182"); CostRules.AddModifier(modified, EffectOperator.SET, 1);
        var other = Deck(p, "CS2_171");
        var selection = g.CloneExact(); int expectedId = new IPlayable[] { modified, other }[selection.Random.Next(2)].Id;
        Play(g, p, id);
        Core2026Proof.Check(new[] { modified, other }.Count(x => x.Zone == p.HandZone) == 1 && p.DeckZone.Count == 1 && p.HandZone[0].Cost == 0 && p.HandZone[0].Id == expectedId, "Lean_: current cost buckets, seeded duplicate selection, missing buckets skipped");
        EmptyHand(p); EmptyDeck(p); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        draws = ids.Select(x => Deck(p, x)).ToArray(); Resolve(g, p, id);
        Core2026Proof.Check(draws.All(x => x.Zone == p.GraveyardZone && x.Cost == x.Card.Cost) && p.HandZone.All(x => x.Cost == 4), "Lean_: full-hand burns never discounted or applied to old cards");
        EmptyHand(p); while (p.HandZone.Count < 8) Add(p, "CS2_182");
        draws = ids.Select(x => Deck(p, x)).ToArray(); Resolve(g, p, id);
        Core2026Proof.Check(draws.Take(2).All(x => x.Zone == p.HandZone) && draws.Take(2).Select(x => x.Cost).SequenceEqual(new[] { 0, 1 })
            && draws.Skip(2).All(x => x.Zone == p.GraveyardZone && x.Cost == x.Card.Cost), "Lean_: partial hand discounts successful draws only");
        EmptyHand(p); draws = ids.Select(x => Deck(p, x)).ToArray();
        IPlayable replacement = null;
        TriggerManager.TriggerHandler replaceDraw = entity => {
            if (entity.Id != draws[1].Id) return;
            p.SetasideZone.Add(p.HandZone.Remove(draws[0]));
            replacement = Add(p, "CS2_182");
        };
        g.TriggerManager.DrawTrigger += replaceDraw;
        Play(g, p, id);
        g.TriggerManager.DrawTrigger -= replaceDraw;
        Core2026Proof.Check(draws[0].Zone == p.SetasideZone && draws[0].Cost == 1 && replacement.Cost == 4
            && draws.Skip(1).Select(x => x.Cost).SequenceEqual(new[] { 1, 2, 3 }), "Lean_: draw-trigger replacement never discounted; captured card leaving hand excluded");
    }

    // data/rules-evidence/kindred/kindred-cryosleep.yaml (supports lean).
    private static void Lean_CryosleepOrderAndTargets(Game g, Controller p, string id)
    {
        Counter(g, p, id, p.Opponent.Hero);
        var target = Summon(p.Opponent, "CS2_200");
        var source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        var targets = p.Options().Where(x => x.Source?.Id == source.Id).Select(x => x.Target?.Id).ToArray();
        target.HasStealth = true;
        Core2026Proof.Check(!p.Options().Any(x => x.Source?.Id == source.Id && x.Target?.Id == target.Id), "Lean_: enemy Stealth blocks spell targeting");
        target.HasStealth = false;
        Core2026Proof.Check(targets.Contains(p.Hero.Id) && targets.Contains(p.Opponent.Hero.Id) && targets.Contains(target.Id), "Lean_: any character target");
        int draws = 0;
        TriggerManager.TriggerHandler damageBeforeDraw = entity => {
            if (entity.Controller != p) return;
            Core2026Proof.Check(target.Damage == 4, "Lean_: damage already applied inside first draw callback");
            draws++;
        };
        g.TriggerManager.DrawTrigger += damageBeforeDraw;
        try { PlayEntity(g, p, source, target); }
        finally { g.TriggerManager.DrawTrigger -= damageBeforeDraw; }
        Core2026Proof.Check(target.Damage == 4 && p.HandZone.Count == 1 && draws == 1, "Lean_: unmatched damage four and one draw");
        NextOwnTurn(g); EmptyHand(p); ClearBoard(g, p.Opponent); Summon(p, "EX1_012");
        int health = p.Hero.Health; Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Health == health - 5 && p.HandZone.Count == 2, "Lean_: friendly hero, Spell Damage once, matched two draws");
        EmptyHand(p); while (p.HandZone.Count < 9) Add(p, "CS2_182");
        int deck = p.DeckZone.Count; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.IsFull && p.DeckZone.Count == deck - 2, "Lean_: matched draw burns only overflow");
        EmptyHand(p); EmptyDeck(p); int fatigue = p.Hero.Fatigue; health = p.Hero.Health;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Fatigue == fatigue + 2 && p.Hero.Health == health - (fatigue + 1) - (fatigue + 2), "Lean_: matched empty deck causes two ordered fatigue draws");
    }

    // data/rules-evidence/kindred/kindred-caustic-fumes.yaml (supports lean).
    private static void Lean_CausticFumesDestroyThenArea(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_200");
        Counter(g, p, id, enemy);
        TargetRules(g, p, id, enemy, friend, true); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && friend.Damage == 0, "Lean_: unmatched destroy only");
        NextOwnTurn(g); EmptyHand(p);
        enemy = Summon(p.Opponent, "CS2_200"); var deathrattle = Summon(p.Opponent, "FP1_002");
        int health = p.Hero.Health, enemyHealth = p.Opponent.Hero.Health;
        Play(g, p, id, deathrattle);
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2 && p.Hero.Health == health && p.Opponent.Hero.Health == enemyHealth, "Lean_: bonus hits both minion boards, no heroes");
        Core2026Proof.Check(deathrattle.Zone == p.Opponent.GraveyardZone && p.Opponent.BoardZone.Count(x => x.Card.Id == "FP1_002t" && x.Health == 1) == 2, "Lean_: established scheduler resolves destruction Deathrattle after area; retail order unpinned");
        ClearBoard(g, p); var spellDamage = Summon(p, "EX1_012");
        friend = Summon(p, "CS2_182"); enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id, enemy);
        Core2026Proof.Check(spellDamage.Zone == p.GraveyardZone && friend.Zone == p.BoardZone && friend.Damage == 3 && friend.Health == 2,
            "Lean_: Spell Damage makes area exactly three on surviving minion");
        ClearBoard(g, p.Opponent);
        var cast = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(x => x.Source?.Id == cast.Id), "Lean_: empty enemy board forbids play");
    }

    // data/rules-evidence/kindred/kindred-ambush.yaml (supports lean).
    private static void Lean_AmbushSpitter(Game g, Controller p, string id)
    {
        Counter(g, p, id);
        Play(g, p, id); var token = p.BoardZone.Single();
        Core2026Proof.Check(token.Card.Id == "TLC_519t" && token.Card.AssetId == 119664 && token.AttackDamage == 1 && token.Health == 1 && token.Cost == 2 && token.IsRace(Race.BEAST) && token.HasStealth && token.Poisonous, "Lean_: exact 1/1 Beast, Stealth and Poisonous");
        Core2026Proof.Check(token.Card[(GameTag)4028] == 0 && token.Card.Text == "<b>Stealth</b>\n <b>Poisonous</b>", "Lean_: pinned token text, no Kindred");
        token.Silence(); Core2026Proof.Check(!token.HasStealth && !token.Poisonous, "Lean_: Silence clears token keywords");
        NextOwnTurn(g); ParkBoard(p); EmptyHand(p);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Slots.Select(x => x.Card.Id).SequenceEqual(new[] { location.Card.Id, left.Card.Id, "TLC_519t", "TLC_519t" }), "Lean_: match appends two tokens after physical slots");
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182"); int before = p.BoardZone.Count(x => x.Card.Id == "TLC_519t");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.IsFull && p.BoardZone.Count(x => x.Card.Id == "TLC_519t") == before + 1, "Lean_: one free physical slot truncates second summon");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "Lean_: full board remains seven");
    }

    // data/rules-evidence/kindred/kindred-voidbulb.yaml (supports lean).
    private static void Lean_VoidbulbIndependentSummons(Game g, Controller p, string id)
    {
        var requirement = OfflinePoolDeclarations.Cards.Single(x => x.CardId == id).Pools.Single();
        Core2026Proof.Check(requirement.ClassDomain.SequenceEqual(new[] { "PRIEST" }), "Lean_: pool declaration preserves printed Priest identity");
        Core2026Proof.Check(requirement.Filter == OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4),
            "Lean_: Any-class exact-four minion filter has no class, race or keyword restriction");
        // ClassDomain is printed identity; cross-class runtime coverage still needs all eleven controllers.
        var counts = PoolRequirement.PlayableClasses.Select(c => OfflineCardPools.Candidates(g, PoolRequirement.HeroClass(c), requirement.Filter, 117316).Count).ToArray();
        Core2026Proof.Check(counts.All(n => n >= 3), "Lean_: minimum admitted candidates for every class");
        Console.WriteLine("kindred-pool: Any MINION cost=4; classes=" + counts.Length + "; min=" + counts.Min());
        KnownMetaProofFixture.UseBlackEmpire(g);
        var enchantment = Cards.FromId("TLC_815e");
        Core2026Proof.Check(enchantment.Implemented && enchantment.AssetId == 117550 && enchantment.Type == CardType.ENCHANTMENT
            && enchantment.Text == "<b>Taunt</b>." && enchantment.Cost == 0, "Lean_: pinned Void Touched enchantment");
        Counter(g, p, id);
        var first = Play(g, p, id); var minion = p.BoardZone.Single();
        Batch9PoolProof.Generated(g, first, new[] { minion }, 1);
        Core2026Proof.Check(minion.Card.Type == CardType.MINION && minion.Card.Cost == 4 && minion.HasTaunt && minion.AppliedEnchantments.Any(x => x.Card.Id == "TLC_815e"), "Lean_: admitted exact-four minion receives exact enchantment");
        minion.Silence(); Core2026Proof.Check(!minion.HasTaunt && minion.AppliedEnchantments.All(x => x.Card.Id != "TLC_815e"), "Lean_: Silence removes Taunt attachment");
        NextOwnTurn(g); ParkBoard(p); EmptyHand(p);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var clone = g.CloneExact(history: true);
        var filter = Batch9PoolProof.Filter(id);
        var expected = g.CloneExact();
        var rolls = Enumerable.Range(0, 2).Select(_ => OfflineCardPools.Pick(expected, filter, p.HeroClass, 117316).Id).ToArray();
        long beforeRandom = g.Random.DrawCount;
        var castSource = Play(g, p, id); Play(clone, clone.Player1, id);
        var outputs = p.BoardZone.Where(x => x != left).ToArray();
        Batch9PoolProof.Generated(g, castSource, outputs, 2);
        Core2026Proof.Check(outputs.Select(x => x.Card.Id).SequenceEqual(rolls) && g.Random.DrawCount - beforeRandom == 2, "Lean_: two independent RNG draws, not one duplicated roll");
        Core2026Proof.Check(outputs.Length == 2 && outputs.All(x => x.Card.Cost == 4 && x.HasTaunt && x.AppliedEnchantments.Any(e => e.Card.Id == "TLC_815e")), "Lean_: two independent admitted rolls with Taunt");
        Core2026Proof.Check(p.BoardZone.Slots.Select(x => x.Card.Id).SequenceEqual(clone.Player1.BoardZone.Slots.Select(x => x.Card.Id)) && p.BoardZone.GetSlot(0).Id == location.Id && p.BoardZone.GetSlot(1).Id == left.Id, "Lean_: cloned RNG and far-right physical placement: " + string.Join(",", p.BoardZone.Slots.Select(x => x.Card.Id)) + " / " + string.Join(",", clone.Player1.BoardZone.Slots.Select(x => x.Card.Id)));
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.IsFull && p.BoardZone.GetSlot(6).AppliedEnchantments.Any(x => x.Card.Id == "TLC_815e"), "Lean_: only remaining slot summoned and enchanted");
        int count = g.IdEntityDic.Count; beforeRandom = g.Random.DrawCount; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.IdEntityDic.Count == count + 1 && g.Random.DrawCount == beforeRandom, "Lean_: full board makes no minion, orphan enchantment or random roll");
        ParkBoard(p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            long random = g.Random.DrawCount; Resolve(g, p, id);
            Core2026Proof.Check(p.BoardZone.SlotCount == 0 && g.Random.DrawCount == random, "Lean_: empty pool creates nothing and consumes no RNG");
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            var spell = Entity.FromCard(p, Cards.FromId(id));
            spell.Card.Power.PowerTask.Process(g, p, spell, null, new TaskStack()); g.DeathProcessingAndAuraUpdate();
            Batch9PoolProof.Generated(g, spell, p.BoardZone, 2);
            Core2026Proof.Check(p.BoardZone.Select(x => x.Card.Id).Distinct().Count() == 1 && p.BoardZone.All(x => x.HasTaunt), "Lean_: singleton pool independently creates two entities, repeats allowed");
        });
    }

    // data/rules-evidence/kindred/kindred-sunbloom-cost.yaml (supports lean).
    private static void Lean_SunbloomAdaptiveCost(Game g, Controller p, string id)
    {
        Counter(g, p, id);
        var source = Add(p, id); Core2026Proof.Check(source.Cost == 4 && source[GameTag.COST] == 4, "Lean_: unmatched visible cost four");
        p.BaseMana = 0; p.UsedMana = 0; int deck = p.DeckZone.Count;
        Core2026Proof.Check(!CostRules.CanPay(p, CostRules.Quote(p, source)) && !Generic.PayPhase(p, source) && source.Zone == p.HandZone && p.DeckZone.Count == deck && p.UsedMana == 0, "Lean_: failed payment leaves hand and deck intact");
        var holyTarget = Summon(p, "CS2_182"); Play(g, p, "CORE_CS2_004", holyTarget);
        Core2026Proof.Check(source.Cost == 4, "Lean_: current turn not previous turn");
        NextOwnTurn(g); Core2026Proof.Check(source.Cost == 2 && source[GameTag.COST] == 2, "Lean_: previous Holy visible cost two");
        var generated = Add(p, id); Core2026Proof.Check(generated.Cost == 2, "Lean_: newly generated card immediately discounted");
        var drawn = Deck(p, id); Generic.Draw(p, drawn);
        var copied = Generic.Copy(p, p.Hero, source, Zone.HAND);
        Core2026Proof.Check(drawn.Zone == p.HandZone && drawn.Cost == 2 && copied.Cost == 2 && copied[GameTag.COST] == 2, "Lean_: newly drawn and copied Sunblooms immediately discounted");
        CostRules.AddModifier((Playable)generated, EffectOperator.SUB, 3); Core2026Proof.Check(generated.Cost == 0, "Lean_: external reduction and zero floor");
        var clone = g.CloneExact(history: true); int hand = p.HandZone.Count;
        PlayEntity(g, p, source); Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.UsedMana == 2, "Lean_: two draws and quoted cost paid");
        Core2026Proof.Check(clone.IdEntityDic[source.Id].Zone == clone.Player1.HandZone && clone.IdEntityDic[source.Id].Cost == 2, "Lean_: clone isolated");
        NextOwnTurn(g); NextOwnTurn(g); Core2026Proof.Check(generated.Cost == 1 && drawn.Cost == 4 && copied.Cost == 4, "Lean_: empty Holy window expires adaptive part only");
        EmptyHand(p); EmptyDeck(p);
        var unmatchedDraws = new[] { Deck(p, "CS2_182"), Deck(p, "CS2_200") };
        Play(g, p, id);
        Core2026Proof.Check(p.UsedMana == 4 && p.DeckZone.IsEmpty && p.HandZone.Count == 2
            && unmatchedDraws.All(x => x.Zone == p.HandZone), "Lean_: unmatched Sunbloom draws both exact cards at full cost with hand space");
    }
}
