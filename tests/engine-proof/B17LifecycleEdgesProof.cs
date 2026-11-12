using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17LifecycleEdgesProof
{
    internal static void Run()
    {
        Unpinned_HuskSimultaneousAndConcession();
        Unpinned_RebornAuraAndExpiry();
        Unpinned_RecruitCopyAndBounce();
        Unpinned_RecruitControlTransfer();
        Unpinned_AmitutSeededOrderAndCapacity();
        Unpinned_UntimelyOwnerAndLateTurn();
        Unpinned_BananaRepeatAndFullHand();
        Unpinned_GuardianHealingAndStacking();
        Unpinned_AllGrantsSurviveRewind();
        Unpinned_UntimelyFreedCapacity();
        PendingDeathContinuationChoice();
        Unpinned_RecruitTransformAdmission();
        HistoryOff();
    }
    private static void Unpinned_HuskSimultaneousAndConcession()
    {
        foreach (int seat in new[]{1,2})
        foreach (bool both in new[]{false,true})
        foreach (int corpses in new[]{0,1,25})
        {
            var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
            foreach (var owner in new[]{p,p.Opponent}) {
                if(g.CurrentPlayer!=owner) End(g);
                if(both || owner.PlayerId==seat) Play(g,owner,"TIME_618");
                CorpseRules.Gain(owner,corpses);
            }
            PoolPilotProof.Resume(g.CloneExact(history:true),next=>{
                next.Player1.Hero.TakeDamage(next.Player2.Hero,100);
                next.Player2.Hero.Destroy();next.DeathProcessingAndAuraUpdate();
                foreach(var owner in new[]{next.Player1,next.Player2}) {
                    bool alive=corpses>0 && (both || owner.PlayerId==seat);
                    Core2026Proof.Check(owner.Hero.ToBeDestroyed!=alive,"simultaneous death resolves each owner's own grant");
                    if(alive) Core2026Proof.Check(owner.Hero.Health==Math.Min(20,corpses) && CorpseRules.Available(owner)==Math.Max(0,corpses-20),"bounded resurrection payment");
                }
                if(both && corpses>0) Core2026Proof.Check(next[GameTag.NEXT_STEP]!=(int)Step.FINAL_WRAPUP,"both successful resurrections continue");
            });
        }
        var conceded=DeathKnightC1Proof.NewGame(1);Play(conceded,conceded.Player1,"TIME_618");CorpseRules.Gain(conceded.Player1,20);
        Core2026Proof.Check(conceded.Process(ConcedeTask.Any(conceded.Player1)) && CorpseRules.Available(conceded.Player1)==20,"Husk cannot defeat concession");
    }
    private static void Unpinned_RebornAuraAndExpiry()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        var champion=Summon(p,"CS2_222");var steed=Summon(p,"CAP_800");
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_009e"),steed,steed);
        int atk=steed.AttackDamage,health=steed.BaseHealth;
        Kill(g,steed);g.DeathProcessingAndAuraUpdate();
        var reborn=p.BoardZone.Single(m=>m.Card.Id=="CAP_800");
        Core2026Proof.Check(reborn.Health==health && reborn.AttackDamage==atk,"rebirth does not fold external aura into base");
        Kill(g,champion);Core2026Proof.Check(reborn.Health==health-1 && reborn.AttackDamage==atk-1,"aura removal after rebirth");
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_011o"),reborn,reborn);
        reborn[GameTag.HAS_BEEN_REBORN]=0;reborn[GameTag.REBORN]=1;
        int temporary=reborn.AttackDamage;Kill(g,reborn);g.DeathProcessingAndAuraUpdate();
        var second=p.BoardZone.Single();Core2026Proof.Check(second.AttackDamage==temporary,"retained temporary buff before original expiry");
        End(g);Core2026Proof.Check(second.AttackDamage==temporary-2,"retained temporary buff expires normally");
    }
    private static void Unpinned_RecruitCopyAndBounce()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        var savior=(Minion)Play(g,p,"MEND_801");savior.HasDivineShield=false;g.ProcessTasks();
        var recruit=Summon(p,"CS2_101t");
        var copy=(Minion)Generic.Copy(p,savior,recruit,Zone.PLAY);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.BaseHealth==2,"board copy receives no duplicate ledger");
        recruit.Silence();var silenced=(Minion)Generic.Copy(p,savior,recruit,Zone.PLAY);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(silenced.BaseHealth==1,"silenced board copy does not regain old stacks");
        Core2026Proof.Check(Generic.ReturnToHandBlock(p,copy) && copy.BaseHealth==2,"backward reset reapplies current ledger once");
        PlayEntity(g,p,copy);Core2026Proof.Check(copy.BaseHealth==2,"forward hand play never doubles grant");
        savior.HasDivineShield=true;savior.Silence();g.ProcessTasks();
        Core2026Proof.Check(Summon(p,"CS2_101t").Health==2,"Silence-caused shield loss is suppressed");
    }
    private static void Unpinned_RecruitControlTransfer()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var g = DeathKnightC1Proof.NewGame(1);
            var owner = seat == 1 ? g.Player1 : g.Player2;
            var first = Summon(owner, "MEND_801");
            first.HasDivineShield = false; g.ProcessTasks();
            first.HasDivineShield = true; first.HasDivineShield = false; g.ProcessTasks();
            var recruit = Summon(owner, "CS2_101t");
            var second = Summon(owner.Opponent, "MEND_801");
            new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET)
                .Process(g, owner.Opponent, second, recruit);
            Core2026Proof.Check(recruit.Health == 3, "control transfer preserves two realized Health increments");
            PoolPilotProof.Resume(g, next => {
                var moved = (Minion)next.IdEntityDic[recruit.Id];
                var savior = (Minion)next.IdEntityDic[second.Id];
                savior.HasDivineShield = false; next.ProcessTasks();
                Core2026Proof.Check(moved.Health == 4, "new controller's first Savior increment applies independently");
                var copy = (Minion)Generic.Copy(moved.Controller, savior, moved, Zone.PLAY);
                Core2026Proof.Check(copy.Health == 4, "exact copy retains both controllers' application counts");
                moved.Silence(); copy.Silence();
                savior.HasDivineShield = true; savior.HasDivineShield = false; next.ProcessTasks();
                Core2026Proof.Check(moved.Health == 2 && copy.Health == 2, "Silence suppresses old increments from both controllers");
                var original = (Minion)next.IdEntityDic[first.Id];
                new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET)
                    .Process(next, original.Controller, original, moved);
                Core2026Proof.Check(moved.Health == 2, "returning control never reapplies original ledger");
                original.HasDivineShield = true; original.HasDivineShield = false; next.ProcessTasks();
                Core2026Proof.Check(moved.Health == 3 && copy.Health == 2, "only current owner's new increment applies");
                Core2026Proof.Check(Generic.ReturnToHandBlock(original.Controller, moved) && moved.Health == 4,
                    "new residence clears both counts and receives current owner's full ledger once");
            });
        }
    }
    private static void Unpinned_AmitutSeededOrderAndCapacity()
    {
        for(int seed=1;seed<=8;seed++) {
            var g=DeathKnightC1Proof.NewGame(seed);var p=g.Player1;PoolPilotProof.ClassDeck(p);
            string[] ids={"CORE_CS2_093","CS2_029","TIME_447","CS2_182"};
            foreach(string id in ids) Entity.FromCard(p,Cards.FromId(id),zone:p.DeckZone);
            var expected=ids.ToList();var rng=g.Random.Clone();
            expected.Insert(rng.Next(5),"CORE_CS2_093");expected.Insert(rng.Next(6),"TIME_447");
            Play(g,p,"BE_143");
            Core2026Proof.Check(p.DeckZone.Select(c=>c.Card.Id).SequenceEqual(expected),"seeded physical insertion order");
            Core2026Proof.Check(g.Random.Next(100000)==rng.Next(100000),"shuffle consumes exactly two bounded random choices");
            Play(g,p,"BE_143");Core2026Proof.Check(p.DeckZone.Count==10,"later Battlecry includes preexisting enchanted copies once");
        }
        var cap=DeathKnightC1Proof.NewGame(1);var owner=cap.Player1;PoolPilotProof.ClassDeck(owner);
        Entity.FromCard(owner,Cards.FromId("CORE_CS2_093"),zone:owner.DeckZone);
        while(owner.DeckZone.Count<99) Entity.FromCard(owner,Cards.FromId("CS2_182"),zone:owner.DeckZone);
        Play(cap,owner,"BE_143");Core2026Proof.Check(owner.DeckZone.Count==99,"full deck suppresses copies without throwing");
        Console.WriteLine("b17 pools: no generated-definition pools; physical Holy bucket 0/1/2/4, deck cap 99");
    }
    private static void Unpinned_UntimelyOwnerAndLateTurn()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        Play(g,p,"TIME_620");var own=(Minion)Play(g,p,"CS2_182");Kill(g,own);
        Core2026Proof.Check(p.SecretZone.Count==1,"Secret never triggers during owner turn");
        var late=(Minion)Play(g,p,"CS2_182");End(g);End(g);End(g);Kill(g,late);
        Core2026Proof.Check(p.SecretZone.Count==1,"later opponent turn does not qualify");
    }
    private static void Unpinned_BananaRepeatAndFullHand()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;PoolPilotProof.EmptyHand(p);
        Play(g,p,"EDR_464");var body=Summon(p,"CS2_182");
        Play(g,p,"TIME_042t",body);
        Core2026Proof.Check(p.HandZone.Count(c=>c.Card.Id=="TIME_042t")==2 && body.AttackDamage==6,"Niri repeat grants separate fresh Banana each time");
        PoolPilotProof.FillHand(p);int count=p.HandZone.Count;
        var source=(Spell)Entity.FromCard(p,Cards.FromId("TIME_042t"),zone:p.SetasideZone);
        Generic.CastSpell(p,g,source,body,0);
        Core2026Proof.Check(p.HandZone.Count==count,"full hand loses replacement without deferral");
        g.DeathProcessingAndAuraUpdate();
    }
    private static void Unpinned_GuardianHealingAndStacking()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;p.Opponent.Hero.Health=80;
        Play(g,p,"CATA_307");Play(g,p,"CATA_307");
        p.Hero.TakeHeal(p.Hero,1);Core2026Proof.Check(p.Opponent.Hero.Health==80,"partial healing never triggers");
        p.Hero.TakeHeal(p.Hero,99);Core2026Proof.Check(p.Opponent.Hero.Health==50,"two pending grants each resolve once");
        p.Hero.TakeHeal(p.Hero,99);Core2026Proof.Check(p.Opponent.Hero.Health==50,"no-op heal never retriggers consumed grants");
    }
    private static void Unpinned_AllGrantsSurviveRewind()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;PoolPilotProof.EmptyHand(p);PoolPilotProof.ClassDeck(p);
        var guardian=(Minion)Play(g,p,"CATA_307");Kill(g,guardian);
        var husk=(Minion)Play(g,p,"TIME_618");Kill(g,husk);
        var savior=(Minion)Play(g,p,"MEND_801");savior.HasDivineShield=false;g.ProcessTasks();Kill(g,savior);
        var steed=Summon(p,"CAP_800");Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_009e"),steed,steed);
        var recruit=Summon(p,"CS2_101t");
        Entity.FromCard(p,Cards.FromId("CORE_CS2_093"),zone:p.DeckZone);
        var am=(Minion)Play(g,p,"BE_143");Kill(g,am);
        var enchanted=p.DeckZone.Single(c=>c[GameTag.CASTSWHENDRAWN]>0);
        Add(p,"TIME_042t");Play(g,p,"TIME_620");
        var portal=Add(p,"TIME_003");p.BaseMana=10;p.UsedMana=0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,portal)) && g.Rewind!=null,"real Portal Vanguard Rewind offer");
        PoolPilotProof.Resume(g.CloneExact(history:true),next=>{
            var transaction=next.Rewind;
            Core2026Proof.Check(next.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
                transaction.OfferChoiceId,transaction.ControllerId,new[]{transaction.RewindEntityId}),out var error),"Rewind resumes: "+error);
            var owner=next.Player1;
            Core2026Proof.Check(owner.Hero.AppliedEnchantments.Count(e=>e.Card.Id=="CATA_307e")==1
                && owner.AppliedEnchantments.Count(e=>e.Card.Id=="TIME_618e")==1
                && owner.AppliedEnchantments.Count(e=>e.Card.Id=="MEND_801e")==1,"Rewind keeps independent pending player grants");
            Core2026Proof.Check(((Minion)next.IdEntityDic[steed.Id]).Health==5 && ((Minion)next.IdEntityDic[recruit.Id]).Health==2
                && next.IdEntityDic[enchanted.Id][GameTag.CASTSWHENDRAWN]==1
                && owner.HandZone.Any(c=>c.Card.Id=="TIME_042t") && owner.SecretZone.Count==1,"Rewind retains enchantments, hidden grant and Secret");
        });
    }
    private static void Unpinned_UntimelyFreedCapacity()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        Play(g,p,"TIME_620");var body=(Minion)Play(g,p,"CS2_182");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_101t");
        End(g);Kill(g,body);
        Core2026Proof.Check(p.BoardZone.Count==7 && p.SecretZone.Count==0 && p.BoardZone.Any(m=>m.Card.Id=="CS2_182"),
            "Untimely continuation uses dead minion's freed slot on a full board");
    }
    private static void PendingDeathContinuationChoice()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        var secret=Play(g,p,"TIME_620");var dead=Summon(p,"EX1_097");
        p.GraveyardZone.Add(p.BoardZone.Remove(dead));
        secret[LifecycleRules.DeathContinuationTarget]=dead.Id;
        Generic.CreateChoiceCards(p,p.Hero,null,ChoiceType.GENERAL,ChoiceAction.HAND,new[]{Cards.FromId("CS2_231")},null);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.SecretZone.Count==1,"pending death continuation waits for earlier choice");
        PoolPilotProof.Resume(g.CloneExact(history:true),next=>{
            var owner=next.Player1;
            Core2026Proof.Check(next.Process(ChooseTask.Pick(owner,owner.Choice.Choices.Single())),"pending choice answer accepted");
            Core2026Proof.Check(owner.SecretZone.Count==0 && owner.BoardZone.Single().Health==4,
                "pending continuation survives clone/snapshot and resumes after choice");
        });
    }
    private static void Unpinned_RecruitTransformAdmission()
    {
        var g=DeathKnightC1Proof.NewGame(1);var p=g.Player1;
        var savior=(Minion)Play(g,p,"MEND_801");savior.HasDivineShield=false;g.ProcessTasks();
        var body=Summon(p,"CS2_182");
        var recruit=(Minion)Generic.ChangeEntity(p,body,Cards.FromId("CS2_101t"),true);
        Core2026Proof.Check(recruit.Health==2,"new Recruit form receives persistent player grant");
        recruit.Silence();
        var transform=(Minion)Play(g,p,"EX1_564",recruit);
        savior.HasDivineShield=true;savior.HasDivineShield=false;g.ProcessTasks();
        Core2026Proof.Check(transform.Health==2,"exact transform copy retains Silence application count");
    }
    private static void HistoryOff()
    {
        var g=new Game(new GameConfig { History=false,EnableCorpses=true,RandomSeed=1,StartPlayer=1,SkipMulligan=true,Shuffle=false,
            Player1HeroClass=CardClass.DEATHKNIGHT,Player2HeroClass=CardClass.MAGE,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        g.StartGame();var p=g.Player1;PoolPilotProof.EmptyHand(p);
        Play(g,p,"TIME_618");CorpseRules.Gain(p,7);p.Hero.Destroy();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Health==7 && !p.Hero.ToBeDestroyed,"Husk independent of history recording");
        var savior=(Minion)Play(g,p,"MEND_801");savior.HasDivineShield=false;g.ProcessTasks();
        Core2026Proof.Check(Summon(p,"CS2_101t").Health==2,"Savior independent of history recording");
        PoolPilotProof.ClassDeck(p);Entity.FromCard(p,Cards.FromId("CORE_CS2_093"),zone:p.DeckZone);Play(g,p,"BE_143");
        var granted=p.DeckZone.Single(c=>c[GameTag.CASTSWHENDRAWN]>0);int enemy=p.Opponent.Hero.Health;
        Generic.Draw(p,granted);Core2026Proof.Check(p.Opponent.Hero.Health==enemy-2,"granted draw cast survives History=false");
    }
}
