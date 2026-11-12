using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class B15CombatContextEdges
{
    internal static void Run()
    {
        var failures=new List<string>();
        foreach(bool history in new[]{false,true})
        foreach(bool opponent in new[]{false,true})
        foreach(var test in new Action<Game,Controller>[] { Unpinned_AnchorSummons, Unpinned_AnchorBlockers, Unpinned_MixedDeathAnchor, Unpinned_LateAnchor,
            Unpinned_FatalSource, Unpinned_TorethLifecycle, LastAttackBatch, Unpinned_Capacity, Unpinned_RehgarBoundaries,
            Unpinned_KillMemory, Unpinned_UrsocNewSummons, Unpinned_SeededCombatPicks, Unpinned_Vashj, Unpinned_ExcessRedirectCancel, Unpinned_DeadUrsoc, Unpinned_FrostmourneOrder, Unpinned_SurvivingDracorex,
            BouncedDoubleBattlecry, Unpinned_UrsocResurrectionPosition, FrostmourneDefensiveKill })
        {
            try
            {
                var g=HistoryFactsProof.Fixture(history); if(opponent) End(g);
                test(g,opponent?g.Player2:g.Player1);
                Console.WriteLine($"PASS b15 {test.Method.Name} history={history} opponent={opponent}");
            }
            catch(Exception e) { failures.Add($"{test.Method.Name} history={history} opponent={opponent}: {e}"); }
        }
        try { RewindState(); } catch(Exception e) { failures.Add("RewindState: "+e); }
        Core2026Proof.Check(failures.Count==0,string.Join("\n",failures));
    }
    private static void Buff(Minion m,int n) => new Enchant(Effects.Attack_N(n)).ActivateTo(m);
    private static Game[] Futures(Game g) => new[]{g,g.CloneExact(history:g.History),Game.LoadSnapshot(g.SaveSnapshot())};
    private static void Unpinned_AnchorSummons(Game g,Controller p)
    {
        var far=Summon(p,"CS2_231"); var left=Summon(p,"EX1_556");
        var vault=Summon(p,"BE_151"); var right=Summon(p,"CS2_231"); var farRight=Summon(p,"CS2_231");
        foreach(var next in Futures(g))
        {
            foreach(int entity in new[]{left.Id,vault.Id,right.Id}) next.IdEntityDic[entity].ToBeDestroyed=true;
            next.DeathProcessingAndAuraUpdate(); next.DeathProcessingAndAuraUpdate();
            var owner=next.IdEntityDic[vault.Id].Controller;
            Core2026Proof.Check(((Minion)next.IdEntityDic[far.Id]).AttackDamage==1
                && owner.BoardZone.Single(m=>m.Card.Id=="skele21").AttackDamage==4
                && ((Minion)next.IdEntityDic[farRight.Id]).AttackDamage==3,
                "Unpinned_AnchorSummons: earlier left Deathrattle summon becomes immediate neighbour, farther left excluded");
        }
    }
    private static void Unpinned_MixedDeathAnchor(Game g,Controller p)
    {
        var left=Summon(p,"CS2_231");
        var location=Entity.FromCard(p,Cards.FromId("CORE_REV_990"),zone:p.BoardZone);
        var vault=Summon(p,"BE_151"); var right=Summon(p,"CS2_231");
        location.ToBeDestroyed=true; vault.ToBeDestroyed=true; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(left.AttackDamage==3 && right.AttackDamage==3 && vault[DeathContextRules.Anchor]==0,
            "mixed Location/minion death batch shifts physical anchor before neighbours resolve");
    }
    private static void Unpinned_LateAnchor(Game g,Controller p)
    {
        var a=Summon(p,"CS2_231"); a.BaseHealth=20;
        var abomination=Summon(p,"EX1_097"); var vault=Summon(p,"BE_151"); vault.Damage=1;
        var right=Summon(p,"CS2_231"); right.BaseHealth=20;
        Kill(g,abomination);
        Core2026Proof.Check(vault.Zone==p.GraveyardZone && vault[DeathContextRules.Anchor]==0 && a.AttackDamage==3 && right.AttackDamage==3,
            "a Deathrattle-caused later death pass owns and expires its position anchor");
    }
    private static void Unpinned_AnchorBlockers(Game g,Controller p)
    {
        var far=Summon(p,"CS2_231"); var location=Entity.FromCard(p,Cards.FromId("CORE_REV_990"),zone:p.BoardZone);
        var vault=Summon(p,"BE_151"); var right=Summon(p,"CS2_231");
        Kill(g,vault);
        Core2026Proof.Check(far.AttackDamage==1 && right.AttackDamage==3,"location immediately beside death anchor blocks further minion");
        StandardSetsProof.ParkBoard(p);
        far=Summon(p,"CS2_231"); var sleeper=Summon(p,"CS2_182"); sleeper[GameTag.DORMANT]=1;
        vault=Summon(p,"BE_151"); right=Summon(p,"CS2_231"); Kill(g,vault);
        Core2026Proof.Check(far.AttackDamage==1 && sleeper.AttackDamage==4 && right.AttackDamage==3,
            "Dormant physical occupant blocks, never skips to farther minion");
    }
    private static void Unpinned_FatalSource(Game g,Controller p)
    {
        var victim=Summon(p,"CATA_185"); var prior=Summon(p.Opponent,"CS2_182");
        victim.TakeDamage(prior,1); victim.TakeDamage(Add(p.Opponent,"CS2_029"),2); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(prior.Card.Id=="CS2_182","nonlethal minion never gets later spell credit");
        victim=Summon(p,"CATA_185"); var poison=Summon(p.Opponent,"CS2_231"); poison.Poisonous=true;
        victim.TakeDamage(poison,1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(poison.Card.Id=="CATA_185","direct minion Poisonous fatal source");
        victim=Summon(p,"CATA_185");
        new DestroyTask(EntityType.TARGET).Process(g,prior.Controller,prior,victim); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(prior.Card.Id=="CATA_185","direct minion destroy source qualifies");
        var direct=Summon(p.Opponent,"CATA_185");
        var rift=Play(g,p,"CORE_ULD_165",direct);
        Core2026Proof.Check(rift.Card.Id=="CATA_185" && rift.Controller==p,"raw minion Battlecry Destroy retains direct killer and controller");
    }
    private static void Unpinned_TorethLifecycle(Game g,Controller p)
    {
        var first=Summon(p,"EDR_258"); var second=Summon(p,"EDR_258");
        var target=Summon(p,"CS2_182"); target.HasDivineShield=true;
        target.IsImmune=true; target.TakeDamage(p.Opponent.Hero,1); target.IsImmune=false; target.TakeDamage(p.Opponent.Hero,0);
        Core2026Proof.Check(target[DeathContextRules.ShieldHits]==0,"Immune and zero damage consume no shield capacity");
        target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        var copiedShield=(Minion)Generic.Copy(p,p.Hero,target,Zone.PLAY);
        Core2026Proof.Check(copiedShield.HasDivineShield,"copy retains active partially used Divine Shield");
        copiedShield.TakeDamage(p.Opponent.Hero,1);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!copiedShield.HasDivineShield,"exact board copy retains two spent shield hits");
        var disguised=(Minion)Play(g,p,"EX1_564",target);
        Core2026Proof.Check(disguised.HasDivineShield,"Faceless transform-copy retains active Shield");
        disguised.TakeDamage(p.Opponent.Hero,1);
        Core2026Proof.Check(!disguised.HasDivineShield,"Faceless transform-copy retains two spent Shield hits");
        foreach(var next in Futures(g))
        {
            var copy=(Minion)next.IdEntityDic[target.Id]; copy.TakeDamage(copy.Controller.Opponent.Hero,1);
            Core2026Proof.Check(!copy.HasDivineShield && copy.Damage==0,"Unpinned_TorethNoStack: two providers still only three hits");
        }
        target.HasDivineShield=true; target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        new Effect(GameTag.DIVINE_SHIELD,EffectOperator.SET,1).ApplyTo(target);
        target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        Core2026Proof.Check(target.HasDivineShield,"Unpinned_TorethRefresh: ordinary effect grant resets even already shielded");
        Generic.ReturnToHandBlock(p,first); g.DeathProcessingAndAuraUpdate();
        target.TakeDamage(p.Opponent.Hero,1); Core2026Proof.Check(!target.HasDivineShield,"other Toreth retains three-hit rule");
        target.HasDivineShield=true; target.TakeDamage(p.Opponent.Hero,1); Kill(g,second);
        Core2026Proof.Check(target.HasDivineShield,"Unpinned_TorethRemoval: last provider death keeps surviving shield");
        target.TakeDamage(p.Opponent.Hero,1); Core2026Proof.Check(!target.HasDivineShield,"ordinary shield after last provider death");
        var hostile=Summon(p.Opponent,"CS2_182"); hostile.HasDivineShield=true;
        hostile.TakeDamage(p.Hero,1); Core2026Proof.Check(!hostile.HasDivineShield,"opponent never gets shield aura");
        target.HasDivineShield=true; target.Silence(); Core2026Proof.Check(!target.HasDivineShield && target[DeathContextRules.ShieldHits]==0,"Silence removes shield and private capacity");
    }
    private static void LastAttackBatch(Game g,Controller p)
    {
        var flytrap=Summon(p,"EDR_484"); var a=Summon(p,"CORE_YOD_026"); var b=Summon(p.Opponent,"CS2_231");
        Buff(a,5); Buff(b,6); a.ToBeDestroyed=true; b.ToBeDestroyed=true;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(flytrap.AttackDamage==23,"Flytrap gains both pre-batch attacks 7+7; Servant also grants 7");
        flytrap.Silence(); var extra=Summon(p.Opponent,"CS2_182"); Kill(g,extra);
        Core2026Proof.Check(flytrap.AttackDamage==2,"Silence disables further deaths and removes gains");
        var servant=Summon(p,"CORE_YOD_026"); Buff(servant,9);
        Generic.ReturnToHandBlock(p,servant); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(servant[DeathContextRules.Captured]==0 && servant.AttackDamage==2,"backward movement clears death state and stat buffs");
    }
    private static void Unpinned_Capacity(Game g,Controller p)
    {
        var experiment=Summon(p,"CATA_464"); var rehgar=Summon(p,"CORE_CATA_004"); var attacker=Summon(p,"CS2_231");
        while(!p.HandZone.IsFull) Add(p,"CS2_231");
        Kill(g,experiment);
        Core2026Proof.Check(p.HandZone.Count==10 && !p.HandZone.Any(c=>c.Card.Id=="CATA_464t"),"full hand burns generated Dragon Breath");
        Attack(g,attacker,p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count==10 && !p.HandZone.Any(c=>c.Card.Id=="CORE_EX1_238"),"full hand burns Rehgar Bolt");
        var ursoc=Summon(p,"EDR_819");
        foreach(var card in new[]{"CS2_231","CS2_171","CS2_168"}) PayloadRules.StoreCard(ursoc,DeathContextRules.KillKey,Cards.FromId(card));
        while(!p.BoardZone.IsFull) Summon(p,"CS2_231");
        Kill(g,ursoc); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count==7 && p.BoardZone.Last().Card.Id=="CS2_231","full board allows exactly first stored resurrection in freed slot");
    }
    private static void Unpinned_RehgarBoundaries(Game g,Controller p)
    {
        var rehgar=Summon(p,"CORE_CATA_004"); var spacer=Summon(p,"CS2_182"); var far=Summon(p,"CS2_231");
        Attack(g,far,p.Opponent.Hero);
        Core2026Proof.Check(!p.HandZone.Any(c=>c.Card.Id=="CORE_EX1_238"),"nonadjacent attacker does not trigger");
        Attack(g,rehgar,p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count(c=>c.Card.Id=="CORE_EX1_238")==1,"Rehgar self attack grants one Bolt");
        Generic.ReturnToHandBlock(p,rehgar); g.DeathProcessingAndAuraUpdate();
        Attack(g,spacer,p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count(c=>c.Card.Id=="CORE_EX1_238")==1,"leave-play removes observer");
    }
    private static void Unpinned_KillMemory(Game g,Controller p)
    {
        var ursoc=Summon(p,"EDR_819"); var first=Summon(p.Opponent,"CS2_231");
        Attack(g,ursoc,first); var ledger=PayloadRules.Entries(ursoc,DeathContextRules.KillKey);
        Core2026Proof.Check(ledger.Length==1 && ledger[0].Card.Id=="CS2_231","outgoing kill stored");
        foreach(var next in Futures(g))
        {
            var source=(Minion)next.IdEntityDic[ursoc.Id];
            Core2026Proof.Check(PayloadRules.Entries(source,DeathContextRules.KillKey).Single().Card.Id=="CS2_231","kill ledger clone/load");
            var copied=Generic.Copy(source.Controller,source,source,Zone.PLAY);
            Core2026Proof.Check(PayloadRules.Entries(copied,DeathContextRules.KillKey).Length==1,"exact board copy retains independent kill memory");
            source.Silence();
            Core2026Proof.Check(PayloadRules.Entries(source,DeathContextRules.KillKey).Length==0 && PayloadRules.Entries(copied,DeathContextRules.KillKey).Length==1,"Silence clears only original memory");
            Generic.ReturnToHandBlock(copied.Controller,(Minion)copied); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(PayloadRules.Entries(copied,DeathContextRules.KillKey).Length==0,"bounce clears copied kill memory");
        }
    }
    private static void Unpinned_UrsocNewSummons(Game g,Controller p)
    {
        Summon(p.Opponent,"EX1_556"); Summon(p.Opponent,"CS2_231");
        var ursoc=(Minion)Play(g,p,"EDR_819");
        Core2026Proof.Check(p.Opponent.BoardZone.Single().Card.Id=="skele21" && PayloadRules.Entries(ursoc,DeathContextRules.KillKey).Length==2,
            "Ursoc initial snapshot excludes mid-sequence summons");
        var retaliator=Summon(p.Opponent,"CS2_231"); retaliator.BaseHealth=1;
        var defending=Summon(p,"EDR_819"); defending.Health=1;
        new AttackTask(EntityType.TARGET,EntityType.SOURCE).Process(g,p,defending,retaliator,new TaskStack()); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(PayloadRules.Entries(defending,DeathContextRules.KillKey).Length==0,"retaliation never enters outgoing kill set");
    }
    private static void Unpinned_SeededCombatPicks(Game g,Controller p)
    {
        // Independent xoroshiro vectors already checked by OfflinePoolSamplingProof; first draw selects final element.
        var start=new RngState(1,-2,0); var once=new RngState(36028797018980351,-1,1);
        var twice=new RngState(-71987225025331169,-1125831321583617,2);
        var servant=Summon(p,"CORE_YOD_026"); var left=Summon(p,"CS2_231"); var right=Summon(p,"CS2_171");
        g.Random.RestoreState(start);
        foreach(var next in Futures(g))
        {
            Kill(next,(Minion)next.IdEntityDic[servant.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[left.Id]).AttackDamage==1 && ((Minion)next.IdEntityDic[right.Id]).AttackDamage==3
                && next.Random.CaptureState().Equals(once),"seeded Servant picks final physical minion with exactly one draw");
        }
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var a=Summon(p.Opponent,"CS2_231"); var b=Summon(p.Opponent,"CS2_171"); var c=Summon(p.Opponent,"CS2_168");
        var ursoc=Summon(p,"EDR_819"); g.Random.RestoreState(start);
        ursoc.Card.Power.PowerTask.Process(g,p,ursoc,null,new TaskStack()); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(PayloadRules.Entries(ursoc,DeathContextRules.KillKey).Select(e=>e.Card.Id).SequenceEqual(new[]{"CS2_171","CS2_231","CS2_168"})
            && g.Random.CaptureState().Equals(twice),"seeded Ursoc shuffles initial physical order, draws n-1 times");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var drake=Summon(p,"EDR_453"); a=Summon(p.Opponent,"CS2_231"); b=Summon(p.Opponent,"CS2_171"); c=Summon(p.Opponent,"CS2_168");
        g.Random.RestoreState(start); End(g);
        Core2026Proof.Check(c.Zone==p.Opponent.GraveyardZone && a.Zone==p.Opponent.BoardZone && b.Zone==p.Opponent.BoardZone
            && g.Random.CaptureState().Equals(once),"seeded Briarspawn chooses final minion, not always first");
    }
    private static void Unpinned_Vashj(Game g,Controller p)
    {
        Play(g,p,"JAIL_315"); End(g);
        var vashj=Summon(p.Opponent,"REV_925"); int attack=vashj.AttackDamage;
        foreach(var next in Futures(g))
        {
            var body=(Minion)next.IdEntityDic[vashj.Id]; Attack(next,body,next.IdEntityDic[vashj.Id].Controller.Opponent.Hero);
            Core2026Proof.Check(body.Card.Id=="REV_925" && body.Controller.BoardZone.Count==2
                && body.Controller.BoardZone.Any(m=>m.Card.Id=="CS2_tk1") && body.Controller.Opponent.Hero.Damage==attack,
                "Unpinned_MysticVashj: prevented transform summons Sheep and preserves pending attack");
        }
    }
    private static void InstallSecret(Game g,Controller p,string id)
    {
        var secret=(Spell)Entity.FromCard(p,Cards.FromId(id)); Generic.CastSpell(p,g,secret,null,0);
        secret.IsExhausted=false; // Fixture represents a Secret armed on an earlier turn.
        g.DeathProcessingAndAuraUpdate();
    }
    private static void Unpinned_ExcessRedirectCancel(Game g,Controller p)
    {
        InstallSecret(g,p.Opponent,"EX1_130"); var source=Summon(p,"EDR_453"); var original=Summon(p.Opponent,"CS2_182");
        End(g);
        Core2026Proof.Check(original.Damage==0 && p.Opponent.Hero.Damage==11,"Unpinned_Briarspawn: redirected 1-Health actual defender determines 11 spill; original="+original.Damage+", hero="+p.Opponent.Hero.Damage+", secrets="+p.Opponent.SecretZone.Count);
        End(g); StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        p.Opponent.Hero.Damage=0; InstallSecret(g,p.Opponent,"EX1_611");
        source=Summon(p,"EDR_453"); Summon(p.Opponent,"CS2_182"); End(g);
        Core2026Proof.Check(source.Zone==p.HandZone && p.Opponent.Hero.Damage==0,"Unpinned_Briarspawn: cancelled combat has no excess damage");
        End(g); StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        p.Hero.Damage=0; p.Opponent.Hero.Damage=0;
        var redirected=Summon(p,"CS2_231"); redirected.BaseHealth=3;
        var drake=Summon(p,"EDR_453"); Summon(p.Opponent,"CS2_182");
        void Redirect(IEntity attacker) { if(attacker==drake) g.CurrentEventData.EventTarget=redirected; }
        g.TriggerManager.AttackTrigger+=Redirect;
        try { End(g); } finally { g.TriggerManager.AttackTrigger-=Redirect; }
        Core2026Proof.Check(p.Hero.Damage==0 && p.Opponent.Hero.Damage==9,
            "Unpinned_Briarspawn: even friendly redirection spills to enemy hero using actual defender Health");
    }
    private static void Unpinned_DeadUrsoc(Game g,Controller p)
    {
        var source=Summon(p,"EDR_819"); source.Health=1;
        var victim=Summon(p.Opponent,"CS2_182"); Attack(g,source,victim);
        Core2026Proof.Check(source.Zone==p.GraveyardZone && victim.Zone==p.Opponent.GraveyardZone
            && p.BoardZone.Single().Card.Id=="CS2_182" && p.BoardZone.Single().Health==5,
            "Ursoc simultaneous death retains its outgoing victim before Deathrattle, resurrects fresh");
    }
    private static void Unpinned_FrostmourneOrder(Game g,Controller p)
    {
        Play(g,p,"CORE_RLK_086"); var weapon=p.Hero.Weapon;
        var a=Summon(p.Opponent,"CS2_231"); var b=Summon(p.Opponent,"CS2_171");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,a)),"Frostmourne first kill");
        Core2026Proof.Check(PayloadRules.Entries(weapon,DeathContextRules.KillKey).Length==1,"first weapon kill retained");
        p.Hero.NumAttacksThisTurn=0; p.Hero.IsExhausted=false; weapon.Damage=2;
        foreach(var next in Futures(g))
        {
            var owner=next.IdEntityDic[weapon.Id].Controller;
            Core2026Proof.Check(next.Process(HeroAttackTask.Any(owner,(Minion)next.IdEntityDic[b.Id])),"Frostmourne final kill");
            Core2026Proof.Check(owner.BoardZone.Select(m=>m.Card.Id).SequenceEqual(new[]{"CS2_231","CS2_171"}),"Frostmourne ordered previous and final kills survive clone/load");
        }
    }
    private static void Unpinned_SurvivingDracorex(Game g,Controller p)
    {
        var source=Summon(p,"DINO_401"); Buff(source,3);
        var defender=Summon(p.Opponent,"CS2_182"); defender.BaseHealth=30;
        var other=Summon(p.Opponent,"CS2_182"); other.BaseHealth=30;
        Attack(g,source,defender);
        Core2026Proof.Check(source.Zone==p.BoardZone && defender.Damage==8 && other.Damage==8 && p.Opponent.Hero.Damage==0,
            "Unpinned_Dracorex: surviving buffed source uses live 8 Attack, excludes defender and hero");
    }
    private static void BouncedDoubleBattlecry(Game g,Controller p)
    {
        Summon(p,"LOE_077"); Kill(g,Summon(p,"CS2_231"));
        InstallSecret(g,p.Opponent,"EX1_611");
        foreach(var next in Futures(g))
        {
            var owner=next.ControllerById(p.Id);
            var terror=Play(next,owner,"BE_564");
            Core2026Proof.Check(terror.Zone==owner.HandZone && owner.Opponent.Hero.Damage==0
                && owner.Opponent.SecretZone.Count==0 && next.CurrentEventData==null,
                "Brann + Terror + Freezing Trap: bounced source safely cancels second forced attack without damage");
        }
    }
    private static void Unpinned_UrsocResurrectionPosition(Game g,Controller p)
    {
        Summon(p,"CS2_231"); var ursoc=Summon(p,"EDR_819"); Summon(p,"CS2_171");
        var first=Summon(p.Opponent,"CS2_182"); var second=Summon(p.Opponent,"CS2_168");
        Attack(g,ursoc,first); ursoc.NumAttacksThisTurn=0; Attack(g,ursoc,second);
        foreach(bool triggered in new[]{false,true})
        foreach(var next in Futures(g.CloneExact(history:g.History)))
        {
            var source=(Minion)next.IdEntityDic[ursoc.Id]; var owner=source.Controller;
            if(triggered)
            {
                source.Card.Power.DeathrattleTask.Process(next,owner,source,null,new TaskStack());
                next.DeathProcessingAndAuraUpdate();
            }
            else Kill(next,source);
            var expected=triggered?new[]{"CS2_231","EDR_819","CS2_182","CS2_168","CS2_171"}
                :new[]{"CS2_231","CS2_182","CS2_168","CS2_171"};
            Core2026Proof.Check(owner.BoardZone.Select(m=>m.Card.Id).SequenceEqual(expected),
                "Unpinned_Ursoc: resurrect in kill order at death slot, or right of living source for triggered Deathrattle");
        }
    }
    private static void FrostmourneDefensiveKill(Game g,Controller p)
    {
        Play(g,p,"CORE_RLK_086"); var weapon=p.Hero.Weapon;
        var first=Summon(p.Opponent,"CS2_171");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,first)),"Frostmourne outgoing kill before retaliation");
        InstallSecret(g,p.Opponent,"EX1_533"); var wisp=Summon(p,"CS2_231");
        foreach(var next in Futures(g))
        {
            var owner=next.IdEntityDic[weapon.Id].Controller; var currentWeapon=owner.Hero.Weapon;
            Attack(next,(Minion)next.IdEntityDic[wisp.Id],owner.Opponent.Hero);
            Core2026Proof.Check(next.IdEntityDic[wisp.Id].Zone==owner.GraveyardZone && owner.Opponent.SecretZone.Count==0,
                "Misdirection sends friendly Wisp into armed hero on hero's own turn");
            Core2026Proof.Check(PayloadRules.Entries(currentWeapon,DeathContextRules.KillKey).Select(e=>e.Card.Id)
                .SequenceEqual(new[]{"CS2_171","CS2_231"}),"Frostmourne records outgoing then defensive combat kills");
            currentWeapon.Destroy(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.BoardZone.Select(m=>m.Card.Id).SequenceEqual(new[]{"CS2_171","CS2_231"}),
                "D4 Frostmourne resurrects outgoing and defensive kills fresh, in kill order");
        }
    }
    private static void RewindState()
    {
        var g=HistoryFactsProof.Fixture(); var p=g.Player1;
        var ursoc=Summon(p,"EDR_819"); var victim=Summon(p.Opponent,"CS2_231"); Attack(g,ursoc,victim);
        var toreth=Summon(p,"EDR_258"); var shield=Summon(p,"CS2_182"); shield.HasDivineShield=true; shield.TakeDamage(p.Opponent.Hero,1);
        var experiment=Summon(p,"CATA_464"); Buff(experiment,4); Kill(g,experiment);
        var breath=p.HandZone.Single(c=>c.Card.Id=="CATA_464t");
        var rewind=Play(g,p,"TIME_001");
        Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(g),"real Rewind offer after context-bearing board");
        foreach(var next in Futures(g))
        {
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(PayloadRules.Entries(next.IdEntityDic[ursoc.Id],DeathContextRules.KillKey).Single().Card.Id=="CS2_231"
                && next.IdEntityDic[shield.Id][DeathContextRules.ShieldHits]==1
                && next.IdEntityDic[breath.Id][GameTag.TAG_SCRIPT_DATA_NUM_1]==7,"Rewind restores ordered kill memory, shield hits and generated spell scalar");
            RewindTestHelpers.KeepIfOffered(next);
        }
    }
}
