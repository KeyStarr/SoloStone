using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B16FabledCardsProof
{
    [GroupProof("b16-fabled-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (string id in new[]{"TIME_005","TIME_020","TIME_209","TIME_211","TIME_619","TIME_852","TIME_875"})
            StandardSetsProof.Case(id, Unpinned_CompanionClosure);
    }
    private static void Unpinned_CompanionClosure(Game g, Controller p, string id)
    {
        Core2026Proof.Check(FabledRules.Bundle(id).All(c=>Cards.FromId(c).Implemented), "all companions executable: "+id);
        switch(id)
        {
            case "TIME_875":
                var llane=Add(p.Opponent,"TIME_875t"); p.Opponent.Hero.Health=23; p.Opponent.Hero.Armor=10;
                Play(g,p,id);
                Core2026Proof.Check(llane.Zone.Type==Zone.GRAVEYARD && p.Opponent.Hero.Health==11 && p.Opponent.Hero.Armor==10,"Garona destroys held Llane and floors current Health, preserving Armor");
                int before=p.DeckZone.Count; var own=Play(g,p,"TIME_875t");
                Core2026Proof.Check(own.Zone==p.DeckZone && p.DeckZone.Count==before,"Llane draws then shuffles himself");
                break;
            case "TIME_852":
                var arcane=Add(p,"CORE_CS2_023"); var fire=Add(p,"CS2_029");
                var queen=(Minion)Play(g,p,id); Core2026Proof.Check(arcane.Cost==3,"queen alone no discount");
                var king=Summon(p,"TIME_852t1"); g.AuraUpdate();
                Core2026Proof.Check(arcane.Cost==1 && fire.Cost==4,"Arcane only discount with another Dragon");
                queen.Silence(); g.AuraUpdate(); Core2026Proof.Check(arcane.Cost==3,"silence removes queen aura");
                int deckBefore=p.DeckZone.Count; PlayEntity(g,p,arcane);
                Core2026Proof.Check(p.DeckZone.Count==deckBefore-4,"Malygos repeats complete Arcane body");
                B16FabledRegressionProof.Unpinned_Oathstone(g.Seed);
                break;
            case "TIME_005":
                // SDN1 is progress: client ENTITY_TAG_THRESHOLD (2460=9) prints 9-SDN1 left and Ready only at >= 9.
                var held=Add(p,id); g.RefreshHistoryDisplays();
                Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1]==0,"Rafaam progress starts at zero (client prints 9 left)");
                p.SetasideZone.Add(p.HandZone.Remove(held));
                Play(g,p,"TIME_005t4"); Core2026Proof.Check(p.Hero.Armor==5,"Warchief basic armor");
                held=Add(p,id); g.RefreshHistoryDisplays();
                Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"one distinct identity played is progress one");
                p.SetasideZone.Add(p.HandZone.Remove(held));
                StandardSetsProof.ParkBoard(p);
                var giant=Add(p,"TIME_005t7"); Core2026Proof.Check(giant.Cost==7,"Giant counts own family plays");
                Play(g,p,id); Core2026Proof.Check(!p.Opponent.Hero.ToBeDestroyed,"missing eight identities cannot win");
                StandardSetsProof.ParkBoard(p);
                foreach(var token in FabledRules.Bundle(id).Where(x=>x!="TIME_005t4"))
                {
                    Play(g,p,token);
                    while(p.Choice!=null) Choose(g,p);
                    StandardSetsProof.ParkBoard(p);
                }
                foreach(var won in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
                { Play(won,won.Player1,id); Core2026Proof.Check(won.Player2.Hero.ToBeDestroyed || won.State==State.COMPLETE,"nine distinct identities destroy enemy hero after clone/snapshot"); }
                break;
            case "TIME_020":
                // Broxigar and Llane sit on top of the unshuffled deck, so only exclusion keeps them out of the opening.
                var opening=B15GameSetupProof.Opening(new[]{id,"TIME_875t","TIME_020t2","TIME_020t1"}.Concat(Enumerable.Repeat("CS2_182",12)).ToArray());
                var owner=opening.Player1;
                Core2026Proof.Check(owner.HandZone.Count==3 && !owner.HandZone.Any(x=>x.Card.Id==id || x.Card.Id=="TIME_875t")
                    && owner.DeckZone.TopCard.Card.Id==id,"Broxigar and Llane are never dealt in the opening hand");
                Core2026Proof.Check(opening.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(owner,new())),"replace whole opening hand");
                Core2026Proof.Check(!owner.HandZone.Any(x=>x.Card.Id==id || x.Card.Id=="TIME_875t"),"Broxigar and Llane are never mulligan replacements");
                Core2026Proof.Check(opening.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(opening.Player2,opening.Player2.Choice.Choices.ToList())),"opponent keeps");
                opening.MainBegin();
                Core2026Proof.Check(!owner.HandZone.Concat(owner.DeckZone).Any(x=>x.Card.Id==id),"Broxigar disappears after mulligan");
                Core2026Proof.Check(!owner.DeckZone.Any(x=>x.Card.Id=="TIME_875t") && owner.Opponent.DeckZone.Count(x=>x.Card.Id=="TIME_875t")==1,"Llane hides in the enemy deck after mulligan");
                Core2026Proof.Check(owner.HandZone.Count==4,"no replacement draw: three kept slots plus the turn draw");
                var short_=B15GameSetupProof.Opening(new[]{id,"TIME_875t","CS2_182","CS2_182","CS2_182"});
                var shortOwner=short_.Player1; var kept=shortOwner.HandZone.Select(x=>x.Id).ToArray();
                Core2026Proof.Check(short_.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(shortOwner,new())),"replace all with only excluded cards left");
                Core2026Proof.Check(shortOwner.HandZone.Select(x=>x.Id).SequenceEqual(kept) && shortOwner.DeckZone.Count==2,
                    "with only Broxigar/Llane left, mulligan keeps the cards instead of dealing them");
                var only=B15GameSetupProof.Opening(new[]{id,"TIME_875t"});
                Core2026Proof.Check(only.Player1.HandZone.Count==0 && only.Player1.DeckZone.Count==2,"an all-excluded deck deals no opening cards");
                var vanished=Add(p,id); FabledRules.Vanish(vanished);
                foreach(var portal in new[]{"TIME_020t2","TIME_020t3","TIME_020t4","TIME_020t5"})
                { Play(g,p,portal); var demon=p.Opponent.BoardZone.Last(); Kill(g,demon); }
                Core2026Proof.Check(vanished.Zone==p.HandZone && p.HandZone.Count(x=>x.Card.Id==id)==1,"four spell-killed Demons return original Broxigar once");
                Play(g,p,"TIME_020t5"); Kill(g,p.Opponent.BoardZone.Last());
                Core2026Proof.Check(p.HandZone.Count(x=>x.Card.Id==id)==1,"duplicate final cannot duplicate Broxigar");
                B16FabledRegressionProof.Unpinned_PortalLifecycle(g.Seed);
                break;
            case "TIME_209":
                var hammer=Entity.FromCard(p,Cards.FromId("TIME_209t"),zone:p.DeckZone);
                var muradin=(Minion)Play(g,p,id);
                Core2026Proof.Check(muradin.AttackDamage==6 && muradin.Health==6 && muradin.HasWindfury,"single Hammer capture gives stats and Windfury");
                Core2026Proof.Check(!p.DeckZone.Contains(hammer),"captured Hammer leaves deck");
                Kill(g,muradin); Core2026Proof.Check(p.HandZone.Any(x=>x.Card.Id=="TIME_209t"),"death releases Hammer");
                break;
            case "TIME_619":
                Entity.FromCard(p,Cards.FromId("TIME_619t"),zone:p.DeckZone);
                Play(g,p,id); Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count==3,"Talanji offers three boons");
                Choose(g,p);
                var bwon=p.HandZone.Single(x=>x.Card.Id=="TIME_619t"); PlayEntity(g,p,bwon);
                Core2026Proof.Check(((Minion)bwon).HasTaunt,"chosen boon grants Taunt; ledger="+string.Join(",",p.AppliedEnchantments.Select(e=>e.Card.Id))+" aura="+bwon.AuraEffects?[GameTag.TAUNT]); ((Minion)bwon).Silence(); g.AuraUpdate();
                Core2026Proof.Check(((Minion)bwon).HasTaunt && !bwon.HasDeathrattle,"boon survives Silence but printed Deathrattle does not");
                break;
            case "TIME_211":
                var well=Play(g,p,"TIME_211t1"); var city=Play(g,p,"TIME_211t2");
                var lady=Add(p,id); p.UsedMana=0;
                Core2026Proof.Check(g.Process(PlayCardTask.Any(p,lady,chooseOne:1)),"Azshara Choose One");
                Core2026Proof.Check(city.Card.Id=="TIME_211t2t" && well.Zone.Type==Zone.GRAVEYARD,"empower city and destroy Well");
                Unpinned_WellSeedAndMarks(g,p);
                break;
        }
    }
    private static void Choose(Game g,Controller p)
    {
        var decision=g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id,decision.ControllerId,new[]{p.Choice.Choices[0]}),out _),"saved choice answer accepted");
    }
    private static void Unpinned_WellSeedAndMarks(Game g,Controller p)
    {
        KnownMetaProofFixture.UseBlackEmpire(g);
        foreach(var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var well=(BoardObject)Play(g,p,"TIME_211t1t");
        g.SetRandomSeed(71);
        var candidates=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.SPELL),well.Card.AssetId).ToArray();
        var expected=g.CloneExact(history:true);
        var ids=Enumerable.Range(0,10).Select(_=>candidates[expected.Random.Next(candidates.Length)].Id).ToArray();
        Core2026Proof.Check(LocationRules.Activate(p,well),"empowered Well usable");
        Core2026Proof.Check(p.HandZone.Select(x=>x.Card.Id).SequenceEqual(ids),"seeded ordered Well picks, not first candidate");
        Core2026Proof.Check(p.HandZone.All(x=>TemporaryRules.IsTemporary(x) && x.AppliedEnchantments.Any(e=>e.Card.Id=="TIME_211t1te")),"every generated spell temporary and locally marked");
        well.Destroy();g.DeathProcessingAndAuraUpdate();
        foreach(var next in new[]{g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)})
        {
            Core2026Proof.Check(next.Player1.HandZone.All(x=>x.AppliedEnchantments.Any(e=>e.Card.Id=="TIME_211t1te")),"marks survive source destruction and clone/snapshot");
            End(next); Core2026Proof.Check(next.Player1.HandZone.IsEmpty,"Temporary spells expire at owner end");
        }
        Console.WriteLine("Well spell bucket="+candidates.Length);
    }
}
