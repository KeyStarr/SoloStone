using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B15GameSetupExtraProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,message);
    private static Game Ready(params string[] setup)
    {
        var deck=setup.Concat(Enumerable.Repeat("CS2_182",30-setup.Length)).ToArray();
        var g=B15GameSetupProof.Opening(deck);
        foreach(var p in new[]{g.Player1,g.Player2}) Check(g.Process(ChooseTask.Mulligan(p,p.Choice.Choices.ToList())),"keep setup opening");
        g.MainBegin();
        foreach(var p in new[]{g.Player1,g.Player2}) { foreach(var e in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(e)); p.BaseMana=10; }
        return g;
    }
    private static void Answer(Game g,int index=0)
    {
        var d=g.GetPendingDecisions().Single();
        Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{d.Options[index].OptionId}),out var error),"setup answer: "+error);
    }
    private static void Fill(Controller p,int count)
    { while(p.DeckZone.Count<count) Entity.FromCard(p,Cards.FromId("CS2_182"),zone:p.DeckZone); }
    private static string[] Offers(System.Collections.Generic.IReadOnlyList<Card> candidates,Util.DeepCloneableRandom random)
    {
        var indices=Enumerable.Range(0,candidates.Count).ToArray();var result=new string[Math.Min(3,candidates.Count)];
        for(int i=0;i<result.Length;i++) { int j=i+random.Next(indices.Length-i); (indices[i],indices[j])=(indices[j],indices[i]);result[i]=candidates[indices[i]].Id; }
        return result;
    }
    private static void Pair(Game g)
    { while(g.CurrentPlayer.Choice!=null) Answer(g); }

    internal static void Run()
    {
        BlightCopiedToHand(); PlagueShuffleOperation(); ExcessManaAtActualCap(); SteamcleanerFairView(); Capacity(); Unpinned_MerchSeedAndCapacity(); Unpinned_GreenwingCapacity();
        Unpinned_HamuulCastScope(); HoggerPriorityAndOverflow(); Unpinned_PlaguePreventionCount();
        Unpinned_CultivatorBottomPairOrder(); Unpinned_CultivatorShortPools(); RewindState();
        Console.WriteLine("PASS B15 boundaries: order/RNG, overflow, both seats, Silence, clone/load/Rewind");
    }
    private static void BlightCopiedToHand()
    {
        var g=Ready(); var p=g.Player1;
        foreach(var e in p.Opponent.DeckZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(e));
        var original=Entity.FromCard(p.Opponent,Cards.FromId("JAIL_443t"),zone:p.Opponent.DeckZone);
        Play(g,p,"CORE_EX1_193");
        var copy=p.HandZone.Single();
        Check(copy.Card.Id=="JAIL_443t" && copy.Id!=original.Id,"Psychic Conjurer copies hidden Blight to hand");
        foreach(var branch in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var owner=branch.Player1; int deck=owner.DeckZone.Count, mana=owner.RemainingMana;
            Check(branch.Process(owner.Options().Single(t=>t.PlayerTaskType==PlayerTaskType.PLAY_CARD && t.Source?.Id==copy.Id)),"play copied Blight");
            Check(owner.Hero.Damage==2 && owner.RemainingMana==mana-1,"hand-played Blight deals two damage for one mana");
            Check(owner.DeckZone.Count==deck && owner.HandZone.Count==0,"hand-played Blight does not draw a replacement");
            Check(owner.Opponent.Hero.Damage==0 && owner.Opponent.DeckZone.Single().Id==original.Id,"Blight copy leaves opponent and original untouched");
        }
    }
    private static void PlagueShuffleOperation()
    {
        foreach(bool ownHero in new[]{true,false})
        foreach(int space in new[]{8,3,0}) {
            var g=Ready(); var p=g.Player1;
            var source=Summon(p,"JAIL_443"); var tracker=Add(p,"TLC_520");
            Fill(ownHero?p:p.Opponent,99-space);
            foreach(var branch in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=branch.Player1; var destination=ownHero?owner:owner.Opponent;
                destination.Hero.TakeDamage((IPlayable)branch.IdEntityDic[source.Id],8);
                branch.DeathProcessingAndAuraUpdate();
                Check(((IPlayable)branch.IdEntityDic[tracker.Id]).Cost==(ownHero && space>0?5:6),"one Plague shuffle discounts Underbrush Tracker once only for own deck");
                var members=branch.HistoryFacts.Query.Events<ShuffleOperationFact>().ToArray();
                Check(members.Length==space && members.Select(f=>f.OperationId).Distinct().Count()==(space>0?1:0),"one operation contains every successful Blight insertion");
                Check(members.All(f=>f.ControllerId==owner.Id && f.DestinationControllerId==destination.Id)
                    && destination.DeckZone.Count==99 && destination.Hero.Damage==0,"Plague shuffle preserves source ownership and overflow replacement");
                var restored=Game.LoadSnapshot(branch.SaveSnapshot());
                Check(restored.HistoryFacts.Query.Shuffles().Count()==(space>0?1:0),"Plague shuffle operation survives snapshot");
            }
        }
    }
    private static void ExcessManaAtActualCap()
    {
        foreach(int cap in new[]{15,10})
        foreach(var input in new[]{(mana:cap,temporary:0,excess:true),(mana:cap-1,temporary:0,excess:false),
            (mana:10,temporary:0,excess:cap==10),(mana:cap-2,temporary:Cards.FromId("CS2_013").Cost+2,excess:true)}) {
            var g=cap==15?Ready("EDR_000"):Ready(); var p=g.Player1;
            p.BaseMana=input.mana; p.UsedMana=0; p.TemporaryMana=input.temporary;
            var growth=Add(p,"CS2_013");
            Check(SabberStoneCore.Conditions.SelfCondition.IsManaCrystalFull.Eval(p.Hero)==(input.mana==10),"fixed-ten conditions stay separate from actual mana cap");
            foreach(var branch in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=branch.Player1;
                Check(branch.Process(owner.Options().Single(t=>t.PlayerTaskType==PlayerTaskType.PLAY_CARD && t.Source?.Id==growth.Id)),"play Wild Growth at actual mana cap");
                Check(owner.BaseMana==Math.Min(input.mana+1,cap) && owner[GameTag.MAXRESOURCES]==cap,"Wild Growth respects saved maximum resources");
                Check(owner.HandZone.Count(e=>e.Card.Id=="CS2_013t")== (input.excess?1:0),$"Wild Growth excess: cap={cap}, mana={input.mana}, temporary={input.temporary}, remaining={owner.RemainingMana}, hand={string.Join(',',owner.HandZone.Select(e=>e.Card.Id))}");
            }
        }
    }
    private static void SteamcleanerFairView()
    {
        var original=Ready();var p=original.Player1;
        Entity.FromCard(p.Opponent,Cards.FromId("CS2_029"),zone:p.Opponent.DeckZone);
        var fair=FairBotState.Create(original,p.PlayerId,73,DeckStyle.Control);
        var game=(Game)typeof(FairBotState).GetField("game",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(fair);
        Play(game,game.Player1,"CORE_REV_946");
        Check(game.Step==Step.MAIN_ACTION && game.Player1.Choice==null,"Steamcleaner completes with opaque deck origins in fair search");
        Check(original.Player1.BoardZone.Count==0,"fair Steamcleaner leaves authoritative root untouched");
    }
    private static void Capacity()
    {
        var g=Ready(); var p=g.Player1; Fill(p,99);
        var order=p.DeckZone.Select(e=>e.Id).ToArray(); int entities=g.IdEntityDic.Count; var rng=g.Random.CaptureState();
        Check(p.DeckZone.AddGenerated(Cards.FromId("CS2_029"),p.Hero)==null
            && p.DeckZone.AddGenerated(Cards.FromId("CS2_029"),p.Hero,0,true)==null,"99 capacity no new actor");
        Check(entities==g.IdEntityDic.Count && rng.Equals(g.Random.CaptureState()) && order.SequenceEqual(p.DeckZone.Select(e=>e.Id)),"overflow preserves identity/order/RNG");
        var legacy=Ready();var zone=legacy.Player1.DeckZone;
        typeof(SabberStoneCore.Model.Zones.LimitedZone<IPlayable>).GetField("MaxSize").SetValue(zone,60);
        var array=new IPlayable[60];zone.ToArray().CopyTo(array,0);
        typeof(SabberStoneCore.Model.Zones.Zone<IPlayable>).GetField("_entities",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(zone,array);
        foreach(var branch in new[]{legacy.CloneExact(history:true),Game.LoadSnapshot(legacy.SaveSnapshot())}) {
            Check(branch.Player1.DeckZone.FreeSpace==99-zone.Count,"old 60-cap field payload uses current capacity");
            Fill(branch.Player1,99);Check(branch.Player1.DeckZone.IsFull,"old 60-slot snapshot grows safely to 99");
        }
    }
    private static void Unpinned_MerchSeedAndCapacity()
    {
        foreach(int seed in new[]{2,9,71}) {
            var g=Ready(); g.SetRandomSeed(seed);
            var p=g.Player1; var seller=(Minion)Play(g,p,"CORE_ETC_111");
            var candidates=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.SPELL),130822);
            Console.WriteLine("Merch spell bucket="+candidates.Count);
            var expected=g.Random.Clone(); var pick=candidates[expected.Next(candidates.Count)].Id;
            End(g);
            Check(p.Opponent.HandZone.Any(e=>e.Card.Id==pick) && expected.CaptureState().Equals(g.Random.CaptureState()),"Merch exact seeded topdeck and RNG");
            End(g); Fill(p.Opponent,99); int count=g.IdEntityDic.Count;
            var rng=g.Random.CaptureState(); var top=p.Opponent.DeckZone.TopCard.Id;
            End(g);
            Check(count==g.IdEntityDic.Count && rng.Equals(g.Random.CaptureState()) && p.Opponent.HandZone.Any(e=>e.Id==top),"Unpinned_MerchFullDeck creates nothing");
            End(g); seller.Silence(); var before=p.Opponent.DeckZone.Count; End(g);
            Check(p.Opponent.DeckZone.Count==before-1,"Silence removes Merch trigger");
        }
    }
    private static void Unpinned_GreenwingCapacity()
    {
        foreach(bool fullHand in new[]{false,true})
        foreach(bool indexed in new[]{false,true}) {
            var g=Ready(); var p=g.Player1;
            for(int i=0;i<7;i++) Summon(p,"CS2_182");
            while(p.HandZone.Count<(fullHand?10:9)) Add(p,"CS2_182");
            var token=Entity.FromCard(p,Cards.FromId("EDR_260t"),zone:p.DeckZone); int before=p.DeckZone.Count;
            if(indexed) Generic.Draw(p,p.DeckZone.Count-1);else Generic.Draw(p,token);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
            Check(p.BoardZone.Count==7 && p.HandZone.Count==10,"Greenwing full-board keeps drawn Illusion or full-hand burns");
            Check(token.Zone.Type==(fullHand?Zone.GRAVEYARD:Zone.HAND) && p.DeckZone.Count==before-(fullHand?1:2),"Unpinned_Greenwing retention/replacement versus burn");
            if(!fullHand) Check(!g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Any(e=>e.EntityId==token.Id && e.Tag==GameTag.ZONE && e.Value==(int)Zone.SETASIDE),
                "full-board Illusion never leaves its hand residence");
        }
        var cap=Ready(); Fill(cap.Player1,98); var body=Summon(cap.Player1,"EDR_260"); Kill(cap,body);
        Check(cap.Player1.DeckZone.Count==99 && cap.Player1.DeckZone.Count(e=>e.Card.Id=="EDR_260t")==1,"Greenwing loses only overflowing second token");
    }
    private static void Unpinned_HamuulCastScope()
    {
        var nature=Ready("EDR_845","CORE_EX1_169");
        Check(nature.Player1[GameTag.IMBUE]==1,"all-Nature nonempty original spell set qualifies");
        var duplicate=Ready("EDR_845","EDR_845");
        Check(duplicate.Player1[GameTag.IMBUE]==1 && duplicate.Player1.AppliedEnchantments.Count(e=>e.Card.Id=="EDR_845e1")==1,
            "multiple starting Hamuuls install one recurring effect");
        var g=Ready("EDR_845"); var p=g.Player1;
        Check(p[GameTag.IMBUE]==1,"Hamuul empty spell set qualifies once");
        Play(g,p,"EDR_845"); ((Minion)p.BoardZone.Last()).Silence();
        for(int i=0;i<2;i++) Play(g,p,"GAME_005");
        foreach(var branch in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var owner=branch.Player1; End(branch); Play(branch,branch.Player2,"GAME_005"); Play(branch,branch.Player2,"EX1_287");End(branch);
            Check(owner[GameTag.IMBUE]==1,"opponent cast does not advance Hamuul");
            Play(branch,owner,"GAME_005");Check(owner[GameTag.IMBUE]==1,"countered player spell does not advance Hamuul");
            Generic.CastSpell(owner,branch,(Spell)Entity.FromCard(owner,Cards.FromId("GAME_005")),null,0);
            Check(owner[GameTag.IMBUE]==1,"automatic spell does not advance Hamuul");
            Play(branch,owner,"GAME_005");
            Check(owner[GameTag.IMBUE]==2,"third player spell across turns advances once after source Silence");
        }
    }
    private static void HoggerPriorityAndOverflow()
    {
        // Malchezaar creates five Legendaries; Hogger must include those and the opening-hand copy.
        var priority=B15GameSetupProof.Kept(new[]{"JAIL_384","KAR_096"}.Concat(Enumerable.Repeat("CS2_182",28)).ToArray());
        Check(priority.Player1.DeckZone.Concat(priority.Player1.HandZone).Count(e=>e.Card.Rarity==Rarity.LEGENDARY)==13,
            "Hogger follows legacy Malchezaar and copies five generated Legendaries");
        var orders=new HashSet<string>();
        foreach(int seed in new[]{1,7,19}) {
            var names=new[]{"EX1_559","EX1_016","EX1_298","EX1_563","EX1_572","EX1_562","EX1_116"};
            var input=new[]{"JAIL_384"}.Concat(Enumerable.Range(0,98).Select(i=>names[i%names.Length])).ToArray();
            // Keep constructor legal at 99; opening draws create three slots before Hogger.
            var g=B15GameSetupProof.Resolved(input.Take(99).ToArray(),seed:seed);
            Check(g.Player1.DeckZone.Count==99,"Hogger stops precisely at cap");
            var candidates=g.Player1.DeckZone.Concat(g.Player1.HandZone).Where(e=>e.Card.Id!="JAIL_384" && g.HistoryFacts.Query.StartedInDeck(e.Id,g.Player1.Id)).ToArray();
            // Rebuild the original pre-copy deck-then-hand sequence from surviving originals.
            var expected=new Util.DeepCloneableRandom(seed);
            for(int i=0;i<candidates.Length-1;i++) { int j=expected.Next(i,candidates.Length); (candidates[i],candidates[j])=(candidates[j],candidates[i]); }
            var generated=g.Player1.DeckZone.Where(e=>!g.HistoryFacts.Query.StartedInDeck(e.Id,g.Player1.Id)).OrderBy(e=>e.Id).ToArray();
            Check(generated.Select(e=>e.Card.Id).SequenceEqual(candidates.Take(3).Select(e=>e.Card.Id)),"Hogger overflow keeps exact seeded random subset");
            orders.Add(string.Join(",",g.Player1.DeckZone.Select(e=>e.Id)));
            g.MainBegin(); // enter turn one: snapshots need a decision boundary
            var clone=g.CloneExact(history:true); var load=Game.LoadSnapshot(g.SaveSnapshot());
            Check(Canonical.State(g)==Canonical.State(clone) && Canonical.State(g)==Canonical.State(load),"Hogger mulligan clone/load");
        }
        Check(orders.Count>1,"Hogger seeded insertion order varies");
    }
    private static void Unpinned_PlaguePreventionCount()
    {
        var g=Ready(); var p=g.Player1; var source=Summon(p,"JAIL_443"); var target=p.Opponent.Hero;
        target.Armor=20; target.IsImmune=true; target.HasDivineShield=true;
        int count=p.Opponent.DeckZone.Count;
        Check(target.TakeDamage(source,8)==0 && target.HasDivineShield && p.Opponent.DeckZone.Count==count,"Immune retains Shield without Blights");
        target.IsImmune=false; target.TakeDamage(source,8);
        Check(!target.HasDivineShield && target.Armor==20 && p.Opponent.DeckZone.Count==count,"Shield consumed without Blights");
        Fill(p.Opponent,99); target.TakeDamage(source,8);
        Check(target.Armor==20 && target.Damage==0 && p.Opponent.DeckZone.Count==99,"full deck still replaces damage");
        source.Silence(); target.TakeDamage(source,8);
        Check(target.Armor==12,"Silence restores ordinary Armor damage");
        var shield=Ready();var attacker=Summon(shield.Player1,"JAIL_443");shield.Player2.Hero.Damage=25;
        End(shield);Play(shield,shield.Player2,"EX1_295");End(shield);int before=shield.Player2.DeckZone.Count;
        shield.Player2.Hero.TakeDamage(attacker,8);
        Check(shield.Player2.Hero.IsImmune && shield.Player2.DeckZone.Count==before,"Ice Block suppresses lethal shuffle");
        var draw=Ready();var owner=draw.Player1;var blight=Entity.FromCard(owner,Cards.FromId("JAIL_443t"),zone:owner.DeckZone);
        int deck=owner.DeckZone.Count;Generic.Draw(owner,blight);draw.ProcessTasks();draw.DeathProcessingAndAuraUpdate();
        Check(owner.Hero.Damage==2 && owner.DeckZone.Count==deck-2 && owner.HandZone.Count==1,"Blight damage and one replacement draw");
        foreach(var branch in new[]{draw,draw.CloneExact(history:true),Game.LoadSnapshot(draw.SaveSnapshot())}) {
            var a=Summon(branch.Player1,"JAIL_443");int own=branch.Player1.DeckZone.Count;branch.Player1.Hero.TakeDamage(a,8);
            Check(branch.Player1.DeckZone.Count==own+8,"redirected own-hero proposals replace too");
        }
    }
    private static void Unpinned_CultivatorBottomPairOrder()
    {
        var orders=new HashSet<string>();
        foreach(int seed in new[]{1,2,7,19}) {
            var g=Ready();g.SetRandomSeed(seed);var p=g.Player1;
            var old=p.DeckZone.Select(e=>e.Id).ToArray();
            var candidates=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Discover(type:CardType.MINION,race:Race.BEAST),121460);
            Console.WriteLine("Cultivator Beast bucket="+candidates.Count);
            var expected=g.Random.Clone();var expectedFirst=Offers(candidates,expected);var expectedSecond=Offers(candidates,expected);bool reverse=expected.Next(2)==1;
            Play(g,p,"TIME_730");
            Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).SequenceEqual(expectedFirst),"exact first Beast offer order");
            Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).Distinct().Count()==p.Choice.Choices.Count,"first Discover distinct");
            int first=p.Choice.Choices[0];Answer(g);
            Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).SequenceEqual(expectedSecond),"exact second Beast offer order");
            Check(p.DeckZone.Count==old.Length && p.HandZone.Count==0,"first selection held outside hand and deck");
            foreach(var branch in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=branch.Player1; int second=owner.Choice.Choices.FirstOrDefault(id=>branch.IdEntityDic[id].Card.Id!=branch.IdEntityDic[first].Card.Id);
                var d=branch.GetPendingDecisions().Single();Check(branch.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{second}),out _),"second Beast answer");
                Check(old.SequenceEqual(owner.DeckZone.Skip(2).Select(e=>e.Id)),"only bottom pair randomized");
                var added=owner.DeckZone.Take(2).ToArray();
                Check(added.All(e=>((Minion)e).AttackDamage==e.Card.ATK+5 && ((Minion)e).Health==e.Card.Health+5),"pair buffs use exact +5/+5");
                var drawOrder=new[]{first,second};if(reverse) Array.Reverse(drawOrder);
                Check(added.Reverse().Select(e=>e.Id).SequenceEqual(drawOrder) && expected.CaptureState().Equals(branch.Random.CaptureState()),"exact seeded bottom draw order and RNG continuation");
                orders.Add(string.Join(",",added.Select(e=>e.Id)));
            }
        }
        Check(orders.Count>1,"both bottom pair permutations reachable");
        foreach(int capacity in new[]{98,99}) {
            var g=Ready();Fill(g.Player1,capacity);while(g.Player1.HandZone.Count<9) Add(g.Player1,"CS2_182");
            for(int i=0;i<6;i++) Summon(g.Player1,"CS2_182");
            Play(g,g.Player1,"TIME_730");var source=g.Player1.BoardZone.Single(e=>e.Card.Id=="TIME_730");Answer(g);
            while(g.Player1.HandZone.Count<10) Add(g.Player1,"CS2_182");Kill(g,source);Pair(g);
            Check(g.Player1.DeckZone.Count==99,"Cultivator respects cap and continues after source death");
        }
    }
    private static void Unpinned_CultivatorShortPools()
    {
        foreach(int size in new[]{0,1,2}) {
            var g=Ready();var p=g.Player1;var xml=PinnedPoolFixture.Catalog();var section=xml.Root.Element("Members");
            var rows=section.Elements("Card").Where(e=>(string)e.Attribute("scope")=="Standard" && (string)e.Attribute("type")=="MINION"
                && e.Elements("Class").Any(c=>(string)c.Attribute("name")=="NEUTRAL") && e.Elements("Race").Any(r=>(string)r.Attribute("name")=="BEAST")).Take(size).ToArray();
            var nonbeast=section.Elements("Card").First(e=>(string)e.Attribute("scope")=="Standard" && (string)e.Attribute("type")=="SPELL");
            section.RemoveNodes();section.Add(rows);section.Add(nonbeast);xml.Root.Element("Dependencies").RemoveNodes();
            foreach(var row in section.Elements("Card")) row.Elements("Dependency").Remove();
            typeof(Game).GetProperty("OfflinePools").SetValue(g,OfflinePoolFixtures.Manifest(xml));
            int before=p.DeckZone.Count;Play(g,p,"TIME_730");Pair(g);
            Check(p.DeckZone.Count==before+(size==0?0:2),"Cultivator short/empty exact pool yields only available choices");
            if(size==1) Check(p.DeckZone.Take(2).Select(e=>e.Card.Id).Distinct().Count()==1 && p.DeckZone.Take(2).Select(e=>e.Id).Distinct().Count()==2,
                "two independent singleton Discovers allow same definition as distinct entities");
            if(size==1) {
                var original=(Minion)p.DeckZone.First();var copied=(Minion)Generic.Copy(p,p.Hero,original,Zone.PLAY);
                Check(copied.AttackDamage==original.AttackDamage && copied.AppliedEnchantments.Any(e=>e.Card.Id=="TIME_730e"),"forward copy retains Beast buff layer");
                copied.Silence();Check(copied.AttackDamage==copied.Card.ATK && copied.Health==copied.Card.Health
                    && original.AttackDamage==original.Card.ATK+5,"Silence removes only copied Beast buff");
            }
            Console.WriteLine("Cultivator short bucket="+size);
        }
    }
    private static void RewindState()
    {
        var g=Ready("EDR_845","EDR_000");var p=g.Player1;Play(g,p,"GAME_005");
        var source=Add(p,"GAME_005");source[GameTag.REWIND]=1;PlayEntity(g,p,source);
        Check(RewindTestHelpers.IsRewindOffered(g),"spell opens Rewind with active setup state");
        RewindTestHelpers.AnswerRewind(g);
        Check(g.Player1[GameTag.MAXRESOURCES]==15 && g.Player1[GameTag.IMBUE]==1
            && g.Player1.AppliedEnchantments.Single(e=>e.Card.Id=="EDR_845e1")[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,
            "Rewind retry restores setup mana and advances saved Hamuul remainder exactly once");
        Pair(g);RewindTestHelpers.KeepIfOffered(g);
    }
}
