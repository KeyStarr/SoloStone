using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class EvCardsProof
{
    internal static readonly string[] Ids = { "TLC_EVENT_402", "EDR_950", "CATA_EVENT_401", "CATA_EVENT_402", "TIME_EVENT_301", "CATA_EVENT_000", "CATA_EVENT_002", "TIME_EVENT_300", "CATA_EVENT_400", "JAIL_EVENT_102" };
    [GroupProof("ev-cards", ProofKind.Facility)]
    internal static void Facility()
    {
        foreach (var id in Ids) Core2026Proof.Check(Cards.FromId(id)?.Implemented == true, "EVENT module missing: " + id);
    }
    [GroupProof("ev-cards-cards", ProofKind.Cards)]
    internal static void CardsProof()
    {
        StandardSetsProof.Case("TLC_EVENT_402", Unpinned_Staff);
        StandardSetsProof.Case("EDR_950", Unpinned_Lookout);
        StandardSetsProof.Case("CATA_EVENT_401", Unpinned_PrepareSpellDamage);
        StandardSetsProof.Case("CATA_EVENT_402", Unpinned_Coins);
        StandardSetsProof.Case("TIME_EVENT_301", Unpinned_Disciple);
        StandardSetsProof.Case("CATA_EVENT_000", Unpinned_PastColossal);
        StandardSetsProof.Case("CATA_EVENT_002", Unpinned_FireHistory);
        StandardSetsProof.Case("TIME_EVENT_300", Unpinned_Doomsayer);
        StandardSetsProof.Case("CATA_EVENT_400", Unpinned_SpendAllMana);
        StandardSetsProof.Case("JAIL_EVENT_102", Unpinned_SummonEvolve);
    }
    private static void Unpinned_Staff(Game g, Controller p, string id)
    {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g,p,id);
        Resume(g, next => { next.Player1.Hero.Weapon.Destroy(); next.DeathProcessingAndAuraUpdate(); });
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone && enemy.Zone == p.Opponent.GraveyardZone, "weapon Deathrattle destroys both boards");
        var replacement = Summon(p, "EX1_556"); Play(g,p,id); Play(g,p,"CS2_106");
        Core2026Proof.Check(replacement.Zone == p.GraveyardZone && p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "skele21", "replacement breaks Staff; new Deathrattle body survives single destruction wave");
    }
    private static void Unpinned_Lookout(Game g, Controller p, string id)
    {
        var held = Add(p,id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_6] == p.DeckZone.Last().Card.AssetId, "Lookout previews actual top card");
        int top = p.DeckZone.Last().Id;
        Resume(g, next => PlayEntity(next,next.Player1,next.IdEntityDic[held.Id]));
        var drawn=(Playable)g.IdEntityDic[top];
        Core2026Proof.Check(drawn.Zone == p.HandZone && drawn.Cost == drawn.Card.Cost-1 && drawn.AppliedEnchantments.Any(e=>e.Card.Id=="EDR_950e"), "drawn entity gets exported temporary discount");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_6]==0, "public Lookout never retains private deck preview");
        var discountCopy=Generic.Copy(p,p.Hero,drawn,Zone.HAND);
        Core2026Proof.Check(discountCopy.Cost==drawn.Cost,"hand copy retains temporary discount");
        var prepared=Add(p,"CATA_EVENT_401");p.BaseMana=10;p.UsedMana=8;g.Process(PrepareCardTask.Any(p,prepared));
        var rewind=Add(p,"GAME_005");rewind[GameTag.REWIND]=1;PlayEntity(g,p,rewind);
        Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(g),"Rewind opens beside Lookout discount and prepared state");
        RewindTestHelpers.AnswerRewind(g);RewindTestHelpers.KeepIfOffered(g);
        p=g.Player1;drawn=(Playable)g.IdEntityDic[drawn.Id];discountCopy=g.IdEntityDic[discountCopy.Id];held=g.IdEntityDic[held.Id];
        Core2026Proof.Check(drawn.Cost==drawn.Card.Cost-1 && g.IdEntityDic[prepared.Id][GameTag.PREPARING]==1,
            "Rewind retains temporary discount and prepared lock without duplicating actors");

        Resume(g, End);
        Core2026Proof.Check(discountCopy.Cost==discountCopy.Card.Cost,"copied discount expires on same global boundary");
        Core2026Proof.Check(drawn.Cost == drawn.Card.Cost && !drawn.AppliedEnchantments.Any(e=>e.Card.Id=="EDR_950e"), "discount and actor expire together");
        End(g); EmptyHand(p); ClassDeck(p); int damage=p.Hero.Damage; Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==0 && p.Hero.Damage>damage,"empty deck fatigues with no discount");
        AddDeck(p,"CS2_182"); FillHand(p,9); var source=Add(p,id); PlayEntity(g,p,source);
        Core2026Proof.Check(p.HandZone.Count==10,"play frees slot before draw");
        FillHand(p); var extra=Summon(p,id); Resolve(g,extra);
        Core2026Proof.Check(p.HandZone.Count==10,"full hand burns without a discounted replacement");
    }
    private static void AddDeck(Controller p,string id) => p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(id)));
    private static void Unpinned_PrepareSpellDamage(Game g, Controller p,string id)
    {
        var held=Add(p,id); p.BaseMana=10;p.UsedMana=8;
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p,held)),"Prepare accepted");
        Core2026Proof.Check(held.Cost==0 && p.RemainingMana==0 && held[GameTag.PREPARING]==1 && p.CurrentSpellPower==0,"Prepare pays two, reduces three, grants no hand Spell Damage");
        var copied=Generic.Copy(p,p.Hero,held,Zone.HAND);
        Core2026Proof.Check(copied.Cost==0 && copied[GameTag.PREPARING]==1 && !copied.IsPlayableByPlayer,"same-turn hand copy preserves prepared lock and discount");

        Resume(g, next=>{End(next);End(next);});
        Core2026Proof.Check(held[GameTag.PREPARING]==0,"Prepare unlocks after boundary");
        PlayEntity(g,p,held); Core2026Proof.Check(p.CurrentSpellPower==1,"played Geomancer grants one Spell Damage");
        ((Minion)held).Silence(); Core2026Proof.Check(p.CurrentSpellPower==0,"Silence removes Spell Damage");
        EmptyHand(p);var one=(Playable)Add(p,id);CostRules.AddModifier(one,SabberStoneCore.Enchants.EffectOperator.SET,1);p.UsedMana=0;
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p,one)) && one.Cost==0 && p.UsedMana==1,"cost-one Prepare spends one and floors two-point discount");
    }
    private static void Unpinned_Coins(Game g,Controller p,string id)
    {
        var target=Summon(p.Opponent,"CS2_182");Play(g,p,id,target);
        Core2026Proof.Check(target.Zone==p.Opponent.GraveyardZone && p.Opponent.HandZone.Single().Card.Id=="GAME_005" && p.HandZone.Count==0,"noncombo gives only opponent Coin");
        target=Summon(p,"CS2_182");Play(g,p,id,target);
        Core2026Proof.Check(target.Zone==p.GraveyardZone && p.HandZone.Single().Card.Id=="GAME_005" && p.Opponent.HandZone.Count==2,"Combo independent recipient Coins");
        EmptyHand(p);FillHand(p.Opponent);target=Summon(p.Opponent,"CS2_182");Play(g,p,id,target);
        Core2026Proof.Check(p.HandZone.Single().Card.Id=="GAME_005" && p.Opponent.HandZone.Count==10,"opponent overflow does not block own Coin");
    }
    private static void Unpinned_Disciple(Game g,Controller p,string id)
    {
        var source=Summon(p,id); var victims=Enumerable.Range(0,4).Select(i=>Summon(i%2==0?p:p.Opponent,"CS2_182")).ToArray();
        Add(p,"EX1_562");Add(p,"EX1_562");
        var rng=g.Random.Clone(); var order=new[]{0,2,1,3}.ToList();
        for(int i=0;i<3;i++) order.RemoveAt(rng.Next(order.Count));
        Resume(g,next=>Resolve(next,next.IdEntityDic[source.Id]));
        Core2026Proof.Check(victims.Where(m=>m.Zone.Type==Zone.PLAY).Select(m=>System.Array.IndexOf(victims,m)).OrderBy(i=>i).SequenceEqual(order.OrderBy(i=>i)),"seeded three destruction picks use fresh ordered both-side candidates");
        Core2026Proof.Check(source.Zone==p.BoardZone,"source excluded");
    }
    private static void Unpinned_PastColossal(Game g,Controller p,string id)
    {
        var filter=EventCardPools.PastColossal();var candidates=OfflineCardPools.Candidates(g,p.HeroClass,filter,Cards.FromId(id).AssetId);
        System.Console.WriteLine("ev-cards past-colossal bucket="+candidates.Count);
        Core2026Proof.Check(candidates.All(c=>c.Collectible && c[(GameTag)2247]>0 && !Cards.StandardSets.Contains(c.Set)),"only qualified historical Colossal bodies");
        var source=Summon(p,id);Resume(g,next=>Resolve(next,next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.HandZone.Count==(candidates.Count==0?0:1),"past generation obeys empty pool");
        if(candidates.Count>0) Core2026Proof.Check(p.HandZone.All(c=>c.Cost==c.Card.Cost && !(c.AppliedEnchantments?.Any(e=>e.Card.Id=="CATA_EVENT_000e") ?? false)),"no invented zero-cost discount");
    }
    private static void Unpinned_FireHistory(Game g,Controller p,string id)
    {
        var victim=Summon(p.Opponent,"CS2_182");Play(g,p,id);
        Core2026Proof.Check(victim.Zone==p.Opponent.BoardZone,"no Fire no destruction");
        Play(g,p,"CORE_CS2_029",p.Opponent.Hero);
        var held=Add(p,id);Resume(g,next=>PlayEntity(next,next.Player1,next.IdEntityDic[held.Id],(ICharacter)next.IdEntityDic[victim.Id]));
        Core2026Proof.Check(victim.Zone==p.Opponent.GraveyardZone,"own Fire spell enables destruction");
        Resume(g,next=>{End(next);End(next);});
        Core2026Proof.Check(!SpellPlayHistory.PlayedSchoolThisTurn(p,2),"Fire query expires next turn");
        End(g); Play(g,p.Opponent,"EX1_287"); End(g);
        int damage=p.Opponent.Hero.Damage;Play(g,p,"CORE_CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==damage && SpellPlayHistory.PlayedSchoolThisTurn(p,2),"countered player Fire still counts as played");
        victim=Summon(p.Opponent,"CS2_182");Play(g,p,id,victim);
        Core2026Proof.Check(victim.Zone==p.Opponent.GraveyardZone,"countered Fire enables actual Blazer effect");

    }
    private static void Unpinned_Doomsayer(Game g,Controller p,string id)
    {
        var harbinger=Summon(p,id);Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[harbinger.Id]));
        var doom=p.BoardZone.Single();Core2026Proof.Check(doom.Card.Id=="CORE_NEW1_021" && doom.AttackDamage==0 && doom.Health==7,"exact Doomsayer identity and base stats");
        End(g);Core2026Proof.Check(doom.Zone==p.BoardZone,"opponent turn does not trigger Doomsayer");
        End(g);Core2026Proof.Check(doom.Zone==p.GraveyardZone,"owner start destroys Doomsayer");
        harbinger=Summon(p,id);harbinger.Silence();Kill(g,harbinger);Core2026Proof.Check(p.BoardZone.Count==0,"Silenced Harbinger has no Deathrattle");
    }
    private static void Unpinned_SpendAllMana(Game g,Controller p,string id)
    {
        var held=Add(p,id);p.BaseMana=10;p.UsedMana=2;p.TemporaryMana=1;
        int spent=p.RemainingMana-held.Cost; var candidates=EventCardPools.MinionCandidates(g,p,held,spent,PoolDestination.Summon);
        System.Console.WriteLine("ev-cards Crook exact "+spent+" bucket="+candidates.Length);
        var rng=g.Random.Clone();var expected=candidates.Length==0?null:candidates[rng.Next(candidates.Length)].Id;

        Resume(g,next=> {var owner=next.Player1;Core2026Proof.Check(next.Process(PlayCardTask.Minion(owner,next.IdEntityDic[held.Id])),"Crook played");});
        Core2026Proof.Check(p.RemainingMana==0 && p.BoardZone.Count==(candidates.Length==0?1:2),"all remaining usable Mana spent once");
        Core2026Proof.Check(p.BoardZone.Where(m=>m.Id!=held.Id).All(m=>m.Card.Cost==spent),"exact printed cost of actual Mana spent");
        Core2026Proof.Check(p.BoardZone.FirstOrDefault(m=>m.Id!=held.Id)?.Card.Id==expected,"seeded Crook chooses exact ordered candidate, not always first");

        StandardSetsProof.ParkBoard(p);EmptyHand(p);for(int i=0;i<6;i++)Summon(p,"CS2_182");
        held=Add(p,id);p.BaseMana=10;p.UsedMana=0;g.Process(PlayCardTask.Minion(p,held));
        Core2026Proof.Check(p.RemainingMana==0 && p.BoardZone.Count==7,"full board still spends mana");
    }
    private static void Unpinned_SummonEvolve(Game g,Controller p,string id)
    {
        var old=Summon(p,"CS2_182");int oldId=old.Id;

        var source=Add(p,id);var rng=g.Random.Clone();
        var twos=EventCardPools.MinionCandidates(g,p,source,2,PoolDestination.Summon);
        var expectedSummons=Enumerable.Range(0,4).Select(_=>twos[rng.Next(twos.Length)].Id).ToArray();
        var expectedFriendly=new[]{old.Card.Cost+1,3,3}.Select(cost=> {
            var bucket=EventCardPools.MinionCandidates(g,p,source,cost,PoolDestination.Transform);return bucket[rng.Next(bucket.Length)].Id;
        }).ToArray();
        Resume(g,next=>PlayEntity(next,next.Player1,next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.BoardZone.Count==3 && p.Opponent.BoardZone.Count==2,"two summons per side then friendly evolution");
        Core2026Proof.Check(p.BoardZone[0].Card.Cost==old.Card.Cost+1 && p.BoardZone.Skip(1).All(m=>m.Card.Cost==3) && p.Opponent.BoardZone.All(m=>m.Card.Cost==2),"only friendlies evolve once by printed cost");
        Core2026Proof.Check(p.BoardZone.Select(m=>m.Card.Id).SequenceEqual(expectedFriendly) && p.Opponent.BoardZone.Select(m=>m.Card.Id).SequenceEqual(expectedSummons.Skip(2)),
            "seeded caster-first summons then left-to-right friendly evolution use seven ordered picks");
        StandardSetsProof.ParkBoard(p);StandardSetsProof.ParkBoard(p.Opponent);
        var expensive=Cards.FromId("CS2_182").Clone();expensive.Tags[GameTag.COST]=19;typeof(Card).GetProperty("Cost").SetValue(expensive,19);
        var original=(Minion)Entity.FromCard(p,expensive);Generic.SummonBlock(g,original,-1,null);
        original.Damage=1;var originalHealth=original.Health;
        while(p.BoardZone.Count<7) Summon(p,"CS2_182");
        var eligible = p.BoardZone.Skip(1).ToArray();
        var fullRng = g.Random.Clone();
        for (int i = 0; i < 4; i++) fullRng.Next(twos.Length);
        var fullExpected = eligible.Select(body => {
            var bucket = EventCardPools.MinionCandidates(g,p,source,body.Card.Cost+1,PoolDestination.Transform);
            Core2026Proof.Check(bucket.Length >= 3, "full-board evolution has at least three eligible replacements");
            return bucket[fullRng.Next(bucket.Length)].Id;
        }).ToArray();
        Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==7 && p.BoardZone.Skip(1).Select(m=>m.Card.Id).SequenceEqual(fullExpected)
            && p.BoardZone.Skip(1).All(m=>m.Card.Cost==eligible[0].Card.Cost+1),
            "all six eligible full-board bodies evolve left-to-right into seeded exact-cost replacements");
        Core2026Proof.Check(original.Zone==p.BoardZone && original.Health==originalHealth && p.BoardZone[0].Id==original.Id,
            "empty exact twenty bucket preserves original identity, position and damage");
        Core2026Proof.Check(p.Opponent.BoardZone.Count==2,"full friendly board does not prevent enemy summons");

        foreach(int cost in new[]{0,2,3,7,20}) System.Console.WriteLine("ev-cards exact "+cost+" bucket="+EventCardPools.MinionCandidates(g,p,source,cost,PoolDestination.Transform).Length);
    }
}
