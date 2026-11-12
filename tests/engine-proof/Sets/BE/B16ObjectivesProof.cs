using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16ObjectivesProof
{
    [GroupProof("b16-objectives-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_005", Unpinned_ExcessBeforeOrdinaryDraw);
        StandardSetsProof.Case("BE_085", Pinned_DuplicateSummoning);
        StandardSetsProof.Case("BE_090", Unpinned_SinisterRituals);
        StandardSetsProof.Case("BE_726", Unpinned_RitualSpellDamageAtCompletion);
        B16ObjectivesLostCityProof.Run(); B16ObjectivesTrialProof.Run();
    }
    internal static void Cycle(Game g) { End(g); End(g); }
    private static void Unpinned_ExcessBeforeOrdinaryDraw(Game g,Controller p,string id)
    {
        Play(g,p,id); Cycle(g);
        var dueCards=p.DeckZone.Reverse().Take(3).ToArray();
        foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            Cycle(branch);var owner=branch.Player1;
            Core2026Proof.Check(branch.IdEntityDic[dueCards[0].Id].Cost==2 && branch.IdEntityDic[dueCards[1].Id].Cost==2
                && branch.IdEntityDic[dueCards[2].Id].Cost==4,"two due draws discounted before ordinary draw");
            Core2026Proof.Check(ObjectiveRules.CompletedRituals(owner)==1 && ObjectiveRules.CompletedRituals(owner.Opponent)==0,"completion belongs to owner");
        }
        var burn=HeroDivineShieldProof.NewGame(true);var q=burn.Player1;
        foreach(var held in q.HandZone.ToArray())q.SetasideZone.Add(q.HandZone.Remove(held));
        ObjectivesProof.Install(q,id);while(!q.HandZone.IsFull)Add(q,"CS2_182");Cycle(burn);Cycle(burn);
        Core2026Proof.Check(q.HandZone.All(x=>x.Cost==4) && ObjectiveRules.CompletedRituals(q)==1,"burned draws do not receive discount or defer completion");
        Unpinned_ExcessStopsAfterLethalFatigue();
    }
    private static void Unpinned_ExcessStopsAfterLethalFatigue()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        ObjectivesProof.Install(p,"BE_005");Cycle(g);End(g);
        foreach(var card in p.DeckZone.ToArray())p.SetasideZone.Add(p.DeckZone.Remove(card));
        p.Hero.Damage=29;p.Hero.Fatigue=0;
        foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            End(branch);var owner=branch.Player1;
            Core2026Proof.Check(branch.State==State.COMPLETE && owner.Hero.Fatigue==1,
                "Unpinned_ExcessStopsAfterLethalFatigue stops at first lethal fatigue attempt");
            Core2026Proof.Check(ObjectiveRules.CompletedRituals(owner)==1 && !owner.SecretZone.Any(ObjectiveRules.IsActive),
                "lethal Excess still completes and removes objective exactly once");
        }
    }
    private static void Pinned_DuplicateSummoning(Game g,Controller p,string id)
    {
        var first=Play(g,p,id);Cycle(g);var second=Play(g,p,id);
        Cycle(g);Core2026Proof.Check(p.BoardZone.Count==2 && ObjectiveRules.Remaining(second)==1,"staggered objectives retain separate clocks");
        Cycle(g);Core2026Proof.Check(p.BoardZone.Count==4 && p.BoardZone.All(m=>m.AttackDamage==3 && m.Health==5 && m.HasTaunt),"exported C'thraxxi");
        Play(g,p,id);while(!p.BoardZone.IsFull)Summon(p,"CS2_231");Cycle(g);Cycle(g);
        Core2026Proof.Check(!p.SecretZone.Any(ObjectiveRules.IsActive) && ObjectiveRules.CompletedRituals(p)==3,"full board consumes objective");
    }
    private static void Unpinned_SinisterRituals(Game g,Controller p,string id)
    {
        Play(g,p,id);Core2026Proof.Check(p.Choice==null,"no ritual completed gate");
        ObjectivesProof.Install(p,"BE_085");Cycle(g);Cycle(g);StandardSetsProof.ParkBoard(p);
        Play(g,p,id);Core2026Proof.Check(p.Choice.Choices.Count==3,"all three sinister choices");
        CthunRulesProof.Pick(g,"BE_090t3");var held=Add(p,"CS2_182");var deck=p.DeckZone.TopCard;
        ((Minion)p.BoardZone.Single()).Silence();Cycle(g);Cycle(g);
        Core2026Proof.Check(((Minion)held).AttackDamage==10 && ((Minion)deck).AttackDamage==10 && p.BoardZone.Single().AttackDamage==12,"Conquest survives Kith'ix Silence, buffs every zone");
        p.BoardZone.Single().Silence();Core2026Proof.Check(p.BoardZone.Single().AttackDamage==6,"Conquest uses removable buff layer");
        Unpinned_ConquestDormantExclusions();
        Unpinned_SubjugationCopiesSurviveBlockedSummons();Unpinned_FamineForeignDeckFatigue();Unpinned_FamineLethal();
    }
    private static void Unpinned_ConquestDormantExclusions()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        var sleeper=Summon(p,"TIME_063");var active=Summon(p,"CS2_182");
        var enemy=Summon(p.Opponent,"CS2_182");var held=Add(p,"TIME_063");var deck=p.DeckZone[0];
        ObjectivesProof.Install(p,"BE_090t3");
        foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            Cycle(branch);Cycle(branch);
            var sleeping=(Minion)branch.IdEntityDic[sleeper.Id];
            Core2026Proof.Check(sleeping.IsDormant && sleeping.AttackDamage==8 && sleeping.Health==8,
                "Unpinned_ConquestDormantExclusions skips sleeping board minion");
            Core2026Proof.Check(((Minion)branch.IdEntityDic[active.Id]).AttackDamage==10
                && ((Minion)branch.IdEntityDic[held.Id]).AttackDamage==14
                && ((Minion)branch.IdEntityDic[deck.Id]).AttackDamage==7
                && ((Minion)branch.IdEntityDic[enemy.Id]).AttackDamage==4,
                "Conquest retains friendly active/hand/deck buffs, excludes opponent");
            DormantRules.Awaken(sleeping);
            Core2026Proof.Check(sleeping.AttackDamage==8 && sleeping.Health==8,"Conquest leaves no hidden buff after awakening");
        }
    }
    private static void Unpinned_SubjugationCopiesSurviveBlockedSummons()
    {
        for(int slots=0;slots<=3;slots++) {
            var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
            while(p.BoardZone.FreeSpace>slots)Summon(p,"CS2_231");
            ObjectivesProof.Install(p,"BE_090t2");Cycle(g);End(g);
            var pool=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.MINION,rarity:Rarity.LEGENDARY),129824);
            Console.WriteLine("Subjugation bucket="+pool.Count);
            var rng=g.Random.Clone();var expected=Enumerable.Range(0,3).Select(_=>pool[rng.Next(pool.Count)].Id).ToArray();
            End(g);
            var copies=p.HandZone.Where(x=>x.AppliedEnchantments?.Any(e=>e.Card.Id=="BE_090e2")==true).ToArray();
            Core2026Proof.Check(copies.Select(x=>x.Card.Id).SequenceEqual(expected) && copies.All(x=>x.Cost==1),"seeded ordered independent selections grant all copies despite capacity");
            Core2026Proof.Check(ObjectiveRules.CompletedRituals(p)==1,"Subjugation completes once");
        }
    }
    private static void Unpinned_FamineForeignDeckFatigue()
    {
        foreach(int remaining in new[] {0,1,4,5}) {
            var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;var enemy=p.Opponent;
            ObjectivesProof.Install(p,"BE_090t1");Cycle(g);End(g);
            while(enemy.DeckZone.Count>remaining)enemy.SetasideZone.Add(enemy.DeckZone.Remove(enemy.DeckZone[0]));
            p.Hero[GameTag.HEALTH]=100;p.Hero.Damage=0;p.Hero.Fatigue=2;enemy.Hero.Fatigue=7;
            foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
            int start=p.HandZone.Count;End(g);
            Core2026Proof.Check(p.Hero.Fatigue==2+5-remaining && enemy.Hero.Fatigue==7 && p.HandZone.Count==start+remaining+1,"foreign deck attempts fatigue recipient; ordinary owner draw follows");
        }
    }
    private static void Unpinned_FamineLethal()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        ObjectivesProof.Install(p,"BE_090t1");Cycle(g);End(g);
        foreach(var card in p.Opponent.DeckZone.ToArray())p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(card));
        p.Hero.Damage=29;p.Hero.Fatigue=0;End(g);
        Core2026Proof.Check(g.State==State.COMPLETE && p.Hero.Fatigue==1,"Famine stops at first lethal fatigue attempt");
    }
    private static void Unpinned_RitualSpellDamageAtCompletion(Game g,Controller p,string id)
    {
        Play(g,p,id);var provider=Summon(p,"EX1_012");provider.Silence();
        Cycle(g);End(g);int before=p.HandZone.Count;End(g);
        Core2026Proof.Check(p.Choice?.Choices.Count==4 && p.HandZone.Count==before,"delayed choice blocks ordinary turn draw");
        foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            var owner=branch.Player1;Summon(owner,"EX1_012");
            CthunRulesProof.Pick(branch,"BE_013t1");
            Core2026Proof.Check(owner.Opponent.Hero.Damage==8 && owner.HandZone.Count==before+1 && branch.Step==Step.MAIN_ACTION,"current Spell Damage scales missile count once; continuation resumes draw");
        }
        var glare=HeroDivineShieldProof.NewGame(true);var q=glare.Player1;
        ObjectivesProof.Install(q,id);var victim=Summon(q.Opponent,"CS2_200");victim[GameTag.HEALTH]=20;
        var shadow=Summon(q,"CS2_231");shadow[(GameTag)1950]=2;q.Hero.Damage=10;
        Cycle(glare);Cycle(glare);CthunRulesProof.Pick(glare,"BE_013t3");
        CthunRulesProof.PickTarget(glare,victim.Id);
        Core2026Proof.Check(victim.Damage==7 && q.Hero.Damage==3,"Shadow bonus and Lifesteal from original Ritual");
    }
}
