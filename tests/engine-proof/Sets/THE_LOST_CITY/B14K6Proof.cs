using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Config;
using static Core2026Proof;
using static StandardSetsProof;
using static DiscoverRealProof;
internal static class B14K6LostCityProof
{
    internal static void Run() { StandardSetsProof.Case("DINO_402", Bat); StandardSetsProof.Case("TLC_451", Unpinned_Catacombs); StandardSetsProof.Case("TLC_521", Unpinned_EyesInSkyPhysicalCopies); }
    private static void Bat(Game g, Controller p, string id)
    {
        var location=LocationProofs.Location(p); var left=Summon(p,"CS2_182"); var target=Summon(p,"EX1_110"); var right=Summon(p,"CS2_231");
        target.Damage=3;
        var spell=Add(p,id); var enemy=Summon(p.Opponent,"CS2_182");
        Core2026Proof.Check(!spell.IsValidPlayTarget(enemy) && spell.IsValidPlayTarget(target) && !spell.IsValidPlayTarget(p.Hero), "friendly minion targeting only");
        p.SetasideZone.Add(p.HandZone.Remove(spell));
        Saved(g,next=>{
            var owner=next.Player1; var original=(Minion)next.IdEntityDic[target.Id];
            Play(next,owner,id,original);
            var copies=owner.BoardZone.Where(m=>m.Card.Id=="EX1_110").ToArray();
            Core2026Proof.Check(owner.BoardZone.SlotCount==7 && copies.Length==4 && copies.All(m=>m.AttackDamage==1 && m.Health==1 && m.Damage==0 && m.HasDeathrattle), "fills physical slots with independent 1/1 copies");
            Core2026Proof.Check(owner.BoardZone.GetSlot(0).Id==location.Id && owner.BoardZone.GetSlot(1).Id==left.Id && owner.BoardZone.GetSlot(6).Id==right.Id, "copies right of target in creation order");
            Core2026Proof.Check(copies.All(m=>m.AppliedEnchantments.Count(e=>e.Card.Id=="DINO_402e")==1), "exact named stat attachment once per copy");
            original.Silence(); Core2026Proof.Check(original.AttackDamage==original.Card.ATK && original.Health==original.Card.Health && copies.Skip(1).All(m=>m.Health==1 && m.HasDeathrattle), "Silence restores original independently");
            Kill(next,copies[1]); Core2026Proof.Check(owner.BoardZone.Any(m=>m.Card.Id=="EX1_110t"), "copied Deathrattle executes");
        });
        ParkBoard(p); var single=Summon(p,"CS2_182");
        while(!p.BoardZone.IsFull) LocationProofs.Location(p);
        Play(g,p,id,single); Core2026Proof.Check(p.BoardZone.SlotCount==7 && p.BoardZone.Count==1 && single.Health==1, "full mixed board still sets original; no extra copies");
    }
    // Sources: tier C wiki.gg/Cursed_Catacombs and Discover; tier D exact text; owner-approved card-state/temporary-catacombs-deck-discover.yaml;
    // discover/w14-deck-offers.yaml (PINNED), w14-deck-entities.yaml (STILL-UNPINNED).
    private static void Unpinned_Catacombs(Game original, Controller ignored, string id)
    {
        for(int count=0;count<=4;count++)
        {
            var g=New(original.Seed); var p=g.Player1;
            var cards=Deck(p,Enumerable.Repeat("CS2_182",count).Append(id).Append(id).ToArray());
            foreach(var real in cards.Where(c=>c.Card.Id=="CS2_182"))
            { real.Cost=2;Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_004e"),p.Hero,real); }
            var representativeGame=g.CloneExact(history:true);int expectedReal=count==0?0:representativeGame.Player1.DeckZone.Where(c=>c.Card.Id=="CS2_182").ToArray()[count==1?0:representativeGame.Random.Next(count)].Id;
            Play(g,p,id); var offers=Offers(g);
            Core2026Proof.Check(offers.Length==(count==0?0:1) && offers.All(o=>o.Card.Id!="TLC_451"), "exclude every self identity, deduplicate");
            if(count==0) continue;
            Core2026Proof.Check(offers[0].Cost==2 && ((Minion)offers[0]).Health==7,"deck offer displays existing entity modifications");
            int chosen=Bound(offers[0]);
            Core2026Proof.Check(chosen==expectedReal,"seeded representative among matching deck entities, not always first");
            Saved(g,next=>{
                Answer(next,offers[0].Id); var real=next.IdEntityDic[chosen];
                Core2026Proof.Check(real.Zone==next.Player1.HandZone && TemporaryRules.IsTemporary(real) && real.Cost==2 && ((Minion)real).Health==7 && next.Player1.DeckZone.Count==count+1, "selected real entity moved and Temporary");
                End(next); Core2026Proof.Check(real.Zone==next.Player1.GraveyardZone, "Temporary expires at owner end");
            });
        }
        var sample=New(original.Seed); var owner=sample.Player1;
        var deck=Deck(owner,"CS2_182","CS2_029","CS2_231","CS2_200","CS2_172");
        if(original.Seed%2==0) { foreach(var e in deck) owner.DeckZone.Remove(e); foreach(var e in deck.Reverse()) owner.DeckZone.Add(e); }
        var expected=sample.CloneExact(history:true);
        var groups=expected.Player1.DeckZone.GroupBy(c=>c.Card.Id).Select(a=>a.ToList()).ToList();
        var wanted=new List<int>(); while(wanted.Count<3){int index=expected.Random.Next(groups.Count); wanted.Add(groups[index][0].Id);groups.RemoveAt(index);}
        Play(sample,owner,id); Core2026Proof.Check(Offers(sample).Select(Bound).SequenceEqual(wanted), "seeded reordered-deck sample, not first three");
        while(!owner.HandZone.IsFull) Add(owner,"CS2_182");
        var pick=Offers(sample)[0]; Answer(sample,pick.Id);
        Core2026Proof.Check(sample.IdEntityDic[Bound(pick)].Zone==owner.GraveyardZone && owner.HandZone.Count==10, "full-hand selected original burns");
        CatacombsHeroCardFairView(original.Seed, id); CatacombsHeroCard(original.Seed, id);
    }
    // Regression (Herald Warlock admission crash): "another card" includes hero cards; tier D exact text has no type
    // filter and wiki.gg/Temporary lists no type restriction. A drawn hero card is a hand card and becomes Temporary.
    private static void CatacombsHeroCard(long seed, string id)
    {
        foreach (bool playIt in new[] { false, true })
        {
            var g=New(seed); var p=g.Player1; var hero=p.Hero;
            Deck(p,"CATA_190h");
            Play(g,p,id); var offers=Offers(g);
            Core2026Proof.Check(offers.Length==1 && offers[0].Card.Id=="CATA_190h", "hero card offered from deck");
            int chosen=Bound(offers[0]);
            Saved(g,next=>{
                Answer(next,offers[0].Id); var player=next.Player1; var real=next.IdEntityDic[chosen];
                Core2026Proof.Check(real is Hero && real.Zone==player.HandZone && TemporaryRules.IsTemporary(real) && player.DeckZone.Count==0, "selected hero card moved and Temporary");
                if(playIt)
                {
                    PlayEntity(next,player,real);
                    for(int guard=0;player.Choice!=null && guard<8;guard++) Answer(next,player.Choice.Choices[0],player);
                    Core2026Proof.Check(player.Choice==null, "hero battlecry choice settled");
                    Core2026Proof.Check(player.Hero==real && !TemporaryRules.IsTemporary(real), "played Temporary hero replaces hero and drops Temporary");
                    End(next); Core2026Proof.Check(player.Hero==real && real[GameTag.ZONE]==(int)Zone.PLAY, "played hero survives end of turn");
                }
                else
                {
                    End(next); Core2026Proof.Check(real.Zone==player.GraveyardZone && player.Hero.Id==hero.Id && player.DiscardedEntities.Contains(real.Id), "unplayed Temporary hero card discarded at owner end");
                }
            });
        }
    }
    private static void CatacombsHeroCardFairView(long seed, string id)
    {
        // Bot search builds fair views with sampled own-deck knowledge; the same resolution must not throw there.
        // The setup decklist must hold the hero card: fair views sample hidden own-deck identities from it.
        var source=new Game(new GameConfig { RandomSeed=seed, StartPlayer=1, Shuffle=false, SkipMulligan=true, History=true, Logging=false,
            Player1HeroClass=CardClass.WARLOCK, Player2HeroClass=CardClass.MAGE, FormatType=FormatType.FT_STANDARD,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),28).Concat(new[]{"CATA_190h",id}.Select(Cards.FromId)).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        source.StartGame(); var owner=source.Player1;
        var catacombs=owner.DeckZone.Single(c=>c.Card.Id==id);
        Core2026Proof.Check(owner.DeckZone.Any(c=>c.Card.Id=="CATA_190h"), "setup keeps hero card in deck");
        Generic.Draw(owner,catacombs); owner.BaseMana=10; owner.UsedMana=0;
        var state=FairBotState.Create(source,owner.PlayerId,seed==0?1:(int)seed,DeckStyle.Value,
            new FairOptions { RemoveOpponentSecrets=true, TrackOwnDeck=true, MaterializeOwnDeckTriggers=true });
        var fair=(Game)typeof(FairBotState).GetField("game",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(state);
        var fairOwner=fair.Player1.PlayerId==owner.PlayerId?fair.Player1:fair.Player2;
        var task=fairOwner.Options().FirstOrDefault(t=>t.PlayerTaskType==SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.PLAY_CARD && t.Source?.Id==catacombs.Id);
        Core2026Proof.Check(task!=null && fair.Process(task), "fair view plays Catacombs");
        var fairOffers=Offers(fair,fairOwner);
        Core2026Proof.Check(fairOffers.Any(o=>fair.IdEntityDic[Bound(o)] is Hero), "fair view offers the own-deck hero card");
        foreach(var offer in fairOffers)
        {
            var branch=fair.CloneExact(); var bo=branch.Player1.PlayerId==owner.PlayerId?branch.Player1:branch.Player2;
            Answer(branch,offer.Id,bo);
            var real=branch.IdEntityDic[Bound(offer)];
            Core2026Proof.Check(real.Zone==bo.HandZone && TemporaryRules.IsTemporary(real), "fair branch selection is Temporary in hand: "+real.Card.Id);
        }
    }
    // Sources: tier C wiki.gg/Eyes_in_the_Sky, tier E reddit.com/r/hearthstone/comments/1lj7mhl, tier D export.
    // discover/eyes-in-sky-copy-sampling.yaml, STILL-UNPINNED; owner-approved physical-entity sampling.
    private static void Unpinned_EyesInSkyPhysicalCopies(Game original, Controller ignored, string id)
    {
        for(int count=0;count<=5;count++)
        {
            var g=New(original.Seed);var p=g.Player1;
            var deck=Deck(p.Opponent,Enumerable.Repeat("CS2_182",count).ToArray());
            if(original.Seed%2==0){foreach(var e in deck)p.Opponent.DeckZone.Remove(e);foreach(var e in deck.Reverse())p.Opponent.DeckZone.Add(e);}
            int[] order=p.Opponent.DeckZone.Select(e=>e.Id).ToArray();
            var expected=g.CloneExact(history:true); var candidates=order.ToList(); var wanted=new List<int>();
            if(count<=3)wanted.AddRange(order);else while(wanted.Count<3){int index=expected.Random.Next(candidates.Count);wanted.Add(candidates[index]);candidates.RemoveAt(index);}
            Play(g,p,id);var offers=Offers(g);
            Core2026Proof.Check(offers.Length==Math.Min(3,count) && offers.Select(Bound).SequenceEqual(wanted), "physical duplicate offers, seeded/reordered sample");
            if(count==0)continue;
            int selected=Bound(offers[0]);
            Saved(g,next=>{
                Answer(next,offers[0].Id);var enemy=next.Player2;
                Core2026Proof.Check(enemy.DeckZone.Last().Id==selected && enemy.DeckZone.Where(c=>c.Id!=selected).Select(c=>c.Id).SequenceEqual(order.Where(x=>x!=selected)), "actual selected original on top; other order intact");
                Core2026Proof.Check(!next.HistoryFacts.Query.Events<DiscoverCompletedFact>().Any(), "Look earns no Discover fact");
            });
        }
        var mixed=New(original.Seed);var chooser=mixed.Player1;
        var realCards=Deck(chooser.Opponent,"CS2_029","CS2_106","CORE_REV_990");
        for(int i=0;i<6;i++)Summon(chooser,"CS2_182");
        Play(mixed,chooser,id);var shown=Offers(mixed);
        Core2026Proof.Check(shown.Length==3 && shown.Select(o=>o.Card.Type).Order().SequenceEqual(new[]{CardType.SPELL,CardType.WEAPON,CardType.LOCATION}.Order()),"all enemy deck card types; Battlecry on full board");
        int stale=Bound(shown[0]);chooser.Opponent.SetasideZone.Add(chooser.Opponent.DeckZone.Remove(mixed.IdEntityDic[stale]));
        int[] retained=chooser.Opponent.DeckZone.Select(c=>c.Id).ToArray();
        Answer(mixed,shown[0].Id);Core2026Proof.Check(chooser.Opponent.DeckZone.Select(c=>c.Id).SequenceEqual(retained),"missing bound original no substitute");
    }
}
