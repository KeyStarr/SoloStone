using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Reflection;
using System.Runtime.Serialization;
using static Core2026Proof;

internal static class AchievementHistoryProof
{
    private static void Exact(bool value,string message)=>Core2026Proof.Check(value,"achievement history: "+message);

    internal static void Run()
    {
        DamageDeathAndAttack();
        ActionOutcomes();
        BurnDeathrattleAndBoundaries();
        LifecycleAndTransientState();
        PlayerActionBoundary();
        PersistenceProjectionAndRewind();
        FreezeEventOncePerFreeze();
        UnexpectedEvidenceShapeDuringRewind();
        AcceptTwiceRebornAndStickyVerdict();
        Console.WriteLine("PASS achievement history: causal outcomes/transient state/clone/snapshot/fair projection/rewind");
    }

    private static void DamageDeathAndAttack()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var attacker=Summon(p,"CS2_182");var defender=Summon(p.Opponent,"CS2_231");
        Attack(g,attacker,defender);
        var attack=g.HistoryFacts.Query.CombatStarts().Single(f=>f.AttackerId==attacker.Id);
        var combat=g.HistoryFacts.Query.Events<DamageResolvedHistoryFact>().Where(f=>f.AttackId==attack.AttackId).ToArray();
        Exact(attack.AttackDamage==attacker.AttackDamage && attack.AttackerDefinition.ReadDefinition().Id==attacker.Card.Id
            && attack.TargetPreHitHealth==1 && combat.Any(f=>f.VictimId==defender.Id && f.Cause==HistoryDamageCause.Combat),
            "attack start freezes definition, attack, target pre-hit state, and causal damage link");
        var death=g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==defender.Id);
        Exact(death.SourceId==attacker.Id && death.SourceControllerId==p.Id && death.Cause==HistoryDeathCause.Combat
            && death.ResolutionId>0,"combat death freezes source/controller/cause/resolution");

        var storm=HistoryFactsProof.Fixture();var stormOwner=storm.Player1;
        var brewer=Summon(stormOwner,"TLC_107");var stormTarget=Summon(stormOwner.Opponent,"CS2_182");stormTarget.BaseHealth=30;
        Attack(storm,brewer,stormTarget);
        var stormAttack=storm.HistoryFacts.Query.CombatStarts().Single();
        var stormDamage=storm.HistoryFacts.Query.Damage().Where(f=>f.SourceId==brewer.Id).ToArray();
        Exact(stormDamage.Length==2 && stormDamage[0].AttackId==0 && stormDamage[0].Cause==HistoryDamageCause.SpellOrEffect
            && stormDamage[1].AttackId==stormAttack.AttackId && stormDamage[1].Cause==HistoryDamageCause.Combat,
            "Stormbrewer pre-combat trigger damage is not attributed to combat");
        RoundTrip(storm,stormOwner.PlayerId);

        var redirected=HistoryFactsProof.Fixture();var redirectOwner=redirected.Player1;
        var redirectAttacker=Summon(redirectOwner,"CS2_182");var originalTarget=Summon(redirectOwner.Opponent,"CS2_231");
        var actualTarget=Summon(redirectOwner.Opponent,"CS2_182");actualTarget.BaseHealth=20;int combatAttack=redirectAttacker.AttackDamage+7;
        void BuffAndRedirect(IEntity source)
        { if(source==redirectAttacker) { redirectAttacker.AttackDamage=combatAttack;redirected.CurrentEventData.EventTarget=actualTarget; } }
        redirected.TriggerManager.AttackTrigger+=BuffAndRedirect;
        try { Attack(redirected,redirectAttacker,originalTarget); } finally { redirected.TriggerManager.AttackTrigger-=BuffAndRedirect; }
        var redirectedAttack=redirected.HistoryFacts.Query.CombatStarts().Single();
        Exact(redirectedAttack.AttackDamage==combatAttack && redirectedAttack.TargetId==actualTarget.Id
            && redirectedAttack.TargetPreHitHealth==20 && redirectedAttack.TargetId!=originalTarget.Id,
            "attack witness freezes post-trigger Attack and redirected combat target");
        RoundTrip(redirected,redirectOwner.PlayerId);

        var stale=Summon(p.Opponent,"CS2_182");var first=Summon(p,"CS2_231");var second=Summon(p,"CS2_231");
        stale.TakeDamage(first,stale.Health);
        stale.Reset();
        stale.TakeDamage(second,stale.Health);g.DeathProcessingAndAuraUpdate();
        var afterReset=g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==stale.Id);
        Exact(afterReset.SourceId==second.Id && afterReset.SourceId!=first.Id,
            "reset clears stale fatal source before a later different-source death");

        var destroyed=Summon(p.Opponent,"CS2_182");destroyed.Destroy();g.DeathProcessingAndAuraUpdate();
        Exact(g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==destroyed.Id).Cause==HistoryDeathCause.Destroy,
            "direct destruction is distinct from damage death");

        var shielded=Summon(p.Opponent,"CS2_231");shielded[GameTag.DIVINE_SHIELD]=1;int deaths=g.HistoryFacts.Query.Deaths().Count();
        shielded.TakeDamage(first,99);g.DeathProcessingAndAuraUpdate();
        Exact(shielded.Zone==p.Opponent.BoardZone && g.HistoryFacts.Query.Deaths().Count()==deaths,
            "prevented lethal damage emits no death evidence");
        var noRescue=Summon(p.Opponent,"CS2_231");noRescue.TakeDamage(first,noRescue.Health);noRescue.TakeHeal(second,1);g.DeathProcessingAndAuraUpdate();
        Exact(g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==noRescue.Id).SourceId==first.Id,
            "healing cannot rescue an already marked minion, so frozen first fatal source remains authoritative");
        var multiHit=Summon(p.Opponent,"CS2_231");multiHit.TakeDamage(first,multiHit.Health);multiHit.TakeDamage(second,1);g.DeathProcessingAndAuraUpdate();
        var multiDeath=g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==multiHit.Id);
        var firstFatalHit=g.HistoryFacts.Query.Damage().Single(f=>f.VictimId==multiHit.Id && f.SourceId==first.Id);
        Exact(multiDeath.FatalDamageSequence==firstFatalHit.Sequence && multiDeath.SourceId==first.Id,
            "fatal cause binds the exact accepted lethal hit, not a later hit before death processing");
        RejectCorruption<MinionDiedFact>(g,"SourceId",p.Hero.Id,"fatal death linked to unrelated valid source");
        RoundTrip(g,p.PlayerId);
    }

    private static void ActionOutcomes()
    {
        CounterAndSpellBody();
        ComboManaCorpseAndExtraTurn();
    }

    private static void CounterAndSpellBody()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        Play(g,p,"CORE_EX1_287");End(g);
        var countered=(Spell)Play(g,p.Opponent,"CORE_EX1_312");
        var counter=g.HistoryFacts.Query.Counters().Single();
        Exact(counter.SpellEntityId==countered.Id && counter.BaseCost==8 && counter.CasterControllerId==p.Opponent.Id
            && counter.CounterControllerId==p.Id && counter.CastId>0,"counter fact freezes spell cost, caster, counter owner, and cast");
        Exact(!g.HistoryFacts.Query.Events<SpellBodyStartedFact>().Any(f=>f.SpellEntityId==countered.Id),
            "countered spell never records body start");
        var live=(Spell)Play(g,p.Opponent,"GAME_005");
        Exact(g.HistoryFacts.Query.Events<SpellBodyStartedFact>().Single(f=>f.SpellEntityId==live.Id).CastId
            ==g.HistoryFacts.Query.Casts().First(f=>f.EntityId==live.Id).CastId,"uncountered spell body links to its cast");
        RejectCorruption<SpellCounteredFact>(g,"CastId",g.HistoryFacts.Query.Casts().First(f=>f.EntityId==live.Id).CastId,
            "counter linked to unrelated valid cast");
        var miss=HistoryFactsProof.Fixture();var missOwner=miss.Player1;var target=Summon(missOwner.Opponent,"CS2_182");
        var untargeted=(Spell)Add(missOwner,"CS2_029");TriggerManager.TriggerHandler remove=_=>Generic.ReturnToHandBlock(missOwner.Opponent,target);
        miss.TriggerManager.CastSpellTrigger+=remove;PlayEntity(miss,missOwner,untargeted,target);miss.TriggerManager.CastSpellTrigger-=remove;
        Exact(!miss.HistoryFacts.Query.Counters().Any(),"target removal/fizzle is not a counter");
        RoundTrip(g,p.PlayerId);
    }

    private static void ComboManaCorpseAndExtraTurn()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var plain=(Spell)Play(g,p,"EX1_124",p.Opponent.Hero);
        Exact(!g.HistoryFacts.Query.Combos().Any(f=>f.CardEntityId==plain.Id),"printed Combo without activation emits no success");
        Play(g,p,"GAME_005");var combo=(Spell)Play(g,p,"EX1_124",p.Opponent.Hero);
        var comboFact=g.HistoryFacts.Query.Combos().Single(f=>f.CardEntityId==combo.Id);
        Exact(g.HistoryFacts.Query.Plays().Single(f=>f.PlayId==comboFact.PlayId).EntityId==combo.Id,
            "activated Combo links to committed play");

        int manaFacts=g.HistoryFacts.Query.MaxManaChanges().Count();Generic.AddTempMana(p,1);
        Exact(g.HistoryFacts.Query.MaxManaChanges().Count()==manaFacts,"temporary mana is not maximum-mana evidence");
        int old=p.BaseMana;Generic.ChangeManaCrystal(p,-2,false);
        var mana=g.HistoryFacts.Query.MaxManaChanges().Last();
        Exact(mana.OldMaximum==old && mana.NewMaximum==old-2 && !mana.Temporary,"permanent maximum-mana delta is exact");

        var corpseGame=CorpseFixture();var corpseOwner=corpseGame.Player1;var corpseSource=Summon(corpseOwner,"CS2_231");CorpseRules.Gain(corpseOwner,3);
        Exact(!CorpseRules.TrySpend(corpseOwner,4) && !corpseGame.HistoryFacts.Query.CorpsesSpent().Any(),"failed corpse spend emits nothing");
        Exact(CorpseRules.TrySpend(corpseOwner,3,corpseSource) && corpseGame.HistoryFacts.Query.CorpsesSpent().Single().Amount==3,
            "committed corpse spend records exact amount and source");

        var withoutProvenance=g.CloneExact(history:true);TurnQueueRules.Prepend(withoutProvenance,withoutProvenance.Player1);
        var withProvenance=g.CloneExact(history:true);long operation=withProvenance.Facts.AllocateOperation();
        TurnQueueRules.PrependGranted(withProvenance,withProvenance.Player1,operation);
        withProvenance.Facts.ExtraTurnGranted(operation,withProvenance.Player1,combo);
        Exact(withoutProvenance.Hash()==withProvenance.Hash(),
            "turn provenance changes no gameplay hash beyond the identical owner queue");
        Exact(Game.LoadSnapshot(withProvenance.SaveSnapshot()).HistoryFacts.Query.Events<ExtraTurnGrantedFact>().Single().OperationId==operation,
            "queued grant provenance survives validated snapshot");

        var extra=HistoryFactsProof.Fixture();var owner=extra.Player1;var warp=(Spell)Play(extra,owner,"UNG_028t");
        var grant=extra.HistoryFacts.Query.Events<ExtraTurnGrantedFact>().Single();
        Exact(owner.NumTurnsLeft==2 && !TurnQueueRules.Active(extra),"extra-turn grant preserves legacy NumTurnsLeft state");
        var pendingExtra=Game.LoadSnapshot(extra.SaveSnapshot());
        Exact(pendingExtra.Player1.NumTurnsLeft==2 && pendingExtra.HistoryFacts.Query.Events<ExtraTurnGrantedFact>().Single().OperationId==grant.OperationId,
            "pending legacy extra-turn provenance survives validated snapshot");
        End(extra);
        var started=extra.HistoryFacts.Query.ExtraTurnsStarted().Single();
        Exact(grant.SourceId==warp.Id && started.OperationId==grant.OperationId && extra.CurrentPlayer==owner,
            "extra-turn start links exact grant operation and retains owner");
        var scheduled=HistoryFactsProof.Fixture();TurnQueueRules.Prepend(scheduled,scheduled.Player1);End(scheduled);
        Exact(!scheduled.HistoryFacts.Query.ExtraTurnsStarted().Any(),"non-grant scheduled turn is not achievement extra-turn evidence");
        RoundTrip(g,p.PlayerId);RoundTrip(corpseGame,corpseOwner.PlayerId);RoundTrip(extra,owner.PlayerId);
    }

    private static void BurnDeathrattleAndBoundaries()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var operatorCard=(Minion)Add(p,"CAP_004");
        Exact(g.Process(PlayCardTask.Any(p,operatorCard,zonePosition:0,chooseOne:2)),"source-caused burn fixture play accepted");
        while(!p.HandZone.IsFull) Add(p,"CS2_182");
        Kill(g,operatorCard);
        var burns=g.HistoryFacts.Query.Burns().ToArray();
        Exact(burns.Length==2 && burns.All(f=>f.SourceId==operatorCard.Id && f.Reason==HistoryBurnReason.FullHand),
            "full-hand forced draws record each burned card and causal source");
        Exact(g.HistoryFacts.Query.DeathrattlesResolved().Count(f=>f.EntityId==operatorCard.Id)==1,
            "executed Deathrattle links the dead incarnation");

        var silenced=Summon(p.Opponent,"CAP_004");
        silenced.Silence();int deathrattles=g.HistoryFacts.Query.DeathrattlesResolved().Count();Kill(g,silenced);
        Exact(g.HistoryFacts.Query.DeathrattlesResolved().Count()==deathrattles,"suppressed Deathrattle emits no resolution fact");

        var ordinary=HistoryFactsProof.Fixture();var ordinaryOwner=ordinary.Player1;
        while(!ordinaryOwner.HandZone.IsFull) Add(ordinaryOwner,"CS2_182");Generic.Draw(ordinaryOwner);
        Exact(ordinary.HistoryFacts.Query.Burns().Single().SourceId==0,"normal full-hand draw burn has no card-source attribution");

        var enemyBurn=HistoryFactsProof.Fixture();var burnedOwner=enemyBurn.Player1;
        var enemyOperator=(Minion)Add(burnedOwner,"CAP_004");
        Exact(enemyBurn.Process(PlayCardTask.Any(burnedOwner,enemyOperator,zonePosition:0,chooseOne:2)),"enemy-side Operator play accepted");
        while(!burnedOwner.HandZone.IsFull) Add(burnedOwner,"CS2_182");Kill(enemyBurn,enemyOperator);
        Exact(enemyBurn.HistoryFacts.Query.Burns().All(f=>f.ControllerId==burnedOwner.Id && f.SourceControllerId==burnedOwner.Opponent.Id),
            "enemy-initiated full-hand burns retain enemy source controller and cannot credit the victim");

        var turn=HistoryFactsProof.Fixture();var turnOwner=turn.Player1;End(turn);End(turn);
        var begun=turn.HistoryFacts.Query.Events<TurnStartedFact>().Last(f=>f.ControllerId==turnOwner.Id);
        var turnDraw=turn.HistoryFacts.Query.HandEntries().Last(f=>f.ControllerId==turnOwner.Id);
        Exact(begun.HandCount==0 && begun.HeroHealth==turnOwner.Hero.BaseHealth && begun.BaseMana==10
            && turnDraw.EntryKind==HistoryHandEntryKind.TurnStartDraw,"turn-start receipt precedes and distinguishes normal draw");

        var fatigue=HistoryFactsProof.Fixture();var tired=fatigue.Player1;
        while(!tired.DeckZone.IsEmpty) tired.SetasideZone.Add(tired.DeckZone.Remove(0));Generic.Draw(tired);
        Exact(fatigue.HistoryFacts.Query.Events<DamageResolvedHistoryFact>().Last().Cause==HistoryDamageCause.Fatigue,
            "fatigue damage is distinct from ordinary self-effect damage");

        var terminal=HistoryFactsProof.Fixture();var killer=terminal.Player1;terminal.Player2.Hero.Damage=29;
        var fireball=(Spell)Play(terminal,killer,"CS2_029",terminal.Player2.Hero);
        var fatal=terminal.HistoryFacts.Query.HeroFatals().Single();var receipt=terminal.HistoryFacts.Query.Events<MatchTerminalFact>().Single();
        var terminalPlay=terminal.HistoryFacts.Query.Plays().Single(f=>f.EntityId==fireball.Id);
        var terminalAction=terminal.HistoryFacts.Query.PlayerActions().Single(f=>f.SourceId==fireball.Id);
        Exact(fatal.HeroId==terminal.Player2.Hero.Id && fatal.SourceId==fireball.Id && fatal.Cause==HistoryDeathCause.SpellOrEffect
            && fatal.FatalDamageSequence==terminal.HistoryFacts.Query.Damage().Single(f=>f.VictimId==terminal.Player2.Hero.Id).Sequence
            && terminalAction.RelatedPlayId==terminalPlay.PlayId && terminalAction.Sequence<receipt.Sequence
            && receipt.Player2State==PlayState.LOST && receipt.Player2Health<=0,"hero fatal cause, exact lethal hit, and quiescent terminal receipt are frozen");
        Exact(terminal.CloneExact(history:true).HistoryFacts.CanonicalText()==terminal.HistoryFacts.CanonicalText(),"terminal receipt survives exact clone");
        RoundTrip(g,p.PlayerId);RoundTrip(turn,turnOwner.PlayerId);RoundTrip(fatigue,tired.PlayerId);
    }

    private static void LifecycleAndTransientState()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var controlled=Summon(p.Opponent,"CS2_182");var controllerSource=Summon(p,"CS2_231");
        new ControlTask(EntityType.TARGET).Process(g,p,controllerSource,controlled,new TaskStack());
        var control=g.HistoryFacts.Query.ControlChanges().Single();
        Exact(control.EntityId==controlled.Id && control.FromControllerId==p.Opponent.Id && control.ToControllerId==p.Id
            && control.FromIncarnation!=control.ToIncarnation && control.SourceId==controllerSource.Id,
            "control change bridges old/new incarnation without changing entity");
        var natural=Summon(p,"CS2_182");
        Exact(g.HistoryFacts.Query.ControlChanges().Count()==1,"ordinary summon of same definition is not control change");

        new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(g,p,controllerSource,natural,new TaskStack());
        new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(g,p,controllerSource,natural,new TaskStack());
        Exact(g.HistoryFacts.Query.KeywordsApplied().Count(f=>f.TargetId==natural.Id)==1,"freeze records false-to-true only");

        var potionTarget=Summon(p.Opponent,"CS2_182");var potion=(Spell)Play(g,p,"CFM_621t5");
        var potionFreeze=g.HistoryFacts.Query.KeywordsApplied().Single(f=>f.TargetId==potionTarget.Id);
        Exact(potionFreeze.SourceId==potion.Id && potionFreeze.CastId>0,"Aya freeze potion uses the shared sourced freeze evidence path");

        Generic.ReturnToHandBlock(p,natural);
        Exact(g.HistoryFacts.Query.MinionsLeftPlay().Single(f=>f.EntityId==natural.Id).Incarnation>0,
            "bounce records board departure before reset");
        var old=Summon(p,"CS2_231");Generic.TransformBlock(p,Cards.FromId("CS2_182"),old);
        var form=g.HistoryFacts.Query.FormChanges().Single();
        Exact(form.OldEntityId==old.Id && form.OldDefinition.ReadDefinition().Id=="CS2_231"
            && form.NewDefinition.ReadDefinition().Id=="CS2_182" && form.OldIncarnation!=form.NewIncarnation,
            "replacement transform records both identities and definitions");

        var ogre=(Minion)Play(g,p,"TLC_253");DormantRules.Awaken(ogre);
        Exact(g.HistoryFacts.Query.Awakenings().Single(f=>f.EntityId==ogre.Id).Incarnation>0,
            "actual dormant-to-awake transition records incarnation");

        var foreign=p.Opponent.DeckZone[0];Generic.DrawFrom(p,p.Opponent,foreign);
        var transfer=g.HistoryFacts.Query.Transfers().Single(f=>f.EntityId==foreign.Id);
        Exact(transfer.FromControllerId==p.Opponent.Id && transfer.ControllerId==p.Id && transfer.FromZone==Zone.DECK,
            "cross-controller deck transfer records origin and recipient");
        Add(p,"CS2_231");Exact(g.HistoryFacts.Query.Transfers().Count()==1,"generated own card is not transfer");
        var wrongControlIncarnation=g.HistoryFacts.Query.Summons().Single(f=>f.EntityId==natural.Id).Incarnation;
        RejectCorruption<ControlChangedFact>(g,"FromIncarnation",wrongControlIncarnation,"unrelated control incarnation bridge");
        RejectCorruption<CardTransferredFact>(g,"FromZone",Zone.HAND,"transfer without matching hand-entry origin");
        RoundTrip(g,p.PlayerId);
    }

    private static void PlayerActionBoundary()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        while(!p.DeckZone.IsEmpty) p.SetasideZone.Add(p.DeckZone.Remove(0));
        var drawn=(Spell)Entity.FromCard(p,Cards.FromId("GAME_005"),zone:p.DeckZone);
        var location=LocationProofs.Location(p,"TIME_044");var target=Summon(p,"CS2_182");
        End(g);End(g);
        Exact(g.HistoryFacts.Query.HandEntries().Any(f=>f.EntityId==drawn.Id && f.EntryKind==HistoryHandEntryKind.TurnStartDraw),
            "fixture spell is the normal turn-start draw");
        Exact(!g.Process(new ActivateLocationTask(p,location)) && !g.HistoryFacts.Query.PlayerActions(g.HistoryFacts.Query.ThisTurn).Any(),
            "rejected location activation emits no player action");
        Exact(g.Process(new ActivateLocationTask(p,location,target)),"zero-cost location activation accepted");
        PlayEntity(g,p,drawn);
        var actions=g.HistoryFacts.Query.PlayerActions(g.HistoryFacts.Query.ThisTurn).ToArray();
        var spellPlay=g.HistoryFacts.Query.Plays(g.HistoryFacts.Query.ThisTurn).Single(f=>f.EntityId==drawn.Id);
        Exact(actions.Length==2 && actions[0].Kind==HistoryPlayerActionKind.ActivateLocation && actions[0].SourceId==location.Id
            && actions[1].Kind==HistoryPlayerActionKind.PlayCard && actions[1].RelatedPlayId==spellPlay.PlayId,
            "successful zero-cost activation precedes the normal-draw spell as the turn's first player action");
        var clonedActions=g.CloneExact(history:true).HistoryFacts.Query.PlayerActions().ToArray();
        Exact(clonedActions.Length==actions.Length && Enumerable.Range(0,actions.Length).All(i=>clonedActions[i].Kind==actions[i].Kind
            && clonedActions[i].SourceId==actions[i].SourceId && clonedActions[i].TargetId==actions[i].TargetId
            && clonedActions[i].RelatedPlayId==actions[i].RelatedPlayId),"accepted player actions survive exact clone");
        RoundTrip(g,p.PlayerId);

        var autonomous=HistoryFactsProof.Fixture();var autonomousOwner=autonomous.Player1;
        var automatic=(Spell)Add(autonomousOwner,"GAME_005");
        Exact(autonomous.ProcessAutonomous(PlayCardTask.Any(autonomousOwner,automatic))
            && !autonomous.HistoryFacts.Query.PlayerActions().Any(),"autonomous accepted actions do not become player actions");
    }

    private static void PersistenceProjectionAndRewind()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var attacker=Summon(p,"CS2_182");Attack(g,attacker,p.Opponent.Hero);
        var hidden=p.Opponent.DeckZone[0];Generic.DrawFrom(p,p.Opponent,hidden);
        string authoritative=g.HistoryFacts.CanonicalText();
        foreach(var copy in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
            Exact(copy.HistoryFacts.CanonicalText()==authoritative,"clone/snapshot retain exact extended ledger");
        var fork=g.CloneExact(history:true);Generic.ReturnToHandBlock(fork.Player1,(Minion)fork.IdEntityDic[attacker.Id]);
        Exact(g.HistoryFacts.CanonicalText()==authoritative,"fork tail is independent");
        var fair=g.CloneExact(history:true);fair.ProjectHistoryForFairView(p.PlayerId);
        Game.LoadSnapshot(fair.SaveSnapshot());
        Exact(fair.HistoryFacts.Query.Transfers().Single().DefinitionKnown,"recipient fair view keeps transferred card definition");
        var opposingFair=g.CloneExact(history:true);opposingFair.ProjectHistoryForFairView(p.Opponent.PlayerId);
        var hiddenTransfer=opposingFair.HistoryFacts.Query.Transfers().Single();
        Exact(hiddenTransfer.EntityId==0 && !hiddenTransfer.DefinitionKnown,"opponent fair view redacts transferred hidden hand identity");

        var before=HistoryFactsProof.Fixture();var branch=before.CloneExact(history:true);var branchTarget=Summon(branch.Player1,"CS2_182");
        new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(branch,branch.Player1,branch.Player1.Hero,branchTarget,new TaskStack());
        Exact(branch.HistoryFacts.Query.KeywordsApplied().Count()==1 && !before.HistoryFacts.Query.KeywordsApplied().Any(),
            "event applied after fork does not appear in original");

        var rewind=HistoryFactsProof.Fixture();var owner=rewind.Player1;var rewindTarget=Summon(owner,"CS2_182");
        Play(rewind,owner,"TIME_001");
        new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(rewind,owner,owner.Hero,rewindTarget,new TaskStack());
        Exact(rewind.HistoryFacts.Query.KeywordsApplied().Any(f=>f.TargetId==rewindTarget.Id),"new post-checkpoint freeze fact exists before rewind");
        RewindTestHelpers.AnswerRewind(rewind);
        Exact(!rewind.HistoryFacts.Query.KeywordsApplied().Any(f=>f.TargetId==rewindTarget.Id),"new completed fact disappears on rewind");
    }

    // A Rewind card answered over an unexpected achievement fact shape keeps playing and invalidates the match's evidence.
    private static void UnexpectedEvidenceShapeDuringRewind()
    {
        foreach(bool rewindAnswer in new[]{false,true})
        {
            var g=HistoryFactsProof.Fixture();var p=g.Player1;var target=Summon(p.Opponent,"CS2_182");
            new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(g,p,p.Hero,target,new TaskStack());
            var keyword=g.HistoryFacts.Query.KeywordsApplied().Single();
            typeof(KeywordAppliedFact).GetField("<ChangedFromFalse>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(keyword,false);
            bool strict=false;try { Game.LoadSnapshot(g.SaveSnapshot()); } catch(SerializationException) { strict=true; }
            Exact(strict && g.HistoryFacts.EvidenceValid,"external load rejects the unexpected shape; live saving does not judge it");
            Play(g,p,"TIME_001");
            Exact(RewindTestHelpers.IsRewindOffered(g),"Rewind offered over an unexpected evidence shape");
            if(rewindAnswer) RewindTestHelpers.AnswerRewind(g); else RewindTestHelpers.KeepIfOffered(g);
            RewindTestHelpers.KeepIfOffered(g);
            Exact(g.Rewind==null && g.Player1.Choice==null && g.Player2.Choice==null && g.State==State.RUNNING,(rewindAnswer?"Rewind":"Keep")+" answer clears the choice");
            Exact(!g.HistoryFacts.EvidenceValid && g.HistoryFacts.EvidenceFailure.Contains("KeywordAppliedFact"),
                (rewindAnswer?"Rewind":"Keep")+" marks evidence invalid with the failing shape: "+g.HistoryFacts.EvidenceFailure);
            Exact(g.CloneExact(history:true).HistoryFacts.EvidenceFailure==g.HistoryFacts.EvidenceFailure,"clones keep the evidence verdict");
            int mover=g.CurrentPlayer.Id;End(g);
            Exact(g.CurrentPlayer.Id!=mover && g.State==State.RUNNING && !g.HistoryFacts.EvidenceValid,"game continues after "+(rewindAnswer?"Rewind":"Keep")
                +" ("+g.CurrentPlayer.Id+"/"+mover+"/"+g.State+"/"+g.HistoryFacts.EvidenceValid+")");
        }
    }

    private static void AcceptTwiceRebornAndStickyVerdict()
    {
        // Warmaul Challenger's own battle attacks run inside its play action: one player action, evidence intact.
        var g=HistoryFactsProof.Fixture();var p=g.Player1;var foe=Summon(p.Opponent,"CS2_182");
        int before=g.HistoryFacts.Query.PlayerActions().Count();
        var challenger=Play(g,p,"CORE_BT_120",foe);
        var actions=g.HistoryFacts.Query.PlayerActions().Skip(before).ToArray();
        Exact(g.HistoryFacts.Query.AttacksStarted().Any(a=>a.AttackerId==challenger.Id),"Warmaul battle attacks are recorded");
        Exact(actions.Length==1 && actions[0].Kind==HistoryPlayerActionKind.PlayCard && actions[0].SourceId==challenger.Id && g.HistoryFacts.EvidenceValid,
            "same-source attack inside a play is an effect: one PlayCard action, evidence valid ("+g.HistoryFacts.EvidenceFailure+")");

        // Reborn facts are gameplay input for Raith van Geist, also when the recorder cannot link incarnations.
        foreach(bool linked in new[]{true,false})
        {
            var r=HistoryFactsProof.Fixture();var o=r.Player1;
            if(linked) { var reborn=Summon(o,"CS2_182");reborn[GameTag.REBORN]=1;Kill(r,reborn); }
            else
            {
                var ghost=(Minion)Entity.FromCard(o,Cards.FromId("CS2_182"));var back=(Minion)Entity.FromCard(o,Cards.FromId("CS2_182"));
                using(r.Facts.Rebirth(ghost,back)) Generic.SummonBlock(r,back,-1,ghost);
            }
            var rebirths=r.HistoryFacts.Query.Reborns().Where(f=>f.ControllerId==o.Id).ToArray();
            Exact(rebirths.Length==1 && r.HistoryFacts.EvidenceValid==linked && (linked || rebirths[0].SourceIncarnation==0),
                (linked?"linked":"unlinked")+" rebirth appends its Reborn fact; only unlinked evidence is invalid");
            int board=o.BoardZone.Count;var raith=Play(r,o,"CAP_806");
            Exact(o.BoardZone.Count==board+2 && o.BoardZone.Any(m=>m!=raith && m.Card.Id=="CS2_182" && m.Id>raith.Id),
                (linked?"linked":"unlinked")+" Raith van Geist raises the reborn minion");
        }

        // A hero's Poisonous weapon is the death's poison source while the hero deals the fatal hit (transport strict reset oracle).
        var w=HistoryFactsProof.Fixture();var wp=w.Player1;var giant=Summon(wp.Opponent,"CS2_182");
        var axe=(Weapon)Play(w,wp,"CS2_106");axe[GameTag.POISONOUS]=1;
        Exact(w.Process(HeroAttackTask.Any(wp,giant)) && giant.Zone.Type==Zone.GRAVEYARD,"poisonous weapon swing kills the minion");
        var poisoned=w.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==giant.Id);
        Exact(poisoned.Cause==HistoryDeathCause.Poisonous && poisoned.SourceId==axe.Id && w.HistoryFacts.EvidenceValid,"weapon is the recorded poison source");
        bool loads=true;try { Game.LoadSnapshot(w.SaveSnapshot()); } catch(SerializationException) { loads=false; }
        Exact(loads,"strict load accepts a weapon-poison death whose fatal hit came from the hero");

        // A recorder-invalidated match stays invalid across a later Rewind.
        var s=HistoryFactsProof.Fixture();s.HistoryFacts.InvalidateEvidence("proof: recorder inconsistency");
        Play(s,s.Player1,"TIME_001");RewindTestHelpers.AnswerRewind(s);RewindTestHelpers.KeepIfOffered(s);
        Exact(s.Rewind==null && s.HistoryFacts.EvidenceFailure=="proof: recorder inconsistency","recorder verdict is sticky across Rewind");
    }

    // SetGameTagTask FROZEN raises exactly one freeze event per application (main behaviour); evidence stays one fact per new freeze.
    private static void FreezeEventOncePerFreeze()
    {
        foreach(string id in new[]{"BE_201","CATA_612","CORE_CATA_009","task","task-refreeze"})
        {
            var g=HistoryFactsProof.Fixture();var p=g.Player1;var victim=Summon(p.Opponent,"CS2_182");
            if(id=="task-refreeze") victim.IsFrozen=true;
            int events=0;var frozen=new List<int>();
            TriggerManager.TriggerHandler count=sender=>{events++;frozen.Add(sender.Id);};
            g.TriggerManager.FreezeTrigger+=count;
            int facts=g.HistoryFacts.Query.KeywordsApplied().Count();
            if(id.StartsWith("task")) new SetGameTagTask(GameTag.FROZEN,1,EntityType.TARGET).Process(g,p,p.Hero,victim,new TaskStack());
            else Play(g,p,id,id=="CATA_612"?null:victim);
            g.TriggerManager.FreezeTrigger-=count;
            int newFacts=g.HistoryFacts.Query.KeywordsApplied().Count()-facts;
            Exact(events>=1 && frozen.Distinct().Count()==frozen.Count,id+" raises one freeze event per frozen character, got "+events);
            Exact(newFacts==(id=="task-refreeze"?0:events),id+" records one keyword fact per new freeze");
        }
    }

    private static void RejectCorruption<T>(Game source,string field,object value,string label) where T:HistoryFact
    {
        var bad=Game.LoadSnapshot(source.SaveSnapshot());var fact=bad.HistoryFacts.Records.OfType<T>().First();
        typeof(T).GetField("<"+field+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fact,value);
        bool rejected=false;try { Game.LoadSnapshot(bad.SaveSnapshot()); } catch(SerializationException) { rejected=true; }
        Exact(rejected,label+" fails closed at snapshot load");
    }

    private static Game CorpseFixture()
    {
        var g=new Game(new GameConfig { RandomSeed=73,StartPlayer=1,Shuffle=false,SkipMulligan=true,History=true,Logging=false,
            FormatType=FormatType.FT_WILD,EnableCorpses=true,Player1HeroClass=CardClass.DEATHKNIGHT,Player2HeroClass=CardClass.MAGE,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        g.StartGame();foreach(var owner in new[]{g.Player1,g.Player2}) { foreach(var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));owner.BaseMana=10; }
        return g;
    }

    private static void RoundTrip(Game game,int viewerPlayerId)
    {
        Game.LoadSnapshot(game.SaveSnapshot());
        var fair=game.CloneExact(history:true);fair.ProjectHistoryForFairView(viewerPlayerId);Game.LoadSnapshot(fair.SaveSnapshot());
    }
}
