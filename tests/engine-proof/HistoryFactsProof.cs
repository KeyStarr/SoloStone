using HsSim;
using SabberStoneCore.Kettle;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HistoryFactsProof
{
    internal static Game Fixture(bool history = true, bool mulligan = false, Action<Game> beforeStart = null)
    {
        var g = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
            SkipMulligan = !mulligan, History = history, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        beforeStart?.Invoke(g);
        g.StartGame();
        if (!mulligan) foreach (var p in new[] { g.Player1, g.Player2 })
        {
            foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
            p.BaseMana = 10;
        }
        return g;
    }
    private static void BurnVisualScope()
    {
        var g = Fixture(); var p = g.Player1;
        while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var source = Entity.FromCard(p, Cards.FromId("CATA_485"), zone: p.SetasideZone);
        var victim = Summon(p.Opponent, "CS2_200");
        int cursor = g.PowerHistory.Full.Count;
        g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
        Generic.Draw(p);
        using (g.PowerHistory.Visual(g, SabberStoneCore.Presentation.VisualPhase.Damage, source, victim))
        {
            Generic.Draw(p);
            victim.TakeDamage(source, 1);
        }
        g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        var rows = g.PowerHistory.Full.Skip(cursor).ToArray();
        var meta = rows.OfType<PowerHistoryMetaData>().Single(m => m.Type == MetaDataType.BURNED_CARD);
        Check(meta.Info.Count == 2 && rows[^2] == meta && rows[^1] is PowerHistoryBlockEnd,
            "burn range emitted once at block end with live Visual scope");
        int second = Array.FindIndex(rows, e => e is PowerHistoryShowEntity show && show.Entity.Id == meta.Info[1]);
        Check(second > 0 && Array.FindIndex(rows, e => e is PowerHistorySubSpellStart) < second && victim.Damage == 1,
            "live Visual scope still starts before second burn snapshot");
        g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, p.Id, "", -1, 0));
        Generic.Draw(p);
        foreach (var copy in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            copy.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            Check(copy.PowerHistory.Last[^2] is PowerHistoryMetaData { Type: MetaDataType.BURNED_CARD, Info.Count: 1 },
                "pending burn range survives clone/snapshot inside open block");
        }
        g.PowerHistory.FlushBurnedCards();
        Check(g.PowerHistory.Last[^1] is PowerHistoryMetaData { Type: MetaDataType.BURNED_CARD, Info.Count: 1 }, "open block burns flush once");
        g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, p.Id, "", -1, 0));
        Generic.Draw(p);
        g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, victim.Id, "", -1, 0));
        int before = g.PowerHistory.Full.Count; g.PowerHistory.FlushBurnedCards();
        Check(g.PowerHistory.Full.Count == before, "flush cannot emit ancestor burns inside child block");
        g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd()); g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        Check(g.PowerHistory.Last[^2] is PowerHistoryMetaData { Type: MetaDataType.BURNED_CARD, Info.Count: 1 }, "ancestor burn emitted at its own block end");
        Console.WriteLine("PASS burned-card live Visual scope and open-block flush");
    }
    internal static void Run()
    {
        HistorySnapshotProof.Register();
        HistoryHandDisplayProof.Run();
        BurnVisualScope();
        HistoryTargetingProof.Run();
        HistoryMaterializationProof.Run();
        HistoryFairOriginProof.Run();
        HistoryResolutionProof.Run();
        HistoryRegressionProof.Run();
        var g = Fixture();
        Play(g, g.Player1, "CS2_231");
        Play(g, g.Player1, "GAME_005");
        var state = Canonical.State(g);
        Core2026Proof.Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(state))) == "AA65C2B042C32E91D1DF8FE25ADF27D3BA29C312B09B84B8790CF01AE01B00D1", "no-consumer canonical bytes changed");
        Core2026Proof.Check(g.HistoryFacts.Query.Plays().Count() == 2, "committed plays exactly once");
        Core2026Proof.Check(g.HistoryFacts.Query.Casts().Count() == 2, "cast start and completion");
        Core2026Proof.Check(g.HistoryFacts.Query.Summons().Count() == 1, "play links one summon");
        Core2026Proof.Check(g.HistoryFacts.Query.TurnsTaken(g.Player1.Id) == 1, "current turn counted");
        EventVisibility();
        AuraVisibility();
        HistoryWindowProof.Run();
        HistoryOriginProof.Run();
        HistoryOperationsProof.Run();
        HistoryCanonicalProof.Run();
        HistorySnapshotProof.Run();
        HistoryReplayProof.Run();
        HistoryCompositionProof.Run();
        Console.WriteLine("PASS history facts: plays, casts, summon, turns, old canonical bytes");
    }

    private static void Check(bool value, string message) => Core2026Proof.Check(value,"history event: "+message);
    private static void EventVisibility()
    {
        foreach (bool history in new[] { false,true })
        foreach (bool mixed in new[] { false,true })
        {
            var g=Fixture(history); var p=g.Player1; var q=g.HistoryFacts.Query;
            if(mixed) p.BoardZone.Add((BoardObject)Entity.FromCard(p,Cards.FromId("CORE_REV_990")));
            int zones=0,summons=0,deaths=0,plays=0,starts=0,finished=0,draws=0,automaticSource=0,automaticReads=0;
            g.TriggerManager.ZoneTrigger += entity => {
                if(entity is Minion m && m.Zone==p.BoardZone)
                {
                    Check(q.Summons().Last().EntityId==m.Id,"summon visible inside ZONE before later callbacks"); zones++;
                }
                if(entity is IPlayable card && card.Zone==p.HandZone)
                    Check(q.HandEntries().Last().EntityId==card.Id,"hand entry visible inside ZONE");
            };
            g.TriggerManager.AfterSummonTrigger += entity => {
                Check(q.Summons().Any(f=>f.EntityId==entity.Id),"summon visible in same summon reaction"); summons++;
            };
            g.TriggerManager.DeathTrigger += entity => {
                Check(q.Deaths().Last().EntityId==entity.Id,"death visible before same death trigger"); deaths++;
            };
            g.TriggerManager.PlayCardTrigger += entity => {
                Check(q.Plays().Last().EntityId==entity.Id,"play visible inside same play trigger"); plays++;
            };
            g.TriggerManager.CastSpellTrigger += entity => {
                Check(q.Casts().Last().EntityId==entity.Id && q.Casts().Last().Outcome==HistoryCastOutcome.Started,
                    "start visible before cast reactions"); starts++;
            };
            g.TriggerManager.AfterCastTrigger += entity => {
                Check(q.Casts().Last().EntityId==entity.Id && q.Casts().Last().Outcome!=HistoryCastOutcome.Started,
                    "cast outcome visible before AfterCast"); finished++;
            };
            g.TriggerManager.DrawTrigger += entity => {
                if (((IPlayable)entity).Zone == p.GraveyardZone)
                    Check(!q.HandEntries().Any(f => f.EntityId == entity.Id), "burn DRAW has no hand entry");
                else
                    Check(q.HandEntries().Last().EntityId==entity.Id && q.HandEntries().Last().EntryKind==HistoryHandEntryKind.Draw,
                        "draw entry visible inside DRAW trigger");
                draws++;
                if(automaticSource!=0)
                {
                    Check(q.Casts().Last(f=>f.EntityId==automaticSource).Outcome==HistoryCastOutcome.Started,
                        "automatic cast start visible inside its real spell-body draw trigger");automaticReads++;
                }
            };
            var played=(Minion)Play(g,p,"CS2_231");
            var fromPlay=q.Summons().Single();
            Check(fromPlay.FromPlayId==q.Plays().Single().PlayId,"append-only play/incarnation link");
            var summoned=Summon(p,"CS2_231");
            Check(q.Plays().Count()==1 && q.Summons().Count()==2,"summon is not play; location is not minion");
            Kill(g,played); Kill(g,summoned);
            Check(zones==2 && summons==2 && deaths==2 && plays==1,"ordinary/mixed events exactly once");
            Check(q.Deaths().First().FromPlayId==fromPlay.FromPlayId && q.Deaths().First().Incarnation==fromPlay.Incarnation,
                "death retains incarnation and played-turn link");
            var counter=(Spell)Add(p,"GAME_005");
            TriggerManager.TriggerHandler intercept = entity => ((Spell)entity).IsCountered=true;
            g.TriggerManager.CastSpellTrigger += intercept;
            PlayEntity(g,p,counter);
            g.TriggerManager.CastSpellTrigger -= intercept;
            Check(q.Casts().Count(f=>f.EntityId==counter.Id)==2
                && q.Casts().Single(f=>f.EntityId==counter.Id && f.Outcome!=HistoryCastOutcome.Started).Outcome==HistoryCastOutcome.Countered
                && q.Plays().Any(f=>f.EntityId==counter.Id),"countered: CardPlayed+Countered, no Completed");
            var automatic=(Spell)Entity.FromCard(p,Cards.FromId("CS2_023"));
            automaticSource=automatic.Id;
            Generic.CastSpell(p,g,automatic,null,0);automaticSource=0;
            Check(automaticReads==2,"automatic cast actual draw reactions observed");
            Check(!q.Plays().Any(f=>f.EntityId==automatic.Id) && q.Casts().Count(f=>f.EntityId==automatic.Id && f.CasterKind==HistoryCastKind.Effect)==2,
                "automatic Effect cast no CardPlayed");
            var repeat=(Spell)Entity.FromCard(p,Cards.FromId("CS2_023"));
            automaticSource=repeat.Id;
            new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,p.Hero.Id).CastSpell(repeat.Id,HistoryCastKind.Repeat);
            automaticSource=0;
            Check(automaticReads==4,"repeat cast actual draw reactions observed");
            Check(q.Casts().Count(f=>f.EntityId==repeat.Id && f.CasterKind==HistoryCastKind.Repeat)==2,"explicit Repeat invocation");
            foreach(bool indexed in new[]{false,true})
            {
                var gear=Entity.FromCard(p,Cards.FromId("JAIL_386t")); p.DeckZone.Add(gear);
                if(indexed) Generic.Draw(p,p.DeckZone.Count-1); else Generic.Draw(p,gear);
                Check(q.Casts().Count(f=>f.EntityId==gear.Id && f.CasterKind==HistoryCastKind.Drawn)==2,"Found Gear both TopdeckTask sites");
                int castsBefore=q.Casts().Count();
                var drawn=(Spell)Entity.FromCard(p,Cards.FromId("HISTORY_PROOF_DRAWN")); p.DeckZone.Add(drawn);
                if(indexed) Generic.Draw(p,p.DeckZone.Count-1); else Generic.Draw(p,drawn);
                Check(q.Casts().Count(f=>f.EntityId==drawn.Id)==2 && q.Casts().Count()==castsBefore+4,
                    "TopdeckTask-to-CastSpell deduplicates same invocation and preserves nested distinct cast");
            }
            while(!p.HandZone.IsFull) Add(p,"CS2_231");
            int entries=q.HandEntries().Count(), drawsBefore=draws; var burned=Entity.FromCard(p,Cards.FromId("CS2_231"));p.DeckZone.Add(burned);
            Generic.Draw(p,burned);
            Check(q.HandEntries().Count()==entries && burned.Zone==p.GraveyardZone && draws==drawsBefore+1,
                "burned draw emits DRAW exactly once without HandEntered");
            Check(starts==1 && finished==1 && draws>=4,"cast/draw trigger paths exercised");
        }
        Console.WriteLine("PASS history events: real trigger visibility, ordinary/mixed boards, countered/automatic/repeat/drawn, burn negatives");
    }

    private static void AuraVisibility()
    {
        foreach(bool mixed in new[]{false,true})
        {
            var g=Fixture();var p=g.Player1;
            if(mixed) LocationProofs.Location(p);
            var owner=Summon(p,"CS2_222");int checks=0;
            var aura=new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.BOARD,
                new SabberStoneCore.Enchants.Effect(GameTag.ATK,SabberStoneCore.Enchants.EffectOperator.ADD,1)) {
                Condition=new SabberStoneCore.Conditions.SelfCondition(entity=>{
                    if(entity.Card.Id=="CS2_231")
                    {
                        var fact=g.HistoryFacts.Query.Summons().Last(f=>f.EntityId==entity.Id);
                        Check(fact.Definition.Materialize().ATK==1,"summon snapshot visible before real aura application");checks++;
                    }
                    return true;
                })
            };
            aura.Activate(owner);g.AuraUpdate();
            var body=Summon(p,"CS2_231");
            Check(checks>0 && body.AttackDamage==3,"ordinary/mixed aura callback and modifiers exercised");
            var location=mixed?p.BoardZone.GetLocations().Single():LocationProofs.Location(p);
            int deaths=g.HistoryFacts.Query.Deaths().Count();
            location.Destroy();body.ToBeDestroyed=true;g.DeathProcessingAndAuraUpdate();
            Check(g.HistoryFacts.Query.Deaths().Count()==deaths+1 && g.HistoryFacts.Query.Deaths().Last().EntityId==body.Id,
                "mixed pending minion/location deaths record only minion");
        }
        Console.WriteLine("PASS history aura/death ordering: real aura callback, frozen definition, mixed pending location/minion death");
    }
}
