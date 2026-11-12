using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class AchievementEvaluatorProof
{
    internal static void Run()
    {
        TerminalProjectionAndConcessionGates();
        TerminalOutcomeSemantics();
        ResolutionAndEntityContinuity();
        CausalNearMisses();
        ExactCombatAndDirectSource();
        TopdeckAndTurnBoundary();
        StartingClassGate();
        DeckAndCareerPredicates();
        CareerProgressOnLoss();
        CreditedDeathDirection();
        ContinuityPredicates();
        TurnAndThresholdPredicates();
        RemainingPredicatePaths();
        ClassThresholdNearMisses();
        ActivePathPredicates();
        ActivePathAdmission();
        Console.WriteLine("PASS achievement evaluator: terminal facts, exact causes, decks, career, active paths");
    }

    [GroupProof("godfrey-rewards")]
    internal static void ProtectedOverdrawRewards()
    {
        var protectedDelivery = HistoryFactsProof.Fixture();
        while (!protectedDelivery.Player2.HandZone.IsFull) Add(protectedDelivery.Player2, "CS2_182");
        var godfrey = Entity.FromCard(protectedDelivery.Player2, Cards.FromId("JAIL_509"));
        QuestRules.AddPlayerEffect(protectedDelivery.Player2, godfrey, "JAIL_509e");
        Summon(protectedDelivery.Player1, "CAP_004");
        Summon(protectedDelivery.Player1, "CAP_004");
        Play(protectedDelivery, protectedDelivery.Player1, "CORE_CS2_062");
        Exact(protectedDelivery.Player2.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_509e")[GameTag.TAG_SCRIPT_DATA_NUM_2] >= 3,
            "opponent retains at least three source-caused protected overdraws");
        Exact(!protectedDelivery.HistoryFacts.Query.Events<CardBurnedFact>().Any(f => f.ControllerId == protectedDelivery.Player2.Id),
            "retained cards never enter destruction facts");
        Exact(protectedDelivery.Process(ConcedeTask.Any(protectedDelivery.Player2)), "protected-delivery concession accepted");
        var protectedPending = Pending(protectedDelivery, "protected-delivery");
        Lacks(Evaluate(protectedPending, LocalAchievementCollector.From(protectedDelivery, protectedDelivery.Player1.Id, protectedPending).WithConcessions(false, true)),
            "special-delivery", "winning after protected overdraw cannot award burn dust");

        Console.WriteLine("PASS protected overdraw never awards Special Delivery dust");
    }

    private static void TerminalOutcomeSemantics()
    {
        var draw = HistoryFactsProof.Fixture();
        var drawPending = Pending(draw, "mutual-draw");
        draw.Player1.Hero.Damage = 27;
        draw.Player2.Hero.Damage = 27;
        Play(draw, draw.Player1, "CORE_CS2_062");
        Exact(draw.Player1.PlayState == PlayState.TIED && draw.Player2.PlayState == PlayState.TIED,
            "same-resolution double fatal settles both players TIED");
        var drawEvidence = LocalAchievementCollector.From(draw, draw.Player1.Id, drawPending);
        Exact(drawEvidence.Result == "Tied" && drawEvidence.NaturalDraw,
            "same-resolution double fatal projects a natural draw");
        Has(Evaluate(drawPending, drawEvidence), "mutually-assured-destruction", typeof(LocalAchievementResolutionWitness));

        var humanDies = HistoryFactsProof.Fixture();
        humanDies.Player1.Hero.Damage = 27;
        Play(humanDies, humanDies.Player1, "CORE_CS2_062");
        Exact(humanDies.Player1.PlayState == PlayState.LOST && humanDies.Player2.PlayState == PlayState.WON,
            "only human hero dead settles Lost/Won");

        var opponentDies = HistoryFactsProof.Fixture();
        opponentDies.Player2.Hero.Damage = 27;
        Play(opponentDies, opponentDies.Player1, "CORE_CS2_062");
        Exact(opponentDies.Player1.PlayState == PlayState.WON && opponentDies.Player2.PlayState == PlayState.LOST,
            "only opponent hero dead settles Won/Lost");

        var conceded = HistoryFactsProof.Fixture();
        var concededPending = Pending(conceded, "opponent-concedes");
        Exact(conceded.Process(ConcedeTask.Any(conceded.Player2)), "opponent concession accepted");
        var concededEvidence = LocalAchievementCollector.From(conceded, conceded.Player1.Id, concededPending)
            .WithConcessions(false, true);
        Exact(!concededEvidence.NaturalDraw, "concession is not a natural draw");
        Lacks(Evaluate(concededPending, concededEvidence), "mutually-assured-destruction",
            "concession cannot earn a mutual-destruction draw");

        var turnLimit = HistoryFactsProof.Fixture();
        var turnLimitPending = Pending(turnLimit, "turn-limit");
        turnLimit.Player1.PlayState = PlayState.TIED;
        turnLimit.Player2.PlayState = PlayState.TIED;
        turnLimit.FinalWrapUp();
        turnLimit.FinalGameOver();
        var turnLimitEvidence = LocalAchievementCollector.From(turnLimit, turnLimit.Player1.Id, turnLimitPending);
        Exact(turnLimitEvidence.Result == "Tied" && !turnLimitEvidence.NaturalDraw,
            "non-lethal turn-limit draw remains Tied but is not a natural draw");
        Lacks(Evaluate(turnLimitPending, turnLimitEvidence), "mutually-assured-destruction",
            "turn-limit draw cannot earn a mutual-destruction draw");

        var revived = CorpseFixture();
        Play(revived, revived.Player1, "TIME_618");
        End(revived);
        Play(revived, revived.Player2, "TIME_618");
        CorpseRules.Gain(revived.Player1, 20);
        CorpseRules.Gain(revived.Player2, 20);
        revived.Player1.Hero.Damage = 27;
        revived.Player2.Hero.Damage = 27;
        Play(revived, revived.Player2, "CORE_CS2_062");
        Exact(revived.State != State.COMPLETE && revived.Player1.Hero.Health > 0 && revived.Player2.Hero.Health > 0,
            "same-resolution double fatal was revived and match continued");
        revived.Player1.PlayState = PlayState.TIED;
        revived.Player2.PlayState = PlayState.TIED;
        revived.FinalWrapUp();
        revived.FinalGameOver();
        var revivedPending = Pending(revived, "revived-then-turn-limit");
        var revivedEvidence = LocalAchievementCollector.From(revived, revived.Player1.Id, revivedPending);
        Exact(!revivedEvidence.NaturalDraw, "earlier revived double fatal cannot relabel later turn-limit draw");
        Lacks(Evaluate(revivedPending, revivedEvidence), "mutually-assured-destruction",
            "nonterminal double fatal cannot earn mutual destruction after later terminal draw");
    }

    private static void TerminalProjectionAndConcessionGates()
    {
        var game = HistoryFactsProof.Fixture();
        var pending = Pending(game);
        bool early = false;
        try { LocalAchievementCollector.From(game, game.Player1.Id, pending); }
        catch (InvalidOperationException) { early = true; }
        Exact(early, "collector rejects mutable nonterminal games");

        game.Player1.Hero.Damage = 29;
        game.Player1.Hero.Armor = 0;
        foreach (var card in game.Player1.HandZone.ToArray()) game.Player1.SetasideZone.Add(game.Player1.HandZone.Remove(card));
        foreach (var card in game.Player1.DeckZone.ToArray()) game.Player1.SetasideZone.Add(game.Player1.DeckZone.Remove(card));
        Exact(game.Process(ConcedeTask.Any(game.Player2)), "NPC concession is accepted");
        var evidence = LocalAchievementCollector.From(game, game.Player1.Id, pending).WithConcessions(false, true);
        var result = Evaluate(pending, evidence);
        Has(result, "calculated", typeof(LocalAchievementTerminalWitness));
        Has(result, "out-of-cards-not-ideas", typeof(LocalAchievementTerminalWitness));
        Lacks(result, "pocket-sized-problem", "NPC concession cannot invent a lethal attack");
        Exact(result.CareerDelta.EligibleWins == 1 && result.CareerDelta.DefeatedNpcId == pending.QueuedNpcId,
            "legitimate NPC concession advances bounded win career facts");
        var duplicate = LocalAchievementEvaluator.Evaluate(pending, evidence, new LocalAchievementCareer(),
            LocalAchievementCatalog.All.Select(x => x.Id).ToArray());
        Exact(duplicate.Earned.Count == 0, "already unlocked catalog entries are never proposed twice");
        var changedPending = Clone(pending);
        changedPending.MainDeck.Add(Card("FORGED_AFTER_MATCH", 1, "SPELL", "MAGE", 1, "COMMON", "CORE"));
        bool rebound = false;
        try { Evaluate(changedPending, evidence); } catch (InvalidOperationException) { rebound = true; }
        Exact(rebound, "evidence rejects deck/class context changed after terminal collection");

        var conceded = HistoryFactsProof.Fixture();
        var concededPending = Pending(conceded, "player-concede");
        Exact(conceded.Process(ConcedeTask.Any(conceded.Player1)), "player concession is accepted");
        var rejected = LocalAchievementCollector.From(conceded, conceded.Player1.Id, concededPending).WithConcessions(true, false);
        var rejectedResult = Evaluate(concededPending, rejected);
        Exact(rejectedResult.Earned.Count == 0 && rejectedResult.CareerDelta == LocalAchievementCareerDelta.Empty,
            "player concession grants neither match nor career progress");
    }

    private static void ExactCombatAndDirectSource()
    {
        var exact = HistoryFactsProof.Fixture();
        var exactPending = Pending(exact, "exact-one");
        var one = Summon(exact.Player1, "CS2_231");
        one.AttackDamage = 1;
        exact.Player2.Hero.Damage = exact.Player2.Hero.Health - 1;
        Attack(exact, one, exact.Player2.Hero);
        var exactResult = Evaluate(exactPending, LocalAchievementCollector.From(exact, exact.Player1.Id, exactPending));
        Has(exactResult, "pocket-sized-problem", typeof(LocalAchievementAttackWitness));
        Lacks(exactResult, "absolutely-unnecessary", "one-damage exact lethal is not overkill");

        var overkill = HistoryFactsProof.Fixture();
        var overkillPending = Pending(overkill, "overkill");
        var giant = Summon(overkill.Player1, "CS2_182");
        giant.AttackDamage = 31;
        overkill.Player2.Hero.Damage = overkill.Player2.Hero.Health - 1;
        Attack(overkill, giant, overkill.Player2.Hero);
        var overkillResult = Evaluate(overkillPending, LocalAchievementCollector.From(overkill, overkill.Player1.Id, overkillPending));
        Has(overkillResult, "absolutely-unnecessary", typeof(LocalAchievementResolutionWitness));
        Lacks(overkillResult, "pocket-sized-problem", "31 Attack cannot satisfy exact 1 Attack lethal");

        var enemySelfLethal = HistoryFactsProof.Fixture();
        var enemySelfLethalPending = Pending(enemySelfLethal, "enemy-self-overkill");
        End(enemySelfLethal);
        enemySelfLethal.Player2.Hero.Damage = 29;
        var enemyFireball = (Spell)Add(enemySelfLethal.Player2, "CS2_029");
        enemyFireball[GameTag.SPELLPOWER] = 30;
        PlayEntity(enemySelfLethal, enemySelfLethal.Player2, enemyFireball, enemySelfLethal.Player2.Hero);
        Exact(enemySelfLethal.Player1.PlayState == PlayState.WON, "opponent self-overkill hands human the win");
        Lacks(Evaluate(enemySelfLethalPending, LocalAchievementCollector.From(enemySelfLethal, enemySelfLethal.Player1.Id, enemySelfLethalPending)),
            "absolutely-unnecessary", "opponent self-inflicted lethal is not human winning damage");

        var spell = HistoryFactsProof.Fixture();
        var spellPending = Pending(spell, "spell-lethal");
        spell.Player2.Hero.Damage = 29;
        Play(spell, spell.Player1, "CS2_029", spell.Player2.Hero);
        var spellResult = Evaluate(spellPending, LocalAchievementCollector.From(spell, spell.Player1.Id, spellPending));
        Lacks(spellResult, "return-to-sender", "created own Fireball has no opposing-zone transfer witness");
        Lacks(spellResult, "fine-ill-do-it-myself", "spell lethal is not a hero attack");
    }

    private static void ResolutionAndEntityContinuity()
    {
        var clear = HistoryFactsProof.Fixture();
        var clearPending = Pending(clear, "seven-clear");
        for (int i = 0; i < 7; i++) Summon(clear.Player2, "CS2_231");
        Play(clear, clear.Player1, "CS2_032");
        Exact(clear.Process(ConcedeTask.Any(clear.Player2)), "post-clear concession accepted");
        Has(Evaluate(clearPending, LocalAchievementCollector.From(clear, clear.Player1.Id, clearPending).WithConcessions(false, true)),
            "seven-card-pickup", typeof(LocalAchievementResolutionWitness));

        var partialClear = HistoryFactsProof.Fixture();
        for (int i = 0; i < 4; i++) Summon(partialClear.Player2, "CS2_231");
        for (int i = 0; i < 3; i++) Summon(partialClear.Player2, "CS2_182").BaseHealth = 10;
        Play(partialClear, partialClear.Player1, "CS2_032");
        Exact(partialClear.Process(ConcedeTask.Any(partialClear.Player2)), "partial-clear concession accepted");
        var partialPending = Pending(partialClear, "partial-clear");
        Lacks(Evaluate(partialPending, LocalAchievementCollector.From(partialClear, partialClear.Player1.Id, partialPending).WithConcessions(false, true)),
            "seven-card-pickup", "one spell destroying only four of seven starting minions cannot qualify");

        var stolen = HistoryFactsProof.Fixture();
        var stolenPending = Pending(stolen, "stolen-lethal");
        var physical = Summon(stolen.Player2, "CS2_182");
        var controller = Summon(stolen.Player1, "CS2_231");
        new ControlTask(EntityType.TARGET).Process(stolen, stolen.Player1, controller, physical, new TaskStack());
        physical.AttackDamage = 30;
        Attack(stolen, physical, stolen.Player2.Hero);
        Has(Evaluate(stolenPending, LocalAchievementCollector.From(stolen, stolen.Player1.Id, stolenPending)),
            "thanks-ill-take-it-from-here", typeof(LocalAchievementAttackWitness));

        var counter = HistoryFactsProof.Fixture();
        var counterPending = Pending(counter, "counter-eight");
        Play(counter, counter.Player1, "CORE_EX1_287");
        End(counter);
        Play(counter, counter.Player2, "CORE_EX1_312");
        Exact(counter.Process(ConcedeTask.Any(counter.Player2)), "counter fixture concession accepted");
        Has(Evaluate(counterPending, LocalAchievementCollector.From(counter, counter.Player1.Id, counterPending).WithConcessions(false, true)),
            "respectfully-no", typeof(LocalAchievementResolutionWitness));
    }

    private static void CausalNearMisses()
    {
        var split = HistoryFactsProof.Fixture();
        Play(split, split.Player1, "EDR_464");
        for (int i = 0; i < 4; i++) Summon(split.Player2, "CS2_231");
        for (int i = 0; i < 3; i++) Summon(split.Player2, "CS2_182");
        var repeatedClear = Play(split, split.Player1, "CS2_032");
        var splitDeaths = split.HistoryFacts.Query.Deaths().Where(x => x.SourceId == repeatedClear.Id).ToArray();
        int splitDeathCount = splitDeaths.Select(x => x.EntityId).Distinct().Count();
        string splitResolutionCounts = string.Join(",", splitDeaths.GroupBy(x => x.ResolutionId)
            .Select(group => group.Select(x => x.EntityId).Distinct().Count()));
        Exact(split.Player2.BoardZone.Count == 0 && splitDeathCount == 7
            && splitDeaths.GroupBy(x => x.ResolutionId).All(group => group.Select(x => x.EntityId).Distinct().Count() < 7),
            $"real Tyrande repeat clears seven only across separate spell-body resolutions (board={split.Player2.BoardZone.Count}, deaths={splitDeathCount}, groups={splitResolutionCounts})");
        Exact(split.Process(ConcedeTask.Any(split.Player2)), "split-body clear concession accepted");
        var splitPending = Pending(split, "split-body-clear");
        Lacks(Evaluate(splitPending, LocalAchievementCollector.From(split, split.Player1.Id, splitPending).WithConcessions(false, true)),
            "seven-card-pickup", "deaths split across legal repeated spell bodies cannot combine into one seven-minion clear");

        var generated = HistoryFactsProof.Fixture();
        var opposingOriginal = Summon(generated.Player2, "CS2_182");
        var generatedCopy = (Minion)Generic.Copy(generated.Player1, generated.Player1.Hero, opposingOriginal, Zone.PLAY);
        generatedCopy.AttackDamage = 30;
        Attack(generated, generatedCopy, generated.Player2.Hero);
        var generatedPending = Pending(generated, "generated-opponent-copy-lethal");
        Lacks(Evaluate(generatedPending, LocalAchievementCollector.From(generated, generated.Player1.Id, generatedPending)),
            "thanks-ill-take-it-from-here", "a generated copy of an opposing minion never entered play under opposing control");

        var ownSource = HistoryFactsProof.Fixture();
        var ownBody = Summon(ownSource.Player1, "CS2_182");
        var enemyController = Summon(ownSource.Player2, "CS2_231");
        new ControlTask(EntityType.TARGET).Process(ownSource, ownSource.Player2, enemyController, ownBody, new TaskStack());
        var reclaim = Summon(ownSource.Player1, "CS2_231");
        new ControlTask(EntityType.TARGET).Process(ownSource, ownSource.Player1, reclaim, ownBody, new TaskStack());
        var ownControlFacts = ownSource.HistoryFacts.Query.ControlChanges().Where(x => x.EntityId == ownBody.Id).ToArray();
        Exact(ownBody.Controller == ownSource.Player1 && ownControlFacts.Length == 2
            && ownControlFacts[0].FromControllerId == ownSource.Player1.Id && ownControlFacts[0].ToControllerId == ownSource.Player2.Id
            && ownControlFacts[1].FromControllerId == ownSource.Player2.Id && ownControlFacts[1].ToControllerId == ownSource.Player1.Id,
            "own-source minion completes a recorded opposing-control round trip");
        ownBody.AttackDamage = 30;
        Attack(ownSource, ownBody, ownSource.Player2.Hero);
        var ownPending = Pending(ownSource, "own-source-control-lethal");
        Lacks(Evaluate(ownPending, LocalAchievementCollector.From(ownSource, ownSource.Player1.Id, ownPending)),
            "thanks-ill-take-it-from-here", "an own-source minion does not qualify after temporary opposing control");

        var transformed = HistoryFactsProof.Fixture();
        var transformedFirst = Summon(transformed.Player1, "CORE_ULD_723");
        Kill(transformed, transformedFirst);
        var transformedReturn = transformed.Player1.BoardZone.Single(x => x.Card.Id == "CORE_ULD_723");
        End(transformed); End(transformed);
        transformedReturn = (Minion)Generic.ChangeEntityBlock(transformed.Player1, transformedReturn, Cards.FromId("CS2_182"), true);
        transformedReturn.AttackDamage = 30;
        Attack(transformed, transformedReturn, transformed.Player2.Hero);
        var transformedPending = Pending(transformed, "reborn-transformed-lethal");
        Lacks(Evaluate(transformedPending, LocalAchievementCollector.From(transformed, transformed.Player1.Id, transformedPending)),
            "not-today", "a form-changed Reborn return cannot deliver the qualifying lethal");

        var reincarnated = HistoryFactsProof.Fixture();
        var reincarnatedFirst = Summon(reincarnated.Player1, "CORE_ULD_723");
        Kill(reincarnated, reincarnatedFirst);
        var reincarnatedReturn = reincarnated.Player1.BoardZone.Single(x => x.Card.Id == "CORE_ULD_723");
        var returnedFact = reincarnated.HistoryFacts.Query.Reborns().Single(x => x.ReturnedEntityId == reincarnatedReturn.Id);
        End(reincarnated); End(reincarnated);
        var enemySource = Summon(reincarnated.Player2, "CS2_231");
        new ControlTask(EntityType.TARGET).Process(reincarnated, reincarnated.Player2, enemySource, reincarnatedReturn, new TaskStack());
        var friendlySource = Summon(reincarnated.Player1, "CS2_231");
        new ControlTask(EntityType.TARGET).Process(reincarnated, reincarnated.Player1, friendlySource, reincarnatedReturn, new TaskStack());
        reincarnatedReturn.AttackDamage = 30;
        Attack(reincarnated, reincarnatedReturn, reincarnated.Player2.Hero);
        var finalCombat = reincarnated.HistoryFacts.Query.CombatStarts().Last(x => x.AttackerId == reincarnatedReturn.Id);
        Exact(finalCombat.AttackerIncarnation != returnedFact.ReturnedIncarnation,
            "control round-trip creates a different attacking incarnation from the Reborn return");
        var reincarnatedPending = Pending(reincarnated, "reborn-different-incarnation-lethal");
        Lacks(Evaluate(reincarnatedPending, LocalAchievementCollector.From(reincarnated, reincarnated.Player1.Id, reincarnatedPending)),
            "not-today", "a later incarnation of the Reborn entity cannot inherit its survival witness");
    }

    private static void TopdeckAndTurnBoundary()
    {
        var game = HistoryFactsProof.Fixture();
        var pending = Pending(game, "topdeck");
        foreach (var card in game.Player1.HandZone.ToArray()) game.Player1.SetasideZone.Add(game.Player1.HandZone.Remove(card));
        foreach (var card in game.Player1.DeckZone.ToArray()) game.Player1.SetasideZone.Add(game.Player1.DeckZone.Remove(card));
        var fireball = (Spell)Entity.FromCard(game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.DeckZone);
        End(game);
        game.Player2.Hero.Damage = 24;
        End(game);
        Exact(game.Player1.HandZone.Contains(fireball), "normal turn-start draw produced the physical Fireball");
        PlayEntity(game, game.Player1, fireball, game.Player2.Hero);
        Has(Evaluate(pending, LocalAchievementCollector.From(game, game.Player1.Id, pending)),
            "topdecking-is-a-skill", typeof(LocalAchievementResolutionWitness));

        var intervened = HistoryFactsProof.Fixture();
        var intervenedPending = Pending(intervened, "topdeck-intervened");
        foreach (var card in intervened.Player1.HandZone.ToArray()) intervened.Player1.SetasideZone.Add(intervened.Player1.HandZone.Remove(card));
        foreach (var card in intervened.Player1.DeckZone.ToArray()) intervened.Player1.SetasideZone.Add(intervened.Player1.DeckZone.Remove(card));
        var delayed = (Spell)Entity.FromCard(intervened.Player1, Cards.FromId("CS2_029"), zone: intervened.Player1.DeckZone);
        End(intervened);
        intervened.Player2.Hero.Damage = 24;
        End(intervened);
        Play(intervened, intervened.Player1, "GAME_005");
        PlayEntity(intervened, intervened.Player1, delayed, intervened.Player2.Hero);
        Lacks(Evaluate(intervenedPending, LocalAchievementCollector.From(intervened, intervened.Player1.Id, intervenedPending)),
            "topdecking-is-a-skill", "accepted intervening action invalidates immediate topdeck lethal");
    }

    private static void StartingClassGate()
    {
        var game = ClassFixture(CardClass.DRUID);
        var druid = Pending(game, "class-gate");
        Generic.ChangeManaCrystal(game.Player2, -5, false);
        Generic.ChangeManaCrystal(game.Player1, -1, false);
        Generic.ChangeManaCrystal(game.Player1, 1, false);
        Exact(game.Process(ConcedeTask.Any(game.Player2)), "class-gate terminal concession accepted");
        var druidEvidence = LocalAchievementCollector.From(game, game.Player1.Id, druid).WithConcessions(false, true);
        Has(Evaluate(druid, druidEvidence), "druid-ahead-of-the-curve", typeof(LocalAchievementThresholdWitness));
        var wrongClass = Clone(druid);
        wrongClass.StartingHeroClass = "MAGE";
        Lacks(Evaluate(wrongClass, LocalAchievementCollector.From(game, game.Player1.Id, wrongClass).WithConcessions(false, true)),
            "druid-ahead-of-the-curve", "qualifying gameplay cannot bypass Druid starting-class gate");
    }

    private static void DeckAndCareerPredicates()
    {
        var game = HistoryFactsProof.Fixture();
        var basis = Pending(game, "deck-career");
        Exact(game.Process(ConcedeTask.Any(game.Player2)), "deck predicate win concession accepted");
        var cheapMinions = Clone(basis);
        cheapMinions.PersonalDeck = true;
        cheapMinions.MainDeck = Enumerable.Range(1, 30).Select(i => Card("MINION_" + i, 1, "MINION", "MAGE", 3, "COMMON", "EXP_A")).ToList();
        var minionResult = Evaluate(cheapMinions,
            LocalAchievementCollector.From(game, game.Player1.Id, cheapMinions).WithConcessions(false, true));
        Has(minionResult, "aggressively-average", typeof(LocalAchievementDeckWitness));
        Has(minionResult, "no-copies-no-problem", typeof(LocalAchievementDeckWitness));
        Has(minionResult, "creature-feature", typeof(LocalAchievementDeckWitness));
        Has(minionResult, "home-team", typeof(LocalAchievementDeckWitness));
        Has(minionResult, "small-ball", typeof(LocalAchievementDeckWitness));
        Lacks(minionResult, "words-not-violence", "all-minion deck cannot satisfy no-minion deck");
        var copied = Clone(cheapMinions);
        copied.MainDeck[1].BaseCardId = copied.MainDeck[0].BaseCardId;
        Lacks(Evaluate(copied, LocalAchievementCollector.From(game, game.Player1.Id, copied).WithConcessions(false, true)),
            "no-copies-no-problem", "repeated base identity fails singleton deck");

        var mixedGroup = LocalCardCatalog.Supported.GroupBy(card => LocalCardCatalog.CopyGroup(card.Id), StringComparer.Ordinal)
            .First(group => group.Any(card => card.Set == CardSet.CORE) && group.Any(card => card.Set != CardSet.CORE));
        var corePrint = mixedGroup.First(card => card.Set == CardSet.CORE);
        var expansionPrint = mixedGroup.First(card => card.Set != CardSet.CORE);
        string mixedRoot = Path.Combine(Path.GetTempPath(), "achievement-mixed-prints-" + Guid.NewGuid());
        Directory.CreateDirectory(mixedRoot);
        try {
            using var profile = new LocalProfile(Path.Combine(mixedRoot, "profile.xml"));
            var deck = new LocalDeck("Mixed prints", "HERO_08", Enumerable.Repeat(corePrint.Id, 10)
                .Concat(Enumerable.Repeat(expansionPrint.Id, 20)).ToArray());
            var mixed = profile.AchievementContextFor(deck, true, "npc-proof", 2500, "Diamond");
            mixed.MatchId = "mixed-print-gate";
            var rows = mixed.MainDeck.Where(row => LocalCardCatalog.CopyGroup(row.BaseCardId) == LocalCardCatalog.CopyGroup(corePrint.Id)).ToArray();
            Exact(rows.Length == 2 && rows.Sum(row => row.Copies) == 30
                && rows.Any(row => row.CardSet == CardSet.CORE.ToString() && row.Copies == 10)
                && rows.Any(row => row.CardSet == expansionPrint.Set.ToString() && row.Copies == 20),
                "mixed copy-group prints retain their actual metadata and counts");
            Has(Evaluate(mixed, LocalAchievementCollector.From(game, game.Player1.Id, mixed).WithConcessions(false, true)),
                "one-expansion-stand", typeof(LocalAchievementDeckWitness));
        }
        finally { Directory.Delete(mixedRoot, true); }

        var spells = Clone(basis);
        spells.PersonalDeck = true;
        spells.MainDeck = Enumerable.Range(1, 30).Select(i => Card("SPELL_" + i, 1, "SPELL", "NEUTRAL", i <= 15 ? 7 : 2,
            i == 1 ? "LEGENDARY" : "RARE", "EXP_B")).ToList();
        var spellResult = Evaluate(spells,
            LocalAchievementCollector.From(game, game.Player1.Id, spells).WithConcessions(false, true));
        Has(spellResult, "words-not-violence", typeof(LocalAchievementDeckWitness));
        Has(spellResult, "neutral-ground", typeof(LocalAchievementDeckWitness));
        Has(spellResult, "heavy-reading", typeof(LocalAchievementDeckWitness));
        Has(spellResult, "one-expansion-stand", typeof(LocalAchievementDeckWitness));
        Lacks(spellResult, "no-famous-people", "Legendary card fails no-famous-people");
        var noLegendaries = Clone(spells);
        noLegendaries.QueuedNpcBand = "Legend";
        foreach (var card in noLegendaries.MainDeck) card.Rarity = "RARE";
        Has(Evaluate(noLegendaries,
            LocalAchievementCollector.From(game, game.Player1.Id, noLegendaries).WithConcessions(false, true)),
            "no-famous-people", typeof(LocalAchievementDeckWitness));

        var curve = Clone(basis);
        curve.PersonalDeck = true;
        curve.MainDeck = Enumerable.Range(1, 7).SelectMany(cost => new[] {
            Card("CURVE_" + cost + "A", 1, "SPELL", "MAGE", cost, "COMMON", "CORE"),
            Card("CURVE_" + cost + "B", 1, "SPELL", "MAGE", cost, "COMMON", "CORE") }).ToList();
        Has(Evaluate(curve, LocalAchievementCollector.From(game, game.Player1.Id, curve).WithConcessions(false, true)),
            "perfect-curve", typeof(LocalAchievementDeckWitness));

        var prior = new LocalAchievementCareer {
            EligibleWins = 99,
            CreditedEnemyMinionDeaths = 1000,
            PlayedCollectibleBaseCardIds = Enumerable.Range(1, 250).Select(i => "READ_" + i).ToList(),
            DefeatedNpcIds = Enumerable.Range(1, 24).Select(i => "NPC_" + i).ToList(),
            ClassWins = Classes.Select(x => new LocalAchievementClassWins { Hero = x, Wins = 10 }).ToList()
        };
        basis.CraftedAndOwnedBaseCardIds = Enumerable.Range(1, 10).Select(i => "CRAFT_" + i).ToList();
        basis.PersonalDeck = true;
        basis.MainDeck = basis.CraftedAndOwnedBaseCardIds.Select(x => Card(x, 1, "SPELL", "MAGE", 1, "COMMON", "CORE")).ToList();
        var careerEvidence = LocalAchievementCollector.From(game, game.Player1.Id, basis).WithConcessions(false, true);
        var career = LocalAchievementEvaluator.Evaluate(basis, careerEvidence, prior, Array.Empty<string>(),
            new LocalAchievementLedgerContext("EXP_A", 1, new[] { "legend", "legend:top-100", "legend:1" }));
        foreach (var id in new[] { "a-hundred-good-games", "class-act", "well-read", "the-board-is-a-workplace-hazard",
            "the-complete-works", "world-tour", "made-to-measure", "welcome-to-legend", "among-the-hundred", "there-can-be-only-one" })
            Has(career, id, typeof(LocalAchievementCareerWitness));
    }

    private static void CareerProgressOnLoss()
    {
        var game = HistoryFactsProof.Fixture();
        var pending = Pending(game, "career-loss");
        Play(game, game.Player1, "CS2_029", game.Player2.Hero);
        game.Player1.Hero.Damage = 24;
        End(game);
        Play(game, game.Player2, "CS2_029", game.Player1.Hero);
        var evidence = LocalAchievementCollector.From(game, game.Player1.Id, pending);
        var prior = new LocalAchievementCareer {
            PlayedCollectibleBaseCardIds = Enumerable.Range(1, 249).Select(i => "OLD_" + i).ToList()
        };
        var result = LocalAchievementEvaluator.Evaluate(pending, evidence, prior, Array.Empty<string>());
        Has(result, "well-read", typeof(LocalAchievementCareerWitness));
        Exact(result.CareerDelta.EligibleWins == 0 && result.CareerDelta.PlayedCollectibleBaseCardIds.Count == 1,
            "real loss advances played-card identity but not win counters");
    }

    private static void CreditedDeathDirection()
    {
        var defending = HistoryFactsProof.Fixture();
        var defendingPending = Pending(defending, "defending-death");
        var defender = Summon(defending.Player1, "CS2_182");
        defender.AttackDamage = 5;
        var enemyAttacker = Summon(defending.Player2, "CS2_231");
        enemyAttacker.BaseHealth = 1;
        End(defending);
        Attack(defending, enemyAttacker, defender);
        Exact(defending.Process(ConcedeTask.Any(defending.Player2)), "defending-kill terminal concession accepted");
        var defendingResult = Evaluate(defendingPending,
            LocalAchievementCollector.From(defending, defending.Player1.Id, defendingPending).WithConcessions(false, true));
        Exact(defendingResult.CareerDelta.CreditedEnemyMinionDeaths == 0,
            "enemy attacker dying to a friendly defender is not caused by the player's attack/effect");

        var attacking = HistoryFactsProof.Fixture();
        var attackingPending = Pending(attacking, "attacking-death");
        var attacker = Summon(attacking.Player1, "CS2_182");
        var victim = Summon(attacking.Player2, "CS2_231");
        victim.BaseHealth = 1;
        Attack(attacking, attacker, victim);
        Exact(attacking.Process(ConcedeTask.Any(attacking.Player2)), "attacking-kill terminal concession accepted");
        var attackingResult = Evaluate(attackingPending,
            LocalAchievementCollector.From(attacking, attacking.Player1.Id, attackingPending).WithConcessions(false, true));
        Exact(attackingResult.CareerDelta.CreditedEnemyMinionDeaths == 1,
            "player attack must credit one enemy death occurrence");
    }

    private static void ActivePathPredicates()
    {
        var totems = ClassFixture(CardClass.SHAMAN);
        foreach (var id in new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009", "DINO_412", "END_029" }) Summon(totems.Player1, id);
        Play(totems, totems.Player1, "CORE_AT_052");
        Exact(totems.Process(ConcedeTask.Any(totems.Player2)), "seven-Totem win concession accepted");
        var totemPending = Pending(totems, "seven-totems");
        Has(Evaluate(totemPending, LocalAchievementCollector.From(totems, totems.Player1.Id, totemPending).WithConcessions(false, true)),
            "shaman-the-whole-band", typeof(LocalAchievementResolutionWitness));
        var wrongClass = Clone(totemPending);
        wrongClass.StartingHeroClass = "MAGE";
        Lacks(Evaluate(wrongClass, LocalAchievementCollector.From(totems, totems.Player1.Id, wrongClass).WithConcessions(false, true)),
            "shaman-the-whole-band", "seven qualifying Totems cannot bypass starting-class gate");

        var missingBasic = ClassFixture(CardClass.SHAMAN);
        foreach (var id in new[] { "CS2_050", "CS2_051", "CS2_052", "DINO_412", "DINO_412", "END_029" }) Summon(missingBasic.Player1, id);
        Play(missingBasic, missingBasic.Player1, "CORE_AT_052");
        Exact(missingBasic.Process(ConcedeTask.Any(missingBasic.Player2)), "missing-basic Totem concession accepted");
        var missingBasicPending = Pending(missingBasic, "missing-basic-totem");
        Lacks(Evaluate(missingBasicPending, LocalAchievementCollector.From(missingBasic, missingBasic.Player1.Id, missingBasicPending).WithConcessions(false, true)),
            "shaman-the-whole-band", "seven Totems missing one basic Totem do not qualify");

        var freeze = HistoryFactsProof.Fixture();
        for (int i = 0; i < 5; i++) Summon(freeze.Player2, "CS2_182");
        Play(freeze, freeze.Player1, "CORE_CS2_028");
        freeze.Player2.Hero.Damage = 24;
        PlayKeepingMana(freeze, freeze.Player1, "CS2_029", freeze.Player2.Hero);
        Has(Evaluate(Pending(freeze, "freeze-five"), LocalAchievementCollector.From(freeze, freeze.Player1.Id, Pending(freeze, "freeze-five"))),
            "no-touching", typeof(LocalAchievementResolutionWitness));

        var repeatedFreeze = HistoryFactsProof.Fixture();
        var alreadyFrozen = Summon(repeatedFreeze.Player2, "CS2_182");
        alreadyFrozen.IsFrozen = true;
        for (int i = 0; i < 4; i++) Summon(repeatedFreeze.Player2, "CS2_182");
        Play(repeatedFreeze, repeatedFreeze.Player1, "CORE_CS2_028");
        repeatedFreeze.Player2.Hero.Damage = 24;
        PlayKeepingMana(repeatedFreeze, repeatedFreeze.Player1, "CS2_029", repeatedFreeze.Player2.Hero);
        var repeatedPending = Pending(repeatedFreeze, "freeze-four-new");
        Lacks(Evaluate(repeatedPending, LocalAchievementCollector.From(repeatedFreeze, repeatedFreeze.Player1.Id, repeatedPending)),
            "no-touching", "already-Frozen target does not count as a fifth distinct newly Frozen entity");

        var automaticFreeze = HistoryFactsProof.Fixture();
        for (int i = 0; i < 5; i++) Summon(automaticFreeze.Player2, "CS2_182");
        Generic.CastSpell(automaticFreeze.Player1, automaticFreeze,
            (Spell)Add(automaticFreeze.Player1, "CORE_CS2_028"), null, 0);
        Exact(automaticFreeze.Process(ConcedeTask.Any(automaticFreeze.Player2)), "automatic freeze concession accepted");
        var automaticPending = Pending(automaticFreeze, "automatic-freeze-five");
        Lacks(Evaluate(automaticPending, LocalAchievementCollector.From(automaticFreeze, automaticFreeze.Player1.Id, automaticPending).WithConcessions(false, true)),
            "no-touching", "automatic spell cast cannot qualify as one spell the player played");

        var chain = HistoryFactsProof.Fixture();
        Summon(chain.Player1, "TIME_064");
        for (int i = 0; i < 4; i++) Summon(chain.Player1, "CORE_EX1_096");
        Play(chain, chain.Player1, "CORE_CS2_062");
        Exact(chain.Process(ConcedeTask.Any(chain.Player2)), "Deathrattle-chain concession accepted");
        var chainPending = Pending(chain, "deathrattle-chain");
        Has(Evaluate(chainPending, LocalAchievementCollector.From(chain, chain.Player1.Id, chainPending).WithConcessions(false, true)),
            "chain-reaction", typeof(LocalAchievementTurnWitness));

        var threeBodies = HistoryFactsProof.Fixture();
        Summon(threeBodies.Player1, "TIME_064");
        threeBodies.Player1[GameTag.EXTRA_DEATHRATTLES_ADDITIONAL] = 2;
        for (int i = 0; i < 3; i++) Summon(threeBodies.Player1, "CORE_EX1_096");
        Play(threeBodies, threeBodies.Player1, "CORE_CS2_062");
        Exact(threeBodies.HistoryFacts.Query.DeathrattlesResolved().Count() >= 8,
            "three-entity near miss resolves at least eight Deathrattles");
        Exact(threeBodies.Process(ConcedeTask.Any(threeBodies.Player2)), "three-entity chain concession accepted");
        var threePending = Pending(threeBodies, "deathrattle-three-entities");
        Lacks(Evaluate(threePending, LocalAchievementCollector.From(threeBodies, threeBodies.Player1.Id, threePending).WithConcessions(false, true)),
            "chain-reaction", "eight Deathrattles from only three physical entities do not qualify");

        var delivery = HistoryFactsProof.Fixture();
        while (!delivery.Player2.HandZone.IsFull) Add(delivery.Player2, "CS2_182");
        Summon(delivery.Player1, "CAP_004");
        Summon(delivery.Player1, "CAP_004");
        Play(delivery, delivery.Player1, "CORE_CS2_062");
        Exact(delivery.Process(ConcedeTask.Any(delivery.Player2)), "special-delivery concession accepted");
        var deliveryPending = Pending(delivery, "special-delivery");
        Has(Evaluate(deliveryPending, LocalAchievementCollector.From(delivery, delivery.Player1.Id, deliveryPending).WithConcessions(false, true)),
            "special-delivery", typeof(LocalAchievementTurnWitness));

        ProtectedOverdrawRewards();

        var twoBurns = HistoryFactsProof.Fixture();
        while (!twoBurns.Player2.HandZone.IsFull) Add(twoBurns.Player2, "CS2_182");
        Summon(twoBurns.Player1, "CAP_004");
        Play(twoBurns, twoBurns.Player1, "CORE_CS2_062");
        Exact(twoBurns.Process(ConcedeTask.Any(twoBurns.Player2)), "two-burn concession accepted");
        var twoBurnsPending = Pending(twoBurns, "two-burns");
        Lacks(Evaluate(twoBurnsPending, LocalAchievementCollector.From(twoBurns, twoBurns.Player1.Id, twoBurnsPending).WithConcessions(false, true)),
            "special-delivery", "two source-caused burns do not meet three-burn threshold");

        var extra = HistoryFactsProof.Fixture();
        var extraPending = Pending(extra, "extra-turn-win");
        Play(extra, extra.Player1, "UNG_028t");
        End(extra);
        Exact(extra.CurrentPlayer == extra.Player1, "card-granted extra turn started for owner");
        extra.Player2.Hero.Damage = 24;
        Play(extra, extra.Player1, "CS2_029", extra.Player2.Hero);
        Has(Evaluate(extraPending, LocalAchievementCollector.From(extra, extra.Player1.Id, extraPending)),
            "borrowed-time", typeof(LocalAchievementTurnWitness));

        var ordinary = HistoryFactsProof.Fixture();
        var ordinaryPending = Pending(ordinary, "ordinary-turn-win");
        End(ordinary);
        End(ordinary);
        ordinary.Player2.Hero.Damage = 24;
        Play(ordinary, ordinary.Player1, "CS2_029", ordinary.Player2.Hero);
        Lacks(Evaluate(ordinaryPending, LocalAchievementCollector.From(ordinary, ordinary.Player1.Id, ordinaryPending)),
            "borrowed-time", "ordinary later turn is not a card-granted extra turn");

        var stolen = HistoryFactsProof.Fixture();
        foreach (var held in stolen.Player2.HandZone.ToArray()) stolen.Player2.SetasideZone.Add(stolen.Player2.HandZone.Remove(held));
        var physicalFireball = Add(stolen.Player2, "CS2_029");
        Add(stolen.Player2, "GAME_005");
        PlayEntity(stolen, stolen.Player1,
            B16ObjectivesTrialProof.Trial(stolen.Player1, "CAP_405tb1", "CAP_405t2", "CAP_405t3"));
        Exact(physicalFireball.Controller == stolen.Player1 && stolen.Player1.HandZone.Contains(physicalFireball),
            "trial transferred physical opposing Fireball");
        stolen.Player2.Hero.Damage = 24;
        PlayEntity(stolen, stolen.Player1, physicalFireball, stolen.Player2.Hero);
        var stolenPending = Pending(stolen, "stolen-spell-lethal");
        Has(Evaluate(stolenPending, LocalAchievementCollector.From(stolen, stolen.Player1.Id, stolenPending)),
            "return-to-sender", typeof(LocalAchievementResolutionWitness));
    }

    private static void ContinuityPredicates()
    {
        var hunter = StartingDeckFixture("CS2_172", CardClass.HUNTER);
        var beast = (Minion)StartingCard(hunter.Player1, "CS2_172");
        PlayEntity(hunter, hunter.Player1, beast);
        End(hunter);
        End(hunter);
        beast.AttackDamage = 20;
        hunter.Player2.Hero.Damage = 10;
        Attack(hunter, beast, hunter.Player2.Hero);
        var hunterPending = Pending(hunter, "starting-beast");
        Has(Evaluate(hunterPending, LocalAchievementCollector.From(hunter, hunter.Player1.Id, hunterPending)),
            "hunter-whos-a-good-boy", typeof(LocalAchievementAttackWitness));
        WrongClassLacks(hunter, hunterPending, "hunter-whos-a-good-boy");

        var generated = HistoryFactsProof.Fixture();
        var generatedBeast = Summon(generated.Player1, "CS2_172");
        End(generated);
        End(generated);
        generatedBeast.AttackDamage = 20;
        generated.Player2.Hero.Damage = 10;
        Attack(generated, generatedBeast, generated.Player2.Hero);
        var generatedPending = Pending(generated, "generated-beast");
        generatedPending.StartingHeroClass = "HUNTER";
        Lacks(Evaluate(generatedPending, LocalAchievementCollector.From(generated, generated.Player1.Id, generatedPending)),
            "hunter-whos-a-good-boy", "generated Beast cannot satisfy starting-deck continuity");

        var warrior = StartingDeckFixture("TLC_253", CardClass.WARRIOR);
        var ogre = (Minion)StartingCard(warrior.Player1, "TLC_253");
        PlayEntity(warrior, warrior.Player1, ogre);
        Exact(DormantRules.Awaken(ogre), "starting-deck Ogre awakened");
        End(warrior);
        End(warrior);
        warrior.Player2.Hero.Damage = 25;
        ogre.IsExhausted = false;
        Attack(warrior, ogre, warrior.Player2.Hero);
        var warriorPending = Pending(warrior, "ogre-survives");
        Has(Evaluate(warriorPending, LocalAchievementCollector.From(warrior, warrior.Player1.Id, warriorPending)),
            "warrior-good-stats-for-the-cost", typeof(LocalAchievementAttackWitness));
        WrongClassLacks(warrior, warriorPending, "warrior-good-stats-for-the-cost");

        var reawakened = StartingDeckFixture("TLC_253", CardClass.WARRIOR);
        var resetOgre = (Minion)StartingCard(reawakened.Player1, "TLC_253");
        PlayEntity(reawakened, reawakened.Player1, resetOgre);
        Exact(DormantRules.Awaken(resetOgre), "near-miss Ogre first awakening");
        End(reawakened);
        End(reawakened);
        DormantRules.Make(resetOgre, null, "TLC_253e", reawakened.Player1.Hero);
        Exact(DormantRules.Awaken(resetOgre), "near-miss Ogre second awakening");
        reawakened.Player2.Hero.Damage = 25;
        resetOgre.IsExhausted = false;
        Attack(reawakened, resetOgre, reawakened.Player2.Hero);
        var reawakenedPending = Pending(reawakened, "ogre-reawakened");
        Lacks(Evaluate(reawakenedPending, LocalAchievementCollector.From(reawakened, reawakened.Player1.Id, reawakenedPending)),
            "warrior-good-stats-for-the-cost", "latest awakening resets complete-opposing-turn survival window");

        var reborn = HistoryFactsProof.Fixture();
        var firstBody = Summon(reborn.Player1, "CORE_ULD_723");
        Kill(reborn, firstBody);
        var returned = reborn.Player1.BoardZone.Single(x => x.Card.Id == "CORE_ULD_723");
        End(reborn);
        End(reborn);
        returned.AttackDamage = 30;
        reborn.Player2.Hero.Damage = 0;
        Attack(reborn, returned, reborn.Player2.Hero);
        var rebornPending = Pending(reborn, "reborn-survives");
        Has(Evaluate(rebornPending, LocalAchievementCollector.From(reborn, reborn.Player1.Id, rebornPending)),
            "not-today", typeof(LocalAchievementAttackWitness));
    }

    private static void TurnAndThresholdPredicates()
    {
        var ambulance = HistoryFactsProof.Fixture();
        End(ambulance);
        ambulance.Player1.Hero.Damage = 27;
        End(ambulance);
        Exact(ambulance.Process(ConcedeTask.Any(ambulance.Player2)), "low-health turn win concession accepted");
        var ambulancePending = Pending(ambulance, "ambulance");
        Has(Evaluate(ambulancePending, LocalAchievementCollector.From(ambulance, ambulance.Player1.Id, ambulancePending).WithConcessions(false, true)),
            "call-an-ambulance", typeof(LocalAchievementTurnWitness));

        var armorOnly = HistoryFactsProof.Fixture();
        End(armorOnly);
        armorOnly.Player1.Hero.Damage = 27;
        armorOnly.Player2.Hero.Damage = 6;
        armorOnly.Player2.Hero.Armor = 10;
        End(armorOnly);
        Exact(armorOnly.Process(ConcedeTask.Any(armorOnly.Player2)), "enemy-armor near-miss concession accepted");
        var armorOnlyPending = Pending(armorOnly, "ambulance-armor-only");
        Lacks(Evaluate(armorOnlyPending, LocalAchievementCollector.From(armorOnly, armorOnly.Player1.Id, armorOnlyPending).WithConcessions(false, true)),
            "call-an-ambulance", "enemy Armor cannot replace required 25 opposing Health");

        var exam = HistoryFactsProof.Fixture();
        for (int i = 0; i < 10; i++) Play(exam, exam.Player1, "GAME_005");
        Exact(exam.Process(ConcedeTask.Any(exam.Player2)), "ten-spell turn concession accepted");
        var examPending = Pending(exam, "ten-spell-turn");
        Has(Evaluate(examPending, LocalAchievementCollector.From(exam, exam.Player1.Id, examPending).WithConcessions(false, true)),
            "mage-final-exam", typeof(LocalAchievementTurnWitness));
        var examWrongClass = Clone(examPending);
        examWrongClass.StartingHeroClass = "PRIEST";
        Lacks(Evaluate(examWrongClass, LocalAchievementCollector.From(exam, exam.Player1.Id, examWrongClass).WithConcessions(false, true)),
            "mage-final-exam", "ten accepted manual spell plays cannot bypass Mage starting-class gate");

        var downtown = HistoryFactsProof.Fixture();
        Exact(downtown.Process(ConcedeTask.Any(downtown.Player2)), "empty-board opening-turn concession accepted");
        var downtownPending = Pending(downtown, "downtown");
        Has(Evaluate(downtownPending, LocalAchievementCollector.From(downtown, downtown.Player1.Id, downtownPending).WithConcessions(false, true)),
            "from-downtown", typeof(LocalAchievementTurnWitness));

        var clearedLater = HistoryFactsProof.Fixture();
        var presentAtStart = Summon(clearedLater.Player1, "CS2_231");
        End(clearedLater);
        End(clearedLater);
        Kill(clearedLater, presentAtStart);
        Exact(clearedLater.Process(ConcedeTask.Any(clearedLater.Player2)), "post-start clear concession accepted");
        var clearedLaterPending = Pending(clearedLater, "not-downtown");
        Lacks(Evaluate(clearedLaterPending, LocalAchievementCollector.From(clearedLater, clearedLater.Player1.Id, clearedLaterPending).WithConcessions(false, true)),
            "from-downtown", "clearing a minion after turn start does not rewrite turn-start board");

        var fatigue = HistoryFactsProof.Fixture();
        while (!fatigue.Player1.DeckZone.IsEmpty) fatigue.Player1.SetasideZone.Add(fatigue.Player1.DeckZone.Remove(0));
        for (int i = 0; i < 4; i++) Generic.Draw(fatigue.Player1);
        Exact(fatigue.Process(ConcedeTask.Any(fatigue.Player2)), "fatigue win concession accepted");
        var fatiguePending = Pending(fatigue, "fatigue-ten");
        Has(Evaluate(fatiguePending, LocalAchievementCollector.From(fatigue, fatigue.Player1.Id, fatiguePending).WithConcessions(false, true)),
            "running-on-fumes", typeof(LocalAchievementThresholdWitness));

        var armoredFatigue = HistoryFactsProof.Fixture();
        armoredFatigue.Player1.Hero.Armor = 10;
        while (!armoredFatigue.Player1.DeckZone.IsEmpty) armoredFatigue.Player1.SetasideZone.Add(armoredFatigue.Player1.DeckZone.Remove(0));
        for (int i = 0; i < 4; i++) Generic.Draw(armoredFatigue.Player1);
        Exact(armoredFatigue.Process(ConcedeTask.Any(armoredFatigue.Player2)), "armor-absorbed fatigue concession accepted");
        var armoredFatiguePending = Pending(armoredFatigue, "fatigue-armor");
        Lacks(Evaluate(armoredFatiguePending, LocalAchievementCollector.From(armoredFatigue, armoredFatigue.Player1.Id, armoredFatiguePending).WithConcessions(false, true)),
            "running-on-fumes", "fatigue absorbed entirely by Armor is not actual Health damage");

        var swarm = HistoryFactsProof.Fixture();
        for (int wave = 0; wave < 2; wave++)
        {
            for (int i = 0; i < 7; i++) PlayKeepingMana(swarm, swarm.Player1, "CS2_231");
            PlayKeepingMana(swarm, swarm.Player1, "CORE_CS2_062");
        }
        PlayKeepingMana(swarm, swarm.Player1, "CS2_231");
        Exact(swarm.Process(ConcedeTask.Any(swarm.Player2)), "population/effect-death concession accepted");
        var swarmPending = Pending(swarm, "population-and-losses");
        var swarmResult = Evaluate(swarmPending, LocalAchievementCollector.From(swarm, swarm.Player1.Id, swarmPending).WithConcessions(false, true));
        Has(swarmResult, "population-boom", typeof(LocalAchievementTurnWitness));
        Has(swarmResult, "acceptable-losses", typeof(LocalAchievementTurnWitness));

        var enemyClear = HistoryFactsProof.Fixture();
        for (int i = 0; i < 7; i++) Summon(enemyClear.Player1, "CS2_231");
        End(enemyClear);
        Play(enemyClear, enemyClear.Player2, "CORE_CS2_062");
        End(enemyClear);
        Exact(enemyClear.Process(ConcedeTask.Any(enemyClear.Player2)), "enemy-clear later win concession accepted");
        var enemyClearPending = Pending(enemyClear, "enemy-caused-losses");
        Lacks(Evaluate(enemyClearPending, LocalAchievementCollector.From(enemyClear, enemyClear.Player1.Id, enemyClearPending).WithConcessions(false, true)),
            "acceptable-losses", "enemy-caused friendly deaths cannot credit player's own effects");

        var power = HistoryFactsProof.Fixture();
        for (int turn = 0; turn < 20; turn++)
        {
            power.Player1.UsedMana = 0;
            Exact(power.Process(HeroPowerTask.Any(power.Player1, power.Player2.Hero)), "Mage Hero Power accepted");
            if (turn < 19) { End(power); End(power); }
        }
        Exact(power.Process(ConcedeTask.Any(power.Player2)), "Hero Power damage concession accepted");
        var powerPending = Pending(power, "hero-power-damage");
        Has(Evaluate(powerPending, LocalAchievementCollector.From(power, power.Player1.Id, powerPending).WithConcessions(false, true)),
            "power-trip", typeof(LocalAchievementResolutionWitness));
    }

    private static void RemainingPredicatePaths()
    {
        var heroAttacks = ClassFixture(CardClass.DEMONHUNTER);
        for (int turn = 0; turn < 5; turn++)
        {
            if (heroAttacks.Player1.Hero.Weapon == null) Play(heroAttacks, heroAttacks.Player1, "CS2_106");
            if (turn == 4) heroAttacks.Player2.Hero.Damage = 27;
            Exact(heroAttacks.Process(HeroAttackTask.Any(heroAttacks.Player1, heroAttacks.Player2.Hero)),
                "player hero attack accepted on turn " + turn);
            if (turn < 4) { End(heroAttacks); End(heroAttacks); }
        }
        var attackPending = Pending(heroAttacks, "five-hero-attacks");
        var attackResult = Evaluate(attackPending, LocalAchievementCollector.From(heroAttacks, heroAttacks.Player1.Id, attackPending));
        Has(attackResult, "fine-ill-do-it-myself", typeof(LocalAchievementAttackWitness));
        Has(attackResult, "demon-hunter-face-value", typeof(LocalAchievementAttackWitness));
        WrongClassLacks(heroAttacks, attackPending, "demon-hunter-face-value");

        var corpses = CorpseFixture();
        var corpseSource = Summon(corpses.Player1, "CS2_231");
        CorpseRules.Gain(corpses.Player1, 30);
        Exact(CorpseRules.TrySpend(corpses.Player1, 30, corpseSource), "thirty-Corpse spend committed");
        Exact(corpses.Process(ConcedeTask.Any(corpses.Player2)), "Corpse-spend concession accepted");
        var corpsePending = Pending(corpses, "corpse-thirty");
        Has(Evaluate(corpsePending, LocalAchievementCollector.From(corpses, corpses.Player1.Id, corpsePending).WithConcessions(false, true)),
            "death-knight-grave-expenditure", typeof(LocalAchievementThresholdWitness));
        WrongClassLacks(corpses, corpsePending, "death-knight-grave-expenditure", npcConceded: true);

        var recruit = ClassFixture(CardClass.PALADIN);
        var recruitBody = Summon(recruit.Player1, "CS2_101t");
        recruitBody.AttackDamage = 15;
        recruit.Player2.Hero.Damage = 15;
        Attack(recruit, recruitBody, recruit.Player2.Hero);
        var recruitPending = Pending(recruit, "recruit-lethal");
        Has(Evaluate(recruitPending, LocalAchievementCollector.From(recruit, recruit.Player1.Id, recruitPending)),
            "paladin-entry-level-position", typeof(LocalAchievementAttackWitness));
        WrongClassLacks(recruit, recruitPending, "paladin-entry-level-position");

        var priest = ClassFixture(CardClass.PRIEST);
        priest.Player1.Hero.Damage = 29;
        for (int i = 0; i < 4; i++) Play(priest, priest.Player1, "CS2_007", priest.Player1.Hero);
        Exact(priest.Player1.Hero.Health == priest.Player1.Hero.BaseHealth, "healing fixture restored full actual Health");
        Exact(priest.Process(ConcedeTask.Any(priest.Player2)), "healing concession accepted");
        var priestPending = Pending(priest, "priest-recovery");
        Has(Evaluate(priestPending, LocalAchievementCollector.From(priest, priest.Player1.Id, priestPending).WithConcessions(false, true)),
            "priest-we-can-rebuild-you", typeof(LocalAchievementThresholdWitness));
        WrongClassLacks(priest, priestPending, "priest-we-can-rebuild-you", npcConceded: true);

        var combos = ClassFixture(CardClass.ROGUE);
        for (int i = 0; i < 4; i++)
        {
            Play(combos, combos.Player1, "GAME_005");
            Play(combos, combos.Player1, "EX1_124", combos.Player2.Hero);
        }
        Exact(combos.Process(ConcedeTask.Any(combos.Player2)), "four-Combo concession accepted");
        var comboPending = Pending(combos, "four-combos");
        Has(Evaluate(comboPending, LocalAchievementCollector.From(combos, combos.Player1.Id, comboPending).WithConcessions(false, true)),
            "rogue-combo-meal", typeof(LocalAchievementTurnWitness));
        WrongClassLacks(combos, comboPending, "rogue-combo-meal", npcConceded: true);

        var selfDamage = ClassFixture(CardClass.WARLOCK);
        selfDamage.Player1.Hero.Armor = 30;
        selfDamage.Player2.Hero.Armor = 30;
        for (int turn = 0; turn < 5; turn++)
        {
            Play(selfDamage, selfDamage.Player1, "CORE_CS2_062");
            Play(selfDamage, selfDamage.Player1, "CORE_CS2_062");
            if (turn < 4) { End(selfDamage); End(selfDamage); }
        }
        Exact(selfDamage.Player1.Hero.Health == 30 && selfDamage.Player1.Hero.Armor == 0,
            "own-card damage consumed thirty Armor without Health loss");
        Exact(selfDamage.Process(ConcedeTask.Any(selfDamage.Player2)), "self-damage concession accepted");
        var selfDamagePending = Pending(selfDamage, "self-damage-thirty");
        Has(Evaluate(selfDamagePending, LocalAchievementCollector.From(selfDamage, selfDamage.Player1.Id, selfDamagePending).WithConcessions(false, true)),
            "warlock-damage-is-a-resource", typeof(LocalAchievementResolutionWitness));
        WrongClassLacks(selfDamage, selfDamagePending, "warlock-damage-is-a-resource", npcConceded: true);

        var armor = ClassFixture(CardClass.WARRIOR);
        for (int turn = 0; turn < 25; turn++)
        {
            armor.Player1.UsedMana = 0;
            Exact(armor.Process(HeroPowerTask.Any(armor.Player1)), "Warrior Hero Power accepted");
            if (turn < 24) { End(armor); End(armor); }
        }
        Exact(armor.Process(ConcedeTask.Any(armor.Player2)), "fifty-Armor concession accepted");
        var armorPending = Pending(armor, "fifty-armor");
        Has(Evaluate(armorPending, LocalAchievementCollector.From(armor, armor.Player1.Id, armorPending).WithConcessions(false, true)),
            "armored-division", typeof(LocalAchievementThresholdWitness));

        var safe = HistoryFactsProof.Fixture();
        Exact(safe.Process(ConcedeTask.Any(safe.Player2)), "untouched win concession accepted");
        var safePending = Pending(safe, "untouched");
        var safeResult = Evaluate(safePending, LocalAchievementCollector.From(safe, safe.Player1.Id, safePending).WithConcessions(false, true));
        Has(safeResult, "untouchable", typeof(LocalAchievementTerminalWitness));
        Has(safeResult, "no-time-to-explain", typeof(LocalAchievementTerminalWitness));

        var touched = HistoryFactsProof.Fixture();
        Play(touched, touched.Player1, "CORE_CS2_062");
        Exact(touched.Process(ConcedeTask.Any(touched.Player2)), "damaged-hero concession accepted");
        var touchedPending = Pending(touched, "health-lost-before-win");
        Lacks(Evaluate(touchedPending, LocalAchievementCollector.From(touched, touched.Player1.Id, touchedPending).WithConcessions(false, true)),
            "untouchable", "real Health loss before the first turn boundary still invalidates Untouchable");

        var sixTypes = HistoryFactsProof.Fixture();
        var races = new[] { Race.BEAST, Race.DEMON, Race.DRAGON, Race.MECHANICAL, Race.PIRATE };
        foreach (var race in races)
        {
            var card = Cards.All.First(x => x.Type == CardType.MINION && x.Implemented && x.IsRace(race));
            Summon(sixTypes.Player1, card.Id);
        }
        PlayKeepingMana(sixTypes, sixTypes.Player1, "CS2_168");
        Exact(sixTypes.Process(ConcedeTask.Any(sixTypes.Player2)), "six-type concession accepted");
        var sixTypesPending = Pending(sixTypes, "six-types");
        Has(Evaluate(sixTypesPending, LocalAchievementCollector.From(sixTypes, sixTypes.Player1.Id, sixTypesPending).WithConcessions(false, true)),
            "one-of-everything", typeof(LocalAchievementResolutionWitness));

        var fiveTypes = HistoryFactsProof.Fixture();
        foreach (var race in races.Take(4))
            Summon(fiveTypes.Player1, Cards.All.First(x => x.Type == CardType.MINION && x.Implemented && x.IsRace(race)).Id);
        PlayKeepingMana(fiveTypes, fiveTypes.Player1, "CS2_168");
        Exact(fiveTypes.Process(ConcedeTask.Any(fiveTypes.Player2)), "five-type concession accepted");
        var fiveTypesPending = Pending(fiveTypes, "five-types");
        Lacks(Evaluate(fiveTypesPending, LocalAchievementCollector.From(fiveTypes, fiveTypes.Player1.Id, fiveTypesPending).WithConcessions(false, true)),
            "one-of-everything", "five assignable minion types do not meet six-type threshold");

        var family = HistoryFactsProof.Fixture();
        foreach (var card in Cards.All.Where(x => x.Type == CardType.MINION && x.Implemented && x.IsRace(Race.BEAST))
            .Where(x => LocalCardCatalog.CopyGroup(x.Id) != LocalCardCatalog.CopyGroup("CS2_172"))
            .GroupBy(x => LocalCardCatalog.CopyGroup(x.Id), StringComparer.Ordinal).Select(x => x.First()).Take(6))
            Summon(family.Player1, card.Id);
        PlayKeepingMana(family, family.Player1, "CS2_172");
        Exact(family.Player1.BoardZone.Count == 7, "seven distinct Beast identities admitted");
        Exact(family.Process(ConcedeTask.Any(family.Player2)), "seven-family concession accepted");
        var familyPending = Pending(family, "seven-family");
        Has(Evaluate(familyPending, LocalAchievementCollector.From(family, family.Player1.Id, familyPending).WithConcessions(false, true)),
            "family-reunion", typeof(LocalAchievementResolutionWitness));

        var sixFamily = HistoryFactsProof.Fixture();
        foreach (var card in Cards.All.Where(x => x.Type == CardType.MINION && x.Implemented && x.IsRace(Race.BEAST))
            .Where(x => LocalCardCatalog.CopyGroup(x.Id) != LocalCardCatalog.CopyGroup("CS2_172"))
            .GroupBy(x => LocalCardCatalog.CopyGroup(x.Id), StringComparer.Ordinal).Select(x => x.First()).Take(5))
            Summon(sixFamily.Player1, card.Id);
        PlayKeepingMana(sixFamily, sixFamily.Player1, "CS2_172");
        Exact(sixFamily.Process(ConcedeTask.Any(sixFamily.Player2)), "six-family concession accepted");
        var sixFamilyPending = Pending(sixFamily, "six-family");
        Lacks(Evaluate(sixFamilyPending, LocalAchievementCollector.From(sixFamily, sixFamily.Player1.Id, sixFamilyPending).WithConcessions(false, true)),
            "family-reunion", "six distinct same-type identities do not meet seven-member threshold");

        var takeover = HistoryFactsProof.Fixture();
        for (int i = 0; i < 2; i++)
        {
            var victim = Summon(takeover.Player2, "CS2_231");
            var source = Summon(takeover.Player1, "CS2_231");
            new ControlTask(EntityType.TARGET).Process(takeover, takeover.Player1, source, victim, new TaskStack());
        }
        var finalVictim = Summon(takeover.Player2, "CS2_231");
        PlayKeepingMana(takeover, takeover.Player1, "CS1_113", finalVictim);
        Exact(takeover.Process(ConcedeTask.Any(takeover.Player2)), "three-stolen concession accepted");
        var takeoverPending = Pending(takeover, "three-stolen");
        Has(Evaluate(takeoverPending, LocalAchievementCollector.From(takeover, takeover.Player1.Id, takeoverPending).WithConcessions(false, true)),
            "hostile-takeover", typeof(LocalAchievementResolutionWitness));

        var twoStolen = HistoryFactsProof.Fixture();
        for (int i = 0; i < 2; i++)
        {
            var victim = Summon(twoStolen.Player2, "CS2_231");
            var source = Summon(twoStolen.Player1, "CS2_231");
            new ControlTask(EntityType.TARGET).Process(twoStolen, twoStolen.Player1, source, victim, new TaskStack());
        }
        Exact(twoStolen.Process(ConcedeTask.Any(twoStolen.Player2)), "two-stolen concession accepted");
        var twoStolenPending = Pending(twoStolen, "two-stolen");
        Lacks(Evaluate(twoStolenPending, LocalAchievementCollector.From(twoStolen, twoStolen.Player1.Id, twoStolenPending).WithConcessions(false, true)),
            "hostile-takeover", "two stolen physical entities do not meet three-entity threshold");
    }

    private static void ClassThresholdNearMisses()
    {
        var corpses = CorpseFixture();
        for (int i = 0; i < 29; i++) Kill(corpses, (Minion)Play(corpses, corpses.Player1, "CS2_231"));
        Exact(CorpseRules.Available(corpses.Player1) == 29, "twenty-nine legal minion deaths bank twenty-nine Corpses");
        for (int i = 0; i < 3; i++) Play(corpses, corpses.Player1, "CATA_465");
        Play(corpses, corpses.Player1, "RLK_707");
        Exact(corpses.HistoryFacts.Query.CorpsesSpent().Sum(x => x.Amount) == 29,
            "legal Corpse-spender card plays consume exactly twenty-nine Corpses");
        Exact(corpses.Process(ConcedeTask.Any(corpses.Player2)), "twenty-nine-Corpse concession accepted");
        var corpsePending = Pending(corpses, "corpse-twenty-nine");
        Lacks(Evaluate(corpsePending, LocalAchievementCollector.From(corpses, corpses.Player1.Id, corpsePending).WithConcessions(false, true)),
            "death-knight-grave-expenditure", "twenty-nine Corpses do not meet the thirty-Corpse threshold");

        var fourTurns = ClassFixture(CardClass.DEMONHUNTER);
        for (int turn = 0; turn < 4; turn++)
        {
            if (fourTurns.Player1.Hero.Weapon == null) Play(fourTurns, fourTurns.Player1, "CS2_106");
            if (turn == 3) fourTurns.Player2.Hero.Damage = 27;
            Exact(fourTurns.Process(HeroAttackTask.Any(fourTurns.Player1, fourTurns.Player2.Hero)),
                "four-turn near-miss hero attack accepted on turn " + turn);
            if (turn < 3) { End(fourTurns); End(fourTurns); }
        }
        var fourTurnPending = Pending(fourTurns, "four-turn-hero-lethal");
        Lacks(Evaluate(fourTurnPending, LocalAchievementCollector.From(fourTurns, fourTurns.Player1.Id, fourTurnPending)),
            "demon-hunter-face-value", "hero lethal on only four distinct turns does not meet the five-turn threshold");

        var fourteenRecruit = ClassFixture(CardClass.PALADIN);
        var recruit = Summon(fourteenRecruit.Player1, "CS2_101t");
        recruit.AttackDamage = 14;
        fourteenRecruit.Player2.Hero.Damage = 16;
        Attack(fourteenRecruit, recruit, fourteenRecruit.Player2.Hero);
        var fourteenPending = Pending(fourteenRecruit, "fourteen-recruit-lethal");
        Lacks(Evaluate(fourteenPending, LocalAchievementCollector.From(fourteenRecruit, fourteenRecruit.Player1.Id, fourteenPending)),
            "paladin-entry-level-position", "a Recruit lethal at fourteen Attack misses the fifteen-Attack threshold");

        var transformedRecruit = ClassFixture(CardClass.PALADIN);
        var originalRecruit = Summon(transformedRecruit.Player1, "CS2_101t");
        var otherForm = (Minion)Generic.ChangeEntityBlock(transformedRecruit.Player1, originalRecruit, Cards.FromId("CS2_182"), true);
        var changedRecruit = (Minion)Generic.ChangeEntityBlock(transformedRecruit.Player1, otherForm, Cards.FromId("CS2_101t"), true);
        changedRecruit.AttackDamage = 15;
        transformedRecruit.Player2.Hero.Damage = 15;
        Attack(transformedRecruit, changedRecruit, transformedRecruit.Player2.Hero);
        var transformedRecruitPending = Pending(transformedRecruit, "transformed-recruit-lethal");
        Lacks(Evaluate(transformedRecruitPending,
                LocalAchievementCollector.From(transformedRecruit, transformedRecruit.Player1.Id, transformedRecruitPending)),
            "paladin-entry-level-position", "a transformed Recruit form cannot satisfy Recruit continuity");

        var recovery = ClassFixture(CardClass.PRIEST);
        recovery.Player1.Hero.BaseHealth = 29;
        recovery.Player1.Hero.Damage = 24;
        for (int i = 0; i < 3; i++) Play(recovery, recovery.Player1, "CS2_007", recovery.Player1.Hero);
        int actualHealing = recovery.HistoryFacts.Query.HealthChanges().Where(x => x.HeroId == recovery.Player1.Hero.Id
            && x.Reason == HistoryHealthChangeReason.Heal).Sum(x => x.NewHealth - x.OldHealth);
        Exact(actualHealing == 24 && recovery.Player1.Hero.Health == recovery.Player1.Hero.BaseHealth,
            "three legal heals restore exactly twenty-four actual Health from five and finish full");
        Exact(recovery.Process(ConcedeTask.Any(recovery.Player2)), "twenty-four-heal concession accepted");
        var recoveryPending = Pending(recovery, "priest-recovery-twenty-four");
        Lacks(Evaluate(recoveryPending, LocalAchievementCollector.From(recovery, recovery.Player1.Id, recoveryPending).WithConcessions(false, true)),
            "priest-we-can-rebuild-you", "twenty-four actual healing at full terminal Health misses the recovery threshold");

        var threeCombos = ClassFixture(CardClass.ROGUE);
        for (int i = 0; i < 3; i++)
        {
            Play(threeCombos, threeCombos.Player1, "GAME_005");
            Play(threeCombos, threeCombos.Player1, "EX1_124", threeCombos.Player2.Hero);
        }
        Exact(threeCombos.Process(ConcedeTask.Any(threeCombos.Player2)), "three-Combo concession accepted");
        var threeComboPending = Pending(threeCombos, "three-combos");
        Lacks(Evaluate(threeComboPending, LocalAchievementCollector.From(threeCombos, threeCombos.Player1.Id, threeComboPending).WithConcessions(false, true)),
            "rogue-combo-meal", "three activated Combo plays do not meet the four-Combo threshold");

        var selfDamage = ClassFixture(CardClass.WARLOCK);
        selfDamage.Player1.Hero.Armor = 29;
        selfDamage.Player2.Hero.Armor = 100;
        for (int turn = 0; turn < 4; turn++)
        {
            Play(selfDamage, selfDamage.Player1, "CORE_CS2_062");
            Play(selfDamage, selfDamage.Player1, "CORE_CS2_062");
            End(selfDamage); End(selfDamage);
        }
        Summon(selfDamage.Player1, "CS2_142");
        Summon(selfDamage.Player1, "CS2_142");
        Play(selfDamage, selfDamage.Player1, "CORE_CS2_062");
        var selfDamageFacts = selfDamage.HistoryFacts.Query.Damage().Where(x => x.VictimId == selfDamage.Player1.Hero.Id
            && x.SourceControllerId == selfDamage.Player1.Id && x.Cause == HistoryDamageCause.SpellOrEffect).ToArray();
        Exact(selfDamageFacts.Sum(x => x.Amount) == 29 && selfDamage.Player1.Hero.Health == 30,
            "legal own spells deal exactly twenty-nine damage while Armor preserves Health");
        Exact(selfDamage.Process(ConcedeTask.Any(selfDamage.Player2)), "twenty-nine-self-damage concession accepted");
        var selfDamagePending = Pending(selfDamage, "self-damage-twenty-nine");
        Lacks(Evaluate(selfDamagePending, LocalAchievementCollector.From(selfDamage, selfDamage.Player1.Id, selfDamagePending).WithConcessions(false, true)),
            "warlock-damage-is-a-resource", "twenty-nine own-card damage misses the thirty-damage threshold");

        var lowHealth = ClassFixture(CardClass.WARLOCK);
        lowHealth.Player1.Hero.Armor = 30;
        lowHealth.Player2.Hero.Armor = 30;
        for (int turn = 0; turn < 5; turn++)
        {
            Play(lowHealth, lowHealth.Player1, "CORE_CS2_062");
            Play(lowHealth, lowHealth.Player1, "CORE_CS2_062");
            if (turn < 4) { End(lowHealth); End(lowHealth); }
        }
        End(lowHealth);
        var healthAttacker = Summon(lowHealth.Player2, "CS2_182");
        healthAttacker.AttackDamage = 21;
        Attack(lowHealth, healthAttacker, lowHealth.Player1.Hero);
        Exact(lowHealth.Player1.Hero.Health == 9 && lowHealth.Process(ConcedeTask.Any(lowHealth.Player2)),
            "thirty-damage fixture wins below ten terminal Health");
        var lowHealthPending = Pending(lowHealth, "self-damage-low-terminal-health");
        Lacks(Evaluate(lowHealthPending, LocalAchievementCollector.From(lowHealth, lowHealth.Player1.Id, lowHealthPending).WithConcessions(false, true)),
            "warlock-damage-is-a-resource", "thirty own-card damage still requires at least ten terminal Health");

        var armor = ClassFixture(CardClass.WARRIOR);
        for (int turn = 0; turn < 24; turn++)
        {
            armor.Player1.UsedMana = 0;
            Exact(armor.Process(HeroPowerTask.Any(armor.Player1)), "forty-eight-Armor Hero Power accepted");
            if (turn < 23) { End(armor); End(armor); }
        }
        Exact(armor.Process(ConcedeTask.Any(armor.Player2)), "forty-eight-Armor concession accepted");
        var armorPending = Pending(armor, "forty-eight-armor");
        Lacks(Evaluate(armorPending, LocalAchievementCollector.From(armor, armor.Player1.Id, armorPending).WithConcessions(false, true)),
            "armored-division", "forty-eight Armor gained misses the fifty-Armor threshold");

        var armorSpent = ClassFixture(CardClass.WARRIOR);
        for (int turn = 0; turn < 25; turn++)
        {
            armorSpent.Player1.UsedMana = 0;
            Exact(armorSpent.Process(HeroPowerTask.Any(armorSpent.Player1)), "fifty-Armor spend fixture Hero Power accepted");
            if (turn < 24) { End(armorSpent); End(armorSpent); }
        }
        End(armorSpent);
        var armorAttacker = Summon(armorSpent.Player2, "CS2_182");
        armorAttacker.AttackDamage = 26;
        Attack(armorSpent, armorAttacker, armorSpent.Player1.Hero);
        Exact(armorSpent.Player1.Hero.Armor == 24 && armorSpent.Process(ConcedeTask.Any(armorSpent.Player2)),
            "fifty gained Armor finishes at twenty-four after a legal attack");
        var armorSpentPending = Pending(armorSpent, "fifty-armor-twenty-four-remaining");
        Lacks(Evaluate(armorSpentPending, LocalAchievementCollector.From(armorSpent, armorSpent.Player1.Id, armorSpentPending).WithConcessions(false, true)),
            "armored-division", "fifty Armor gained still requires twenty-five remaining at terminal");
    }

    private static void ActivePathAdmission()
    {
        foreach (var id in new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009", "DINO_412", "END_029", "CORE_AT_052",
            "TLC_253", "CORE_EX1_287", "CORE_EX1_312", "CAP_004", "JAIL_205", "CAP_405t3", "TLC_602", "UNG_028t" })
        {
            var card = Cards.FromId(id);
            Exact(card != null && card.Implemented, "required active achievement path is not implemented: " + id);
        }
        var counter = HistoryFactsProof.Fixture();
        Play(counter, counter.Player1, "CORE_EX1_287");
        End(counter);
        Play(counter, counter.Player2, "CORE_EX1_312");
        Exact(counter.HistoryFacts.Query.Counters().Single().BaseCost == 8,
            "active Counterspell/Twisting Nether path did not produce exact counter evidence");

        var extra = HistoryFactsProof.Fixture();
        Play(extra, extra.Player1, "UNG_028t");
        End(extra);
        Exact(extra.CurrentPlayer == extra.Player1 && extra.HistoryFacts.Query.ExtraTurnsStarted().Count() == 1,
            "active Lost City reward did not start an evidenced extra turn");
    }

    private static readonly string[] Classes = { "DEATHKNIGHT", "DEMONHUNTER", "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" };

    private static LocalAchievementPendingContext Pending(Game game, string id = "achievement-proof") => new LocalAchievementPendingContext {
        MatchId = id,
        EligibleWorldMatch = true,
        StartingHero = game.Player1.Hero.Card.Id,
        StartingHeroClass = game.Player1.BaseClass.ToString(),
        QueuedNpcId = "NPC_FINAL",
        QueuedNpcBand = "Diamond"
    };

    private static LocalAchievementPendingContext Clone(LocalAchievementPendingContext source) => new LocalAchievementPendingContext {
        MatchId = source.MatchId, EligibleWorldMatch = source.EligibleWorldMatch, PersonalDeck = source.PersonalDeck,
        StartingHero = source.StartingHero, StartingHeroClass = source.StartingHeroClass,
        QueuedNpcId = source.QueuedNpcId, QueuedNpcBand = source.QueuedNpcBand,
        MainDeck = source.MainDeck.Select(Copy).ToList(), Sideboards = source.Sideboards.Select(Copy).ToList(),
        CraftedAndOwnedBaseCardIds = source.CraftedAndOwnedBaseCardIds.ToList()
    };

    private static LocalAchievementDeckCard Card(string id, int copies, string type, string cardClass, int cost, string rarity, string set) =>
        new LocalAchievementDeckCard { BaseCardId = id, Copies = copies, CardType = type, CardClass = cardClass,
            BaseCost = cost, Rarity = rarity, CardSet = set };

    private static LocalAchievementDeckCard Copy(LocalAchievementDeckCard card) =>
        Card(card.BaseCardId, card.Copies, card.CardType, card.CardClass, card.BaseCost, card.Rarity, card.CardSet);

    private static Game StartingDeckFixture(string cardId, CardClass heroClass = CardClass.MAGE)
    {
        var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = heroClass, Player2HeroClass = CardClass.MAGE,
            Player1Deck = new[] { Cards.FromId(cardId) }.Concat(Enumerable.Repeat(Cards.FromId("CS2_182"), 29)).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var held in player.HandZone.Where(x => x.Card.Id != cardId).ToArray())
                player.SetasideZone.Add(player.HandZone.Remove(held));
            player.BaseMana = 10;
        }
        return game;
    }

    private static Game ClassFixture(CardClass heroClass, bool corpses = false)
    {
        var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            EnableCorpses = corpses, Player1HeroClass = heroClass, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
            player.BaseMana = 10;
        }
        return game;
    }

    private static Game CorpseFixture() => ClassFixture(CardClass.DEATHKNIGHT, true);

    private static IPlayable StartingCard(Controller player, string cardId)
    {
        var card = player.HandZone.FirstOrDefault(x => x.Card.Id == cardId);
        return card ?? Generic.Draw(player, player.DeckZone.First(x => x.Card.Id == cardId));
    }

    private static void PlayKeepingMana(Game game, Controller player, string id, ICharacter target = null)
    {
        var source = Add(player, id);
        int before = player.UsedMana;
        var task = player.Options().FirstOrDefault(x => x.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && x.Source?.Id == source.Id && x.Target?.Id == target?.Id);
        Exact(task != null && game.Process(task), "legal same-turn play rejected: " + id);
        Exact(player.UsedMana == before + source.Cost, "same-turn play paid current cost: " + id);
    }

    private static LocalAchievementEvaluation Evaluate(LocalAchievementPendingContext pending, LocalAchievementMatchEvidence evidence) =>
        LocalAchievementEvaluator.Evaluate(pending, evidence, new LocalAchievementCareer(), Array.Empty<string>());

    private static void Has(LocalAchievementEvaluation result, string id, System.Type witness)
    {
        var candidate = result.Earned.SingleOrDefault(x => x.Id == id);
        Exact(candidate != null && witness.IsInstanceOfType(candidate.Witness), id + " missing typed witness");
    }

    private static void Lacks(LocalAchievementEvaluation result, string id, string message) =>
        Exact(result.Earned.All(x => x.Id != id), message);

    private static void WrongClassLacks(Game game, LocalAchievementPendingContext pending, string id, bool npcConceded = false)
    {
        var wrong = Clone(pending);
        wrong.StartingHeroClass = pending.StartingHeroClass == "MAGE" ? "PRIEST" : "MAGE";
        var evidence = LocalAchievementCollector.From(game, game.Player1.Id, wrong);
        if (npcConceded) evidence = evidence.WithConcessions(false, true);
        Lacks(Evaluate(wrong, evidence), id, "qualifying gameplay cannot bypass " + pending.StartingHeroClass + " starting-class gate");
    }

    private static void Exact(bool value, string message) => Core2026Proof.Check(value, "achievement evaluator: " + message);
}
