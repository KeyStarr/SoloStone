using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using static Core2026Proof;

internal static class B16OldGodProof
{
    internal static void Activate(Game g, Minion god) {
        Core2026Proof.Check(g.Process(new ActivateOldGodTask(god.Controller,god)),"Old God action accepted");
    }
    internal static void Pick(Game g, string id) {
        int chosen = g.CurrentPlayer.Choice.Choices.Single(e => g.IdEntityDic[e].Card.Id == id);
        CoreCostProof.Answer(g,chosen);
    }
    internal static void Use(Game g, Minion god, string id) { Activate(g,god); Pick(g,id); }
    [GroupProof("b16-old-gods")]
    internal static void Run()
    {
        Unpinned_CopiedAttachedImmunity();
        Unpinned_MinionsOwnTheirAbilities();
        Pinned_SummonTurnBudget();
        Unpinned_CloneSnapshotLifecycle();
        Unpinned_CthunLiveTripling();
        Unpinned_SeededMissiles();
        Unpinned_SeededPoolsAndResurrection();
        Unpinned_CopyTransformRewind();
        Unpinned_TransformCopyRetainsUsedAbilities();
        Unpinned_PoolsAndCapacity();
        Unpinned_ExplicitOldGodPoolOnly();
        Unpinned_TargetAvailabilityAndSilence();
        Console.WriteLine("PASS Old God: summon turn, per-instance budget, targets, clone/load, lifecycle, tripling, seeded damage");
    }
    private static void Unpinned_CopiedAttachedImmunity()
    {
        foreach (string path in new[] { "Faceless", "Molten Reflection", "Generic", "Generic preserved" })
        foreach (bool opponent in new[] { false, true }) {
            // Enemy Immune minions cannot be targeted by Faceless or Molten Reflection.
            if (opponent && (path == "Faceless" || path == "Molten Reflection")) continue;
            var g = CthunRulesProof.Fixture(); var p = g.Player1;
            foreach (string prior in new[] { "CS2_222", "CS2_222", "CS2_200" }) {
                Play(g,p,prior); StandardSetsProof.ParkBoard(p);
            }
            var god = (Minion)Play(g,p,"BE_784");
            Core2026Proof.Check(god.IsImmune,"Y'Shaarj earned immunity through real Mana spending");
            if (opponent) End(g);
            PoolPilotProof.Resume(g,copy => {
                var owner = copy.CurrentPlayer; var original = (Minion)copy.IdEntityDic[god.Id];
                if (path == "Faceless") Play(copy,owner,"EX1_564",original);
                else if (path == "Molten Reflection") Play(copy,owner,"UNG_948",original);
                else Generic.Copy(owner,owner.Hero,original,Zone.PLAY,preserveBoardMinionState:path == "Generic preserved");
                copy.DeathProcessingAndAuraUpdate();
                var body = owner.BoardZone.Single(m => m.Id != god.Id);
                new DamageTask(3,EntityType.TARGET).Process(copy,owner,owner.Hero,body);
                copy.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(body.Id != original.Id && body.IsImmune && body.Damage == 0,
                    path + " copied attached immunity prevents damage");
            });
            int copiedId = g.CurrentPlayer.BoardZone.Single(m => m.Id != god.Id).Id;
            var silenced = g.CloneExact(history:true);
            ((Minion)silenced.IdEntityDic[copiedId]).Silence(); silenced.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!((Minion)silenced.IdEntityDic[copiedId]).IsImmune && ((Minion)silenced.IdEntityDic[god.Id]).IsImmune,
                path + " Silence removes only copied immunity");
            var bounced = g.CloneExact(history:true); var returned = (Minion)bounced.IdEntityDic[copiedId];
            new ReturnHandTask(EntityType.TARGET).Process(bounced,returned.Controller,returned,returned);
            bounced.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!returned.IsImmune && returned.Zone == returned.Controller.HandZone,
                path + " leaving play removes copied immunity");
            PoolPilotProof.Resume(g,copy => {
                var body = (Minion)copy.IdEntityDic[copiedId];
                End(copy); Core2026Proof.Check(body.IsImmune,path + " copy persists through opponent turn");
                End(copy); Core2026Proof.Check(!body.IsImmune,path + " copy expires at its next owner turn");
                new DamageTask(3,EntityType.TARGET).Process(copy,body.Controller,body.Controller.Hero,body);
                copy.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(body.Damage == 3,path + " expired copy takes damage");
            });
        }
    }

    private static void Pinned_SummonTurnBudget()
    {
        foreach (string id in new[] { "BE_013","BE_017","BE_784" }) {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,id);
            Core2026Proof.Check(p.Options().OfType<ActivateOldGodTask>().Count(t => t.Source.Id == god.Id) == 1,"summon-turn Old God activation must be offered");
            Core2026Proof.Check(!god.CanAttack && !p.Options().OfType<MinionAttackTask>().Any(),"unused abilities block ordinary attack");
            var first = id == "BE_013" ? "BE_013t2" : id == "BE_017" ? "BE_017t5" : "BE_784t3";
            var second = id == "BE_013" ? "BE_013t4" : id == "BE_017" ? "BE_017t3" : "BE_784t4";
            Use(g,god,first);
            Core2026Proof.Check(god.NumAttacksThisTurn == 0 && OldGodRules.CanActivate(p,god),"first ability free despite summon sickness");
            Activate(g,god);
            Core2026Proof.Check(!p.Choice.Choices.Any(e => g.IdEntityDic[e].Card.Id == first),"used ability not offered twice");
            Pick(g,second);
            if (p.Choice != null) CoreCostProof.Answer(g,p.Choice.Choices[0]);
            Core2026Proof.Check(god.NumAttacksThisTurn == 1 && !OldGodRules.CanActivate(p,god) && !god.CanAttack,"second ability spends normal attack");
            Core2026Proof.Check(OldGodRules.UsedAbilities(Summon(p,id)) == 0,"second body has independent mask");
        }
        foreach (int limit in new[] { 1,2,4 }) {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
            if (limit == 2) god.HasWindfury = true;
            if (limit == 4) god[GameTag.MEGA_WINDFURY] = 1;
            var catalog = new[] {"BE_013t2","BE_013t4","BE_013t6","BE_013t1"};
            int count = Math.Min(4,limit + 1);
            for (int i = 0; i < count; i++) Use(g,god,catalog[i]);
            Core2026Proof.Check(god.NumAttacksThisTurn == count - 1,"abilities consume only post-free attack allowance");
            Core2026Proof.Check(OldGodRules.CanActivate(p,god) == false,"budget exhausted or catalog empty");
            Core2026Proof.Check(god.CanAttack == (limit == 4),"Mega-Windfury permits four abilities plus normal attack");
            if (limit == 2) {
                End(g); End(g); Use(g,god,"BE_013t1");
                Core2026Proof.Check(god.CanAttack && god.NumAttacksThisTurn == 0,"one remaining ability is free then can attack");
            }
        }
    }
    private static void Unpinned_CloneSnapshotLifecycle()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
        var enemy = Summon(p.Opponent,"CS2_200"); enemy[GameTag.HEALTH] = 20;
        var stale = new ActivateOldGodTask(p,god);
        Activate(g,god);
        PoolPilotProof.Resume(g,copy => Pick(copy,"BE_013t6"));
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { enemy.Id }),"on-board Glare targets enemy minion only");
        PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,enemy.Id));
        Core2026Proof.Check(enemy.Damage == 5 && OldGodRules.UsedAbilities(god) == 4,"Glare resolves once using saved actor and mask");
        var before = g.SaveSnapshot();
        Core2026Proof.Check(!g.Process(stale) && before.SequenceEqual(g.SaveSnapshot()),"stale action rejected before state/RNG/history change");
        god.IsFrozen = true; Core2026Proof.Check(!OldGodRules.CanActivate(p,god),"Frozen disables activation"); god.IsFrozen = false;
        End(g); Core2026Proof.Check(!OldGodRules.CanActivate(p,god),"opponent turn cannot activate body"); End(g);
        god.Silence(); Core2026Proof.Check(!OldGodRules.CanActivate(p,god) && CthunRules.Progress(p) == 4,"Silence disables body but retains controller ledger");
        new ReturnHandTask(EntityType.TARGET).Process(g,p,god,god);
        Core2026Proof.Check(OldGodRules.UsedAbilities(god) == 0,"backward movement resets local ability mask");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p,god)),"replay bounced Old God");
        Core2026Proof.Check(OldGodRules.CanActivate(p,god),"fresh residence has all abilities and summon-turn access");
    }
    private static readonly SuspendableTask External = CthunRules.ExternalAbility("proof/b16/external/v1");
    private static void Unpinned_CthunLiveTripling()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
        foreach (string id in new[] {"BE_013t1","BE_013t2","BE_013t3"}) {
            External.Process(g,p,god,null,new TaskStack()); Pick(g,id);
        }
        Core2026Proof.Check(CthunRules.Progress(p) == 7,"three external abilities prepare live boundary");
        Use(g,god,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Where(m => m.Card.Id == "BE_013t5").All(m => m.AttackDamage == 2 && m.Health == 3),"fourth ability snapshots base before recording own bit");
        Core2026Proof.Check(CthunRules.Progress(p) == 15,"fourth on-board ability records ledger");
        var a = p.BoardZone.First(m => m.Card.Id == "BE_013t5");
        Use(g,god,"BE_013t2"); Core2026Proof.Check(a.AttackDamage == 8 && a.Health == 12,"next activation reads live tripled Domain");
        a.Silence(); Core2026Proof.Check(a.AttackDamage == 2 && a.Health == 3,"tripled buff removable through normal layer");
        End(g); End(g);
        External.Process(g,p,god,null,new TaskStack()); Pick(g,"BE_013t1");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 14,"external effects remain base after tripling");
        StandardSetsProof.ParkBoard(p);
        var fresh = Summon(p,"BE_013"); Use(g,fresh,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Where(m => m.Card.Id == "BE_013t5").All(m => m.AttackDamage == 6 && m.Health == 9),"new body reads controller history, summons tripled Fanatics");
        while(p.BoardZone.Count < 7) Summon(p,"CS2_182");
        Core2026Proof.Check(OldGodRules.UsedAbilities(fresh) == 8 && CthunRules.Progress(p.Opponent) == 0,"mask and ledger isolated");
        var target = Summon(p.Opponent,"CS2_200"); target[GameTag.HEALTH] = 50; p.Hero.Damage = 20;
        Activate(g,fresh); Pick(g,"BE_013t6");
        PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,target.Id));
        Core2026Proof.Check(target.Damage == 15 && p.Hero.Damage == 5,"tripled on-board Glare deals fifteen with Lifesteal");
        End(g); End(g); StandardSetsProof.ParkBoard(p.Opponent); p.Opponent.Hero.Armor = 100;
        Use(g,fresh,"BE_013t1"); Core2026Proof.Check(p.Opponent.Hero.Armor == 79,"tripled Wrath spends twenty-one one-damage missiles");
    }
    private static void Unpinned_SeededMissiles()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
        var a = Summon(p.Opponent,"CS2_200"); var b = Summon(p.Opponent,"CS2_200");
        a[GameTag.HEALTH] = b[GameTag.HEALTH] = 50;
        g.SetRandomSeed(19);
        Activate(g,god);
        PoolPilotProof.Resume(g,copy => Pick(copy,"BE_013t1"));
        Console.WriteLine($"B16 seeded missile vector {a.Damage},{b.Damage},{p.Opponent.Hero.Damage}; draws={g.Random.DrawCount}");
        Core2026Proof.Check(a.Damage == 2 && b.Damage == 1 && p.Opponent.Hero.Damage == 4 && g.Random.DrawCount == 7,
            "seeded missiles choose from ordered current enemies rather than always first");
    }
    private static void Unpinned_CopyTransformRewind()
    {
        foreach(bool preserve in new[] { false,true }) {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
            Use(g,god,"BE_013t2");
            Use(g,god,"BE_013t4");
            var copy = (Minion)Generic.Copy(p,god,god,Zone.PLAY,preserveBoardMinionState:preserve);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(OldGodRules.UsedAbilities(copy) == 10,"board copy retains used-ability mask");
            Use(g,copy,"BE_013t6");
            Core2026Proof.Check(copy.NumAttacksThisTurn == 0,"new copied identity has its own free activation");
            Generic.ChangeEntityBlock(p,god,Cards.FromId("BE_013"),true); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(OldGodRules.UsedAbilities(god) == 0,"same-definition transform resets old ability state");
        }
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
            Use(g,god,"BE_013t2");
            new SummonCopyTask(EntityType.TARGET).Process(g,p,god,god); g.DeathProcessingAndAuraUpdate();
            var copy = p.BoardZone.Single(m => m.Id != god.Id);
            Core2026Proof.Check(OldGodRules.UsedAbilities(copy) == 2,"legacy summon-copy retains used mask");
            Use(g,copy,"BE_013t4"); Core2026Proof.Check(copy.NumAttacksThisTurn == 0,"legacy summon-copy has fresh free budget");
        }
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
            Use(g,god,"BE_013t2"); Play(g,p,"TIME_001");
            RewindTestHelpers.AnswerRewind(g);
            var restored = (Minion)g.IdEntityDic[god.Id];
            Core2026Proof.Check(OldGodRules.UsedAbilities(restored) == 2,"Rewind preserves used ability mask");
            Use(g,restored,"BE_013t4");
            Core2026Proof.Check(restored.NumAttacksThisTurn == 1,"Rewind preserves spent free activation");
        }
    }

    private static void Unpinned_SeededPoolsAndResurrection()
    {
        // Fixed admitted buckets keep the order oracle independent of later admission growth.
        string[] discover = { "CS3_024", "CORE_EX1_110", "CORE_AT_123", "CORE_GVG_114" };
        string[] spawn = { "CORE_LOOT_137", "CORE_UNG_848", "CORE_SW_068", "CORE_YOP_034" };
        foreach (int seed in new[] { 19, 23 }) {
            foreach (bool nzoth in new[] { true, false }) {
                var g = CthunRulesProof.Fixture(); var p = g.Player1;
                var god = Summon(p,nzoth ? "BE_017" : "BE_784");
                var ids = nzoth ? discover : spawn;
                var xml = OfflinePoolFixtures.FixtureCatalog(ids); var rows = xml.Root.Element("Members");
                rows.ReplaceNodes(rows.Elements("Card").Where(r => ids.Contains((string)r.Attribute("cardId"))).ToArray());
                typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g,OfflinePoolFixtures.Manifest(xml));
                g.SetRandomSeed(seed); Activate(g,god);
                PoolPilotProof.Resume(g,copy => Pick(copy,nzoth ? "BE_017t3" : "BE_784t2"));
                if (nzoth) {
                    var expected = seed == 19
                        ? new[] { "CORE_GVG_114", "CS3_024", "CORE_EX1_110" }
                        : new[] { "CORE_AT_123", "CS3_024", "CORE_GVG_114" };
                    Core2026Proof.Check(p.Choice.Choices.Select(e => g.IdEntityDic[e].Card.Id).SequenceEqual(expected),
                        "seeded Call samples three distinct offers in order");
                    int chosen = p.Choice.Choices[1];
                    PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,chosen));
                    Core2026Proof.Check(p.BoardZone.Last().Card.Id == "CS3_024" && p.HandZone.Count == 0 && g.Random.DrawCount == 3,
                        "Call summons selected offer without a second roll or hand entry");
                } else {
                    string expected = seed == 19 ? "CORE_YOP_034" : "CORE_SW_068";
                    Core2026Proof.Check(p.BoardZone.Last().Card.Id == expected && p.HandZone.Single().Card.Id == expected
                        && g.Random.DrawCount == 1,"Spawn shares one seeded identity between board and hand");
                }
            }
            var armyGame = CthunRulesProof.Fixture(); var owner = armyGame.Player1;
            var nzothBody = Summon(owner,"BE_017");
            foreach (string id in discover.Concat(new[] { "EDR_110", "CORE_EX1_110" }).Reverse()) {
                var dead = Summon(owner,id); dead.Silence(); Kill(armyGame,dead);
            }
            armyGame.SetRandomSeed(seed); Activate(armyGame,nzothBody);
            PoolPilotProof.Resume(armyGame,copy => Pick(copy,"BE_017t5"));
            var expectedArmy = seed == 19
                ? new[] { "CORE_GVG_114", "CORE_AT_123", "CORE_EX1_110", "CS3_024" }
                : new[] { "EDR_110", "CORE_EX1_110", "CORE_GVG_114", "CORE_AT_123" };
            Core2026Proof.Check(owner.BoardZone.Where(m => m.Id != nzothBody.Id).Select(m => m.Card.Id).SequenceEqual(expectedArmy)
                && armyGame.Random.DrawCount == 4,"Army samples four distinct printed identities in seeded order, not death occurrence order");
        }
    }

    private static void Unpinned_TransformCopyRetainsUsedAbilities()
    {
        foreach (bool allUsed in new[] { false, true }) {
            var g = CthunRulesProof.Fixture(); var p = g.Player1;
            var god = Summon(p,"BE_013"); god[GameTag.MEGA_WINDFURY] = 1;
            Use(g,god,"BE_013t2"); Use(g,god,"BE_013t4");
            if (allUsed) { Use(g,god,"BE_013t6"); Use(g,god,"BE_013t1"); }
            End(g);
            PoolPilotProof.Resume(g,copy => {
                var owner = copy.CurrentPlayer;
                var transformed = (Minion)Play(copy,owner,"EX1_564",(Minion)copy.IdEntityDic[god.Id]);
                Core2026Proof.Check(OldGodRules.UsedAbilities(transformed) == (allUsed ? 15 : 10),
                    "Faceless copies used abilities across controllers instead of refreshing them");
                Core2026Proof.Check(transformed.NumAttacksThisTurn == 0 && CthunRules.Progress(owner) == 0,
                    "transform copy retains neither source attack spending nor opponent controller ledger");
                if (allUsed) Core2026Proof.Check(transformed.CanAttack,"exhausted catalog copy can attack on entry");
                else {
                    Use(copy,transformed,"BE_013t1");
                    Core2026Proof.Check(transformed.NumAttacksThisTurn == 0 && CthunRules.Progress(owner) == 1,
                        "transform copy gets its own first free use and records only its controller progress");
                }
            });
        }
    }

    private static void Unpinned_PoolsAndCapacity()
    {
        foreach (string id in new[] { "BE_017","BE_784" })
        foreach (int candidates in new[] { 0,1 })
        foreach (bool fullBoard in new[] { false,true })
        foreach (bool fullHand in new[] { false,true }) {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,id);
            var filter = id == "BE_017" ? OfflinePoolFilter.Discover(type:CardType.MINION,minCost:5,keyword:GameTag.DEATHRATTLE)
                : OfflinePoolFilter.Random(type:CardType.MINION,minCost:8);
            int source = Cards.FromId(id).AssetId;
            var admitted = OfflineCardPools.Candidates(g,p.HeroClass,filter,source).Take(candidates).Select(c => c.Id).ToArray();
            var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
            var selected = candidates == 0 ? rows.Elements("Card").Where(r => (string)r.Attribute("type") == "SPELL").Take(1).ToArray()
                : rows.Elements("Card").Where(r => admitted.Contains((string)r.Attribute("cardId"))).ToArray();
            rows.ReplaceNodes(selected); var fixture = OfflinePoolFixtures.Manifest(xml);
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g,fixture);
            Core2026Proof.Check(OfflineCardPools.Candidates(g,p.HeroClass,filter,source).Count == candidates,"isolated pool bucket size");
            if (fullBoard) while(p.BoardZone.Count < 7) Summon(p,"CS2_182");
            if (fullHand) PoolPilotProof.FillHand(p);
            g.SetRandomSeed(19); long draws = g.Random.DrawCount;
            Activate(g,god);
            PoolPilotProof.Resume(g,copy => Pick(copy,id == "BE_017" ? "BE_017t3" : "BE_784t2"));
            if (p.Choice != null) {
                Core2026Proof.Check(p.Choice.Choices.Count == 1,"singleton Discover has one distinct offer");
                int offered = p.Choice.Choices[0]; PoolPilotProof.Resume(g,copy => CoreCostProof.Answer(copy,offered));
            }
            Core2026Proof.Check(p.Choice == null,"empty/capacity effect never stalls decision");
            Core2026Proof.Check(g.Random.DrawCount - draws == (id == "BE_784" && candidates == 1 ? 1 : 0),"empty and singleton pool RNG budget");
            Core2026Proof.Check(p.BoardZone.Count == (fullBoard ? 7 : 1 + candidates)
                && p.HandZone.Count == (fullHand ? 10 : id == "BE_784" ? candidates : 0),
                "board and hand capacity independently lose only their own excess output");
        }
        var poolGame = CthunRulesProof.Fixture();
        Console.WriteLine("B16 Old God admitted bucket=" + OfflineCardPools.Candidates(poolGame,poolGame.Player1.HeroClass,OldGodRules.Pool,0).Count);
    }

    private static void Unpinned_ExplicitOldGodPoolOnly()
    {
        var ids = new[] { "BE_013","BE_017","BE_784" };
        var xml = KnownMetaProofFixture.BlackEmpireCatalog(); var rows = xml.Root.Element("Members");
        foreach (var row in rows.Elements("Card").Where(row => row.Elements("Tag").Any(tag =>
            (int)tag.Attribute("id") == 4754 && (int)tag.Attribute("value") != 0)).ToArray()) row.Remove();
        foreach(string id in ids) {
            var card = Cards.FromId(id); var row = new System.Xml.Linq.XElement(rows.Elements("Card").First());
            row.SetAttributeValue("cardId",id); row.SetAttributeValue("dbfId",card.AssetId); row.SetAttributeValue("scope","Standard");
            row.SetAttributeValue("set","BE"); row.SetAttributeValue("type","MINION"); row.SetAttributeValue("cost",card.Cost);
            row.SetAttributeValue("attack",card.ATK); row.SetAttributeValue("health",card.Health); row.SetAttributeValue("rarity","LEGENDARY");
            row.SetAttributeValue("spellSchool","NONE"); row.SetAttributeValue("registrationKind","standard-module");
            row.RemoveNodes(); row.Add(new System.Xml.Linq.XElement("Class",new System.Xml.Linq.XAttribute("name","NEUTRAL")));
            row.Add(new System.Xml.Linq.XElement("Tag",new System.Xml.Linq.XAttribute("id",4754),new System.Xml.Linq.XAttribute("value",1)));
            rows.Add(row);
        }
        var manifest = OfflinePoolFixtures.Manifest(xml);
        var gods = OfflineCardPools.Candidates(manifest,CardClass.MAGE,OldGodRules.Pool,0);
        Core2026Proof.Check(gods.Select(c => c.Id).Order().SequenceEqual(ids.Order()),"explicit direct-tag Old God bucket contains implemented gods");
        var ordinary = OfflineCardPools.Candidates(manifest,CardClass.MAGE,OfflinePoolFilter.Random(type:CardType.MINION),0);
        Core2026Proof.Check(!ordinary.Any(c => ids.Contains(c.Id)),"Old Gods never enter ordinary random/Discover pools");
        var curated = OfflineCardPools.Candidates(manifest, CardClass.MAGE,
            new OfflinePoolFilter(type: CardType.MINION, cardIds: ids), 0);
        Core2026Proof.Check(curated.Select(c => c.Id).Order().SequenceEqual(ids.Order()),
            "explicit curated Old God pools survive ordinary-pool exclusion");
    }

    private static void Unpinned_TargetAvailabilityAndSilence()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1; var god = Summon(p,"BE_013");
        god.CantAttack = true;
        Core2026Proof.Check(!OldGodRules.CanActivate(p,god),"Can't attack also disables voluntary ability actions");
        god.CantAttack = false; Use(g,god,"BE_013t2"); god.Silence();
        Core2026Proof.Check(!god.CanAttack,"Silence restores summon sickness after free activation");
        var nzoth = Summon(p,"BE_017"); Activate(g,nzoth);
        Core2026Proof.Check(!p.Choice.Choices.Any(id => g.IdEntityDic[id].Card.Id == "BE_017t4"),"Mastery unavailable without another Deathrattle minion");
        Pick(g,"BE_017t5");
        var frozen = Summon(p,"BE_013"); frozen.IsFrozen = true; End(g);
        Core2026Proof.Check(!frozen.IsFrozen,"new Old God's implicit Charge consumes Freeze at owner end even before first activation");
    }

    private static void Unpinned_MinionsOwnTheirAbilities()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        var raincaller = Summon(p,"CATA_487"); var god = Summon(p,"BE_013");
        var enemy = Summon(p.Opponent,"CS2_200"); enemy[GameTag.HEALTH] = 50;
        god.Poisonous = true; god.HasLifeSteal = true; god.HasStealth = true; p.Hero.Damage = 10;
        g.SetRandomSeed(19); Activate(g,god);
        PoolPilotProof.Resume(g,copy => Pick(copy,"BE_013t1"));
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Hero.Damage == 3 && !god.HasStealth,
            "on-board damage inherits minion Poisonous/Lifesteal and breaks Stealth");
        Core2026Proof.Check(raincaller.AttackDamage == 1 && g.HistoryFacts.Query.Damage().All(f => f.SourceKind != CardType.SPELL),
            "on-board ability damage is minion damage, never a spell-damage trigger");
    }

}
