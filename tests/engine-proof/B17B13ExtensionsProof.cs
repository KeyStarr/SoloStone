using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B17B13ExtensionsProof
{
    [GroupProof("b17-b13-extensions")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new (string, Action)[] {
            ("Talya", Unpinned_Talya), ("Ultraxion", Unpinned_Ultraxion),
            ("ChoGall", Unpinned_ChoGall), ("Ido", Unpinned_Ido),
            ("Gator", Gator), ("Deios", Unpinned_Deios),
            ("TalyaSampling", Unpinned_TalyaSampling), ("ChoGallSampling", Unpinned_ChoGallSampling),
            ("DeiosChoices", Unpinned_DeiosChoices), ("Lifetimes", Unpinned_Lifetimes),
            ("Rewind", Unpinned_Rewind), ("OpponentChoice", Unpinned_OpponentChoice),
            ("IdoFairView", Unpinned_IdoFairView) })
            try { test.Item2(); Console.WriteLine("PASS " + test.Item1); }
            catch(Exception e) { failures.Add(test.Item1 + ": " + e.ToString()); }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }
    private static void Check(bool value, string message) => Core2026Proof.Check(value,message);
    private static void Branches(Game game, Action<Game> action)
    {
        var clone = game.CloneExact(true); var loaded = Game.LoadSnapshot(game.SaveSnapshot());
        foreach(var branch in new[] {game,clone,loaded}) action(branch);
        Check(HsSim.Canonical.State(game) == HsSim.Canonical.State(clone)
            && HsSim.Canonical.State(game) == HsSim.Canonical.State(loaded), "identical future state and RNG across clone/load");
    }
    internal static void Unpinned_Talya()
    {
        var g = ImbueProof.Make(CardClass.HUNTER); var p = g.Player1;
        var talya = (Minion)Play(g,p,"MEND_304"); talya.Silence(); Kill(g,talya);
        foreach(var game in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g }) {
            Play(game,game.Player1,"CORE_NEW1_031");
            Core2026Proof.Check(game.Player1.BoardZone.Count == 2,"Talya survives silence/death and gives one bonus");
        }
    }
    internal static void Unpinned_TalyaSampling()
    {
        for(int seed=1;seed<=6;seed++) {
            var g=ImbueProof.Make(CardClass.HUNTER,seed); var p=g.Player1;
            Play(g,p,"MEND_304"); StandardSetsProof.ParkBoard(p);
            p[CompanionRules.Cost]=4;
            p[(GameTag)31071]=Cards.FromId("CS2_182").AssetId;
            p[(GameTag)31072]=Cards.FromId("CS2_182").AssetId;
            p[(GameTag)31073]=Cards.FromId("CS2_231").AssetId;
            var expected=CompanionRules.Roster(p)[g.CloneExact().Random.Next(3)];
            Branches(g, b => {
                Play(b,b.Player1,"CORE_OG_211");
                Check(b.Player1.BoardZone.Count==4 && b.Player1.BoardZone.Last().Card.Id==expected,"three normal slots then seeded weighted extra");
                while(!b.Player1.BoardZone.IsFull) Summon(b.Player1,"CS2_231");
                var rng=b.Random.CaptureState(); int count=b.IdEntityDic.Count;
                CompanionRules.SummonExtras(b.Player1,b.Player1.Hero);
                Check(rng.Equals(b.Random.CaptureState()) && count==b.IdEntityDic.Count,"full board creates no entity or RNG");
            });
        }
    }
    internal static void Unpinned_Ultraxion()
    {
        var g = ImbueProof.Make(CardClass.WARLOCK); var p = g.Player1;
        p[HeraldRules.Progress] = 3; Play(g,p,"CATA_497");
        var deathwing = (Playable)Add(p,"CATA_190h");
        Core2026Proof.Check(deathwing.Cost == 6,"discount snapshots prior Herald count plus one");
        CostRules.RestorePrintedCost(deathwing);
        Core2026Proof.Check(deathwing.Cost == 6,"printed reset preserves player discount");
        Core2026Proof.Check(Add(p.Opponent,"CATA_190h").Cost == 10,"opponent has no discount");
        foreach(var game in new[] {g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(Add(game.Player1,"CATA_190h").Cost == 6,"later cards inherit cloned player discount");
    }
    internal static void Unpinned_ChoGall()
    {
        var g = ImbueProof.Make(CardClass.WARLOCK); var p = g.Player1;
        Play(g,p,"CATA_726"); int before = p.Opponent.DeckZone.Count;
        Core2026Proof.Check(p.BoardZone.Count == 3,"Colossal adds both Arms");
        End(g);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == before-3,"two Arms destroy two deck minions before opponent draw");
        Core2026Proof.Check(p.BoardZone.Count == 3,"redirect preserves friendly neighbors");
        foreach(var arm in p.BoardZone.Where(m=>m.Card.Id=="CATA_726t" || m.Card.Id=="CATA_726t1")) {
            Check(arm.AttackDamage==arm.Card.ATK+HeraldRules.Magnitude(arm),"arm receives exported stat buff");
            arm.Silence(); Check(arm.AttackDamage==arm.Card.ATK,"Silence removes arm stat buff");
        }
    }
    internal static void Unpinned_ChoGallSampling()
    {
        for(int seed=1;seed<=6;seed++) {
            var g=ImbueProof.Make(CardClass.WARLOCK,seed); var p=g.Player1;
            var body=Summon(p,"CATA_726"); var soldier=Summon(p,"CATA_725t");
            foreach(var card in p.Opponent.DeckZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(card));
            foreach(var id in new[]{"CS2_231","CS2_029","CS2_182","EX1_096"}) Entity.FromCard(p.Opponent,Cards.FromId(id),zone:p.Opponent.DeckZone);
            var victims=p.Opponent.DeckZone.OfType<Minion>().ToArray();
            int expected=victims[g.CloneExact().Random.Next(victims.Length)].Id;
            Branches(g,b=> {
                var actor=(Minion)b.IdEntityDic[soldier.Id];
                HeraldRules.ConsumeNeighbor(actor);
                Check(b.Player2.GraveyardZone.Any(x=>x.Id==expected),"seeded physical deck victim, skipping spell");
                foreach(var card in b.Player2.DeckZone.Where(c=>c is Minion).ToArray()) b.Player2.SetasideZone.Add(b.Player2.DeckZone.Remove(card));
                int attack=actor.AttackDamage; var rng=b.Random.CaptureState();
                HeraldRules.ConsumeNeighbor(actor);
                Check(actor.AttackDamage==attack && rng.Equals(b.Random.CaptureState()),"spell-only deck skips destruction, gain and RNG");
            });
        }
    }
    internal static void Unpinned_Ido()
    {
        var g = ImbueProof.Make(CardClass.PALADIN); var p = g.Player1;
        var ido = (Minion)Play(g,p,"TLC_241");
        var spell = p.HandZone.SingleOrDefault(x => x.Card.Id == "TLC_241t");
        Core2026Proof.Check(spell != null,"Ido supplies linked spell");
        PlayEntity(g,p,spell,ido); PlayEntity(g,p,spell,ido);
        Core2026Proof.Check(spell.Zone == p.HandZone && ido.AttackDamage == 6 && ido.HasDivineShield,"same entity retained after two paid casts");
        foreach(var game in new[] {g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            ((Minion)game.IdEntityDic[ido.Id]).Silence(); game.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!game.Player1.HandZone.Any(x => x.Card.Id == "TLC_241t"),"aura loss removes linked spell");
        }
    }
    private static void Unpinned_IdoFairView()
    {
        foreach (int sourcePlayer in new[] { 1, 2 })
        foreach (int viewer in new[] { 1, 2 })
        foreach (bool inDeck in new[] { false, true })
        foreach (string removal in new[] { "death", "silence", "bounce" })
        {
            var game = ImbueProof.Make(CardClass.PALADIN);
            var owner = sourcePlayer == 1 ? game.Player1 : game.Player2;
            var ido = Summon(owner, "TLC_241");
            var spell = owner.HandZone.Single(c => c.Card.Id == "TLC_241t");
            if (inDeck) owner.DeckZone.Add(owner.HandZone.Remove(spell));
            int handCount = owner.HandZone.Count, deckCount = owner.DeckZone.Count;
            byte[] before = game.SaveSnapshot();
            var state = FairBotState.Create(game, viewer, 83, DeckStyle.Value);
            var fair = (Game)typeof(FairBotState).GetField("game",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(state);
            foreach (var branch in new[] { fair, fair.CloneExact() })
            {
                var player = sourcePlayer == 1 ? branch.Player1 : branch.Player2;
                bool hidden = inDeck || viewer != sourcePlayer;
                int linkedId = player.HandZone.Concat(player.DeckZone).Single(c => c[Generic.RetainedSupplier] == ido.Id).Id;
                for (int refresh = 0; refresh < 2; refresh++)
                {
                    branch.DeathProcessingAndAuraUpdate();
                    Check(player.HandZone.Count == handCount && player.DeckZone.Count == deckCount,
                        "fair Ido refresh must not duplicate linked card");
                    Check(hidden ? branch.IdEntityDic[linkedId] is Unknown unknown && unknown.Card.Id == ""
                        : branch.IdEntityDic[linkedId].Card.Id == "TLC_241t", "fair Ido preserves visibility");
                }
                var source = (Minion)branch.IdEntityDic[ido.Id];
                if (removal == "death") Kill(branch, source);
                else if (removal == "silence") source.Silence();
                else Generic.ReturnToHandBlock(player, source);
                branch.DeathProcessingAndAuraUpdate();
                Check(!player.HandZone.Concat(player.DeckZone).Any(c => c.Id == linkedId)
                    && branch.IdEntityDic[linkedId][GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME,
                    "fair Ido aura loss removes linked card after " + removal);
                Check(!hidden || branch.IdEntityDic[linkedId] is Unknown,
                    "fair Ido removal never reveals masked identity");
            }
            Check(before.SequenceEqual(game.SaveSnapshot()), "fair Ido branches preserve authoritative bytes");
        }
    }

    internal static void Gator()
    {
        var g = ImbueProof.Make(CardClass.PRIEST); var p = g.Player1;
        p.Opponent.Hero.Damage = 10; var source = (Minion)Play(g,p,"TLC_250"); Kill(g,source);
        foreach(var game in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            var enemy = game.Player2.Hero;
            enemy.TakeHeal(game.Player1.Hero,3); Core2026Proof.Check(enemy.Damage == 10,"enemy healing blocked after source death");
            game.Player1.Hero.Damage=4; game.Player1.Hero.TakeHeal(game.Player1.Hero,2);
            Check(game.Player1.Hero.Damage==2,"own hero healing unaffected");
            End(game); enemy.TakeHeal(enemy,3); Core2026Proof.Check(enemy.Damage == 10,"block persists during enemy turn");
            End(game); enemy.TakeHeal(enemy,3); Core2026Proof.Check(enemy.Damage == 7,"expires at next owner start");
        }
    }
    internal static void Unpinned_Deios()
    {
        var g = ImbueProof.Make(CardClass.MAGE); var p = g.Player1;
        var deios = (Minion)Play(g,p,"TIME_064"); p.UsedMana=0;
        Core2026Proof.Check(Generic.HeroPower(p,p.Opponent.Hero),"paid hero power accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.UsedMana == 2 && p.NumTimesHeroPowerUsedThisGame == 1,"two effects one paid use");
        deios.Silence(); p.Hero.HeroPower.IsExhausted=false; p.UsedMana=0;
        Core2026Proof.Check(Generic.HeroPower(p,p.Opponent.Hero),"silenced control accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3,"silence removes power repeat");
    }
    internal static void Unpinned_DeiosChoices()
    {
        var g=ImbueProof.Make(CardClass.PRIEST);var p=g.Player1;
        new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,p.Hero.Id).Imbue();
        Play(g,p,"TIME_064"); p.UsedMana=0;
        Check(Generic.HeroPower(p),"Discover Hero Power use");
        Check(p.Choice!=null,"first Discover pending");
        Branches(g,b=> {
            PlayerDecisionProof.Answer(b,b.Player1.Choice.Choices[0]);
            Check(b.Player1.Choice!=null,"second Discover follows first answer");
            PlayerDecisionProof.Answer(b,b.Player1.Choice.Choices[0]);
            Check(b.Player1.Choice==null && b.Player1.NumTimesHeroPowerUsedThisGame==1,"choice repeat finishes once");
        });
    }

    internal static void Unpinned_Lifetimes()
    {
        var g=ImbueProof.Make(CardClass.PALADIN); var p=g.Player1;
        while(!p.HandZone.IsFull) Add(p,"CS2_231");
        var ido=Summon(p,"TLC_241");
        Check(!p.HandZone.Any(c=>c.Card.Id=="TLC_241t"),"full hand defers linked spell");
        p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.First())); g.DeathProcessingAndAuraUpdate();
        var spell=p.HandZone.Single(c=>c.Card.Id=="TLC_241t");
        p.DeckZone.Add(p.HandZone.Remove(spell)); g.DeathProcessingAndAuraUpdate();
        Check(!p.HandZone.Any(c=>c.Card.Id=="TLC_241t"),"shuffle does not duplicate linked spell");
        Branches(g,b=> {
            var source=(Minion)b.IdEntityDic[ido.Id];
            new SabberStoneCore.Tasks.SimpleTasks.ControlTask(EntityType.TARGET).Process(b,b.Player2,b.Player2.Hero,source);
            b.DeathProcessingAndAuraUpdate();
            Check(b.Player2.HandZone.Count(c=>c.Card.Id=="TLC_241t")==1
                && !b.Player1.DeckZone.Any(c=>c.Card.Id=="TLC_241t"),"control change removes old link and supplies new owner");
            Kill(b,source);
            Check(!b.Player2.HandZone.Any(c=>c.Card.Id=="TLC_241t"),"death removes new owner's link");
        });

        g=ImbueProof.Make(CardClass.PALADIN); p=g.Player1;
        ido=Summon(p,"TLC_241"); Summon(p,"EX1_095");
        spell=p.HandZone.Single(c=>c.Card.Id=="TLC_241t");
        while(!p.HandZone.IsFull) Add(p,"CS2_231");
        PlayEntity(g,p,spell,ido);
        Check(spell.Zone==p.SetasideZone && p.HandZone.IsFull,"on-cast draw filling hand defers retained return");
        Branches(g,b=> {
            b.Player1.SetasideZone.Add(b.Player1.HandZone.Remove(b.Player1.HandZone.First()));
            b.DeathProcessingAndAuraUpdate();
            Check(b.IdEntityDic[spell.Id].Zone==b.Player1.HandZone,"pending retained entity returns when space opens across clone/load");
        });

        g=ImbueProof.Make(CardClass.MAGE); p=g.Player1;
        var deios=Summon(p,"TIME_064"); var victim=Summon(p.Opponent,"CS2_231");
        Check(Generic.HeroPower(p,victim) && victim.Zone==p.Opponent.GraveyardZone
            && p.Opponent.Hero.Damage==0,"dead original target fizzles second power body without replacement");
        p.Hero.HeroPower.IsExhausted=false; p.UsedMana=0;
        p.SetasideZone.Add(p.BoardZone.Remove(deios)); g.DeathProcessingAndAuraUpdate();
        Summon(p.Opponent,"TIME_064");
        Check(Generic.HeroPower(p,p.Opponent.Hero) && p.Opponent.Hero.Damage==1,"opponent Deios cannot double own power");

        g=ImbueProof.Make(CardClass.MAGE); p=g.Player1;
        Summon(p,"TIME_064"); int deck=p.DeckZone.Count;
        Play(g,p,"EX1_015");
        Check(p.DeckZone.Count==deck-2,"Deios doubles Battlecry draw");
        var leper=Summon(p,"EX1_029"); Kill(g,leper);
        Check(p.Opponent.Hero.Damage==4,"Deios doubles Deathrattle");
        var soldier=Summon(p,"CATA_725t"); Summon(p,"CS2_231"); Summon(p,"CS2_231");
        int attack=soldier.AttackDamage, amount=HeraldRules.Magnitude(soldier);
        End(g);
        Check(soldier.AttackDamage==attack+2*amount && !p.BoardZone.Any(c=>c.Card.Id=="CS2_231"),"Deios repeats end effect and resamples right neighbor");

        g=ImbueProof.Make(CardClass.DEATHKNIGHT); p=g.Player1;
        ImbueProof.Context(p).Imbue(); Summon(p,"TIME_064");
        Check(!Generic.HeroPower(p) && !RepeatRules.DoublesEndEffect(p.Hero.HeroPower),"passive power excluded");

        g=ImbueProof.Make(CardClass.WARLOCK); p=g.Player1;
        Play(g,p,"CATA_497"); var deathwing=Add(p,"CATA_190h");
        p.HandZone.Remove(deathwing); deathwing.Controller=p.Opponent;
        deathwing[GameTag.CONTROLLER]=p.Opponent.PlayerId; p.Opponent.HandZone.Add(deathwing);
        Check(deathwing.Cost==10,"stolen Deathwing uses recipient discount");
    }

    internal static void Unpinned_Rewind()
    {
        foreach(var id in new[]{"MEND_304","CATA_497","CATA_726","TLC_241","TLC_250","TIME_064"}) {
            var g=ImbueProof.Make(CardClass.WARLOCK); var p=g.Player1;
            var card=Add(p,id); card[GameTag.REWIND]=1;
            PlayEntity(g,p,card);
            Check(g.Rewind!=null,"test grant starts Rewind for "+id);
            Branches(g,b=> {
                PlayerDecisionProof.Answer(b,b.Rewind.RewindEntityId);
                if(b.Rewind!=null) PlayerDecisionProof.Answer(b,b.Rewind.KeepEntityId);
                Check(b.Rewind==null && b.Player1.NumCardsPlayedThisTurn==1,"Rewind settles one accepted play for "+id);
                Check(b.Player1.BoardZone.Count(c=>c.Card.Id==id)==1,"Rewind restores source for "+id);
                if(id=="MEND_304") Check(b.Player1[CompanionRules.ExtraSummons]==1,"Rewind restores bonus count");
                if(id=="CATA_497") Check(Add(b.Player1,"CATA_190h").Cost==9,"Rewind restores discount and Herald count");
                if(id=="TLC_241") Check(b.Player1.HandZone.Count(c=>c.Card.Id=="TLC_241t")==1,"Rewind restores one linked entity");
                if(id=="TLC_250") Check(b.Player1.AppliedEnchantments.Count(e=>e.Card.Id=="TLC_250e")==1,"Rewind restores one expiry grant");
            });
        }
    }

    private static readonly CustomTask OpponentOffer = new CustomTask((g,c,s,t,stack) =>
        new DiscoverTask(DiscoverType.BASIC_TOTEM).Process(g,c.Opponent,c.Opponent.Hero.HeroPower,null));
    private static void Unpinned_OpponentChoice()
    {
        var g=ImbueProof.Make(CardClass.MAGE); var p=g.Player1;
        var definition=p.Hero.HeroPower.Card; var old=definition.Power;
        try {
            definition.Power=new SabberStoneCore.Enchants.Power { PowerTask=OpponentOffer };
            Summon(p,"TIME_064");
            Check(Generic.HeroPower(p,p.Opponent.Hero) && p.Opponent.Choice!=null,"power opens opponent-owned choice");
            PlayerDecisionProof.Answer(g,p.Opponent.Choice.Choices[0]);
            Check(p.Opponent.Choice!=null,"opponent answer resumes original owner's second power body");
            PlayerDecisionProof.Answer(g,p.Opponent.Choice.Choices[0]);
            Check(p.Opponent.Choice==null && p.NumTimesHeroPowerUsedThisGame==1
                && p.Opponent.NumTimesHeroPowerUsedThisGame==0,"opponent choice completes original paid use once");
        }
        finally { definition.Power=old; }
    }

}
