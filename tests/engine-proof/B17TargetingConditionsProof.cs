using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B17TargetingConditionsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    [GroupProof("b17-targeting-conditions")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var scenario in new (string Id, Action<Game, Controller, string> Run)[] {
            ("CS3_022", Freebooter), ("TIME_435", Eternus), ("TLC_633", Bugsquasher),
            ("CATA_161", Nightmare), ("CATA_898", Lancer), ("MEND_040", Worm),
            ("CATA_472", Unpinned_Maul), ("JAIL_861", Bribe) })
        {
            try { var g = HeraldProof.New(CardClass.DRUID); scenario.Run(g, g.Player1, scenario.Id); }
            catch (Exception e) { failures.Add(scenario.Id + ": " + e); }
        }
        foreach (var scenario in new Action[] { LocalChooseBoth, Unpinned_MaulSeededSources, Unpinned_MaulPreservesControlExpiry, CapacityAndRewind, DefinitionCompatibility })
            try { scenario(); } catch (Exception e) { failures.Add(scenario.Method.Name + ": " + e); }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS b17 targeting conditions: all eight facility consumers");
    }

    internal static void Freebooter(Game g, Controller p, string id)
    {
        var card = Add(p, id);
        Check(card != null && !card.ValidPlayTargets.Any() && !card.HasAnyValidPlayTargets
            && card.IsValidPlayTarget(null) && !card.IsValidPlayTarget(p.Opponent.Hero), "unarmed has no targets");
        PlayEntity(g, p, card);
        Check(p.Opponent.Hero.Damage == 0, "unarmed battlecry does nothing");
        Play(g, p, "CS2_106"); card = Add(p, id);
        foreach (var next in Copies(g))
        {
            var source = next.IdEntityDic[card.Id]; var owner = source.Controller;
            Check(source.ValidPlayTargets.Contains(owner.Opponent.Hero) && source.HasAnyValidPlayTargets
                && !source.IsValidPlayTarget(null), "armed target list and validation agree");
            PlayEntity(next, owner, source, owner.Opponent.Hero);
            Check(owner.Opponent.Hero.Damage == 2, "armed deals two");
        }
    }

    internal static void Eternus(Game g, Controller p, string id)
    {
        var low = Summon(p.Opponent, "CS2_171"); var high = Summon(p.Opponent, "CS2_182");
        high.Damage = 1;
        var source = (Minion)Add(p, id);
        Check(source != null && source.ValidPlayTargets.Contains(low) && !source.ValidPlayTargets.Contains(high), "source Health bounds targets");
        new AddEnchantmentTask("CS2_009e", EntityType.TARGET).Process(g, p, p.Hero, source, new TaskStack());
        Check(source.Health == 4 && source.ValidPlayTargets.Contains(high), "hand Health buff changes target threshold: " + source.Health);
        Check(p.Options().Any(t => t.Source?.Id == source.Id && t.Target?.Id == high.Id), "options use live source threshold");
        foreach (var next in Copies(g))
        {
            var owner = next.Player1; PlayEntity(next, owner, next.IdEntityDic[source.Id], (Minion)next.IdEntityDic[high.Id]);
            Check(next.IdEntityDic[high.Id].Controller == owner && next.IdEntityDic[low.Id].Controller == owner.Opponent, "only selected eligible enemy changes controller");
        }
    }

    internal static void Bugsquasher(Game g, Controller p, string id)
    {
        var typed = Summon(p.Opponent, "CS2_172"); var plain = Summon(p.Opponent, "CS2_182");
        var own = Summon(p, "CS2_172"); var source = Add(p, id);
        Check(source != null && source.ValidPlayTargets.Select(t => t.Id).SequenceEqual(new[] { typed.Id }), "only typed enemy target");
        Check(!source.IsValidPlayTarget(plain) && !source.IsValidPlayTarget(own) && !source.IsValidPlayTarget(p.Opponent.Hero), "reject plain/friendly/hero targets");
        PlayEntity(g, p, source, typed);
        Check(typed.Zone.Type == Zone.GRAVEYARD && plain.Damage == 0, "six damage kills typed target only");
    }

    internal static void Nightmare(Game g, Controller p, string id)
    {
        var hand = (Minion)Add(p, "CS2_182"); var spell = Add(p, "GAME_005");
        var board = Summon(p, "CS2_171"); var enemy = Summon(p.Opponent, "CS2_172");
        var source = Play(g, p, id);
        Check(p.Choice != null && p.Choice.Choices.Contains(hand.Id) && p.Choice.Choices.Contains(board.Id)
            && !p.Choice.Choices.Contains(spell.Id) && !p.Choice.Choices.Contains(enemy.Id), "hand and board minions share one choice");
        foreach (var next in Copies(g))
        {
            PlayerDecisionProof.Answer(next, hand.Id);
            Check(((Minion)next.IdEntityDic[hand.Id]).AttackDamage == 7 && next.Player1.Choice == null, "adds source Attack to held minion once");
        }
        PlayEntity(g, p, hand); hand.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(hand.AttackDamage == 4, "Silence removes attack enchantment");
        var second = Play(g, p, id);
        PlayerDecisionProof.Answer(g, board.Id);
        Check(board.AttackDamage == 4, "board answer gains Attack");
    }

    internal static void Lancer(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182"); var own = Summon(p, "CS2_182");
        var lancer = Summon(p, id); g.DeathProcessingAndAuraUpdate();
        Check(enemy.HasTaunt && !own.HasTaunt, "opponent-only aura");
        enemy.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(enemy.HasTaunt, "recipient Silence preserves external aura layer");
        var second = Summon(p, id); var newcomer = Summon(p.Opponent, "CS2_171");
        foreach (var next in Copies(g))
        {
            ((Minion)next.IdEntityDic[lancer.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Check(((Minion)next.IdEntityDic[enemy.Id]).HasTaunt && ((Minion)next.IdEntityDic[newcomer.Id]).HasTaunt, "stacked source survives first Silence");
            Kill(next, (Minion)next.IdEntityDic[second.Id]);
            Check(!((Minion)next.IdEntityDic[enemy.Id]).HasTaunt, "last source departure removes aura");
        }
    }

    internal static void Worm(Game g, Controller p, string id)
    {
        var worm = Summon(p, id);
        Check(worm.IsDormant, "starts Dormant");
        while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_171");
        Check(worm.IsDormant, "opponent full board does not awaken");
        while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_171");
        LocationProofs.Location(p);
        Check(worm.IsDormant && p.BoardZone.SlotCount == 6, "six physical slots still asleep");
        foreach (var next in Copies(g))
        {
            Summon(next.Player1, "CS2_171"); var body = (Minion)next.IdEntityDic[worm.Id];
            Check(!body.IsDormant && body.AttackDamage == 6 && body.Health == 6, "seventh physical slot awakens after clone/load");
            DormantRules.Make(body, 2, "EDR_469e", body); next.DeathProcessingAndAuraUpdate();
            Check(body.IsDormant, "printed board condition does not override a later external sleep");
            DormantRules.Awaken(body);
            Kill(next, next.Player1.BoardZone.First(m => m.Id != worm.Id));
            Check(!body.IsDormant, "opening slot does not put it back to sleep");
            Generic.ReturnToHandBlock(next.Player1, body); next.DeathProcessingAndAuraUpdate();
            PlayEntity(next, next.Player1, body);
            Check(body.IsDormant, "fresh entry sleeps on nonfull board");
        }
    }

    internal static void Unpinned_Maul(Game g, Controller p, string id)
    {
        var smith = Summon(p, "NEW1_009"); var body = Summon(p, "CS2_182");
        var weapon = Play(g, p, id);
        End(g);
        body.Damage = 2;
        foreach (var next in Copies(g))
        {
            Play(next, next.Player2, "EX1_066");
            Check(((Minion)next.IdEntityDic[body.Id]).Damage == 1, "PinnedRule_MaulOpponentTurn: destroys weapon and activates sole source once");
        }
    }

    internal static void Bribe(Game g, Controller p, string id)
    {
        p.Hero[GameTag.CLASS] = (int)CardClass.DRUID;
        Console.WriteLine("Bribe bucket DRUID=" + OfflineCardPools.Candidates(g, CardClass.DRUID,
            OfflinePoolFilter.Discover(keyword: GameTag.CHOOSE_ONE), 126054).Count);
        var card = Play(g, p, id);
        Check(p.Choice != null && p.Choice.Choices.Count >= 1, "Choose One Discover opens");
        var offers = p.Choice.Choices.Select(n => g.IdEntityDic[n]).ToArray();
        Check(offers.All(e => e.ChooseOne) && offers.Select(e => e.Card.Id).Distinct().Count() == offers.Length, "distinct Choose One offers");
        int chosen = offers[0].Id;
        foreach (var next in Copies(g))
        {
            PlayerDecisionProof.Answer(next, chosen);
            var marked = next.Player1.HandZone.Single(); var plain = next.Player2.HandZone.Single();
            Check(marked.Card.Id == plain.Card.Id && marked[GameTag.CHOOSE_BOTH] == 1 && plain[GameTag.CHOOSE_BOTH] == 0,
                "only owner's card has combined effects; opponent receives plain identity");
            Check(next.Player1.Options().Where(t => t.Source?.Id == marked.Id).All(t => t.ChooseOne <= 0), "local combined card has no branch options");
        }
    }

    private static void LocalChooseBoth()
    {
        var g = HeraldProof.New(CardClass.DRUID); var p = g.Player1;
        var target = Summon(p.Opponent, "CS2_200");
        var marked = Add(p, "EX1_154"); var plain = Add(p, "EX1_154");
        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_861e"), p.Hero, marked);
        var copied = Generic.Copy(p, p.Hero, marked, Zone.HAND);
        Check(copied[GameTag.CHOOSE_BOTH] == 1 && copied.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_861e"), "hand copy retains independent combined mark");
        copied.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_861e").Remove();
        Check(copied[GameTag.CHOOSE_BOTH] == 0 && marked[GameTag.CHOOSE_BOTH] == 1, "removing copied mark restores branch choice independently");
        Check(p.Options().Where(t => t.Source?.Id == marked.Id).All(t => t.ChooseOne <= 0)
            && p.Options().Any(t => t.Source?.Id == plain.Id && t.ChooseOne == 1)
            && p.Options().Any(t => t.Source?.Id == plain.Id && t.ChooseOne == 2), "combined choice is local, ordinary copies still choose");
        foreach (var next in Copies(g))
        {
            int deck = next.Player1.DeckZone.Count;
            PlayEntity(next, next.Player1, next.IdEntityDic[marked.Id], (Minion)next.IdEntityDic[target.Id]);
            Check(((Minion)next.IdEntityDic[target.Id]).Damage == 4 && next.Player1.DeckZone.Count == deck - 1,
                "local Wrath combines both damage and draw after clone/load");
        }
        g = DeathKnightC1Proof.NewGame(1); p = g.Player1;
        target = Summon(p.Opponent, "CS2_200"); CorpseRules.Gain(p, 2);
        var swarm = Add(p, "EDR_813");
        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_861e"), p.Hero, swarm);
        Check(swarm.ValidPlayTargets.Contains(target) && p.Options().Any(t => t.Source?.Id == swarm.Id && t.Target?.Id == target.Id),
            "local combined Morbid Swarm activates its funded damage branch");
        PlayEntity(g, p, swarm, target);
        Check(target.Damage == 4 && p.BoardZone.Count == 2 && CorpseRules.Available(p) == 0, "local Morbid Swarm runs both funded effects");
    }

    private static void Unpinned_MaulSeededSources()
    {
        var selected = new HashSet<int>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var g = HeraldProof.New(CardClass.PALADIN, seed); var p = g.Player1;
            var first = Summon(p, "EX1_597"); var second = Summon(p, "EX1_597");
            var opposing = Summon(p.Opponent, "EX1_597");
            var silenced = Summon(p, "EX1_597"); silenced.Silence();
            var dormant = Summon(p, "CORE_BT_156");
            var weapon = Play(g, p, "CATA_472");
            p.ExtraEndTurnEffect = true;
            var expectedRng = g.Random.Clone(); int index = expectedRng.Next(2); selected.Add(index);
            int expectedId = new[] { first.Id, second.Id }[index];
            foreach (var next in Copies(g))
            {
                // Replacing the weapon triggers its actual Deathrattle, without a turn event.
                Play(next, next.Player1, "CS2_106");
                Check(((Minion)next.IdEntityDic[expectedId]).Damage == 2
                    && ((Minion)next.IdEntityDic[index == 0 ? second.Id : first.Id]).Damage == 0,
                    "Unpinned_Maul: seeded physical source and doubled source-owned execution");
                Check(((Minion)next.IdEntityDic[silenced.Id]).Damage == 0 && ((Minion)next.IdEntityDic[dormant.Id]).IsDormant,
                    "silenced/Dormant sources excluded");
                Check(((Minion)next.IdEntityDic[opposing.Id]).Damage == 0, "opponent end-turn effects never broadcast");
                Check(next.Random.CaptureState().Equals(expectedRng.CaptureState()), "one ordered source RNG draw only");
                Check(next.Player1.BoardZone.Count == 6, "two summons use remaining capacity");
            }
        }
        Check(selected.Count == 2, "seeds exercise both source positions");
        var repeated = HeraldProof.New(CardClass.PALADIN, 1);
        var imp = Summon(repeated.Player1, "EX1_597");
        Summon(repeated.Player1, "TIME_064");
        EndTurnActivation.RandomFriendly(repeated.Player1);
        repeated.ProcessTasks();
        Check(imp.Damage == 2 && repeated.Player1.BoardZone.Count == 4,
            "Chrono Lord Deios doubles Maul-style activated end-turn effects");
    }

    internal static Game[] Copies(Game g) => new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) };

    private static void Unpinned_MaulPreservesControlExpiry()
    {
        foreach (string spell in new[] { "EX1_334", "CFM_603" })
        foreach (bool hasEndEffect in new[] { false, true })
        {
            var g = HeraldProof.New(CardClass.PRIEST); var p = g.Player1;
            var borrowed = Summon(p.Opponent, hasEndEffect ? "NEW1_009" : "CS2_171");
            var patient = Summon(p, "CS2_182"); patient.Damage = 2;
            Play(g, p, spell, borrowed);
            Play(g, p, "CATA_472");
            var expectedRng = g.Random.Clone();
            if (hasEndEffect) expectedRng.Next(1);
            foreach (var next in Copies(g))
            {
                var owner = next.Player1; var minion = (Minion)next.IdEntityDic[borrowed.Id];
                Play(next, owner, "CS2_106");
                Check(minion.Controller == owner && minion.AppliedEnchantments.Any(e => e.Card.Id == spell + "e"),
                    "Maul must not expire temporary control: " + spell + "/" + hasEndEffect);
                Check(((Minion)next.IdEntityDic[patient.Id]).Damage == (hasEndEffect ? 1 : 2),
                    "Maul activates borrowed minion's printed effect without its cleanup listener");
                Check(expectedRng.CaptureState().Equals(next.Random.CaptureState()),
                    "temporary control listener does not qualify an otherwise effectless Maul source");
                End(next);
                Check(minion.Controller == owner.Opponent && !minion.AppliedEnchantments.Any(e => e.Card.Id == spell + "e"),
                    "temporary control still expires at real turn end: " + spell);
            }
        }
    }

    private static void DefinitionCompatibility()
    {
        bool rejected = false;
        try { _ = new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.HandOrBoard); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "mixed-zone selector requires an explicit minion filter");
        B14F1HistoryDefinitionProof.Run();
        foreach (string id in new[] { "CS3_022", "TIME_435", "TLC_633", "MEND_040" })
        {
            string bytes = SabberStoneCore.Model.Snapshots.SnapshotCodec.EncodeDefinition(Cards.FromId(id), detached: true);
            var restored = SabberStoneCore.Model.Snapshots.SnapshotCodec.DecodeDefinition(bytes);
            Check(SabberStoneCore.Model.Snapshots.SnapshotCodec.EncodeDefinition(restored) == bytes,
                "new targeting/condition definition round trips: " + id);
            if (id == "MEND_040") Check(restored.Dormant.AwakenCondition != null, "detached awakening predicate retained");
            else Check(restored.SourceTargetingPredicate != null, "detached live-source predicate retained");
        }
    }

    private static void CapacityAndRewind()
    {
        var g = HeraldProof.New(CardClass.DRUID); var p = g.Player1;
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        Play(g, p, "JAIL_861");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Check(p.HandZone.Count == 10 && p.Opponent.HandZone.Count == 10 && p.Choice == null,
            "full hands burn generated copies and finish decision");

        g = HeraldProof.New(CardClass.DRUID); p = g.Player1;
        var enemy = Summon(p.Opponent, "CS2_182"); Summon(p, "CATA_898");
        var worm = Summon(p, "MEND_040"); var held = Add(p, "CS2_182");
        var combined = Add(p, "EX1_154");
        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_861e"), p.Hero, combined);
        var source = Add(p, "CATA_161"); source[GameTag.REWIND] = 1;
        PlayEntity(g, p, source); PlayerDecisionProof.Answer(g, held.Id);
        Check(((Minion)held).AttackDamage == 7, "first attempt applies hand buff");
        RewindTestHelpers.AnswerRewind(g);
        Check(((Minion)g.IdEntityDic[held.Id]).AttackDamage == 4
            && g.IdEntityDic[combined.Id][GameTag.CHOOSE_BOTH] == 1
            && ((Minion)g.IdEntityDic[enemy.Id]).HasTaunt && ((Minion)g.IdEntityDic[worm.Id]).IsDormant,
            "Rewind restores hidden buff baseline, combined mark, enemy aura and dormant condition");
        PlayerDecisionProof.Answer(g, source.Id);
        Check(((Minion)g.IdEntityDic[source.Id]).AttackDamage == 6 && g.Player1.Choice == null,
            "replayed mixed selector can choose source; no duplicate buff");

        g = HeraldProof.New(CardClass.PALADIN); p = g.Player1;
        var imp = Summon(p, "EX1_597");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "CATA_472"); var replacement = Add(p, "CS2_106"); replacement[GameTag.REWIND] = 1;
        PlayEntity(g, p, replacement);
        Check(imp.Damage == 1 && p.BoardZone.Count == 7, "Maul still executes source effect on full board");
        RewindTestHelpers.AnswerRewind(g);
        Check(((Minion)g.IdEntityDic[imp.Id]).Damage == 1 && g.Player1.BoardZone.Count == 7,
            "Maul replay has one net activation and preserves capacity");
        g = HeraldProof.New(CardClass.PALADIN); p = g.Player1;
        Summon(p, "CS2_182"); Play(g, p, "CATA_472"); var rng = g.Random.CaptureState();
        Play(g, p, "CS2_106");
        Check(rng.Equals(g.Random.CaptureState()), "no eligible Maul source consumes no RNG");
    }
}
