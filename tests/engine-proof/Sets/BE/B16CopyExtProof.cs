using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16CopyExtCardsProof
{
    [GroupProof("b16-copy-ext-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_731", Unpinned_Rockfang);
        StandardSetsProof.Case("EDR_491", Unpinned_Archdruid);
        StandardSetsProof.Case("BE_852", Unpinned_Ammunae);
        Unpinned_DonorReferences();
        B16CopyExtPriestProof.Run(); B16CopyExtNythendraProof.Run();
    }
    private static void Unpinned_Rockfang(Game g,Controller p,string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        var egg=Summon(p,"EDR_454t");
        var dragon=Add(p,"CS2_200");new Enchant(Effects.Attack_N(2)).ActivateTo(dragon);
        PayloadRules.Store(egg,1,dragon);
        var body=(Minion)Play(g,p,id);
        Core2026Proof.Check(body.HasDeathrattle,"Rockfang gains adjacent Deathrattle");
        egg.Silence();
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            Kill(branch,(Minion)branch.IdEntityDic[body.Id]);
            Core2026Proof.Check(branch.Player1.BoardZone.Any(m=>m.Card.Id=="CS2_200" && m.AttackDamage==8),"captured Egg payload independent of donor Silence");
        }
        var bounce=g.CloneExact(true);var bounced=(Minion)bounce.IdEntityDic[body.Id];
        Generic.ReturnToHandBlock(bounce.Player1,bounced);
        Core2026Proof.Check(!bounced.HasDeathrattle && !bounced.AppliedEnchantments.Any(a=>a[PayloadRules.CapturedEffect]!=0),"leaving play removes captured grants");
        body.Silence();Kill(g,body);Core2026Proof.Check(!p.BoardZone.Any(m=>m.Card.Id=="CS2_200"),"recipient Silence removes captured effect");
        var scripted=Summon(p,"CATA_210");scripted[GameTag.TAG_SCRIPT_DATA_NUM_1]=5;scripted[GameTag.TAG_SCRIPT_DATA_NUM_2]=6;
        var captured=Summon(p,id);PayloadRules.GainDeathrattles(captured,scripted,"BE_731e");
        scripted[GameTag.TAG_SCRIPT_DATA_NUM_1]=9;scripted.Silence();
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            Kill(branch,(Minion)branch.IdEntityDic[captured.Id]);branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(branch.Player1.BoardZone.Any(m=>m.Card.Id=="CATA_210t"&&m.AttackDamage==5&&m.Health==6),"captured script values immutable across clone/load");
        }
        var grantDonor=Summon(p,"CS2_182");Enchantment.GetInstance(p,grantDonor,grantDonor,Cards.FromId("CS2_038e"));
        var rebound=Summon(p,id);PayloadRules.GainDeathrattles(rebound,grantDonor,"BE_731e");
        Kill(g,rebound);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Any(m=>m.Card.Id==id && m.Id!=rebound.Id),"granted Ancestral Spirit rebinds self to recipient");
    }
    // Client text "Copied Deathrattle from {0}" (builder 19) names the donor minion by entity id: no new entity, the donor's own id.
    private static void Unpinned_DonorReferences()
    {
        const string id="BE_731";
        var g=DiscoverRealProof.New();var p=g.Player1;
        g.Player1.DeckCards.Add(Cards.FromId("EDR_491")); PayloadRules.CaptureTracking(g);
        var donor=Summon(p,"EX1_029");
        var rock=(Minion)Play(g,p,id);
        var grant=rock.AppliedEnchantments.Single(a=>a.Card.Id=="BE_731e");
        Core2026Proof.Check(grant[PayloadRules.ReferenceEntity]==donor.Id && grant[GameTag.TAG_SCRIPT_DATA_NUM_1]==donor.Card.AssetId
            && PayloadRules.Entries(grant).Length==0,"Pack Mentality references the adjacent donor's entity and keeps the engine dbfId");
        // A copy of the recipient keeps the reference; granting from a recipient names that recipient (the immediate donor).
        var copy=(Minion)SabberStoneCore.Actions.Generic.Copy(p,rock,rock,Zone.PLAY);
        Core2026Proof.Check(copy.AppliedEnchantments.Single(a=>a.Card.Id=="BE_731e")[PayloadRules.ReferenceEntity]==donor.Id,"copied grant keeps its donor reference");
        var next=Summon(p,"CS2_182");var chained=Summon(p,id);
        PayloadRules.GainDeathrattles(chained,rock,"BE_731e");
        Core2026Proof.Check(chained.AppliedEnchantments.Single(a=>a.Card.Id=="BE_731e")[PayloadRules.ReferenceEntity]==rock.Id,"a Deathrattle copied from a recipient names that recipient");
        // Archdruid: a plain printed Deathrattle names the dead minion (death fact); a scripted one (private death ledger) too, never the ledger.
        var plain=Summon(p,"EX1_029");var scripted=Summon(p,"CATA_210");scripted[GameTag.TAG_SCRIPT_DATA_NUM_1]=4;
        Kill(g,plain);Kill(g,scripted);
        var ledgers=PayloadRules.Entries(p,16002);
        Core2026Proof.Check(ledgers.Length==1,"scripted death is kept in the ledger");
        var druid=(Minion)Play(g,p,"EDR_491");
        var soulIds=druid.AppliedEnchantments.Where(a=>a.Card.Id=="EDR_491e").Select(a=>a[PayloadRules.ReferenceEntity]).OrderBy(x=>x).ToArray();
        Core2026Proof.Check(soulIds.SequenceEqual(new[]{plain.Id,scripted.Id}.OrderBy(x=>x)) && !soulIds.Contains(ledgers[0].Id),
            "Devoured Soul names each dead minion, never the private ledger entity");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(branch.IdEntityDic[druid.Id].AppliedEnchantments.Where(a=>a.Card.Id=="EDR_491e").Select(a=>a[PayloadRules.ReferenceEntity]).OrderBy(x=>x).SequenceEqual(soulIds),
                "clone/load keep the donor references");
    }
    private static void Unpinned_Archdruid(Game g,Controller p,string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        Kill(g,Summon(p,"EX1_029"));
        int before=p.Opponent.Hero.Damage;
        var first=(Minion)Play(g,p,id);Kill(g,first);
        Core2026Proof.Check(p.Opponent.Hero.Damage==before+2,"Archdruid acquires same-turn printed Deathrattle");
        var second=(Minion)Play(g,p,id);Kill(g,second);
        Core2026Proof.Check(p.Opponent.Hero.Damage==before+6,"death ledger retains earlier Archdruid grants and multiplicity");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            int damage=branch.Player2.Hero.Damage;var next=(Minion)Play(branch,branch.Player1,id);Kill(branch,next);
            Core2026Proof.Check(branch.Player2.Hero.Damage==damage+8,"death ledger clone/load preserves all captured grants");
        }
        End(g);End(g);
        Kill(g,Summon(p.Opponent,"EX1_029"));var suppressed=Summon(p,"EX1_029");suppressed.Silence();Kill(g,suppressed);
        var expired=(Minion)Play(g,p,id);
        Core2026Proof.Check(!expired.HasDeathrattle,"previous-turn, opponent and suppressed printed deaths excluded");
    }
    private static void Unpinned_Ammunae(Game g, Controller p, string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        var ammunae=(Minion)Play(g,p,id); ammunae.Silence(); Kill(g,ammunae);
        var source=Add(p,"EX1_096");
        new Enchant(Effects.Attack_N(3), Effects.Health_N(2)).ActivateTo(source);
        PlayEntity(g,p,source);
        var copy=p.BoardZone.Single(m=>m.Card.Id=="EX1_096" && m.Id!=source.Id);
        Core2026Proof.Check(copy.AttackDamage==5 && copy.Health==3,"Ammunae copies held buffs after Battlecry");
        Core2026Proof.Check((copy.AppliedEnchantments?.Count??0)==(source.AppliedEnchantments?.Count??0),"Ammunae copied attachments");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var owner=branch.Player1; var before=owner.BoardZone.Count;
            Play(branch,owner,"EX1_096");
            Core2026Proof.Check(owner.BoardZone.Count==before+2,"Ammunae controller grant survives clone/load and source Silence/death");
        }
        var injured=Add(p,"CS2_181");
        Enchantment.GetInstance(p,injured,injured,Cards.FromId("CS2_038e"));injured.HasDeathrattle=true;
        PlayEntity(g,p,injured);
        Core2026Proof.Check(p.BoardZone.Where(m=>m.Card.Id=="CS2_181").Count(m=>m.Damage==4&&m.Health==3)==2,"Ammunae snapshots damage after self-damaging Battlecry");
        var model=Summon(p,"CS2_182");var transformer=Add(p,"EX1_564");
        Enchantment.GetInstance(p,transformer,transformer,Cards.FromId("CS2_038e"));transformer.HasDeathrattle=true;
        PlayEntity(g,p,transformer,model);
        Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="CS2_182")==3,"play-time eligibility survives Battlecry transform");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_231");
        Kill(g,(Minion)source);
        Play(g,p,"EX1_096");
        Core2026Proof.Check(p.BoardZone.Count==7,"Ammunae full board skips copy");
    }
}
