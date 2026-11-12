using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16TimersBoundaryProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,message);
    internal static void Run()
    {
        var failures=new List<string>();
        foreach(var proof in new Action[]{Unpinned_FairTome,Unpinned_RestoredTome,OpeningScope})
            try { proof(); } catch(Exception error) { failures.Add(error.ToString());Console.WriteLine("FAIL "+error.Message); }
        Check(failures.Count==0,string.Join("; ",failures));
        EscrowAdaptiveCost();Clock();Unpinned_Control();Unpinned_Mana();Unpinned_WhelpFullHand();Unpinned_TomeForms();Unpinned_Escrow();Opening();Pools();Rewind();
    }
    private static void EscrowAdaptiveCost()
    {
        var game = FinsGame(); var owner = game.Player1;
        var held = Add(owner, "CATA_568");
        CostRules.AddModifier((Playable)held, SabberStoneCore.Enchants.EffectOperator.SUB, 1);
        int cost = held.Cost;
        Play(game, owner, "TIME_706");
        foreach (var branch in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            End(branch);
            var restored = branch.IdEntityDic[held.Id];
            Check(restored.Zone == branch.Player1.HandZone && restored.Cost == cost, "Fins restores persistent cost adjustment");
            Check(branch.Auras.Count(a => a.Owner?.Id == restored.Id) == 1, "Fins reactivates exactly one adaptive cost aura after clone/load");
            End(branch);
            PlayEntity(branch, branch.Player1, restored);
            branch.AuraUpdate();
            Check(!branch.Auras.Any(a => a.Owner?.Id == restored.Id), "playing restored adaptive-cost card removes its only aura");
        }
    }
    internal static Game FinsGame(int seed=1,string source="TIME_706",OfflinePoolManifest pools=null,bool legacy=false)
    {
        var g=new Game(new SabberStoneCore.Config.GameConfig { RandomSeed=seed,StartPlayer=1,Shuffle=false,
            SkipMulligan=true,Logging=false,History=true,Player1HeroClass=CardClass.PALADIN,Player2HeroClass=CardClass.MAGE,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),29).Append(Cards.FromId(source)).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),ReplayPools=pools,LegacyPoolReplay=legacy });
        g.StartGame();
        foreach(var owner in new[]{g.Player1,g.Player2})
        {
            foreach(var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            owner.BaseMana=10;
        }
        return g;
    }
    private static void OpeningScope()
    {
        var xml=PinnedPoolFixture.Catalog();var rows=xml.Root.Element("Members");
        rows.RemoveNodes();
        var fins=Cards.FromId("TIME_706");
        rows.Add(new XElement("Card",new XAttribute("dbfId",fins.AssetId),new XAttribute("cardId",fins.Id),
            new XAttribute("scope","Standard"),new XAttribute("set","TIME_TRAVEL"),new XAttribute("proofKey","test"),
            new XAttribute("registrationKind","standard-module"),new XAttribute("type","MINION"),new XAttribute("cost",2),
            new XAttribute("attack",2),new XAttribute("health",3),new XAttribute("rarity","LEGENDARY"),new XAttribute("spellSchool","NONE"),
            new XElement("Class",new XAttribute("name","PALADIN")),new XElement("Race",new XAttribute("name","MURLOC")),
            fins.Tags.Select(t=>new XElement("Tag",new XAttribute("id",(int)t.Key),new XAttribute("value",t.Value)))));
        var intermediate=new XElement(rows.Element("Card"));var spell=Cards.FromId("EDR_264");
        intermediate.SetAttributeValue("cardId",spell.Id);intermediate.SetAttributeValue("dbfId",spell.AssetId);
        intermediate.SetAttributeValue("set","EMERALD_DREAM");intermediate.SetAttributeValue("type","SPELL");
        intermediate.SetAttributeValue("cost",spell.Cost);intermediate.SetAttributeValue("attack",0);intermediate.SetAttributeValue("health",0);
        intermediate.SetAttributeValue("rarity",spell.Rarity);intermediate.SetAttributeValue("spellSchool","HOLY");
        intermediate.Elements("Race").Remove();intermediate.Elements("Tag").Remove();
        rows.Add(intermediate);
        xml.Root.Element("Dependencies").RemoveNodes();
        var pools=OfflinePoolFixtures.Manifest(xml);
        foreach(var ordinary in new[]{FinsGame(source:"CS2_182",pools:pools),
            FinsGame(source:"TIME_000",pools:PinnedPoolFixture.Manifest),FinsGame(source:"TIME_000",pools:pools,legacy:true)})
            Check(new[]{ordinary.Player1,ordinary.Player2}.All(p=>!p.NativeTags.Keys.Any(t=>(int)t==32804 || (int)t>=60000)),
                "unrelated initial decks and pools carry no opening snapshot tags");
        foreach(var direct in new[]{FinsGame(),FinsGame(source:"CATA_614",pools:pools)})
            Check(TurnQueueRules.StartingHand(direct.Player1).Length==3 && TurnQueueRules.StartingHand(direct.Player2).Length==5,
                "initial Fins or admitted intermediate generator captures both openings");
        var generated=FinsGame(source:"TIME_000",pools:pools);
        foreach(var branch in new[]{generated,generated.CloneExact(history:true),Game.LoadSnapshotWithPools(generated.SaveSnapshot(),pools)})
        {
            Play(branch,branch.Player1,"TIME_000");RewindTestHelpers.KeepIfOffered(branch);
            var actual=branch.Player1.HandZone.Single(c=>c.Card.Id=="TIME_706");
            PlayEntity(branch,branch.Player1,actual);
            Check(branch.Player1.HandZone.Select(c=>c.Card.Id).SequenceEqual(Enumerable.Repeat("CS2_182",3)),
                "generated Fins uses the early opening snapshot across clone/load");
        }
        Console.WriteLine("PASS opening snapshot initial/direct/indirect/pinned/legacy scope and generated Fins");
    }
    private static void Unpinned_FairTome()
    {
        var g=HeraldProof.New(CardClass.MAGE);var p=g.Player1;
        Summon(p,"CS2_182");Summon(p.Opponent,"CS2_182");
        var tome=Add(p,"BE_327");
        foreach(int seed in new[]{1,73,501})
        {
            string expected=null;
            foreach(string form in new[]{"BE_327t","BE_327t1","BE_327t2","BE_327t3"})
            {
                tome[(GameTag)32805]=Cards.FromId(form).AssetId;
                string before=Canonical.State(g);
                var fair=FairBotState.Create(g,p.PlayerId,seed,DeckStyle.Control);
                var view=(Game)typeof(FairBotState).GetField("game",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(fair);
                Check(view.IdEntityDic[tome.Id][(GameTag)32805]==0,"own unrevealed Tome selection redacted from fair simulation");
                var action=fair.Actions.First(a=>a.Source==tome.Id && a.Type==PlayerTaskType.PLAY_CARD);
                var played=fair.Apply(action);
                string result=played.Describe()+"/"+played.Evaluate();
                expected??=result;
                Check(result==expected,"same visible state and artificial seed ignore every authoritative hidden form");
                Check(Canonical.State(g)==before,"fair Tome simulation leaves authoritative selection and RNG untouched");
            }
        }
        Console.WriteLine("PASS Unpinned_FairTome hidden-form independence");
    }
    private static void Unpinned_RestoredTome()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var g=HeraldProof.New(CardClass.PALADIN);if(enemy) End(g);var p=g.CurrentPlayer;
            var tome=Add(p,"BE_327");
            End(g);End(g);
            int selected=tome[(GameTag)32805];
            Check(tome[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"Tome partially deciphered before escrow");
            var progress=Add(p,"JAIL_803");Play(g,p,"GAME_005");
            Check(progress[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"spell progress partially advanced before escrow");
            Play(g,p,"TIME_706");
            foreach(var next in Branches(g))
            {
                var restored=next.IdEntityDic[tome.Id];
                var random=next.CloneExact().Random;
                End(next);
                Check(restored[(GameTag)32805]==selected && restored[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,
                    "Fins restores saved Tome selection and countdown without fresh initialization");
                Check(next.IdEntityDic[progress.Id][GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"escrow restoration preserves existing hand progress");
                Check(next.Random.Next()==random.Next(),"escrow restoration consumes no new Tome RNG");
                End(next);
                Check(restored.Card.AssetId==selected,"restored Tome deciphers at original due boundary");
            }
        }
        Console.WriteLine("PASS Unpinned_RestoredTome owner/opponent clone/snapshot");
    }
    private static Game[] Branches(Game g)=>new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())};
    private static void Clock()
    {
        var g=HeraldProof.New(CardClass.MAGE);var actor=g.Player1.Hero;
        TurnQueueRules.Arm(actor,g.Player1,2);
        Check(!TurnQueueRules.Tick(actor),"installation start excluded");
        TurnQueueRules.Prepend(g,g.Player1,g.Player1);
        End(g);Check(!TurnQueueRules.Tick(actor),"first extra owner start consumes one");
        Check(!TurnQueueRules.Tick(actor),"duplicate boundary cannot consume twice");
        foreach(var next in Branches(g))
        {
            End(next);Check(TurnQueueRules.Tick(next.Player1.Hero),"second extra owner turn completes saved clock");
            Check(!TurnQueueRules.Tick(next.Player1.Hero),"clock consumed once");
        }
    }
    private static void Unpinned_Control()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var g=HeraldProof.New(CardClass.WARLOCK);if(enemy) End(g);var p=g.CurrentPlayer;
            var victim=Summon(p.Opponent,"CS2_182");Play(g,p,"CATA_496",victim);
            foreach(var next in Branches(g))
            {
                var v=(Minion)next.IdEntityDic[victim.Id];var owner=next.ControllerById(p.Id);
                End(next);Check(v.Controller==owner && !v.CantAttack,"lease remains beyond caster end");
                v[GameTag.CANT_ATTACK]=1;v.Silence();
                Check(v.Controller==owner.Opponent && v.CantAttack,"silence returns saved owner without clearing unrelated ban");
                End(next);Check(v.Controller==owner.Opponent,"cancelled return never fires twice");
            }
        }
        foreach(bool bounce in new[]{false,true})
        {
            var g=HeraldProof.New(CardClass.WARLOCK);var p=g.Player1;
            var victim=Summon(p.Opponent,"CS2_182");Play(g,p,"CATA_496",victim);
            if(bounce) Generic.ReturnToHandBlock(p,victim);else Kill(g,victim);
            End(g);End(g);
            Check(victim.Controller==p && victim.Zone.Type==(bounce?Zone.HAND:Zone.GRAVEYARD),"leave play cancels control return");
        }
        {
            var g=HeraldProof.New(CardClass.WARLOCK);var p=g.Player1;
            var victim=Summon(p.Opponent,"CS2_182");Play(g,p,"CATA_496",victim);
            while(!p.Opponent.BoardZone.IsFull) Summon(p.Opponent,"CS2_182");
            victim.Silence();g.DeathProcessingAndAuraUpdate();
            Check(victim.Zone.Type==Zone.GRAVEYARD,"failed full-board return destroys");
        }
    }
    private static void Unpinned_Mana()
    {
        foreach(int mana in new[]{3,8,9,10})
        {
            var g=HeraldProof.New(CardClass.SHAMAN);var p=g.Player1;
            var source=(Minion)Play(g,p,"FIR_927");
            var offers=p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.Id).ToArray();
            foreach(var branch in Branches(g))
            {
                Check(branch.Player1.Choice.Choices.Select(e=>branch.IdEntityDic[e].Card.Id).SequenceEqual(offers),"Discover offer order persists");
                B16TimersMiscProof.Answer(branch);
                var owner=branch.Player1;((Minion)branch.IdEntityDic[source.Id]).Silence();
                Kill(branch,(Minion)branch.IdEntityDic[source.Id]);
                TemporaryRules.NextTurnMana(owner);owner.BaseMana=mana;
                End(branch);End(branch);
                int permanent=Math.Min(10,mana+1);
                Check(owner.BaseMana==permanent && owner.TemporaryMana==Math.Min(2,10-permanent),"stacked temporary crystals cap after normal growth");
                End(branch);End(branch);Check(owner.BaseMana==Math.Min(10,permanent+1) && owner.TemporaryMana==0,"next refill has no permanent bonus");
            }
        }
    }
    private static void Unpinned_WhelpFullHand()
    {
        var g=HeraldProof.New(CardClass.SHAMAN);var p=g.Player1;
        Play(g,p,"FIR_927");
        while(!p.HandZone.IsFull) Add(p,"CS2_182");
        B16TimersMiscProof.Answer(g);
        Check(p.HandZone.Count==10 && p.Choice==null,"full hand burns Discover without stalling");
        p.BaseMana=3;End(g);End(g);
        Check(p.RemainingMana==5,"full-hand Discover still grants next-turn mana");
    }
    private static void Unpinned_TomeForms()
    {
        var randomGame=HeraldProof.New(CardClass.MAGE,7);var forms=new List<int>();
        for(int i=0;i<8;i++)
        {
            var tome=Add(randomGame.Player1,"BE_327");forms.Add(tome[(GameTag)32805]);
            randomGame.Player1.SetasideZone.Add(randomGame.Player1.HandZone.Remove(tome));
        }
        Console.WriteLine("Tome seed 7 ordered forms: "+string.Join(",",forms));
        Check(forms.SequenceEqual(new[]{131120,131120,129237,129236,131121,129237,129236,129236}),"seed 7 hidden forms preserve selection order");
        foreach(string form in new[]{"BE_327t","BE_327t1","BE_327t2","BE_327t3"})
        {
            var g=HeraldProof.New(CardClass.MAGE);var p=g.Player1;
            var neighbour=Summon(p,"CS2_182");var other=Summon(p.Opponent,"CS2_182");
            var tome=Add(p,"BE_327");tome[(GameTag)32805]=Cards.FromId(form).AssetId;
            var spell=Add(p,"CS2_029");
            PlayEntity(g,p,tome);
            Check(tome.Card.Id==form,"early play reveals preselected variant");
            if(form=="BE_327t") Check(p.BoardZone.Count==4,"two exact-cost Deathrattle summons");
            if(form=="BE_327t1")
            {
                Check(spell.Cost==0,"next spell minus four");
                PlayEntity(g,p,spell,p.Opponent.Hero);
                Check(Add(p,"CS2_029").Cost==4,"discount consumed by next spell");
            }
            if(form=="BE_327t2") Check(neighbour.Damage+other.Damage==6,"six missiles among other minions");
            if(form=="BE_327t3")
            {
                Check(((Minion)tome).AttackDamage==9 && ((Minion)tome).BaseHealth==9,"adjacent stats additive enchantment");
                ((Minion)tome).Silence();Check(((Minion)tome).AttackDamage==5 && ((Minion)tome).BaseHealth==4,"Silence removes gained stats");
            }
        }
        var full=HeraldProof.New(CardClass.MAGE);for(int i=0;i<6;i++) Summon(full.Player1,"CS2_182");
        Play(full,full.Player1,"BE_327t");Check(full.Player1.BoardZone.Count==7,"full board summons yield nothing");
    }
    private static void Unpinned_Escrow()
    {
        var g=FinsGame();var p=g.Player1;
        var saved=Add(p,"CS2_029");saved.Cost=1;
        Play(g,p,"TIME_706");
        foreach(var next in Branches(g))
        {
            var owner=next.Player1;var fresh=Add(owner,"CS2_024");
            End(next);
            Check(owner.HandZone.Select(e=>e.Id).SequenceEqual(new[]{fresh.Id,saved.Id}) && next.IdEntityDic[saved.Id].Cost==1,"escrow preserves order, identity, cost across clone/load");
        }
        var overflow=FinsGame();p=overflow.Player1;
        var originals=new[]{Add(p,"CS2_029"),Add(p,"CS2_024")};Play(overflow,p,"TIME_706");
        foreach(var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        while(!p.HandZone.IsFull) Add(p,"CS2_182");End(overflow);
        Check(originals.All(e=>e.Zone.Type==Zone.GRAVEYARD) && p.HandZone.Count==10,"restore overflow burns originals and retains new cards");

        var bounced=FinsGame();p=bounced.Player1;Play(bounced,p,"TIME_706");
        var temporary=p.HandZone.First();PlayEntity(bounced,p,temporary);Generic.ReturnToHandBlock(p,(Minion)temporary);
        End(bounced);Check(p.HandZone.Contains(temporary),"spent then bounced starting-hand copy survives cleanup");
    }
    private static void Opening()
    {
        var g=B15GameSetupProof.Opening(new[]{"CS2_029","CS2_024","CS2_023","CS2_231","CS2_182","CS2_171","CS2_189","TIME_706"},
            new[]{"CS2_029","CS2_024","CS2_023","CS2_231","CS2_182","CS2_171","CS2_189"});
        var rejects=new List<int>();
        foreach(var p in new[]{g.Player1,g.Player2})
        {
            rejects.Add(p.Choice.Choices[0]);
            Check(g.Process(ChooseTask.Mulligan(p,p.Choice.Choices.Skip(1).ToList())),"partial mulligan accepted");
        }
        var first=g.Player1.HandZone.Select(c=>c.Card.Id).ToArray();var second=g.Player2.HandZone.Select(c=>c.Card.Id).ToArray();
        Check(second.Contains("GAME_005"),"second player's opening includes Coin");
        g.MainBegin();
        Check(TurnQueueRules.StartingHand(g.Player1).Select(c=>c.Id).SequenceEqual(first) &&
            TurnQueueRules.StartingHand(g.Player2).Select(c=>c.Id).SequenceEqual(second),"post-mulligan order frozen before ordinary draw");
        foreach(var branch in Branches(g))
        {
            End(branch);var p=branch.Player2;
            foreach(var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            Play(branch,p,"TIME_706");
            Check(p.HandZone.Select(c=>c.Card.Id).SequenceEqual(second),"opponent Fins reproduces opening plus Coin");
        }
        var fair=FairBotState.Create(g,g.Player1.PlayerId,73,DeckStyle.Control);
        var view=(Game)typeof(FairBotState).GetField("game",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(fair);
        Check(TurnQueueRules.StartingHand(view.Player2).Length==0 && TurnQueueRules.StartingHand(g.Player2).Length==second.Length,
            "fair bot never receives opponent's opening identities");
        var ineligible=B15GameSetupProof.Kept(new[]{"JAIL_860","CS2_029"}.Concat(Enumerable.Repeat("CS2_231",28)).ToArray());
        Check(ineligible.Player1.AppliedEnchantments?.Any(e=>e.Card.Id=="JAIL_860e")!=true,"Chef checks high-cost original deck card captured before mulligan");
    }
    private static void Pools()
    {
        var g=HeraldProof.New(CardClass.SHAMAN);
        Console.WriteLine("B16 buckets: Tome="+OfflineCardPools.Candidates(g,CardClass.SHAMAN,TomeTranslator.Pool,129236).Count+
            " Whelp-Shaman="+OfflineCardPools.Candidates(g,CardClass.SHAMAN,EmberscarredWhelp.Pool,115656).Count);
        var xml=PinnedPoolFixture.Catalog();var section=xml.Root.Element("Members");
        var row=section.Elements("Card").First(e=>(string)e.Attribute("scope")=="Standard" && (string)e.Attribute("cost")=="1");
        section.RemoveNodes();section.Add(row);xml.Root.Element("Dependencies").RemoveNodes();row.Elements("Dependency").Remove();
        typeof(Game).GetProperty("OfflinePools").SetValue(g,OfflinePoolFixtures.Manifest(xml));
        Play(g,g.Player1,"BE_327t");Check(g.Player1.BoardZone.Count==1,"empty exact summon bucket yields nothing");
        Play(g,g.Player1,"FIR_927");Check(g.Player1.Choice==null,"empty exact Discover does not stall");
        g.Player1.BaseMana=3;End(g);End(g);Check(g.Player1.RemainingMana==5,"empty Discover still schedules Whelp mana");
        Console.WriteLine("B16 empty buckets: Tome=0 Whelp=0");
    }
    private static void Rewind()
    {
        var g=FinsGame();var p=g.Player1;
        var saved=Add(p,"CS2_029");Play(g,p,"TIME_706");TemporaryRules.NextTurnMana(p);
        var coin=Add(p,"GAME_005");coin[GameTag.REWIND]=1;PlayEntity(g,p,coin);
        Check(RewindTestHelpers.IsRewindOffered(g),"active timer and escrow permit Rewind");
        RewindTestHelpers.AnswerRewind(g);RewindTestHelpers.KeepIfOffered(g);
        End(g);Check(g.Player1.HandZone.Any(e=>e.Id==saved.Id),"Rewind preserves original escrow entities");
        g.Player1.BaseMana=3;End(g);Check(g.Player1.RemainingMana==5,"Rewind retains exactly one pending mana grant");
    }
}
