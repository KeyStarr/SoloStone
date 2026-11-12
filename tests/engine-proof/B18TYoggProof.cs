using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Reflection;
using SabberStoneCore.Enchants;
using static Core2026Proof;

internal static class B18TYoggProof
{
    [GroupProof("b18-t-yogg")]
    internal static void Run()
    {
        Pinned_YoggAutonomousTurnControl();
        Pinned_CloneSnapshotInputAndSourceLifetime();
        Pinned_HumanConcessionDuringControl();
        Pinned_AutomaticChoicesAndRewind();
        Pinned_BoundedZeroCostLoop();
        Pinned_FullBoardMakesProgress();
        Pinned_HiddenInformationAndLethal();
        Unpinned_YoggAscentDiscountedSpellCostBasis();
        Unpinned_AscentSeededHandOrder();
        Pinned_DomainCapacityAndSeededOrder();
        Pinned_AbilityLifetimeAndBudget();
        Pinned_RefreshChoiceMakesProgress();
        Unpinned_CancelMadnessPrompt();
        Console.WriteLine("PASS Yogg autonomous whole turn");
    }

    private static void Unpinned_CancelMadnessPrompt()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
        B16OldGodProof.Activate(g,god); B16OldGodProof.Pick(g,"BE_009t2");
        PoolPilotProof.Resume(g,copy => {
            Core2026Proof.Check(copy.Process(ConcedeTask.Any(copy.Player1)),"ordinary Madness prompt permits concession");
            Core2026Proof.Check(!copy.Resolution.HasSuspendedEffect && copy.IdEntityDic.Values
                .Where(e=>e.Card.Id=="BE_009t2").All(e=>e[GameTag.ZONE]!=(int)Zone.PLAY),
                "cancelled Madness removes public ability actor");
        });
    }

    private static void Pinned_FullBoardMakesProgress()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        for (int i = 0; i < 7; i++) Summon(p,"CS2_231");
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        var minion = Add(p,"CS2_182");
        PoolPilotProof.Resume(g,AutonomousTurnRules.Run);
        Core2026Proof.Check(p.BoardZone.Count == 7 && minion.Zone == p.HandZone && g.CurrentPlayer == p.Opponent,
            "full board prevents minion play without stalling the controlled turn");
    }

    private static void Pinned_RefreshChoiceMakesProgress()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        p.Hero[GameTag.CLASS] = (int)CardClass.MAGE;
        SpellPool(g,"CORE_CS2_023"); var key = Add(p,"JAIL_319");
        PlayCardTask.Any(p,key).Process();
        Core2026Proof.Check(p.Choice?.Choices.Count == 2,"Skeleton Key has one real offer and refresh");
        int offer = p.Choice.Choices.Single(id=>g.IdEntityDic[id].Card.Id=="CORE_CS2_023");
        g.SetRandomSeed(19);
        PoolPilotProof.Resume(g,AutonomousTurnRules.Run);
        Core2026Proof.Check(g.IdEntityDic[offer].Zone == p.GraveyardZone && p.Hero.Damage == 0
            && g.CurrentPlayer == p.Opponent && p.Choice == null,
            "autonomous choice takes a real offer instead of optional cyclic Skeleton Key refresh");
    }

    internal static void PauseAtControlledTurn(Game g, Controller owner, IPlayable source)
    {
        AutonomousTurnRules.Grant(owner,source);
        EndTurnTask.Any(owner).Process(); // Expose the real turn boundary before the host drains it.
        Core2026Proof.Check(AutonomousTurnRules.IsActive(g),"extra owner turn is active");
    }

    private static void Pinned_CloneSnapshotInputAndSourceLifetime()
    {
        foreach (bool opponent in new[] { false,true })
        foreach (bool remove in new[] { false,true })
        {
            var g = CthunRulesProof.Fixture(); if (opponent) End(g);
            var p = g.CurrentPlayer; var god = Summon(p,"BE_009");
            PauseAtControlledTurn(g,p,god);
            if (remove) Kill(g,god); else god.Silence();
            var decision = g.GetPendingDecisions().Single(); var before = g.SaveSnapshot();
            Core2026Proof.Check(!g.Process(EndTurnTask.Any(p)),"controlled turn rejects normal player tasks");
            Core2026Proof.Check(!g.TryAnswer(new DecisionAnswer(decision.Id,p.Id,new[] {0}),out _)
                && before.SequenceEqual(g.SaveSnapshot()),"controlled turn rejects typed input without mutation");
            int turn = g.Turn;
            PoolPilotProof.Resume(g,copy => {
                AutonomousTurnRules.Run(copy);
                var owner = copy.ControllerById(p.Id);
                Core2026Proof.Check(copy.CurrentPlayer == owner.Opponent && copy.Turn == turn+1
                    && !AutonomousTurnRules.IsActive(copy) && AutonomousTurnRules.HasUsed(owner),
                    "source Silence/death cannot cancel controller turn; clone/load expire control once");
                Core2026Proof.Check(owner[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] == 0
                    && !owner.AppliedEnchantments.Any(e => e.Card.Id == "BE_009e2"),"control projection expires");
            });
        }
    }

    private static void Pinned_HumanConcessionDuringControl()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (bool pending in new[] { false, true })
        foreach (bool concedeOpponent in new[] { false, true })
        {
            var g = CthunRulesProof.Fixture(); if (opponent) End(g);
            var p = g.CurrentPlayer; var god = Summon(p,"BE_009");
            PauseAtControlledTurn(g,p,god);
            p[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] = 1;
            p[(GameTag)33422] = 37;
            if (pending)
                Core2026Proof.Check(new ActivateOldGodTask(p,god).Process() && g.Resolution.HasSuspendedEffect,
                    "saved controlled turn has a real pending ability choice");
            PoolPilotProof.Resume(g,copy => {
                var owner = copy.ControllerById(p.Id);
                var loser = concedeOpponent ? owner.Opponent : owner;
                var random = copy.Random.CaptureState(); int turn = copy.Turn;
                Core2026Proof.Check(copy.Process(ConcedeTask.Any(loser)),"human concession bypasses autonomous input lock");
                Core2026Proof.Check(copy.State == State.COMPLETE && loser.PlayState == PlayState.LOST
                    && loser.Opponent.PlayState == PlayState.WON,"concession settles winner on either controlled seat");
                Core2026Proof.Check(!copy.Resolution.HasSuspendedEffect && copy.Player1.Choice == null
                    && copy.Player2.Choice == null && copy.GetPendingDecisions().Count == 0,
                    "concession clears saved autonomous prompt");
                Core2026Proof.Check(!AutonomousTurnRules.IsActive(copy) && owner[(GameTag)33421] == 0
                    && owner[(GameTag)33422] == 0 && owner[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] == 0
                    && !owner.AppliedEnchantments.Any(e => e.Card.Id == "BE_009e2")
                    && AutonomousTurnRules.HasUsed(owner),"concession releases control without resetting once-per-game latch");
                var terminal = Canonical.State(copy); int history = copy.PowerHistory.Full.Count;
                AutonomousTurnRules.Run(copy);
                Core2026Proof.Check(copy.Turn == turn && copy.Random.CaptureState().Equals(random)
                    && terminal == Canonical.State(copy) && history == copy.PowerHistory.Full.Count,
                    "concession never resumes automatic actions or consumes choice RNG");
            });
        }
    }

    private static void Pinned_AutomaticChoicesAndRewind()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        var god = Summon(p,"BE_013");
        new ActivateOldGodTask(p,god).Process();
        Core2026Proof.Check(g.Resolution.HasSuspendedEffect,"real Old God prompt before snapshot");
        PoolPilotProof.Resume(g,AutonomousTurnRules.Run);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && OldGodRules.UsedAbilities(god) != 0,
            "active saved prompt resolves autonomously without stall");

        g = CthunRulesProof.Fixture(); p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        var rewind = Add(p,"TIME_001"); Add(p,"BE_778"); // Rewind then a nested ability-choice spell.
        PoolPilotProof.Resume(g,AutonomousTurnRules.Run);
        Core2026Proof.Check(g.Rewind == null && p.Choice == null && g.CurrentPlayer == p.Opponent,
            "autonomous policy keeps Rewind and resolves Discover");
        Core2026Proof.Check(g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
            .Any(block => block.Source == rewind.Id && block.BlockType == BlockType.PLAY),
            "autonomous Rewind Keep cannot discard earlier native history");
        End(g); Play(g,p,"TIME_001"); RewindTestHelpers.AnswerRewind(g);
        Core2026Proof.Check(AutonomousTurnRules.HasUsed(g.Player1),"ordinary Rewind retains consumed controller latch");
    }

    private static void Pinned_BoundedZeroCostLoop()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        Summon(p,"EX1_608"); var banana = Add(p,"TIME_042t"); g.AuraUpdate();
        Core2026Proof.Check(banana.Cost == 0,"real Apprentice makes self-replacing Infinite Banana free");
        int turn = g.Turn;
        AutonomousTurnRules.Run(g);
        int plays = g.HistoryFacts.Query.Plays().Count(f => f.DbfId == Cards.FromId("TIME_042t").AssetId);
        Core2026Proof.Check(g.Turn == turn+1 && g.CurrentPlayer == p.Opponent && plays == 128
            && p.HandZone.Any(c => c.Card.Id == "TIME_042t"),"128 free self-replacing plays terminate despite legal actions remaining");
        g = CthunRulesProof.Fixture(); p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        Summon(p,"EX1_608"); Add(p,"TIME_042t"); g.AuraUpdate();
        p[(GameTag)33422] = 127;
        PoolPilotProof.Resume(g,copy => {
            AutonomousTurnRules.Run(copy);
            Core2026Proof.Check(copy.HistoryFacts.Query.Plays().Count(f=>f.DbfId==Cards.FromId("TIME_042t").AssetId)==1,
                "clone/snapshot preserves remaining one-action budget");
        });
    }

    private static void Pinned_HiddenInformationAndLethal()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p);
        Add(p,"CS2_029"); Add(p.Opponent,"CS2_023");
        var other = g.CloneExact(history:true);
        Generic.ChangeEntityBlock(other.Player2,other.Player2.HandZone[0],Cards.FromId("CS2_062"),true);
        Generic.ChangeEntityBlock(other.Player2,other.Player2.DeckZone[0],Cards.FromId("CS2_029"),true);
        AutonomousTurnRules.Run(g); AutonomousTurnRules.Run(other);
        Core2026Proof.Check(g.Player2.Hero.Health == other.Player2.Hero.Health && g.Player1.UsedMana == other.Player1.UsedMana
            && g.Random.CaptureState().Equals(other.Random.CaptureState()),"opponent hidden identities never affect policy");
        var fair = FairBotState.Create(g,1,77,DeckStyle.Tempo);
        var view = (Game)typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fair);
        Core2026Proof.Check(view.Player2.HandZone.All(c => c is Unknown) && view.Player1.DeckZone.All(c => c is Unknown)
            && view.Player2.DeckZone.All(c => c is Unknown),"fair view retains no hand/deck identities after autonomous turn");
        g = CthunRulesProof.Fixture(); p = g.Player1;
        PauseAtControlledTurn(g,p,p.Hero); PoolPilotProof.EmptyHand(p); Add(p,"CS2_029"); p.Opponent.Hero.Damage = 29;
        AutonomousTurnRules.Run(g);
        Core2026Proof.Check(g.State == State.COMPLETE && p.Opponent.PlayState == PlayState.LOST
            && p[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] == 0,"lethal terminates control without concession");
    }

    internal static void SpellPool(Game game, params string[] ids)
    {
        var xml = OfflinePoolFixtures.FixtureCatalog(ids); var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(r => ids.Contains((string)r.Attribute("cardId"))).ToArray());
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game,OfflinePoolFixtures.Manifest(xml));
    }

    private static void Unpinned_YoggAscentDiscountedSpellCostBasis()
    {
        foreach (bool aura in new[] {false,true})
        foreach (bool full in new[] {false,true})
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
            var apprentice = aura ? Summon(p,"EX1_608") : null;
            var spell = Add(p,"CS2_046");
            CostRules.AddModifier((Playable)spell,EffectOperator.SUB,aura ? 2 : 3); g.AuraUpdate();
            Core2026Proof.Check(spell.Cost == 2,"discounted five-Cost spell has effective Cost two");
            var unchanged = Add(p,"CS2_182"); if(full) PoolPilotProof.FillHand(p);
            SpellPool(g,"CORE_UNG_952");
            B16OldGodProof.Activate(g,god);
            PoolPilotProof.Resume(g,copy => B16OldGodProof.Pick(copy,"BE_009t3"));
            Core2026Proof.Check(spell.Card.Id == "CORE_UNG_952" && spell.Cost == 2 && unchanged.Card.Id == "CS2_182"
                && p.HandZone.Count == (full ? 10 : 2),"Ascent transforms same ordered slot, retains effective price even at full capacity");
            PoolPilotProof.Resume(g,copy => {
                if(aura) { ((Minion)copy.IdEntityDic[apprentice.Id]).Silence(); copy.AuraUpdate(); }
                Core2026Proof.Check(copy.IdEntityDic[spell.Id].Cost == (aura ? 3 : 2),"fixed adjustment applies continuous aura exactly once and survives save/load");
                var duplicate = Generic.Copy(copy.Player1,copy.Player1.Hero,copy.IdEntityDic[spell.Id],Zone.HAND);
                Core2026Proof.Check(duplicate == null || duplicate.Cost == (aura ? 3 : 2),"ordinary hand copy retains Ascent adjustment");
            });
        }
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
            var zero = Add(p,"GAME_005"); var high = Add(p,"CORE_EX1_312"); high.Cost = 15;
            SpellPool(g,"CORE_CS2_023");
            B16OldGodProof.Use(g,god,"BE_009t3");
            Core2026Proof.Check(zero.Card.Id == "CORE_CS2_023" && zero.Cost == 0 && high.Card.Id == "CORE_EX1_312" && high.Cost == 15,
                "zero-Cost bucket transforms; empty eighteen-Cost bucket leaves original unchanged without clamp");
        }
        foreach (bool full in new[] {false,true})
        {
            // Ascent into a Shatter parent: the kept Cost lands before the split, so both halves inherit it.
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
            var spell = Add(p,"CS2_023"); spell.Cost = Cards.FromId("CATA_489").Cost - 3; int id = spell.Id;
            Add(p,"CS2_182"); // Separator: adjacent halves would rejoin at once.
            if(full) PoolPilotProof.FillHand(p);
            SpellPool(g,"CATA_489");
            B16OldGodProof.Use(g,god,"BE_009t3");
            var left = p.HandZone.Single(c => c.Card.Id == "CATA_489t");
            var right = full ? null : p.HandZone.Single(c => c.Card.Id == "CATA_489t2");
            Core2026Proof.Check(g.IdEntityDic[id].Zone != p.HandZone && left.Cost == Math.Max(0, Cards.FromId("CATA_489t").Cost - 3)
                && (full ? p.GraveyardZone.Any(c => c.Card.Id == "CATA_489t2") : right.Cost == left.Cost),
                "Ascent into a Shatter parent keeps its Cost on the halves; full hand burns the right half");
        }
        var live = CthunRulesProof.Fixture();
        Console.WriteLine("Yogg Ascent buckets " + string.Join(",",Enumerable.Range(3,16).Select(cost => cost+":"+
            OfflineCardPools.Candidates(live,CardClass.MAGE,OfflinePoolFilter.Random(type:CardType.SPELL).WithCost(cost),132547).Count)));
    }

    private static void Pinned_DomainCapacityAndSeededOrder()
    {
        foreach(int seed in new[] {1,8,19})
        foreach(bool reverse in new[] {false,true})
        foreach(bool full in new[] {false,true})
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
            PoolPilotProof.ClassDeck(p);
            string[] ids = {"CS2_029","CS2_023","CS2_024"}; if(reverse) Array.Reverse(ids);
            var spells = ids.Select(id => Entity.FromCard(p,Cards.FromId(id),zone:p.DeckZone)).ToArray();
            var minion = Entity.FromCard(p,Cards.FromId("CS2_182"),zone:p.DeckZone);
            if(full) PoolPilotProof.FillHand(p);
            g.SetRandomSeed(seed); B16OldGodProof.Activate(g,god);
            PoolPilotProof.Resume(g,copy => B16OldGodProof.Pick(copy,"BE_009t4"));
            int[] order = seed == 1 ? new[] {0,2,1} : seed == 8 ? new[] {0,1,2} : new[] {2,1,0};
            var moved = full ? p.GraveyardZone.Where(c => spells.Any(s => s.Id == c.Id)) : p.HandZone.Where(c => spells.Any(s => s.Id == c.Id));
            Core2026Proof.Check(moved.Select(c=>c.Id).SequenceEqual(order.Select(i=>spells[i].Id)) && g.Random.DrawCount == 3,
                "Domain samples physical spells without replacement in seeded order, including burns");
            Core2026Proof.Check(p.DeckZone.Count == 1 && p.DeckZone[0].Id == minion.Id && p.Hero.Damage == 0,
                "short tutor bucket causes no fatigue or non-spell draw");
        }
    }

    private static void Unpinned_AscentSeededHandOrder()
    {
        string[] bucket = { "RLK_060","CORE_UNG_952","CORE_TSC_650" };
        foreach (int seed in new[] {1,8,19})
        foreach (bool reverse in new[] {false,true})
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
            SpellPool(g,bucket);
            string[] originals = {"CS2_029","CS2_023","CS2_024"}; if(reverse) Array.Reverse(originals);
            var spells = originals.Select(id=>Add(p,id)).ToArray(); foreach(var spell in spells) spell.Cost=2;
            g.SetRandomSeed(seed); B16OldGodProof.Activate(g,god);
            PoolPilotProof.Resume(g,copy=>B16OldGodProof.Pick(copy,"BE_009t3"));
            int[] picks = seed == 1 ? new[] {0,2,0} : seed == 8 ? new[] {0,0,0} : new[] {2,2,1};
            Core2026Proof.Check(spells.Select(s=>s.Card.Id).SequenceEqual(picks.Select(i=>bucket[i]))
                && p.HandZone.Select(s=>s.Id).SequenceEqual(spells.Select(s=>s.Id)) && spells.All(s=>s.Cost==2)
                && g.Random.DrawCount==3,"Ascent chooses independently with replacement in original hand order and consumes three RNG draws");
        }
    }

    private static void Pinned_AbilityLifetimeAndBudget()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_009");
        B16OldGodProof.Use(g,god,"BE_009t1");
        Core2026Proof.Check(god.NumAttacksThisTurn == 0,"first ability is free on summon turn");
        B16OldGodProof.Activate(g,god); B16OldGodProof.Pick(g,"BE_009t2");
        PoolPilotProof.Resume(g,copy=>CoreCostProof.Answer(copy,copy.Player2.Hero.Id));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10 && god.NumAttacksThisTurn == 1 && !god.CanAttack,
            "Madness damages selected character, second ability spends normal attack");
        End(g); End(g); Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 16,"unused Brain expires at turn end");
        god.Silence(); Core2026Proof.Check(!OldGodRules.CanActivate(p,god),"Silence removes remaining ability access");
        new ReturnHandTask(EntityType.TARGET).Process(g,p,god,god);
        Core2026Proof.Check(OldGodRules.UsedAbilities(god) == 0,"leaving board resets per-instance abilities");
    }

    internal static void Pinned_YoggAutonomousTurnControl()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        var card = Cards.FromId("BE_009");
        Core2026Proof.Check(card != null && card.Implemented, "Yogg whole-turn facility is registered");
        var yogg = Add(p,"BE_009"); int turn = g.Turn;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,yogg)), "Yogg play accepted");
        Core2026Proof.Check(g.CurrentPlayer == p.Opponent && g.Turn == turn + 2,
            "Yogg ends original turn, takes exactly one owner turn and returns to opponent");
        Core2026Proof.Check(p.Choice == null && p.Opponent.Choice == null && !g.Resolution.HasSuspendedEffect,
            "no autonomous choice left for human");
        Core2026Proof.Check(OldGodRules.UsedAbilities((Minion)yogg) == 12,
            "autonomous turn starts with Domain then Ascent within two-action budget");
        End(g); p.UsedMana = 0;
        var second = Add(p,"BE_009"); turn = g.Turn;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,second)) && g.Turn == turn,
            "once-per-game belongs to controller, not each copy");
    }
}
