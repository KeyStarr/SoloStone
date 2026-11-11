using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using SabberStoneCore.Model.Decisions;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class PastVarietySpellsProof
{
    internal static readonly string[] Ids = "EX1_248 CS2_045 EX1_251 AT_051 OG_206 ULD_181 DRG_219 UNG_817 DAL_256 ICC_085 ULD_273 NEW1_007 EX1_164 EX1_165 EX1_166 EX1_178 NEW1_008 EX1_573 EX1_155 EX1_277 CS2_025 EX1_294 AT_004 FP1_018 AT_005 CFM_623 UNG_024".Split(' ');
    [GroupProof("pastvar-spells")]
    internal static void Run()
    {
        using var file=File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var zip=new GZipStream(file,CompressionMode.Decompress);
        using var doc=JsonDocument.Parse(zip);
        var export=doc.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c=>c.GetProperty("cardId").GetString());
        PrintedMetadata();
        DamageRegressions();
        var failures=new List<string>();
        foreach(var id in Ids.Concat("AT_005t CS2_045e DAL_256t2 DAL_256ts EX1_155a EX1_155ae EX1_155b EX1_155be EX1_164a EX1_164b EX1_165a EX1_165b EX1_165t1 EX1_165t2 EX1_166a EX1_166b EX1_178a EX1_178ae EX1_178b EX1_178be EX1_573a EX1_573ae EX1_573b EX1_573t EX1_tk11 ICC_085t NEW1_007a NEW1_007b NEW1_008a NEW1_008b OG_044a".Split(' ')).Distinct()) {
            var c=Cards.FromId(id);var d=export[id];
            Core2026Proof.Check(c.Class.ToString()==d.GetProperty("cardClass").GetString() && c.Rarity.ToString()==d.GetProperty("rarity").GetString(),id+" current class and rarity");
            Core2026Proof.Check(c.Name==d.GetProperty("name").GetString() && c.Type.ToString()==d.GetProperty("cardType").GetString()
                && c.AssetId==d.GetProperty("dbfId").GetInt32() && c.Cost==d.GetProperty("cost").GetInt32()
                && c.ATK==d.GetProperty("atk").GetInt32() && c.Health==d.GetProperty("health").GetInt32() && c.Text==d.GetProperty("text").GetString(),id+" exact current identity, text and stats");
            foreach(var tag in d.GetProperty("tags").EnumerateObject())
                if(tag.Name!="1452" && (tag.Name!="1635" || !Ids.Contains(id))) Core2026Proof.Check(c[(GameTag)int.Parse(tag.Name)]==tag.Value.GetInt32(),id+" pinned tag "+tag.Name);
            Core2026Proof.Check(c.Races.Select(r=>r.ToString()).Order().SequenceEqual(d.GetProperty("races").EnumerateArray().Select(r=>r.GetString()).Order()),id+" pinned races");
            int primaryRace=d.GetProperty("tags").TryGetProperty("200",out var race) ? race.GetInt32() : 0;
            Core2026Proof.Check((int)c.GetRawRace()==primaryRace && c[GameTag.CARDRACE]==primaryRace,id+" primary race cache and raw tag");
        }
        foreach(var id in Ids)
        {
            try {
                var c=Cards.FromId(id); var d=export[id];
                Core2026Proof.Check(c.Implemented,id+" implemented");
                Core2026Proof.Check(c.Cost==d.GetProperty("cost").GetInt32() && c.ATK==d.GetProperty("atk").GetInt32() && c.Health==d.GetProperty("health").GetInt32(),id+" current printed stats");
                Scenario(id,export);
                EdgeCases(id,export);
                PoolProofEvidence.Passed(id,"PastVarietySpellsProof.Run:"+id);
                Console.WriteLine("PASS Past variety spells "+id);
            } catch(Exception e) { failures.Add(id+": "+e); Console.WriteLine("FAIL "+id+": "+e.Message); }
        }
        Core2026Proof.Check(failures.Count==0,string.Join("\n",failures));
        Unpinned_PupilCasts(export);
        AlternateReality(export);
        foreach(var pool in new[]{("TIME_013", CardClass.SHAMAN, OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:4,scope:PoolScope.Past)),
            ("TIME_704", CardClass.DRUID, OfflinePoolFilter.Discover(type:CardType.SPELL,minCost:7,scope:PoolScope.Past)),
            ("TIME_707", CardClass.DRUID, OfflinePoolFilter.Random(keyword:GameTag.CHOOSE_ONE,scope:PoolScope.Past)),
            ("TIME_857", CardClass.MAGE, OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:1,scope:PoolScope.Past))})
            Console.WriteLine("BUCKET "+pool.Item1+"="+OfflineCardPools.Candidates(New(),pool.Item2,pool.Item3,0).Count);

    }
    private static Dictionary<string,JsonElement> Export()
    {
        using var file=File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var zip=new GZipStream(file,CompressionMode.Decompress);
        using var doc=JsonDocument.Parse(zip);
        return doc.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c=>c.GetProperty("cardId").GetString(),c=>c.Clone());
    }
    [GroupProof("pastvar-spells-metadata")]
    internal static void PrintedMetadata()
    {
        var export=Export();
        foreach(var id in Ids.Concat("EX1_tk11 ICC_085t".Split(' '))) {
            var c=Cards.FromId(id);var row=export[id];
            int race=row.GetProperty("tags").TryGetProperty("200",out var primary) ? primary.GetInt32() : 0;
            Core2026Proof.Check((int)c.GetRawRace()==race && c[GameTag.CARDRACE]==race,id+" primary Race cache matches current CARDRACE");
            Core2026Proof.Check(c.Races.Select(r=>r.ToString()).Order().SequenceEqual(row.GetProperty("races").EnumerateArray().Select(r=>r.GetString()).Order()),id+" race list cache");
        }
        foreach(var file in Directory.GetFiles("tests/engine-proof/definition-fixtures","*.b64")) {
            var original=File.ReadAllText(file).Trim();
            var card=SnapshotCodec.DecodeDefinition(original);
            Core2026Proof.Check(SnapshotCodec.EncodeDefinition(card)==original,"historical definition bytes unchanged: "+file);
        }
    }
    [GroupProof("pastvar-spells-damage")]
    internal static void DamageRegressions()
    {
        var export=Export();var failures=new List<string>();
        foreach(var id in new[]{"EX1_277","CFM_623","EX1_251","AT_051","ULD_181"})
        foreach(int bonus in new[]{0,1})
        foreach(int multipliers in new[]{0,1,2})
        foreach(int seed in new[]{1,9,21})
        foreach(int seat in new[]{1,2}) {
            try { DamageScenario(id,export,bonus,multipliers,seed,seat); }
            catch(Exception e) { failures.Add(id+" bonus="+bonus+" Velen="+multipliers+" seed="+seed+" seat="+seat+": "+e.Message); }
        }
        Core2026Proof.Check(failures.Count==0,string.Join("\n",failures));
        DamageModifierBoundaries(export);
    }
    private static void DamageScenario(string id,Dictionary<string,JsonElement> export,int bonus,int multipliers,int seed,int seat)
    {
        var g=New(seed);if(seat==2)End(g);var p=g.CurrentPlayer;
        if(bonus>0)Summon(p,"EX1_012");
        for(int i=0;i<multipliers;i++)Summon(p,"EX1_350");
        // Keep modifiers/targets alive: every wave must use the same known damage arithmetic.
        foreach(var m in p.BoardZone)m[GameTag.HEALTH]=100;
        var enemies=Enumerable.Range(0,3).Select(_=>Summon(p.Opponent,"CS2_182")).ToArray();
        foreach(var m in enemies)m[GameTag.HEALTH]=100;
        p.Opponent.Hero.Armor=100;
        g.SetRandomSeed(seed);
        var rng=g.Random.Clone();var expected=new List<(int victim,int amount)>();
        int Scale(int amount)=>(amount+bonus)*(1<<multipliers);
        if(id is "EX1_277" or "CFM_623") {
            var targets=new[]{p.Opponent.Hero.Id}.Concat(enemies.Select(e=>e.Id)).ToArray();
            int missiles=id=="EX1_277" ? Scale(3) : 3;
            int damage=id=="EX1_277" ? 1 : Scale(3);
            for(int i=0;i<missiles;i++)expected.Add((targets[rng.Next(targets.Length)],damage));
        } else if(id=="EX1_251") {
            // Distinct random targets reject duplicate indices without reordering the board.
            var selected=new HashSet<int>();
            while(selected.Count<2) { int pick=rng.Next(enemies.Length);if(selected.Add(pick))expected.Add((enemies[pick].Id,Scale(2))); }
        } else {
            var targets=p.BoardZone.Concat(p.Opponent.BoardZone).ToArray();
            foreach(int wave in id=="ULD_181" ? new[]{5,2} : new[]{4})
                foreach(var target in targets)expected.Add((target.Id,Scale(wave+(id=="AT_051" ? rng.Next(0,2) : 0))));
        }
        foreach(var fork in new[]{g,g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)}) {
            var source=Cast(fork,id,export);
            var actual=fork.HistoryFacts.Query.Damage().Where(d=>d.SourceId==source.Id).Select(d=>(d.VictimId,d.Amount)).ToArray();
            Core2026Proof.Check(actual.SequenceEqual(expected),id+" exact seeded hit order and scaled damage; expected="+string.Join(",",expected)+" actual="+string.Join(",",actual));
            Core2026Proof.Check(fork.Random.DrawCount==rng.DrawCount && fork.Random.Clone().Next()==rng.Clone().Next(),id+" exact RNG consumption and continuation: expected="+rng.DrawCount+" actual="+fork.Random.DrawCount);
            foreach(var target in enemies)Core2026Proof.Check(((Minion)fork.IdEntityDic[target.Id]).Damage==expected.Where(h=>h.victim==target.Id).Sum(h=>h.amount),id+" actual enemy damage");
            Core2026Proof.Check(fork.CurrentPlayer.Opponent.Hero.Armor==100-expected.Where(h=>h.victim==fork.CurrentPlayer.Opponent.Hero.Id).Sum(h=>h.amount),id+" actual hero damage");
            Equal(fork,id+" scaled damage");
            Core2026Proof.Check(fork.Process(ConcedeTask.Any(fork.CurrentPlayer)),id+" Concede remains available");
        }
    }
    private static void DamageModifierBoundaries(Dictionary<string,JsonElement> export)
    {
        foreach(var id in new[]{"EX1_277","CFM_623"})
        foreach(bool bounce in new[]{false,true}) {
            var g=New();var p=g.Player1;var velen=Summon(p,"EX1_350");
            Summon(p.Opponent,"EX1_350");
            if(bounce)Core2026Proof.Check(Generic.ReturnToHandBlock(p,velen),"Velen leaves play");
            else velen.Silence();
            g.DeathProcessingAndAuraUpdate();
            var source=Cast(g,id,export);
            var hits=g.HistoryFacts.Query.Damage().Where(d=>d.SourceId==source.Id).ToArray();
            Core2026Proof.Check(hits.Length==3 && hits.All(d=>d.Amount==(id=="EX1_277" ? 1 : 3)),id+" Silence/leave-play removes owner multiplier; enemy Velen never scales damage");
            Equal(g,id+" modifier removal");
        }
        // Same-class audit: these are the damage minions in the merged legacy group.
        foreach(var id in new[]{"EX1_029","CS2_150","EX1_313","CFM_751"}) {
            var g=New();var p=g.Player1;Summon(p,"EX1_012");Summon(p,"EX1_350");
            var victim=Summon(p.Opponent,"CS2_200");
            var source=Generic.DrawCard(p,Cards.FromId(id));p.BaseMana=10;
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p,source,id=="CS2_150" ? victim : null)),id+" generated minion damage audit play");
            if(id=="EX1_029")Kill(g,(Minion)source);
            var hits=g.HistoryFacts.Query.Damage().Where(d=>d.SourceId==source.Id).ToArray();
            int damage=id switch { "EX1_313"=>5,"CFM_751"=>3,_=>2 };
            Core2026Proof.Check(hits.Length>0 && hits.All(d=>d.Amount==damage),id+" minion damage ignores Spell Damage and Velen");
        }
    }
    private static void Scenario(string id,Dictionary<string,JsonElement> export)
    {
        foreach(int seed in new[]{1,9})
        foreach(int option in Cards.FromId(id).ChooseOne ? new[]{1,2} : new[]{0})
        foreach(bool deckOrigin in Cards.FromId(id).ChooseOne ? new[]{false,true} : new[]{false})
        {
            var g=New(seed); var p=g.Player1;
            var target=Summon(p.Opponent,"CS2_182"); var other=Summon(p.Opponent,"CS2_182"); var friend=Summon(p,"CS2_182");
            p.Hero.Damage=15; p.Opponent.Hero.Damage=10; friend.Damage=1;
            var canonical=Cards.FromId(id); var canonicalTags=canonical.Tags.OrderBy(t=>(int)t.Key).ToArray(); int school=School(export[id]);
            var generated=Definition(g,id,export);
            var card=deckOrigin ? Entity.FromCard(p,generated,zone:p.DeckZone) : Generic.DrawCard(p,generated);
            if(canonical.ChooseOne) CostRules.AddModifier((Playable)card,EffectOperator.SUB,1);
            if(deckOrigin) Core2026Proof.Check(new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,p.Hero.Id).DrawEntity().Id==card.Id,id+" generated deck parent drawn with discount");
            int paidCost=card.Cost;
            p.BaseMana=id=="EX1_164" ? 5 : 10; p.UsedMana=0;
            ICharacter selected=id switch {
                "CS2_045" or "EX1_155"=>friend,
                "NEW1_008" when option==2=>p.Hero,
                "NEW1_007" when option==2=>target,
                "OG_206" or "DRG_219" or "UNG_817" or "ICC_085" or "EX1_166" or "AT_004" or "AT_005"=>target,
                _=>null };
            var forks=new[]{g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)};
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card,selected,chooseOne:option)),id+" generated play accepted");
            if(canonical.ChooseOne) Core2026Proof.Check(p.UsedMana==paidCost && paidCost==canonical.Cost-1,id+" both branches pay the Alternate Reality discount, deck="+deckOrigin);
            foreach(var fork in forks) {
                Core2026Proof.Check(fork.Process(PlayCardTask.Any(fork.Player1,(IPlayable)fork.IdEntityDic[card.Id],selected==null ? null : (ICharacter)fork.IdEntityDic[selected.Id],chooseOne:option)),id+" restored play");
                Core2026Proof.Check(Canonical.State(g)==Canonical.State(fork),id+" identical effect from clone/snapshot");
            }
            if(canonical.Type==CardType.SPELL) Core2026Proof.Check(g.HistoryFacts.Query.Casts().Any(c=>c.EntityId==card.Id && c.SpellSchool==school),id+" current cast school");
            switch(id) {
                case "EX1_248": Core2026Proof.Check(p.BoardZone.Count==3 && p.BoardZone.Count(m=>m.Card.Id=="EX1_tk11" && m.HasTaunt && m.AttackDamage==2 && m.Health==3)==2 && p.OverloadOwed==1,"Feral Spirit wolves and Overload 1"); break;
                case "CS2_045": Core2026Proof.Check(friend.AttackDamage==7,"Rockbiter +3 Attack"); End(g); Core2026Proof.Check(friend.AttackDamage==4,"Rockbiter expiry"); break;
                case "EX1_251": Core2026Proof.Check(target.Damage==2 && other.Damage==2 && p.OverloadOwed==2,"Forked Lightning distinct hits and Overload"); break;
                case "AT_051": Core2026Proof.Check(p.BoardZone.Count==0 && target.Damage>=4 && other.Damage>=4 && p.OverloadOwed==2,"Elemental Destruction damage and Overload 2"); break;
                case "OG_206": Core2026Proof.Check(target.Damage==4 && p.OverloadOwed==1,"Stormcrack damage and Overload"); break;
                case "ULD_181": Core2026Proof.Check(p.BoardZone.Count==0 && p.Opponent.BoardZone.Count==0,"Earthquake two waves"); break;
                case "DRG_219": Core2026Proof.Check(target.Damage==4 && other.Damage==0,"Lightning Breath no Dragon"); break;
                case "UNG_817": Core2026Proof.Check(target.Damage==5 && p.Hero.Damage==10,"Tidal Surge 5 Lifesteal"); break;
                case "DAL_256": Core2026Proof.Check(p.BoardZone.Count==6 && p.BoardZone.Count(m=>m.Card.Id=="DAL_256t2" && m.AttackDamage==2 && m.Health==2)==5 && p.HandZone.Any(c=>c.Card.Id=="DAL_256ts"),"Forest Aid five Treants and Twinspell"); break;
                case "ICC_085": Core2026Proof.Check(target.Damage==5 && p.HandZone.Count==5 && p.Hero.Armor==5 && p.BoardZone.Any(m=>m.Card.Id=="ICC_085t" && m.AttackDamage==5 && m.Health==5),"Infestation four effects"); break;
                case "ULD_273": Core2026Proof.Check(p.Hero.Damage==10 && p.Opponent.Hero.Damage==5 && friend.Damage==0 && p.HandZone.Count==5,"Overflow heal all and draw five"); break;
                case "NEW1_007": Core2026Proof.Check(option==1 ? target.Damage==2 && other.Damage==2 : target.Damage==5 && other.Damage==0,"Starfall both branches"); break;
                case "EX1_164": Core2026Proof.Check(option==1 ? p.BaseMana==7 && p.RemainingMana==7-paidCost : p.HandZone.Count==3,"Nourish both branches"); break;
                case "EX1_165": var form=p.BoardZone.Single(m=>m.Id==card.Id); Core2026Proof.Check(option==1 ? form.AttackDamage==7 && form.Health==6 && form.IsRush && !form.HasCharge : form.AttackDamage==4 && form.Health==9 && form.HasTaunt,"Claw current forms: "+form.Card.Id+" "+form.AttackDamage+"/"+form.Health+" Rush="+form.IsRush+" Charge="+form.HasCharge);
                    new SilenceTask(EntityType.TARGET).Process(g,p,form,form);
                    Core2026Proof.Check(!form.IsRush && !form.HasTaunt && form.AttackDamage==form.Card.ATK && form.Health==form.Card.Health,"Claw Silence removes keywords, preserves transformed printed body");break;
                case "EX1_166": Core2026Proof.Check(option==1 ? target.Damage==2 : target.IsSilenced,"Keeper both branches"); break;
                case "EX1_178": var war=(Minion)card; Core2026Proof.Check(option==1 ? war.AttackDamage==5 && war.Health==10 && war.HasTaunt : war.AttackDamage==10 && war.Health==5,"War both branches"); break;
                case "NEW1_008": Core2026Proof.Check(option==1 ? p.HandZone.Count==2 : p.Hero.Damage==8,"Lore current draw/heal"); break;
                case "EX1_573": Core2026Proof.Check(option==1 ? friend.AttackDamage==6 && (friend.Health+friend.Damage)==7 : p.BoardZone.Count(m=>m.Card.Id=="EX1_573t" && m.HasTaunt)==2,"Cenarius branches"); break;
                case "EX1_155": Core2026Proof.Check(option==1 ? friend.AttackDamage==8 : (friend.Health+friend.Damage)==9 && friend.HasTaunt,"Mark branches"); break;
                case "EX1_277": Core2026Proof.Check(target.Damage+other.Damage+p.Opponent.Hero.Damage==13,"Arcane Missiles total three"); break;
                case "CS2_025": Core2026Proof.Check(target.Damage==1 && other.Damage==1 && friend.Damage==1,"Explosion enemies only"); break;
                case "AT_004": Core2026Proof.Check(target.Damage==2,"Arcane Blast base two"); break;
                case "AT_005": var boar=p.Opponent.BoardZone.Single(m=>m.Id==target.Id); Core2026Proof.Check(boar.Card.Id=="AT_005t" && boar.AttackDamage==4 && boar.Health==2 && boar.HasCharge,"Polymorph Boar"); break;
                case "CFM_623": Core2026Proof.Check(target.Damage+other.Damage+p.Opponent.Hero.Damage==19,"Greater missiles total nine"); break;
                case "EX1_294": case "FP1_018": case "UNG_024":
                    Core2026Proof.Check(p.SecretZone.Count==1,"Secret installed"); End(g);
                    if(id=="EX1_294") { Play(g,p.Opponent,"CS2_182"); Core2026Proof.Check(p.BoardZone.Count==2 && p.SecretZone.Count==0,"Mirror copies enemy played minion"); }
                    if(id=="FP1_018") { Kill(g,friend); Core2026Proof.Check(p.HandZone.Count==2 && p.HandZone.All(c=>c.Card.Id=="CS2_182") && p.SecretZone.Count==0,"Duplicate two copies"); }
                    if(id=="UNG_024") { Play(g,p.Opponent,"CS2_025"); Core2026Proof.Check(p.HandZone.Count==1 && p.HandZone[0].Card.Id=="CS2_025" && p.HandZone[0].Cost==0 && p.SecretZone.Count==0,"Mana Bind zero cost copy"); }
                    break;
            }
            Core2026Proof.Check(canonicalTags.SequenceEqual(canonical.Tags.OrderBy(t=>(int)t.Key)),id+" generated play leaves canonical legacy metadata unchanged");
            Core2026Proof.Check(Canonical.State(g)==Canonical.State(g.CloneExact(history:true)),id+" clone");
            Core2026Proof.Check(Canonical.State(g)==Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)),id+" snapshot");
        }
    }
    private static Game New(int seed=11)
    {
        var game=PastLegacyQualificationProof.New(seed);
        var catalog=Environment.GetEnvironmentVariable("HS_PASTVAR_SPELLS_CATALOG");
        if(!string.IsNullOrEmpty(catalog)) game.RestoreOfflinePools(OfflinePoolPolicy.Parse(XDocument.Load(catalog)).BuildManifest());
        return game;
    }
    private static int School(JsonElement card)=>card.GetProperty("tags").EnumerateObject().Where(t=>t.Name=="1635").Select(t=>t.Value.GetInt32()).FirstOrDefault();
    private static Card Definition(Game g,string id,Dictionary<string,JsonElement> export)
    {
        var legacy=Cards.FromId(id); var generated=legacy.Clone();
        if(legacy.Type==CardType.SPELL && legacy[GameTag.SPELL_SCHOOL]!=School(export[id])) generated.Tags[GameTag.SPELL_SCHOOL]=School(export[id]);
        var member=OfflineCardPools.Candidates(g,CardClass.DRUID,OfflinePoolFilter.Random(scope:PoolScope.Past),0).SingleOrDefault(c=>c.Id==id);
        if(member!=null) {
            Core2026Proof.Check(member.Tags.OrderBy(t=>(int)t.Key).SequenceEqual(generated.Tags.OrderBy(t=>(int)t.Key)),id+" manifest generated definition");
            generated=member;
        }
        return generated;
    }
    private static IPlayable Cast(Game g,string id,Dictionary<string,JsonElement> export,ICharacter target=null,int option=0)
    {
        var p=g.CurrentPlayer; p.BaseMana=10;p.UsedMana=0;
        var card=Generic.DrawCard(p,Definition(g,id,export));
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card,target,chooseOne:option)),id+" edge play"); return card;
    }
    private static void Equal(Game g,string label)
    {
        Core2026Proof.Check(Canonical.State(g)==Canonical.State(g.CloneExact(history:true)),label+" clone");
        Core2026Proof.Check(Canonical.State(g)==Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)),label+" snapshot");
    }
    private static void EdgeCases(string id,Dictionary<string,JsonElement> export)
    {
        var g=New();var p=g.Player1;
        if(id=="EX1_251") {
            var outcomes=new List<string>();
            foreach(int seed in new[]{1,9,21}) {
                var trial=New(seed);var enemies=Enumerable.Range(0,4).Select(_=>Summon(trial.Player2,"CS2_182")).ToArray();
                var friend=Summon(trial.Player1,"CS2_182");long before=trial.Random.DrawCount;
                Cast(trial,id,export);
                Core2026Proof.Check(enemies.Count(m=>m.Damage==2)==2 && enemies.Count(m=>m.Damage==0)==2 && friend.Damage==0 && trial.Random.DrawCount>before,"seeded Forked Lightning chooses two distinct enemies from four");
                outcomes.Add(string.Join(",",enemies.Select(m=>m.Damage)));
            }
            Core2026Proof.Check(outcomes.Distinct().Count()>1,"Forked Lightning seed/order-sensitive selection, not always first pair");
        }
        if(id=="DRG_219") {
            var left=Summon(p.Opponent,"CS2_182");var middle=Summon(p.Opponent,"CS2_182");var right=Summon(p.Opponent,"CS2_182");
            Add(p,"EX1_563");Cast(g,id,export,middle);
            Core2026Proof.Check(new[]{left,middle,right}.All(m=>m.Damage==4),"Dragon Breath hits target and both neighbors");
        }
        if(id=="AT_004" || id=="CFM_623" || id=="EX1_277" || id=="NEW1_007") {
            Summon(p,"EX1_012");var target=Summon(p.Opponent,"CS2_182");var other=Summon(p.Opponent,"CS2_182");
            Cast(g,id,export,id=="AT_004" || id=="NEW1_007" ? target : null,id=="NEW1_007" ? 2 : 0);
            Core2026Proof.Check(id switch { "AT_004"=>target.Damage==4,"CFM_623"=>target.Damage+other.Damage+p.Opponent.Hero.Damage==12,
                "EX1_277"=>target.Damage+other.Damage+p.Opponent.Hero.Damage==4,_=>target.Damage==6 },id+" Spell Damage applied correctly");
        }
        if(id=="AT_004") {
            var multiplied=New();var owner=multiplied.Player1;Summon(owner,"EX1_012");Summon(owner,"EX1_350");
            var victim=Summon(owner.Opponent,"CS2_200");Cast(multiplied,id,export,victim);
            Core2026Proof.Check(victim.Damage==8,"Arcane Blast double Spell Damage bonus precedes Velen damage multiplier");
        }
        if(id=="ULD_181") {
            var golem=Summon(p.Opponent,"EX1_556");Cast(g,id,export);
            Core2026Proof.Check(p.Opponent.BoardZone.Count==0 && p.Opponent.GraveyardZone.Any(m=>m.Card.Id=="skele21"),"Earthquake second wave kills first-wave Deathrattle summon");
        }
        if(id=="UNG_817") {
            p.Hero.Damage=20;var target=Summon(p.Opponent,"GVG_058");Cast(g,id,export,target);
            Core2026Proof.Check(p.Hero.Damage==20 && !target.HasDivineShield && target.Damage==0,"Tidal Surge prevented damage heals zero");
        }
        if(id=="DAL_256" || id=="EX1_248") {
            for(int i=0;i<6;i++) Summon(p,"CS2_182");
            Cast(g,id,export);Core2026Proof.Check(p.BoardZone.Count==7,"summons fill only available slot");
            var extra=Generic.DrawCard(p,Definition(g,id,export));p.UsedMana=0;
            Core2026Proof.Check(!p.Options().Any(t=>t.Source?.Id==extra.Id),"full board summon spell not playable");
            if(id=="DAL_256") {
                foreach(var m in p.BoardZone.ToArray()) Kill(g,m);
                var twin=p.HandZone.Single(c=>c.Card.Id=="DAL_256ts");p.UsedMana=0;
                Core2026Proof.Check(g.Process(PlayCardTask.Any(p,twin)) && p.BoardZone.Count==5 && p.HandZone.All(c=>c.Card.Id!="DAL_256ts"),"Twinspell copy summons and does not replicate");
            }
        }
        if(id=="EX1_155" || id=="EX1_178") {
            var target=id=="EX1_155" ? Summon(p,"CS2_182") : null;
            var card=Cast(g,id,export,target,1);var affected=target??(Minion)card;
            foreach(var fork in new[]{g,g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)}) {
                var m=(Minion)fork.IdEntityDic[affected.Id];
                new SilenceTask(EntityType.TARGET).Process(fork,fork.Player1,m,m);
                Core2026Proof.Check(!m.HasTaunt && m.AttackDamage==m.Card.ATK && m.Health==m.Card.Health,"Choose One buff removed by Silence");
            }
        }
        if(id=="EX1_294" || id=="FP1_018" || id=="UNG_024") {
            var source=Cast(g,id,export);var friend=Summon(p,"CS2_182");
            if(id=="EX1_294")Play(g,p,"CS2_182");
            if(id=="UNG_024")Play(g,p,"CS2_025");
            if(id=="FP1_018")Kill(g,friend);
            Core2026Proof.Check(p.SecretZone.Count==1,"secrets do not trigger on own turn");
            End(g);
            foreach(var fork in new[]{g,g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)}) {
                var owner=fork.Player1;
                if(id=="EX1_294") Play(fork,owner.Opponent,"CS2_182");
                if(id=="UNG_024") Play(fork,owner.Opponent,"CS2_025");
                if(id=="FP1_018") { var m=Summon(owner,"CS2_182");Kill(fork,m); }
                Core2026Proof.Check(owner.SecretZone.Count==0,"secret resumes after clone/snapshot");
            }
        }
        if(id=="EX1_294" || id=="FP1_018" || id=="UNG_024") {
            var full=New();var owner=full.Player1;Cast(full,id,export);
            if(id=="EX1_294")for(int i=0;i<7;i++)Summon(owner,"CS2_182");
            else while(owner.HandZone.Count<10)Add(owner,"CS2_182");
            End(full);
            void Trigger() {
                if(id=="EX1_294")Play(full,owner.Opponent,"CS2_182");
                if(id=="FP1_018")Kill(full,Summon(owner,"CS2_182"));
                if(id=="UNG_024")Play(full,owner.Opponent,"CS2_025");
            }
            Trigger();Core2026Proof.Check(owner.SecretZone.Count==1,id+" full destination does not consume secret");
            if(id=="EX1_294")owner.SetasideZone.Add(owner.BoardZone.Remove(owner.BoardZone[0]));
            else owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
            Trigger();Core2026Proof.Check(owner.SecretZone.Count==0 && (id=="EX1_294" ? owner.BoardZone.Count==7 : owner.HandZone.Count==10),id+" one available slot resolves and consumes once");
            Equal(full,id+" capacity");
        }
        Equal(g,id+" edge");
    }
    private static void Unpinned_PupilCasts(Dictionary<string,JsonElement> export)
    {
        foreach(var id in new[]{"DAL_256","ICC_085","ULD_273"}) {
            var g=New(); var p=g.Player1;IPlayable pupil=null;
            if(g.OfflinePools.Past.Any(c=>c.CardId==id)) {
                for(int seed=1;seed<=80 && pupil==null;seed++) {
                    g=New(seed);p=g.Player1;Play(g,p,"TIME_704");
                    var offered=p.Choice.Choices.Select(i=>(IPlayable)g.IdEntityDic[i]).FirstOrDefault(c=>c.Card.Id==id);
                    if(offered==null)continue;
                    var decision=g.GetPendingDecisions().Single();
                    Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{offered.Id}),out var error),"Mentor teaches candidate: "+error);
                    pupil=p.HandZone.Single();
                    Core2026Proof.Check(pupil.Card.Id=="TIME_704t" && PayloadRules.Entries(pupil,1).Single().Card.Id==id,"real Mentor stores chosen spell only on Pupil");
                }
                Core2026Proof.Check(pupil!=null,id+" available in real Mentor offers");
            } else {
                pupil=Add(p,"TIME_704t"); PayloadRules.StoreCard(pupil,1,Definition(g,id,export));
                pupil[GameTag.TAG_SCRIPT_DATA_NUM_1]=Cards.FromId(id).AssetId;
            }
            p.Hero.Damage=15;p.Opponent.Hero.Damage=15;int beforeBoard=p.BoardZone.Count;
            foreach(var fork in new[]{g,g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)}) {
                var owner=fork.Player1; var student=(IPlayable)fork.IdEntityDic[pupil.Id];
                Core2026Proof.Check(student.Card.ATK==2 && student.Card.Health==2 && PayloadRules.Entries(student,1).Single().Card.Id==id,"2/2 Pupil-local payload");
                PlayEntity(fork,owner,student);
                Core2026Proof.Check(fork.HistoryFacts.Query.Casts().Any(c=>c.DbfId==Cards.FromId(id).AssetId && c.SpellSchool==School(export[id])),id+" Pupil casts current school");
                Core2026Proof.Check(id switch { "DAL_256"=>owner.BoardZone.Count==beforeBoard+6 && owner.HandZone.Any(c=>c.Card.Id=="DAL_256ts"),
                    "ICC_085"=>owner.Hero.Armor==5 && owner.HandZone.Count==5 && owner.BoardZone.Any(m=>m.Card.Id=="ICC_085t"),
                    _=>owner.Hero.Damage==10 && owner.Opponent.Hero.Damage==10 && owner.HandZone.Count==5 },id+" Pupil actual effect");
                Equal(fork,id+" Pupil");
            }
        }
    }
    private static void AlternateReality(Dictionary<string,JsonElement> export)
    {
        foreach(bool second in new[]{false,true}) {
            var g=New(8);if(second)End(g);var p=g.CurrentPlayer;
            foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
            foreach(var id in Ids.Where(i=>Cards.FromId(i).ChooseOne)) {
                Generic.DrawCard(p,Definition(g,id,export));
                Entity.FromCard(p,Definition(g,id,export),zone:p.DeckZone);
            }
            foreach(var viewer in new[]{p,p.Opponent}) {
                var fair=FairBotState.Create(g,viewer.PlayerId,73,DeckStyle.Control);
                Core2026Proof.Check(fair.Facts.Opponent.Hand.All(c=>c.Unknown && c.CardId==null),"opponent fair hand has no identity");
                var hidden=g.IdEntityDic.Values.Where(c=>c.Zone==c.Controller.DeckZone || c.Controller!=viewer && c.Zone==c.Controller.HandZone).Select(c=>c.Id).ToHashSet();
                foreach(var c in g.IdEntityDic.Values.Where(c=>hidden.Contains(c[GameTag.PARENT_CARD])))hidden.Add(c.Id);
                Core2026Proof.Check(hidden.All(i=>!fair.FingerprintGame.IdEntityDic.TryGetValue(i,out var actor) || actor is Unknown),"bot simulation scrubs hidden parents and branches");
            }
            Cast(g,"TIME_707",export);
            Core2026Proof.Check(p.HandZone.Count==8 && p.HandZone.Concat(p.DeckZone).All(c=>c.ChooseOne && c.Cost==Math.Max(0,c.Card.Cost-1)),"Alternate Reality replaces both zones with discounted Choose One cards");
            Equal(g,"Alternate Reality");
            var held=p.HandZone.Concat(p.DeckZone).Select(c=>(c.Id,c.Card.Id,c.Cost)).ToArray();
            p.BaseMana=10;p.UsedMana=0;
            var retry=Generic.DrawCard(p,Cards.FromId("TIME_001"));
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p,retry)) && g.Rewind!=null,"Rewind begins with hidden Past cards");
            var decision=g.GetPendingDecisions().Single();
            Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{g.Rewind.RewindEntityId}),out var error),"Rewind hidden Past state: "+error);
            if(g.Rewind!=null) { decision=g.GetPendingDecisions().Single();Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{g.Rewind.KeepEntityId}),out error),"keep retry"); }
            p=g.CurrentPlayer;
            Core2026Proof.Check(p.HandZone.Concat(p.DeckZone).Select(c=>(c.Id,c.Card.Id,c.Cost)).SequenceEqual(held),"Rewind preserves generated definitions, zones and discounts");
            Equal(g,"rewound Past definitions");
        }
    }

}
